using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.IO.Budgets;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>Shared budgets for one complete generic routing decision.</summary>
public sealed record FileProbeOptions
{
    /// <summary>Maximum prefix bytes read once and shared by every recognizer.</summary>
    public int MaxPrefixBytes { get; init; } = 64 * 1024;

    /// <summary>
    /// Absolute wall-clock budget shared by all recognizers. It only ends a recognizer that
    /// hangs, so it is far above what a cold engine load on a busy machine takes; a shorter
    /// command deadline still ends routing first.
    /// </summary>
    public TimeSpan RecognizerTimeout { get; init; } =
        TimeSpan.FromSeconds(10);

    /// <summary>Maximum recognizers executing concurrently.</summary>
    public int MaxConcurrency { get; init; } = 4;
}

/// <summary>Inputs that uniquely define a generic routing decision.</summary>
public sealed record FileRouteRequest
{
    /// <summary>Creates a request for one existing file.</summary>
    public FileRouteRequest(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = path;
    }

    /// <summary>Input path.</summary>
    public string Path { get; }

    /// <summary>Stable operation name, such as open, app, preview, or review.</summary>
    public string Operation { get; init; } = "open";

    /// <summary>Explicit product selection, which bypasses owner competition.</summary>
    public string? ExplicitProductId { get; init; }
}

/// <summary>Immutable result of one content-backed routing decision.</summary>
public sealed record FileRouteResult
{
    /// <summary>Selected product.</summary>
    public required ProductDefinition Product { get; init; }

    /// <summary>Requested operation.</summary>
    public required string Operation { get; init; }

    /// <summary>Whether the caller selected the product explicitly.</summary>
    public required bool Explicit { get; init; }

    /// <summary>Content format reported by the recognizer, when known.</summary>
    public string? FormatId { get; init; }

    /// <summary>Safe bounded evidence summary.</summary>
    public string? Evidence { get; init; }
}

/// <summary>A product, and its format when known, that a file's content looks like.</summary>
/// <param name="ProductId">The product whose recognizer matched the content.</param>
/// <param name="FormatId">The format the recognizer named, such as <c>docx</c>.</param>
public sealed record FileDetection(string ProductId, string? FormatId);

/// <summary>
/// Deterministic, operation-aware generic file router. A generic route is
/// selected only by positive bounded content evidence; extension ownership is
/// a candidate constraint and never a fallback.
/// </summary>
public sealed class ProductFileRouter
{
    private readonly ProductCatalog _catalog;
    private readonly FileProbeOptions _options;

    /// <summary>Creates a router over one frozen catalog.</summary>
    public ProductFileRouter(
        ProductCatalog catalog,
        FileProbeOptions? options = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _options = options ?? new FileProbeOptions();
        if (_options.MaxPrefixBytes is <= 0 or > 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Probe prefix must be between 1 byte and 1 MiB.");
        }
        if (_options.RecognizerTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The shared routing timeout must be positive.");
        }
        if (_options.MaxConcurrency is <= 0 or > 32)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Recognizer concurrency must be between 1 and 32.");
        }
    }

    /// <summary>Resolves one explicit or generic operation-aware request.</summary>
    public async ValueTask<FileRouteResult> ResolveAsync(
        FileRouteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Operation);
        string operation = request.Operation.Trim().ToLowerInvariant();
        string fullPath = Path.GetFullPath(request.Path);
        if (!File.Exists(fullPath))
        {
            throw CliErrors.FileNotFound(fullPath);
        }

        FileProbeSession session = await CreateSessionAsync(
            fullPath,
            cancellationToken).ConfigureAwait(false);
        if (request.ExplicitProductId is { } explicitId)
        {
            ProductDefinition product = _catalog.Get(explicitId);
            return await ValidateExplicitAsync(
                product,
                session,
                operation,
                cancellationToken).ConfigureAwait(false);
        }

        return await ResolveGenericAsync(
            session,
            operation,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Names the products whose content recognizers positively recognize the file, strongest first,
    /// whatever its extension or the operation. It explains why a product could not open the file,
    /// so it never fails: a file that cannot be read, or a probe that runs out of time, detects
    /// nothing.
    /// </summary>
    public async ValueTask<IReadOnlyList<FileDetection>> DetectAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            FileProbeSession session = await CreateSessionAsync(
                Path.GetFullPath(path),
                cancellationToken).ConfigureAwait(false);
            IReadOnlyList<RecognitionEntry> recognitions = await RecognizeAsync(
                _catalog.Products.Where(static product => product.Files.Recognizer is not null).ToArray(),
                session,
                cancellationToken).ConfigureAwait(false);
            return DetectedEntries(recognitions, except: null)
                .Select(static entry => new FileDetection(
                    entry.Product.Manifest.Id,
                    // Evidence that fits several of the product's formats proves none of them.
                    entry.Recognition.Kind == FileRecognitionKind.Match ? entry.Recognition.FormatId : null))
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CliException)
        {
            return [];
        }
    }

    private async ValueTask<FileRouteResult> ResolveGenericAsync(
        FileProbeSession session,
        string operation,
        CancellationToken cancellationToken)
    {
        ProductDefinition[] candidates = _catalog.Products
            .Where(product => EligibleForOperation(product, operation))
            .Where(static product => product.Files.Recognizer is not null)
            .OrderBy(static product => product.Manifest.Id, StringComparer.Ordinal)
            .ToArray();
        IReadOnlyList<RecognitionEntry> recognitions = await RecognizeAsync(
            candidates,
            session,
            cancellationToken).ConfigureAwait(false);

        string extension = ProductCatalog.NormalizeExtension(
            string.IsNullOrWhiteSpace(session.Extension)
                ? ".unknown"
                : session.Extension);
        if (!string.Equals(extension, ".unknown", StringComparison.Ordinal)
            && _catalog.TryGetDefaultOwner(
                extension,
                operation,
                out ProductDefinition? resolvedOwner))
        {
            return ResolveOwned(
                session.FullPath,
                operation,
                resolvedOwner!,
                candidates,
                recognitions,
                InputReaders(extension));
        }

        return ResolveUnowned(
            session.FullPath,
            operation,
            candidates,
            recognitions,
            InputReaders(extension),
            OnlyWritten(extension),
            PdfWriter(extension));
    }

    /// <summary>
    /// The first product that writes files of <paramref name="extension"/> with its convert and
    /// converts to PDF too, which writes the same content in a format a product reads; null for none.
    /// </summary>
    private string? PdfWriter(string extension) =>
        _catalog.Products.FirstOrDefault(product =>
            product.Formats.Any(format => format.Uses.HasFlag(FormatUse.Convert) && format.Extensions.Any(candidate => string.Equals(
                ProductCatalog.NormalizeExtension(candidate), extension, StringComparison.Ordinal)))
            && product.Formats.Any(static format => format.Id == "pdf" && format.Uses.HasFlag(FormatUse.Convert)))?.Manifest.Id;

    /// <summary>
    /// The commands that write files of <paramref name="extension"/> when no product reads that
    /// extension, such as <c>cells convert</c> for <c>.xps</c>, as <c>product verb</c> in catalog
    /// order; empty when a product reads it or none writes it.
    /// </summary>
    private IReadOnlyList<string> OnlyWritten(string extension) => OnlyWrittenBy(extension);

    /// <summary>
    /// The products that declare <paramref name="extension"/> for one of their input formats,
    /// whether or not it is a generic route of theirs, in catalog order. Content no product
    /// recognizes under such an extension is a damaged file of theirs, never an unsupported one.
    /// </summary>
    private IReadOnlyList<string> InputReaders(string extension) =>
        [.. _catalog.Products
            .Where(product => product.Formats.Any(format => format.Uses.HasFlag(FormatUse.Input)
                && format.Extensions.Any(candidate => string.Equals(
                    ProductCatalog.NormalizeExtension(candidate), extension, StringComparison.Ordinal))))
            .Select(static product => product.Manifest.Id)];

    private IReadOnlyList<string> OnlyWrittenBy(string extension)
    {
        bool Declares(FormatDescriptor format) => format.Extensions.Any(candidate => string.Equals(
            ProductCatalog.NormalizeExtension(candidate), extension, StringComparison.Ordinal));
        if (_catalog.Products.Any(product => product.Formats.Any(format => format.Uses.HasFlag(FormatUse.Input) && Declares(format))))
        {
            return [];
        }

        return [.. _catalog.Products.SelectMany(product => product.Formats
                .Where(Declares)
                .SelectMany(static format => (string[])[
                    .. format.Uses.HasFlag(FormatUse.Convert) ? ["convert"] : Array.Empty<string>(),
                    .. format.Uses.HasFlag(FormatUse.Render) ? ["render"] : Array.Empty<string>()])
                .Select(verb => $"{product.Manifest.Id} {verb}"))
            .Distinct(StringComparer.Ordinal)];
    }

    private static FileRouteResult ResolveOwned(
        string fullPath,
        string operation,
        ProductDefinition extensionOwner,
        IReadOnlyList<ProductDefinition> candidates,
        IReadOnlyList<RecognitionEntry> recognitions,
        IReadOnlyList<string> readers)
    {
        RecognitionEntry? ownerRecognition = recognitions.FirstOrDefault(
            entry => entry.Product == extensionOwner);
        if (ownerRecognition is null
            || ownerRecognition.Recognition.Kind
                is FileRecognitionKind.NoMatch
                or FileRecognitionKind.Indeterminate
                or FileRecognitionKind.Encrypted
                or FileRecognitionKind.Failed)
        {
            IReadOnlyList<string> detected = Detected(recognitions, extensionOwner);
            // An extension many kinds of file share, such as .xml, names no product of its own:
            // content no product recognizes is a damaged file of every product that reads it.
            bool shared = ExtensionIsShared(extensionOwner, Path.GetExtension(fullPath));
            throw detected.Count == 0
                ? shared
                    ? CliErrors.ContentUnrecognized(fullPath, readers)
                    : CliErrors.ContentUnrecognized(fullPath, extensionOwner.Manifest.Id)
                : CliErrors.FormatUnrecognized(
                    fullPath,
                    shared ? null : extensionOwner.Manifest.Id,
                    detected,
                    CandidateIds(candidates));
        }

        RecognitionEntry[] competing = StrongMatches(recognitions)
            .Where(entry => entry.Product != extensionOwner
                && entry.Recognition.Confidence
                    >= ownerRecognition.Recognition.Confidence)
            .ToArray();
        if (competing.Length > 0)
        {
            throw CliErrors.FormatMismatch(
                fullPath,
                extensionOwner.Manifest.Id,
                competing.Select(static entry => entry.Product.Manifest.Id)
                    .ToArray());
        }

        return Result(
            ownerRecognition,
            operation,
            explicitSelection: false);
    }

    private static bool ExtensionIsShared(ProductDefinition owner, string extension)
    {
        FormatDescriptor[] declaring = owner.Formats
            .Where(format => format.Uses.HasFlag(FormatUse.Input)
                && format.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        return declaring.Length > 0
            && declaring.All(format => format.SharedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase));
    }

    private static FileRouteResult ResolveUnowned(
        string fullPath,
        string operation,
        IReadOnlyList<ProductDefinition> candidates,
        IReadOnlyList<RecognitionEntry> recognitions,
        IReadOnlyList<string> readers,
        IReadOnlyList<string> writers,
        string? pdfWriter)
    {
        RecognitionEntry[] matches = StrongMatches(recognitions);
        if (matches.Length > 0)
        {
            int strongest = matches.Max(static entry =>
                entry.Recognition.Confidence);
            RecognitionEntry[] winners = matches
                .Where(entry => entry.Recognition.Confidence == strongest)
                .ToArray();
            if (winners.Length == 1)
            {
                return Result(
                    winners[0],
                    operation,
                    explicitSelection: false);
            }
            throw CliErrors.FormatAmbiguous(
                fullPath,
                winners.Select(static entry => entry.Product.Manifest.Id)
                    .ToArray());
        }

        IReadOnlyList<string> detected = Detected(recognitions, except: null);
        throw detected.Count == 0 && readers.Count > 0
            ? CliErrors.ContentUnrecognized(fullPath, readers)
            : detected.Count == 0 && writers.Count > 0
            ? CliErrors.FormatOnlyWritten(fullPath, writers, pdfWriter, operation, CandidateIds(candidates))
            : CliErrors.FormatUnrecognized(
                fullPath,
                declaredProduct: null,
                detected,
                CandidateIds(candidates));
    }

    private async ValueTask<FileRouteResult> ValidateExplicitAsync(
        ProductDefinition product,
        FileProbeSession session,
        string operation,
        CancellationToken cancellationToken)
    {
        string extension = session.Extension;
        bool acceptsExtension = !string.IsNullOrWhiteSpace(extension)
            && product.Files.AcceptedInputExtensions.Any(
                candidate => string.Equals(
                    ProductCatalog.NormalizeExtension(candidate),
                    ProductCatalog.NormalizeExtension(extension),
                    StringComparison.Ordinal));
        if (!acceptsExtension)
        {
            throw CliErrors.FormatUnsupported(
                extension.TrimStart('.'),
                product.Files.AcceptedInputExtensions
                    .Select(static value => value.TrimStart('.'))
                    .Order(StringComparer.Ordinal)
                    .ToArray());
        }

        // Content is checked against the rules of the formats this extension declares. A format
        // without any rule leaves the content to the engine; a format with rules never skips them.
        FormatDescriptor[] declared = product.Formats
            .Where(format => format.Uses.HasFlag(FormatUse.Input)
                && format.Extensions.Any(candidate => string.Equals(
                    ProductCatalog.NormalizeExtension(candidate),
                    ProductCatalog.NormalizeExtension(extension),
                    StringComparison.Ordinal)))
            .ToArray();
        bool customRules = declared.Any(static format => format.Recognizer is not null);
        FormatDescriptor[] declarativeRules = declared
            .Where(static format => format.Recognition is not null)
            .ToArray();
        if (!customRules && declarativeRules.Length == 0)
        {
            return new FileRouteResult
            {
                Product = product,
                Operation = operation,
                Explicit = true,
                Evidence = "explicit product; engine validation required",
            };
        }

        RecognitionEntry entry = customRules
            ? (await RecognizeAsync([product], session, cancellationToken).ConfigureAwait(false)).Single()
            : new RecognitionEntry(product, Strongest(declarativeRules
                .Select(format => format.Recognition!.Evaluate(format.Id, session.Prefix.Span))));
        // The explicit selection settles evidence that is real but not conclusive; content
        // that contradicts the product still fails closed, as generic routing reports the file.
        if (entry.Recognition.Kind is not (FileRecognitionKind.Match or FileRecognitionKind.Indeterminate))
        {
            throw await ContradictedAsync(product, session, operation, cancellationToken).ConfigureAwait(false);
        }
        return Result(entry, operation, explicitSelection: true);
    }

    /// <summary>
    /// The error for content that contradicts an explicitly selected product: the one generic
    /// routing reports for the file, so that selecting the product changes no code. Content
    /// generic routing gives to another product is <c>FORMAT_MISMATCH</c> naming it in
    /// <c>details.detected</c>; any other content is the selected product's damaged file, since
    /// the product declares the extension.
    /// </summary>
    private async ValueTask<CliException> ContradictedAsync(
        ProductDefinition product,
        FileProbeSession session,
        string operation,
        CancellationToken cancellationToken)
    {
        FileRouteResult routed;
        try
        {
            routed = await ResolveGenericAsync(session, operation, cancellationToken).ConfigureAwait(false);
        }
        catch (CliException refused) when (refused.Code == ErrorCodes.FormatMismatch)
        {
            return refused;
        }
        catch (CliException)
        {
            // The selected product reads the extension, so content no product recognizes is
            // its damaged file, named after it.
            return CliErrors.ContentUnrecognized(session.FullPath, product.Manifest.Id);
        }

        return routed.Product == product
            ? CliErrors.ContentUnrecognized(session.FullPath, product.Manifest.Id)
            : CliErrors.FormatMismatch(session.FullPath, product.Manifest.Id, [routed.Product.Manifest.Id]);
    }

    private async ValueTask<FileProbeSession> CreateSessionAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);
        int descriptorLimit = _catalog.Products
            .Select(static product =>
                product.Files.Recognizer?.Descriptor.MaxProbeBytes ?? 0)
            .DefaultIfEmpty(0)
            .Max();
        int configuredLimit = descriptorLimit == 0
            ? _options.MaxPrefixBytes
            : Math.Min(_options.MaxPrefixBytes, descriptorLimit);
        int prefixLength = (int)Math.Min(info.Length, configuredLimit);
        byte[] prefix = new byte[prefixLength];
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 4096,
            useAsync: true);
        int total = 0;
        while (total < prefix.Length)
        {
            int read = await stream.ReadAsync(
                prefix.AsMemory(total),
                cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }
            total += read;
        }

        return new FileProbeSession(
            path,
            info.Length,
            prefix.AsMemory(0, total));
    }

    private async ValueTask<IReadOnlyList<RecognitionEntry>> RecognizeAsync(
        IReadOnlyList<ProductDefinition> products,
        FileProbeSession session,
        CancellationToken cancellationToken)
    {
        using OperationDeadline deadline = OperationDeadline.Start(_options.RecognizerTimeout, cancellationToken);
        using var concurrency = new SemaphoreSlim(_options.MaxConcurrency);

        Task<RecognitionEntry>[] work = products
            .Select(product => RecognizeOneAsync(
                product,
                session,
                concurrency,
                deadline))
            .ToArray();
        try
        {
            RecognitionEntry[] results = await Task.WhenAll(work)
                .ConfigureAwait(false);
            ThrowIfRoutingExpired(deadline);
            return results.OrderBy(
                    static entry => entry.Product.Manifest.Id,
                    StringComparer.Ordinal)
                .ToArray();
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw CliErrors.FileRoutingTimeout(_options.RecognizerTimeout);
        }
    }

    private static async Task<RecognitionEntry> RecognizeOneAsync(
        ProductDefinition product,
        FileProbeSession session,
        SemaphoreSlim concurrency,
        OperationDeadline deadline)
    {
        await concurrency.WaitAsync(deadline.Token).ConfigureAwait(false);
        try
        {
            IFileRecognizer recognizer = product.Files.Recognizer!;
            FileRecognition recognition;
            try
            {
                recognition = await recognizer.RecognizeAsync(
                    session,
                    deadline.Token).ConfigureAwait(false);
                if (recognition.Kind == FileRecognitionKind.Match
                    && recognition.Confidence == 0)
                {
                    recognition = recognition with { Confidence = 100 };
                }
                if (recognition.Confidence is < 0 or > 100)
                {
                    recognition = new FileRecognition
                    {
                        Kind = FileRecognitionKind.Failed,
                        Evidence = "recognizer returned invalid confidence",
                    };
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                recognition = new FileRecognition
                {
                    Kind = FileRecognitionKind.Failed,
                    Evidence = "recognizer failed",
                };
            }
            ThrowIfRoutingExpired(deadline);
            return new RecognitionEntry(product, recognition);
        }
        finally
        {
            concurrency.Release();
        }
    }

    /// <summary>
    /// Ends routing whose own budget ran out with the error that names that budget, which
    /// <c>--timeout</c> does not govern; caller cancellation stays a cancellation.
    /// </summary>
    private static void ThrowIfRoutingExpired(OperationDeadline deadline)
    {
        if (deadline.IsExpired)
        {
            throw CliErrors.FileRoutingTimeout(deadline.OriginalBudget!.Value);
        }

        deadline.Token.ThrowIfCancellationRequested();
    }

    private static bool EligibleForOperation(
        ProductDefinition product,
        string operation) =>
        product.Formats.Count == 0
            ? StandardFileRouteOperations.Contains(operation)
            : product.Formats.Any(format =>
                format.Uses.HasFlag(FormatUse.Input)
                && format.Ownership == RouteOwnership.Default
                && StandardFileRouteOperations.Contains(operation));

    private static RecognitionEntry[] StrongMatches(
        IEnumerable<RecognitionEntry> entries) =>
        entries
            .Where(static entry =>
                entry.Recognition.Kind == FileRecognitionKind.Match)
            .OrderByDescending(static entry => entry.Recognition.Confidence)
            .ThenBy(static entry => entry.Product.Manifest.Id, StringComparer.Ordinal)
            .ToArray();

    /// <summary>
    /// Products the content points to, for the error that refuses the route: every strong match,
    /// and every strong signature that fits several formats of one product.
    /// </summary>
    private static string[] Detected(
        IEnumerable<RecognitionEntry> recognitions,
        ProductDefinition? except) =>
        DetectedEntries(recognitions, except)
            .Select(static entry => entry.Product.Manifest.Id)
            .ToArray();

    private static IEnumerable<RecognitionEntry> DetectedEntries(
        IEnumerable<RecognitionEntry> recognitions,
        ProductDefinition? except) =>
        recognitions
            .Where(entry => entry.Product != except
                && (entry.Recognition.Kind == FileRecognitionKind.Match
                    || (entry.Recognition.Kind == FileRecognitionKind.Indeterminate
                        && entry.Recognition.Confidence >= DeclarativeFormatRecognizer.StrongConfidence)))
            .OrderByDescending(static entry => entry.Recognition.Confidence)
            .ThenBy(static entry => entry.Product.Manifest.Id, StringComparer.Ordinal);

    /// <summary>The strongest of several rule results: a match, then indeterminate evidence, then no match.</summary>
    private static FileRecognition Strongest(IEnumerable<FileRecognition> results) =>
        results
            .OrderBy(static result => result.Kind switch
            {
                FileRecognitionKind.Match => 0,
                FileRecognitionKind.Indeterminate => 1,
                _ => 2,
            })
            .ThenByDescending(static result => result.Confidence)
            .ThenBy(static result => result.FormatId, StringComparer.Ordinal)
            .First();

    private static IReadOnlyList<string> CandidateIds(
        IEnumerable<ProductDefinition> products) =>
        products.Select(static product => product.Manifest.Id)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static FileRouteResult Result(
        RecognitionEntry entry,
        string operation,
        bool explicitSelection) =>
        new()
        {
            Product = entry.Product,
            Operation = operation,
            Explicit = explicitSelection,
            FormatId = entry.Recognition.FormatId,
            Evidence = entry.Recognition.Evidence,
        };

    private sealed record RecognitionEntry(
        ProductDefinition Product,
        FileRecognition Recognition);
}
