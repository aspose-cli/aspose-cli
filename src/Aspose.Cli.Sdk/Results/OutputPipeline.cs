using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Sdk.Results;

/// <summary>
/// The SDK write pipeline of one product in one invocation, and the sole owner of evaluation
/// disclosure. It resolves the product's license state once, publishes every output the product
/// writes, and inspects each published document, once it is saved and before it is published,
/// through the product's <see cref="IEvaluationProfile{TDocument}"/>. The command template then
/// adds the disclosure to the result (<see cref="Disclose"/>): <c>EVALUATION_MODE</c> when evaluation
/// mode produced an output, naming the evaluation marks it carries, and <c>EVALUATION_INPUT_MARKED</c>
/// when a licensed output carries evaluation marks, which a licensed save never adds, so they come
/// from its input. It is also the sole owner of the <c>ENCRYPTION_REMOVED</c> warning: a product
/// tells it whether each document it opened is encrypted (<see cref="Opened"/>) and whether each
/// copy it publishes is, and the pipeline compares the two. A command that publishes nothing,
/// such as a dry run or a read, discloses nothing.
/// </summary>
public abstract class OutputPipeline : ILicenseState
{
    private readonly ILicenseGate _licenseGate;
    private readonly object _sync = new();
    private int _published;
    private EvaluationMarks _kept = EvaluationMarks.None;
    private EvaluationMarks _shown = EvaluationMarks.None;
    private readonly List<string> _marked = [];
    private Warning? _inputTruncated;
    private Inspection? _view;
    private bool _encryptedInput;
    private bool _openPassword;
    private bool _decrypted;
    private JsonObject? _restrictions;
    private readonly SortedSet<string> _plainCopies = new(StringComparer.Ordinal);
    private IReadOnlyList<FormatDescriptor> _formats = [];

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
    /// Starts an extraction into <paramref name="root"/>, published when the guard commits. What
    /// the engine derives from the documents, such as their text or tables, is an output of this
    /// invocation and disclosed as one. Files the documents embed, copied byte for byte as the
    /// document stores them (<see cref="ExtractionContent.EmbeddedFiles"/>), are no output of the
    /// engine and carry nothing evaluation mode adds, so they are not disclosed.
    /// </summary>
    /// <param name="root">The extraction directory, which may not exist yet.</param>
    /// <param name="overwrite">Whether an existing file in the directory may be replaced (<c>--overwrite</c>).</param>
    /// <param name="content">What the extraction writes.</param>
    public ExtractionGuard BeginExtraction(string root, bool overwrite, ExtractionContent content = ExtractionContent.Derived) =>
        new(Writer.ResourceBudgets, root, overwrite)
        {
            Committed = content == ExtractionContent.EmbeddedFiles ? null : () => Record(null),
        };

    /// <summary>
    /// Publishes one file an extraction writes, such as exported form data, which holds parts
    /// of the opened documents rather than a copy of them, so it is never disclosed as one.
    /// </summary>
    /// <param name="targetPath">The output file.</param>
    /// <param name="overwrite">Whether an existing file may be replaced.</param>
    /// <param name="write">Writes the staged file.</param>
    /// <returns>The facts of the published file.</returns>
    public PublishedFile WriteExtracted(string targetPath, bool overwrite, Action<string> write)
    {
        PublishedFile published = Writer.Write(targetPath, overwrite, write);
        Record(null);
        return published;
    }

    /// <summary>
    /// Records the files the host is about to publish from this product's renders, such as
    /// review evidence, which show what the engine draws, its evaluation watermark included, and
    /// adds the disclosure to the result those files carry, so the files and the command's
    /// output say the same. The disclosure names the marks of the pages the product's view
    /// rendering showed (<see cref="OutputPipeline{TDocument}.ShowsInView"/>), as a render's does.
    /// </summary>
    public ResultEnvelope DiscloseRendered(ResultEnvelope result)
    {
        Inspection? view;
        lock (_sync)
        {
            view = _view;
        }

        Record(view);
        return Disclose(result);
    }

    /// <summary>Keeps the marks of the latest view rendering, which <see cref="DiscloseRendered"/> discloses if the host publishes it.</summary>
    private protected void RecordView(Inspection inspection)
    {
        lock (_sync)
        {
            _view = inspection;
        }
    }

    /// <summary>
    /// Records that the command opened a document whose content its outputs keep: the input it
    /// edits, converts, merges, splits or compares, or a document an operation takes content
    /// from. When one of them is encrypted, every copy the command publishes without encryption
    /// is disclosed as <c>ENCRYPTION_REMOVED</c>.
    /// </summary>
    /// <param name="encrypted">Whether the document is encrypted, including a PDF with only an owner password.</param>
    /// <param name="openPassword">Whether an encrypted document needs a password to open, not only to change it.</param>
    /// <param name="restrictions">
    /// For an encrypted document, the fields of the operation that encrypts
    /// (<see cref="Operations.EncryptsOutputAttribute"/>) that restrict a copy as the document is
    /// restricted, such as the permissions it grants readers without the owner password.
    /// </param>
    public void Opened(bool encrypted, bool openPassword = true, JsonObject? restrictions = null)
    {
        lock (_sync)
        {
            _encryptedInput |= encrypted;
            _openPassword |= encrypted && openPassword;
            if (encrypted)
            {
                _restrictions ??= restrictions;
            }
        }
    }

    /// <summary>Records that the command removed the encryption because it was asked to, such as by an operation that decrypts.</summary>
    public void Decrypted()
    {
        lock (_sync)
        {
            _decrypted = true;
        }
    }

    /// <summary>
    /// Adds the evaluation disclosure of what this invocation published, and of an input it read
    /// that evaluation mode cut short, and the encryption it removed, to its result, before the
    /// result's own warnings; returns the result unchanged when there is nothing to disclose.
    /// </summary>
    public ResultEnvelope Disclose(ResultEnvelope result) => DiscloseWith(result, protection: null);

    /// <inheritdoc cref="Disclose(ResultEnvelope)"/>
    /// <param name="result">The result.</param>
    /// <param name="protection">
    /// The hint of <c>ENCRYPTION_REMOVED</c> for the format ids of the unencrypted copies: the
    /// ways the command's tree offers to protect them; null for the general advice.
    /// </param>
    internal ResultEnvelope DiscloseWith(ResultEnvelope result, Func<EncryptionLoss, string>? protection)
    {
        ArgumentNullException.ThrowIfNull(result);
        // A result that carries a disclosure already, such as a review whose view manifest
        // carried the input's truncation, is not told twice.
        Warning[] disclosure = [.. (Disclosure() ?? []).Append(EncryptionRemoved(protection)).OfType<Warning>().Where(warning =>
            !(result.Warnings ?? []).Any(present => present.Code == warning.Code))];
        return disclosure.Length == 0
            ? result
            : result with { Warnings = EnvelopeParts.CombineWarnings(disclosure, result.Warnings) };
    }

    /// <summary>
    /// <c>ENCRYPTION_REMOVED</c> when the command opened an encrypted document and published a
    /// copy of it without encryption; otherwise null.
    /// </summary>
    /// <param name="protection">Writes the hint for the format ids of the unencrypted copies; null for the general advice.</param>
    internal Warning? EncryptionRemoved(Func<EncryptionLoss, string>? protection = null)
    {
        string[] formats;
        bool openPassword;
        bool decrypted;
        JsonObject? restrictions;
        lock (_sync)
        {
            if (!_encryptedInput || _plainCopies.Count == 0)
            {
                return null;
            }

            formats = [.. _plainCopies];
            openPassword = _openPassword;
            decrypted = _decrypted;
            restrictions = _restrictions;
        }

        return new Warning(
            WarningCodes.EncryptionRemoved,
            $"The {string.Join(", ", formats.Select(static format => $"'{format}'"))} output is not encrypted, although an input it was made from is: it opens without a password.")
        {
            Hint = protection?.Invoke(new EncryptionLoss(formats, openPassword, decrypted, restrictions)) ?? "If the output must stay protected, write it in a format that can carry a password.",
        };
    }

    /// <summary>
    /// Declares the formats the product writes, as its definition declares them once
    /// (<see cref="FormatDescriptor.Image"/>), by which a published file is told to be an image
    /// or a copy.
    /// </summary>
    internal void Declare(IReadOnlyList<FormatDescriptor> formats)
    {
        ArgumentNullException.ThrowIfNull(formats);
        lock (_sync)
        {
            _formats = formats;
        }
    }

    /// <summary>
    /// The published file at <paramref name="path"/>: its format among those the product
    /// declares, named by its id, or its extension when none declares it.
    /// </summary>
    internal Copy CopyAt(string path, bool encrypted)
    {
        IReadOnlyList<FormatDescriptor> formats;
        lock (_sync)
        {
            formats = _formats;
        }

        FormatDescriptor[] named = [.. formats.Where(format => format.Names(path))];
        return named.Length == 0
            ? new Copy(Path.GetExtension(path).TrimStart('.').ToLowerInvariant(), Image: false, encrypted)
            : new Copy(named[0].Id, named.All(static format => format.Image), encrypted);
    }

    /// <summary>
    /// Records a published file: a file in a format that keeps the content of the documents the
    /// command opened is a copy, and an unencrypted copy is disclosed when an opened document
    /// was encrypted. Its declared format alone decides, never how the product rendered it, so
    /// an image is never disclosed; nor is what an extraction writes, which is not recorded here.
    /// </summary>
    internal void RecordCopy(Copy copy)
    {
        if (copy is { Encrypted: false, Image: false } plain)
        {
            lock (_sync)
            {
                _plainCopies.Add(plain.Format);
            }
        }
    }

    /// <summary>
    /// The disclosure of what this invocation published so far, and of an input it read that
    /// evaluation mode cut short (<see cref="OutputPipeline{TDocument}.ReadInput"/>), or null.
    /// </summary>
    internal IReadOnlyList<Warning>? Disclosure()
    {
        int published;
        EvaluationMarks kept;
        EvaluationMarks shown;
        Warning? truncated;
        string[] marked;
        lock (_sync)
        {
            (published, kept, shown, truncated) = (_published, _kept, _shown, _inputTruncated);
            marked = [.. _marked];
        }

        // With several outputs, the marks are named with the files that carry them.
        string subject = published > 1 && marked.Length > 0 ? EvaluationDisclosure.Outputs(marked) : "The output";
        Warning? output = published == 0 ? null : License switch
        {
            LicenseState.Evaluation => EvaluationDisclosure.Output(
                _licenseGate.Resolution.Kind == LicenseSourceKind.EvaluationRequested, kept, shown, subject),
            LicenseState.Licensed when kept.Any || shown.Any => EvaluationDisclosure.InputMarked(kept, shown, subject),
            _ => null,
        };
        Warning[] disclosure = [.. new[] { output, truncated }.OfType<Warning>()];
        return disclosure.Length == 0 ? null : disclosure;
    }

    /// <summary>
    /// The <c>EVALUATION_INPUT_TRUNCATED</c> warning for an input the invocation read so far
    /// (<see cref="OutputPipeline{TDocument}.ReadInput"/>), or null; a view manifest carries it
    /// as a command's result does.
    /// </summary>
    internal Warning? InputTruncation
    {
        get
        {
            lock (_sync)
            {
                return _inputTruncated;
            }
        }
    }

    /// <summary>
    /// Runs a command handler or a view call of the product, and reports an evaluation limit
    /// the engine raised in it as <c>EVALUATION_LIMIT</c>: in evaluation mode, an exception that
    /// is not already a CLI error and that the product's evaluation profile recognizes. This is
    /// the one place that translates an engine's evaluation exception; the error keeps it as its
    /// inner exception. Its hint offers a selection only when <paramref name="selection"/> names
    /// one the command takes over the limit's unit, which keeps the engine from reading the
    /// parts it leaves out.
    /// </summary>
    /// <param name="run">The handler or view call.</param>
    /// <param name="selection">
    /// The selection option of the command that narrows what the engine reads, for a unit such
    /// as <c>pages</c>, such as <c>--pages</c>; null for none.
    /// </param>
    internal T ReportingEvaluationLimits<T>(Func<T> run, Func<string, string?>? selection = null)
    {
        ArgumentNullException.ThrowIfNull(run);
        try
        {
            return run();
        }
        catch (Exception exception) when (exception is not CliException and not OperationCanceledException
            && EvaluationLimitOf(exception, selection) is { } limit)
        {
            throw limit;
        }
    }

    /// <summary>The <c>EVALUATION_LIMIT</c> error for an engine exception, or null when it reports no evaluation limit.</summary>
    private protected abstract CliException? EvaluationLimitOf(Exception exception, Func<string, string?>? selection);

    /// <summary>Keeps the <c>EVALUATION_INPUT_TRUNCATED</c> warning, once, for <see cref="Disclose"/>.</summary>
    private protected void RecordInputTruncated(Warning warning)
    {
        lock (_sync)
        {
            _inputTruncated ??= warning;
        }
    }

    /// <summary>
    /// Records one published output and, when it was inspected, its marks: those a saved copy
    /// keeps, or those the source of a rendering carries. The files of a copy that keeps marks,
    /// <paramref name="paths"/>, are named in the disclosure when the invocation published others.
    /// </summary>
    internal void Record(Inspection? inspection, IEnumerable<string>? paths = null)
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
                    if (marks.Any)
                    {
                        _marked.AddRange((paths ?? []).Select(System.IO.Path.GetFileName).OfType<string>()
                            .Where(name => !_marked.Contains(name, StringComparer.Ordinal)));
                    }
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

    /// <summary>A published file: its format id, whether that format is an image, and whether the file is encrypted.</summary>
    internal sealed record Copy(string Format, bool Image, bool Encrypted);
}

/// <summary>The evaluation warnings the write pipeline adds to a result.</summary>
internal static class EvaluationDisclosure
{
    /// <summary>
    /// <c>EVALUATION_MODE</c> for outputs evaluation mode produced, naming the marks the outputs keep
    /// and those the rendered sources carry.
    /// </summary>
    /// <param name="requested">Whether the caller asked for evaluation mode.</param>
    /// <param name="kept">The marks the outputs keep.</param>
    /// <param name="shown">The marks the rendered sources carry.</param>
    /// <param name="subject">The outputs that keep the marks, such as <c>The outputs 'a.1.pdf' and 'a.2.pdf'</c>.</param>
    internal static Warning Output(bool requested, EvaluationMarks kept, EvaluationMarks shown, string subject = "The output")
    {
        Warning disclosure = requested ? EnvelopeParts.RequestedEvaluationWatermark : EnvelopeParts.EvaluationWatermark;
        string message = disclosure.Message;
        if (kept.Any)
        {
            message += $" {subject} {(subject.StartsWith("The outputs", StringComparison.Ordinal) ? "carry" : "carries")} {List(kept)}.";
        }

        if (Beyond(shown, kept) is { Any: true } source)
        {
            message += $" The rendered source carries {List(source)}, which the output shows.";
        }

        return disclosure with { Message = message };
    }

    /// <summary>
    /// <c>EVALUATION_INPUT_MARKED</c> for licensed outputs that keep the evaluation marks of their
    /// input, or that render a source which carries them.
    /// </summary>
    /// <param name="kept">The marks the outputs keep.</param>
    /// <param name="shown">The marks the rendered sources carry.</param>
    /// <param name="subject">The outputs that keep the marks, as for <see cref="Output"/>.</param>
    internal static Warning InputMarked(EvaluationMarks kept, EvaluationMarks shown, string subject = "The output")
    {
        var sentences = new List<string>();
        if (kept.Any)
        {
            string verb = subject.StartsWith("The outputs", StringComparison.Ordinal) ? "keep" : "keeps";
            sentences.Add($"{subject} {verb} the evaluation marks that an earlier save without a license wrote into its input: {List(kept)}.");
        }

        if (Beyond(shown, kept) is { Any: true } source)
        {
            sentences.Add($"The rendered source carries the evaluation marks that an earlier save without a license wrote into it, which the output shows: {List(source)}.");
        }

        sentences.Add("The license does not remove them.");
        return new Warning(WarningCodes.EvaluationInputMarked, string.Join(" ", sentences))
        {
            Hint = "Tell the user. Regenerate the deliverable from the original, unmarked inputs with a license; editing this file keeps the marks.",
            Docs = "licensing",
            AffectsCompleteness = kept.IsTruncated,
        };
    }

    /// <summary>
    /// <c>EVALUATION_INPUT_TRUNCATED</c> for a result that read an input evaluation mode cut short,
    /// in the words of the product's evaluation profile.
    /// </summary>
    /// <param name="engine">The engine, which also names the license, such as <c>Aspose.Words</c>.</param>
    /// <param name="truncation">What evaluation mode leaves out, completing "Aspose.X evaluation mode ...".</param>
    internal static Warning InputTruncated(string engine, string truncation) => new(
        WarningCodes.EvaluationInputTruncated,
        $"{engine} evaluation mode {truncation}, so this result does not hold all of the input's content.")
    {
        Hint = $"Do not treat the result as complete; apply an {engine} license and run the command again.",
        Docs = "licensing",
        // What a command covers stays complete, as a review's page images show the full content;
        // the warning says the content it read is not.
    };

    /// <summary>The outputs named by their files, such as <c>The output 'a.2.pdf'</c> or <c>The outputs 'a.1.pdf' and 'a.2.pdf'</c>.</summary>
    internal static string Outputs(IReadOnlyList<string> files)
    {
        string[] quoted = [.. files.Select(static file => $"'{file}'")];
        return quoted.Length == 1
            ? $"The output {quoted[0]}"
            : $"The outputs {string.Join(", ", quoted[..^1])} and {quoted[^1]}";
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
    /// Records that the command read <paramref name="document"/>, one of its inputs, into its
    /// result, as an inspect, a read, a search or a text extraction does. In evaluation mode,
    /// when the product's evaluation profile finds the document cut short
    /// (<see cref="EvaluationMarks.IsTruncated"/>), the result carries
    /// <c>EVALUATION_INPUT_TRUNCATED</c>, which only the SDK writes; with a license it records
    /// nothing, since a licensed load cuts nothing short.
    /// </summary>
    public void ReadInput(TDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (IsEvaluation && _profile.Inspect(document).IsTruncated)
        {
            RecordInputTruncated(EvaluationDisclosure.InputTruncated(_profile.Engine, _profile.Truncation));
        }
    }

    private protected override CliException? EvaluationLimitOf(Exception exception, Func<string, string?>? selection)
    {
        if (_profile.RecognizeLimit(exception) is not { } limit || !IsEvaluation)
        {
            return null;
        }

        // A selection is offered only where the command takes one that keeps the engine within
        // the limit; otherwise the hint names no way around it that would fail the same way.
        string[] alternatives =
        [
            .. selection?.Invoke(limit.Unit) is { } option
                ? new[] { string.Create(System.Globalization.CultureInfo.InvariantCulture, $"pass {option} 1-{limit.Limit}") }
                : [],
            .. limit.Alternative is { } alternative ? new[] { alternative } : [],
        ];
        return CliErrors.EngineEvaluationLimit(_profile.Engine, limit.Allows, limit.Limit, limit.Unit, alternatives, exception);
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
    /// <param name="encrypted">
    /// Whether the written file is encrypted. A file in a document, workbook or presentation
    /// format is a copy of the documents the command opened, and an unencrypted copy of an
    /// encrypted one is disclosed; an image or a data file never is.
    /// </param>
    /// <returns>The facts of the published file.</returns>
    public PublishedFile Write(string targetPath, bool overwrite, TDocument? document, Action<string> write, bool rendering = false,
        IEnumerable<int>? pages = null, bool encrypted = false)
    {
        string format = FormatOf(targetPath);
        var inspector = new Inspector(_profile, document, format, rendering);
        inspector.Shows(pages);
        PublishedFile published = Writer.Write(targetPath, overwrite, inspector.After(write));
        Record(inspector.Result, [targetPath]);
        RecordCopy(CopyAt(targetPath, encrypted));
        return published;
    }

    /// <summary>
    /// Publishes <paramref name="document"/> to a resolved output, with the companion files its
    /// format writes beside it, and describes every file published.
    /// </summary>
    /// <param name="output">The resolved output.</param>
    /// <param name="document">The document the output holds.</param>
    /// <param name="encrypted">Whether the written output carries a password.</param>
    /// <param name="write">Writes the staged output.</param>
    public IReadOnlyList<OutputInfo> Write(ResolvedOutput output, TDocument document, bool encrypted, Action<string> write)
    {
        ArgumentNullException.ThrowIfNull(output);
        var inspector = new Inspector(_profile, document, output.Format.Id, rendering: false);
        IReadOnlyList<OutputInfo> written = Writer.Write(output, encrypted, inspector.After(write));
        Record(inspector.Result, [output.Path]);
        RecordCopy(new Copy(output.Format.Id, output.Format.Image, encrypted));
        return written;
    }

    /// <summary>
    /// Starts a set of outputs spanning directories on the same filesystem, published together
    /// when the set commits.
    /// </summary>
    public OutputSet<TDocument> BeginSet(IEnumerable<string> targetDirectories, string operation) =>
        new(this, _profile, new AtomicOutputSetWriter(Writer, targetDirectories, operation));

    /// <summary>
    /// Records that a view rendering, such as review evidence or a live preview, showed
    /// <paramref name="document"/>: its pages <paramref name="pages"/> as <paramref name="format"/>
    /// parts, or the whole document when null. The document is inspected now, while the product
    /// holds it open; nothing is published until the host publishes the rendering
    /// (<see cref="OutputPipeline.DiscloseRendered"/>).
    /// </summary>
    /// <param name="document">The document the view shows.</param>
    /// <param name="format">The format of the view's parts, such as <c>png</c>.</param>
    /// <param name="pages">The pages (or slides) the view shows, numbered from 1; null for all of them.</param>
    public void ShowsInView(TDocument document, string format, IEnumerable<int>? pages = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        var inspector = new Inspector(_profile, document, format, rendering: true);
        inspector.Shows(pages);
        RecordView(inspector.InspectNow());
    }

    internal static string FormatOf(string targetPath) =>
        Path.GetExtension(targetPath).TrimStart('.').ToLowerInvariant();

    /// <summary>
    /// Inspects one document once, right after the first save that writes it to an output of
    /// one format, while the product still holds it open.
    /// </summary>
    internal sealed class Inspector(IEvaluationProfile<TDocument> profile, TDocument? document, string format, bool rendering, bool saved = false)
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

        /// <summary>The marks the document shows now, with no save, as a view rendering shows it.</summary>
        internal Inspection InspectNow()
        {
            _saved = true;
            return Result!;
        }

        internal Action<string> After(Action<string> write)
        {
            ArgumentNullException.ThrowIfNull(write);
            return path =>
            {
                write(path);
                _saved = true;
                // An output saved through something other than the document is read from its
                // file, when the profile reads files; the document stands in otherwise.
                if (saved && document is not null && !_inspected && _pages.Count == 0 && InspectFile(path))
                {
                    return;
                }

                // A saved copy is inspected right after its save, before anything, such as a
                // facade that closes the document, can change it.
                if (document is not null && !_inspected && _pages.Count == 0)
                {
                    Inspect(document);
                }
            };
        }

        /// <summary>Whether the profile read the marks from the staged file.</summary>
        private bool InspectFile(string path)
        {
            try
            {
                if (profile.InspectSaved(path, format) is { } marks)
                {
                    (_inspected, _marks) = (true, marks);
                    return true;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                (_inspected, _marks) = (true, null);
                return true;
            }

            return false;
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
    private readonly Dictionary<OutputPipeline<TDocument>.Inspector, List<string>> _paths = [];
    private readonly List<StagedOutput> _staged = [];
    private readonly List<OutputPipeline.Copy> _copies = [];

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
    /// <param name="encrypted">
    /// Whether the written file is encrypted. A file in a document, workbook or presentation
    /// format is a copy of the documents the command opened, and an unencrypted copy of an
    /// encrypted one is disclosed; an image or a data file never is.
    /// </param>
    /// <param name="saved">
    /// Whether <paramref name="write"/> saves the file through something other than
    /// <paramref name="document"/>, such as a facade that signs it, so its marks are read from the
    /// file (<see cref="IEvaluationProfile{TDocument}.InspectSaved"/>) where the profile reads files.
    /// </param>
    public StagedOutput Stage(string targetPath, bool overwrite, TDocument? document, Action<string> write,
        bool rendering = false, string? backupPath = null, FileWritePrecondition? inputPrecondition = null,
        Action<string>? verify = null, IEnumerable<int>? pages = null, bool encrypted = false, bool saved = false)
    {
        string format = OutputPipeline<TDocument>.FormatOf(targetPath);
        OutputPipeline<TDocument>.Inspector? inspector = document is null || saved ? null
            : _documents.Find(item => ReferenceEquals(item.Document, document) && item.Rendering == rendering
                && string.Equals(item.Format, format, StringComparison.Ordinal));
        if (document is not null && inspector is null)
        {
            inspector = new OutputPipeline<TDocument>.Inspector(_profile, document, format, rendering, saved);
            _documents.Add(inspector);
        }

        if (inspector is not null)
        {
            (_paths.TryGetValue(inspector, out List<string>? paths) ? paths : _paths[inspector] = []).Add(targetPath);
        }

        inspector?.Shows(pages);

        StagedOutput staged = _transaction.Stage(targetPath, overwrite, backupPath, inputPrecondition,
            inspector?.After(write) ?? write, verify);
        _staged.Add(staged);
        _copies.Add(_pipeline.CopyAt(targetPath, encrypted));
        return staged;
    }

    /// <summary>
    /// Publishes every staged output and returns their facts in stage order. A failed commit is
    /// recovered and reported as a stable publication error.
    /// </summary>
    public IReadOnlyList<PublishedFile> Commit()
    {
        _ = _transaction.Commit();
        foreach (OutputPipeline<TDocument>.Inspector inspector in _documents)
        {
            _pipeline.Record(inspector.Result, _paths.GetValueOrDefault(inspector));
        }

        for (int index = _documents.Count; index < _staged.Count; index++)
        {
            _pipeline.Record(null);
        }

        foreach (OutputPipeline.Copy copy in _copies)
        {
            _pipeline.RecordCopy(copy);
        }

        return [.. _staged.Select(static staged => staged.Published)];
    }

    /// <inheritdoc />
    public void Dispose() => _transaction.Dispose();
}

/// <summary>What an extraction writes, which decides whether the write pipeline discloses it.</summary>
public enum ExtractionContent
{
    /// <summary>Content the engine derives from the documents, such as text, tables or comments.</summary>
    Derived,

    /// <summary>
    /// Files the documents embed, such as images, media or attachments, copied byte for byte as
    /// the document stores them; the engine writes none of their bytes.
    /// </summary>
    EmbeddedFiles,
}
