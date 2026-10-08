using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Sdk.Results;

/// <summary>
/// The SDK write pipeline of one product in one invocation, and the sole owner of evaluation
/// disclosure. It resolves the product's license state once, publishes every output the product
/// writes, and inspects each published document, once it is saved and before it is published,
/// through the product's <see cref="IEvaluationProfile{TDocument}"/>. The command template then
/// adds the disclosure to the result (<see cref="Disclose"/>): <c>EVAL_MODE</c> when evaluation
/// mode produced an output, naming the evaluation marks it carries, and <c>EVAL_INPUT_MARKED</c>
/// when a licensed output carries evaluation marks, which a licensed save never adds, so they come
/// from its input. A command that publishes nothing, such as a dry run or a read, discloses
/// nothing.
/// </summary>
public abstract class OutputPipeline : ILicenseState
{
    private readonly ILicenseGate _licenseGate;
    private readonly object _sync = new();
    private int _published;
    private EvaluationMarks _kept = EvaluationMarks.None;
    private EvaluationMarks _shown = EvaluationMarks.None;

    private protected OutputPipeline(ILicenseGate licenseGate, SafeFileWriter writer)
    {
        _licenseGate = licenseGate ?? throw new ArgumentNullException(nameof(licenseGate));
        Writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    /// <inheritdoc />
    public LicenseState License => _licenseGate.EnsureApplied();

    /// <summary>Whether the engine runs in evaluation mode.</summary>
    public bool IsEvaluation => License == LicenseState.Evaluation;

    private protected SafeFileWriter Writer { get; }

    /// <summary>
    /// Starts an extraction into <paramref name="root"/>, published when the guard commits.
    /// </summary>
    /// <param name="root">The extraction directory, which may not exist yet.</param>
    /// <param name="overwrite">Whether an existing file in the directory may be replaced (<c>--overwrite</c>).</param>
    public ExtractionGuard BeginExtraction(string root, bool overwrite) =>
        new(Writer.ResourceBudgets, root, overwrite) { Committed = () => Record(null) };

    /// <summary>
    /// Records the files the host is about to publish from this product's renders, such as
    /// review evidence, which show what the engine draws, its evaluation watermark included, and
    /// adds the disclosure to the result those files carry, so the files and the command's
    /// output say the same.
    /// </summary>
    public ResultEnvelope DiscloseRendered(ResultEnvelope result)
    {
        Record(null);
        return Disclose(result);
    }

    /// <summary>
    /// Adds the evaluation disclosure of what this invocation published to its result, before
    /// the result's own warnings; returns the result unchanged when it published nothing.
    /// </summary>
    public ResultEnvelope Disclose(ResultEnvelope result)
    {
        ArgumentNullException.ThrowIfNull(result);
        IReadOnlyList<Warning>? disclosure = Disclosure();
        return disclosure is null
            ? result
            : result with { Warnings = EnvelopeParts.CombineWarnings(disclosure, result.Warnings) };
    }

    /// <summary>The disclosure of what this invocation published so far, or null.</summary>
    internal IReadOnlyList<Warning>? Disclosure()
    {
        int published;
        EvaluationMarks kept;
        EvaluationMarks shown;
        lock (_sync)
        {
            (published, kept, shown) = (_published, _kept, _shown);
        }

        if (published == 0)
        {
            return null;
        }

        return License switch
        {
            LicenseState.Evaluation => [EvaluationDisclosure.Output(
                _licenseGate.Resolution.Kind == LicenseSourceKind.EvaluationRequested, kept, shown)],
            LicenseState.Licensed when kept.Any || shown.Any => [EvaluationDisclosure.InputMarked(kept, shown)],
            _ => null,
        };
    }

    /// <summary>
    /// Records one published output and, when it was inspected, its marks: those a saved copy
    /// keeps, or those the source of a rendering carries.
    /// </summary>
    internal void Record(Inspection? inspection)
    {
        lock (_sync)
        {
            _published++;
            if (inspection?.Marks is { } marks)
            {
                if (inspection.Rendering)
                {
                    _shown = _shown.Union(marks);
                }
                else
                {
                    _kept = _kept.Union(marks);
                }
            }
        }
    }

    /// <summary>
    /// The marks of one published document, or null when inspecting it failed and its marks are
    /// unknown; <see cref="Rendering"/> says the output shows the document rather than keeping
    /// a copy of it, as page images and extracted page text do.
    /// </summary>
    internal sealed record Inspection(EvaluationMarks? Marks, bool Rendering);
}

/// <summary>The evaluation warnings the write pipeline adds to a result.</summary>
internal static class EvaluationDisclosure
{
    /// <summary>
    /// <c>EVAL_MODE</c> for outputs evaluation mode produced, naming the marks the outputs keep
    /// and those the rendered sources carry.
    /// </summary>
    internal static Warning Output(bool requested, EvaluationMarks kept, EvaluationMarks shown)
    {
        Warning disclosure = requested ? EnvelopeParts.RequestedEvaluationWatermark : EnvelopeParts.EvaluationWatermark;
        string message = disclosure.Message;
        if (kept.Any)
        {
            message += $" The output carries {List(kept)}.";
        }

        if (Beyond(shown, kept) is { Any: true } source)
        {
            message += $" The rendered source carries {List(source)}, which the output shows.";
        }

        return disclosure with { Message = message };
    }

    /// <summary>
    /// <c>EVAL_INPUT_MARKED</c> for licensed outputs that keep the evaluation marks of their
    /// input, or that render a source which carries them.
    /// </summary>
    internal static Warning InputMarked(EvaluationMarks kept, EvaluationMarks shown)
    {
        var sentences = new List<string>();
        if (kept.Any)
        {
            sentences.Add($"The output keeps the evaluation marks that an earlier save without a license wrote into its input: {List(kept)}.");
        }

        if (Beyond(shown, kept) is { Any: true } source)
        {
            sentences.Add($"The rendered source carries the evaluation marks that an earlier save without a license wrote into it, which the output shows: {List(source)}.");
        }

        sentences.Add("The license does not remove them.");
        return new Warning
        {
            Code = WarningCodes.EvalInputMarked,
            Message = string.Join(" ", sentences),
            Hint = "Tell the user. Regenerate the deliverable from the original, unmarked inputs with a license; editing this file keeps the marks.",
            Docs = "licensing",
            AffectsCompleteness = kept.IsTruncated,
        };
    }

    private static EvaluationMarks Beyond(EvaluationMarks shown, EvaluationMarks kept) =>
        new([.. shown.Marks.Except(kept.Marks, StringComparer.Ordinal)], shown.IsTruncated && !kept.IsTruncated);

    private static string List(EvaluationMarks marks)
    {
        string[] items = [.. marks.Marks, .. marks.IsTruncated
            ? new[] { "the notice that evaluation mode cut the content short, so the content after it is missing" }
            : []];
        return items.Length switch
        {
            1 => items[0],
            _ => string.Join(", ", items[..^1]) + " and " + items[^1],
        };
    }
}

/// <summary>
/// The write pipeline of a product whose engine documents are <typeparamref name="TDocument"/>:
/// a publication names the document it saves or renders, so the pipeline inspects it through
/// the product's evaluation profile after the save and before the output is published.
/// Inspection never fails a publication: when it throws, the marks are unknown, and the
/// disclosure is the plain one.
/// </summary>
/// <typeparam name="TDocument">The engine's document type.</typeparam>
public sealed class OutputPipeline<TDocument> : OutputPipeline
    where TDocument : class
{
    private readonly IEvaluationProfile<TDocument> _profile;

    /// <summary>Creates the pipeline of one product invocation.</summary>
    /// <param name="licenseGate">The product's license gate, which the pipeline alone applies.</param>
    /// <param name="profile">How the product recognizes its evaluation marks.</param>
    /// <param name="writer">The invocation's atomic file writer.</param>
    public OutputPipeline(ILicenseGate licenseGate, IEvaluationProfile<TDocument> profile, SafeFileWriter writer)
        : base(licenseGate, writer)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
    }

    /// <summary>
    /// In evaluation mode, removes the marks evaluation mode added when it opened the document,
    /// before the document becomes part of another; with a license the document carries none.
    /// </summary>
    public void Neutralize(TDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (IsEvaluation)
        {
            _profile.Neutralize(document);
        }
    }

    /// <summary>
    /// Publishes one file that <paramref name="write"/> saves from <paramref name="document"/>,
    /// or that holds no document when it is null, such as exported form data.
    /// </summary>
    /// <param name="targetPath">The output file; its extension names the format the profile inspects.</param>
    /// <param name="overwrite">Whether an existing file may be replaced.</param>
    /// <param name="document">The document the file holds or shows, or null.</param>
    /// <param name="write">Writes the staged file.</param>
    /// <param name="rendering">Whether the file shows <paramref name="document"/>, as page images and page text do, rather than keeping a copy of it.</param>
    /// <param name="pages">The pages (or slides) of <paramref name="document"/> a rendering shows, numbered from 1; null for all of them.</param>
    public long Write(string targetPath, bool overwrite, TDocument? document, Action<string> write, bool rendering = false,
        IEnumerable<int>? pages = null)
    {
        var inspector = new Inspector(_profile, document, FormatOf(targetPath), rendering);
        inspector.Shows(pages);
        long size = Writer.Write(targetPath, overwrite, inspector.After(write));
        Record(inspector.Result);
        return size;
    }

    /// <summary>
    /// Publishes <paramref name="document"/> to a resolved output, with the companion files its
    /// format writes beside it, and describes every file published.
    /// </summary>
    public IReadOnlyList<OutputInfo> Write(ResolvedOutput output, TDocument document, Action<string> write)
    {
        ArgumentNullException.ThrowIfNull(output);
        var inspector = new Inspector(_profile, document, output.Format.Id, rendering: false);
        IReadOnlyList<OutputInfo> written = Writer.Write(output, inspector.After(write));
        Record(inspector.Result);
        return written;
    }

    /// <summary>
    /// Starts a set of outputs spanning directories on the same filesystem, published together
    /// when the set commits.
    /// </summary>
    public OutputSet<TDocument> BeginSet(IEnumerable<string> targetDirectories, string operation) =>
        new(this, _profile, new AtomicOutputSetWriter(Writer, targetDirectories, operation));

    internal static string FormatOf(string targetPath) =>
        Path.GetExtension(targetPath).TrimStart('.').ToLowerInvariant();

    /// <summary>
    /// Inspects one document once, right after the first save that writes it to an output of
    /// one format, while the product still holds it open.
    /// </summary>
    internal sealed class Inspector(IEvaluationProfile<TDocument> profile, TDocument? document, string format, bool rendering)
    {
        private readonly SortedSet<int> _pages = [];
        private bool _saved;
        private bool _inspected;
        private EvaluationMarks? _marks;

        internal TDocument? Document => document;

        internal string Format => format;

        internal bool Rendering => rendering;

        /// <summary>
        /// The marks found; null when there is no document; null marks when inspecting it failed.
        /// A rendering of selected pages is inspected over all of them once they are rendered,
        /// while the product still holds the document open.
        /// </summary>
        internal Inspection? Result
        {
            get
            {
                if (document is null)
                {
                    return null;
                }

                if (_saved && !_inspected)
                {
                    Inspect(document);
                }

                return new Inspection(_marks, rendering);
            }
        }

        /// <summary>Adds the pages (or slides) a rendering shows, numbered from 1; none means the whole document.</summary>
        internal void Shows(IEnumerable<int>? pages)
        {
            if (pages is not null)
            {
                _pages.UnionWith(pages);
            }
        }

        internal Action<string> After(Action<string> write)
        {
            ArgumentNullException.ThrowIfNull(write);
            return path =>
            {
                write(path);
                _saved = true;
                // A saved copy is inspected right after its save, before anything, such as a
                // facade that closes the document, can change it.
                if (document is not null && !_inspected && _pages.Count == 0)
                {
                    Inspect(document);
                }
            };
        }

        // The disclosure must never cost the user an output: an inspection that fails leaves
        // the marks unknown, and evaluation mode is still disclosed.
        private void Inspect(TDocument document)
        {
            _inspected = true;
            try
            {
                _marks = _pages.Count == 0 ? profile.Inspect(document, format) : profile.Inspect(document, format, _pages);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _marks = null;
            }
        }
    }
}

/// <summary>
/// A set of outputs staged together and published, refused or rolled back as a whole, through
/// the product's <see cref="OutputPipeline{TDocument}"/>. A document staged to several outputs
/// of one format, such as the pages of a render, is inspected once. Disposing a set that did not
/// commit rolls it back.
/// </summary>
/// <typeparam name="TDocument">The engine's document type.</typeparam>
public sealed class OutputSet<TDocument> : IDisposable
    where TDocument : class
{
    private readonly OutputPipeline<TDocument> _pipeline;
    private readonly IEvaluationProfile<TDocument> _profile;
    private readonly AtomicOutputSetWriter _transaction;
    private readonly List<OutputPipeline<TDocument>.Inspector> _documents = [];
    private int _staged;

    internal OutputSet(OutputPipeline<TDocument> pipeline, IEvaluationProfile<TDocument> profile, AtomicOutputSetWriter transaction)
    {
        _pipeline = pipeline;
        _profile = profile;
        _transaction = transaction;
    }

    /// <summary>Admits a directory and owns only directories this set actually creates.</summary>
    public void EnsureDirectory(string path) => _transaction.EnsureDirectory(path);

    /// <summary>
    /// Stages one output that <paramref name="write"/> saves from <paramref name="document"/>, or
    /// that holds no document when it is null.
    /// </summary>
    /// <param name="targetPath">The output file; its extension names the format the profile inspects.</param>
    /// <param name="overwrite">Whether an existing file may be replaced.</param>
    /// <param name="document">The document the output holds or shows, or null.</param>
    /// <param name="write">Writes the staged file.</param>
    /// <param name="rendering">Whether the output shows <paramref name="document"/>, as a page image does, rather than keeping a copy of it.</param>
    /// <param name="backupPath">The stable backup of the file the output replaces, if requested.</param>
    /// <param name="inputPrecondition">The input the output replaces, which must not change meanwhile.</param>
    /// <param name="verify">Checks the staged file before it is published.</param>
    /// <param name="pages">The pages (or slides) of <paramref name="document"/> a rendering shows, numbered from 1; null for all of them.</param>
    public StagedOutput Stage(string targetPath, bool overwrite, TDocument? document, Action<string> write,
        bool rendering = false, string? backupPath = null, FileWritePrecondition? inputPrecondition = null,
        Action<string>? verify = null, IEnumerable<int>? pages = null)
    {
        string format = OutputPipeline<TDocument>.FormatOf(targetPath);
        OutputPipeline<TDocument>.Inspector? inspector = document is null ? null
            : _documents.Find(item => ReferenceEquals(item.Document, document) && item.Rendering == rendering
                && string.Equals(item.Format, format, StringComparison.Ordinal));
        if (document is not null && inspector is null)
        {
            inspector = new OutputPipeline<TDocument>.Inspector(_profile, document, format, rendering);
            _documents.Add(inspector);
        }

        inspector?.Shows(pages);

        StagedOutput staged = _transaction.Stage(targetPath, overwrite, backupPath, inputPrecondition,
            inspector?.After(write) ?? write, verify);
        _staged++;
        return staged;
    }

    /// <summary>
    /// Publishes every staged output and returns their sizes in stage order. A failed commit is
    /// recovered and reported as a stable publication error.
    /// </summary>
    public IReadOnlyList<long> Commit()
    {
        IReadOnlyList<long> sizes = _transaction.Commit();
        foreach (OutputPipeline<TDocument>.Inspector inspector in _documents)
        {
            _pipeline.Record(inspector.Result);
        }

        for (int index = _documents.Count; index < _staged; index++)
        {
            _pipeline.Record(null);
        }

        return sizes;
    }

    /// <inheritdoc />
    public void Dispose() => _transaction.Dispose();
}
