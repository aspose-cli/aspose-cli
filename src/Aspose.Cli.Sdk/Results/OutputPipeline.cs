using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.IO.Extraction;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.IO.Publication;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Sdk.Results;

/// <summary>
/// The SDK write pipeline of one product in one invocation. It resolves the product's license
/// state once and publishes every output the product writes. It is the sole owner of the
/// <c>ENCRYPTION_REMOVED</c> warning: a product tells it whether each document it opened is
/// encrypted (<see cref="InputOpening"/>) and whether each copy it publishes is, and the pipeline
/// compares the two; the command template adds the warning to the result (<see cref="Disclose"/>).
/// A command that publishes nothing, such as a read, discloses nothing.
/// </summary>
public abstract class OutputPipeline : ILicenseState
{
    private readonly ILicenseGate _licenseGate;
    private readonly object _sync = new();
    private bool _encryptedInput;
    private readonly SortedSet<string> _plainCopies = new(StringComparer.Ordinal);

    private protected OutputPipeline(ILicenseGate licenseGate, SafeFileWriter writer)
    {
        _licenseGate = licenseGate ?? throw new ArgumentNullException(nameof(licenseGate));
        Writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    /// <inheritdoc />
    public LicenseState License => _licenseGate.EnsureApplied();

    /// <summary>
    /// The format declarations of every product of the catalog, by which <see cref="InputOpening"/>
    /// tells an extension that names a format from one no format declares; empty when the
    /// invocation states none, and then only the opening product's declarations count.
    /// </summary>
    public IReadOnlyList<FormatDescriptor> DeclaredFormats { get; init; } = [];

    /// <summary>Whether the engine runs in evaluation mode.</summary>
    public bool IsEvaluation => License == LicenseState.Evaluation;

    private protected SafeFileWriter Writer { get; }

    /// <summary>
    /// Starts an extraction into <paramref name="root"/>, published when the guard commits. Files
    /// the documents embed, copied byte for byte as the document stores them
    /// (<see cref="ExtractionContent.EmbeddedFiles"/>), keep a byte order mark they start with.
    /// </summary>
    /// <param name="root">The extraction directory, which may not exist yet.</param>
    /// <param name="overwrite">Whether an existing file in the directory may be replaced (<c>--overwrite</c>).</param>
    /// <param name="content">What the extraction writes.</param>
    /// <param name="byteOrderMark">Whether a derived file keeps the UTF-8 byte order mark its write starts it with (<see cref="ByteOrderMarks"/>).</param>
    public ExtractionGuard BeginExtraction(string root, bool overwrite, ExtractionContent content = ExtractionContent.Derived,
        bool byteOrderMark = false) =>
        new(Writer.ResourceBudgets, root, overwrite)
        {
            KeepsByteOrderMark = content == ExtractionContent.EmbeddedFiles || byteOrderMark,
        };

    /// <summary>
    /// Records that the command opened a document whose content its outputs keep: the input it
    /// edits, converts, merges, splits or compares, or a document an operation takes content
    /// from. When one of them is encrypted, every copy the command publishes without encryption
    /// is disclosed as <c>ENCRYPTION_REMOVED</c>.
    /// </summary>
    /// <param name="protection">How the document is protected (<see cref="InputProtection"/>).</param>
    /// <remarks>Only <see cref="InputOpening.Open"/> records it, so no loader can skip it.</remarks>
    internal void Opened(InputProtection protection)
    {
        ArgumentNullException.ThrowIfNull(protection);
        lock (_sync)
        {
            _encryptedInput |= protection.Encrypted;
        }
    }

    /// <summary>
    /// Adds the encryption this invocation removed (<see cref="EncryptionRemoved"/>) to its result,
    /// before the result's own warnings; returns the result unchanged when there is nothing to
    /// disclose or the result carries the warning already.
    /// </summary>
    public ResultEnvelope Disclose(ResultEnvelope result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return EncryptionRemoved() is not { } removed || (result.Warnings ?? []).Any(present => present.Code == removed.Code)
            ? result
            : result with { Warnings = EnvelopeParts.CombineWarnings([removed], result.Warnings) };
    }

    /// <summary>
    /// <c>ENCRYPTION_REMOVED</c> when the command opened an encrypted document and published a
    /// copy of it without encryption; otherwise null.
    /// </summary>
    internal Warning? EncryptionRemoved()
    {
        string[] formats;
        lock (_sync)
        {
            if (!_encryptedInput || _plainCopies.Count == 0)
            {
                return null;
            }

            formats = [.. _plainCopies];
        }

        return new Warning(
            WarningCodes.EncryptionRemoved,
            $"The {string.Join(", ", formats.Select(static format => $"'{format}'"))} output is not encrypted, although an input it was made from is: it opens without a password.");
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
    /// Runs a command handler or a view call of the product, and reports an evaluation limit
    /// the engine raised in it as <c>EVALUATION_LIMIT</c>: in evaluation mode, an engine exception
    /// that the product's evaluation profile recognizes, whether it escaped as it was thrown or
    /// as the <c>ENGINE_ERROR</c> that reports it. This is the one place that translates an
    /// engine's evaluation exception; the error keeps it as its inner exception.
    /// </summary>
    /// <param name="run">The handler or view call.</param>
    internal T ReportingEvaluationLimits<T>(Func<T> run)
    {
        ArgumentNullException.ThrowIfNull(run);
        try
        {
            return run();
        }
        catch (Exception exception) when (exception is not OperationCanceledException
            && EvaluationLimitOf(exception) is { } limit)
        {
            throw limit;
        }
    }

    /// <summary>
    /// The <c>EVALUATION_LIMIT</c> error to report for <paramref name="exception"/>: the limit the
    /// profile recognizes in the engine's exception, which an <c>ENGINE_ERROR</c> holds as its
    /// inner exception, through every <c>ENGINE_ERROR</c> that restates it; or null to let the
    /// exception through unchanged, as any other CLI error.
    /// </summary>
    private CliException? EvaluationLimitOf(Exception exception)
    {
        Exception? thrown = exception;
        while (thrown is CliException error && error.Code == ErrorCodes.EngineError)
        {
            thrown = error.InnerException;
        }

        return thrown is null or CliException ? null : EngineEvaluationLimitOf(thrown);
    }

    /// <summary>The <c>EVALUATION_LIMIT</c> error for an engine exception, or null when it reports no evaluation limit.</summary>
    private protected abstract CliException? EngineEvaluationLimitOf(Exception exception);

    /// <summary>A published file: its format id, whether that format is an image, and whether the file is encrypted.</summary>
    internal sealed record Copy(string Format, bool Image, bool Encrypted);
}

/// <summary>
/// The write pipeline of a product whose engine documents are <typeparamref name="TDocument"/>,
/// whose evaluation profile recognizes the engine's evaluation limits.
/// </summary>
/// <typeparam name="TDocument">The engine's document type.</typeparam>
public sealed class OutputPipeline<TDocument> : OutputPipeline
    where TDocument : class
{
    private readonly IEvaluationProfile<TDocument> _profile;

    /// <summary>Creates the pipeline of one product invocation.</summary>
    /// <param name="licenseGate">The product's license gate, which the pipeline alone applies.</param>
    /// <param name="profile">How the product recognizes its engine's evaluation limits.</param>
    /// <param name="writer">The invocation's atomic file writer.</param>
    public OutputPipeline(ILicenseGate licenseGate, IEvaluationProfile<TDocument> profile, SafeFileWriter writer)
        : base(licenseGate, writer)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
    }

    private protected override CliException? EngineEvaluationLimitOf(Exception exception) =>
        _profile.RecognizeLimit(exception) is { } limit && IsEvaluation
            ? CliErrors.EngineEvaluationLimit(_profile.Engine, limit.Allows, limit.Limit, limit.Unit, exception)
            : null;

    /// <summary>Publishes one file that <paramref name="write"/> saves.</summary>
    /// <param name="targetPath">The output file.</param>
    /// <param name="format">
    /// The format the file is written in, as the product declares it: an image is never a copy.
    /// The file's extension never decides it, since one format has several (<c>.jpg</c> for jpeg)
    /// and one extension serves several (<c>.txt</c> for csv).
    /// </param>
    /// <param name="overwrite">Whether an existing file may be replaced.</param>
    /// <param name="write">Writes the staged file.</param>
    /// <param name="encrypted">
    /// Whether the written file is encrypted. A file in a document, workbook or presentation
    /// format is a copy of the documents the command opened, and an unencrypted copy of an
    /// encrypted one is disclosed; an image or a data file never is.
    /// </param>
    /// <param name="byteOrderMark">Whether the file keeps the UTF-8 byte order mark <paramref name="write"/> starts it with (<see cref="ByteOrderMarks"/>).</param>
    /// <returns>The facts of the published file.</returns>
    public PublishedFile Write(string targetPath, FormatDescriptor format, bool overwrite, Action<string> write,
        bool encrypted = false, bool byteOrderMark = false)
    {
        ArgumentNullException.ThrowIfNull(format);
        PublishedFile published = Writer.Write(targetPath, overwrite, ByteOrderMarks.Unless(byteOrderMark, write));
        RecordCopy(new Copy(format.Id, format.Image, encrypted));
        return published;
    }

    /// <summary>
    /// Publishes a resolved output, with the companion files its format writes beside it, and
    /// describes every file published.
    /// </summary>
    /// <param name="output">The resolved output.</param>
    /// <param name="encrypted">Whether the written output carries a password.</param>
    /// <param name="write">Writes the staged output.</param>
    public IReadOnlyList<OutputInfo> Write(ResolvedOutput output, bool encrypted, Action<string> write)
    {
        ArgumentNullException.ThrowIfNull(output);
        IReadOnlyList<OutputInfo> written = Writer.Write(output, encrypted, ByteOrderMarks.Unless(declared: false, write));
        RecordCopy(new Copy(output.Format.Id, output.Format.Image, encrypted));
        return written;
    }

    /// <summary>
    /// Starts a set of outputs spanning directories on the same filesystem, published together
    /// when the set commits.
    /// </summary>
    public OutputSet<TDocument> BeginSet(IEnumerable<string> targetDirectories, string operation) =>
        new(this, new AtomicOutputSetWriter(Writer, targetDirectories, operation));

    /// <summary>
    /// Publishes an edited document: stages <paramref name="output"/> with the input
    /// precondition, reads the staged file back through <paramref name="verify"/> before
    /// anything is published, and commits. When saving, verifying or
    /// committing fails, nothing is published and the input is left as it was.
    /// </summary>
    /// <typeparam name="TVerification">The product's verification of the staged output.</typeparam>
    /// <param name="output">The resolved output.</param>
    /// <param name="precondition">The input the output replaces, which must not change meanwhile.</param>
    /// <param name="encrypted">Whether the written output carries a password.</param>
    /// <param name="save">Saves the staged file and checks that it opens again.</param>
    /// <param name="verify">Reads the staged file back, or null when the edit is not verified. Issues are reported, not refused.</param>
    /// <param name="operation">The name of the publication, such as <c>pdf-edit</c>.</param>
    /// <param name="byteOrderMark">Whether the output keeps the UTF-8 byte order mark <paramref name="save"/> starts it with (<see cref="ByteOrderMarks"/>).</param>
    /// <param name="beforeCommit">
    /// Runs after the staged file is read back and before it is published, such as closing the
    /// input document so the publication replaces a file no handle of the command holds; null runs nothing.
    /// </param>
    public EditPublication<TVerification> PublishEdit<TVerification>(
        ResolvedOutput output,
        FileWritePrecondition precondition,
        bool encrypted,
        Action<string> save,
        Func<string, TVerification>? verify,
        string operation,
        bool byteOrderMark = false,
        Action? beforeCommit = null)
        where TVerification : class
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(save);
        using OutputSet<TDocument> transaction = BeginSet([output.Directory], operation);
        StagedOutput staged = transaction.Stage(
            output.Path,
            output.Format,
            output.Overwrite,
            save,
            inputPrecondition: precondition,
            encrypted: encrypted,
            byteOrderMark: byteOrderMark);
        TVerification? verification = verify is null ? null : staged.Read(verify);
        beforeCommit?.Invoke();
        transaction.Commit();
        return new EditPublication<TVerification>(
            FileObjects.Output(staged.Published, output.Format, encrypted), verification);
    }

}

/// <summary>
/// A set of outputs staged together and published, refused or rolled back as a whole, through
/// the product's <see cref="OutputPipeline{TDocument}"/>. Disposing a set that did not commit
/// rolls it back.
/// </summary>
/// <typeparam name="TDocument">The engine's document type.</typeparam>
public sealed class OutputSet<TDocument> : IDisposable
    where TDocument : class
{
    private readonly OutputPipeline<TDocument> _pipeline;
    private readonly AtomicOutputSetWriter _transaction;
    private readonly List<StagedOutput> _staged = [];
    private readonly List<OutputPipeline.Copy> _copies = [];

    internal OutputSet(OutputPipeline<TDocument> pipeline, AtomicOutputSetWriter transaction)
    {
        _pipeline = pipeline;
        _transaction = transaction;
    }

    /// <summary>Admits a directory and owns only directories this set actually creates.</summary>
    public void EnsureDirectory(string path) => _transaction.EnsureDirectory(path);

    /// <summary>Stages one output that <paramref name="write"/> saves.</summary>
    /// <param name="targetPath">The output file.</param>
    /// <param name="format">The format the output is written in, as the product declares it, never one the file's extension suggests.</param>
    /// <param name="overwrite">Whether an existing file may be replaced.</param>
    /// <param name="write">Writes the staged file.</param>
    /// <param name="inputPrecondition">The input the output replaces, which must not change meanwhile.</param>
    /// <param name="verify">Checks the staged file before it is published.</param>
    /// <param name="encrypted">
    /// Whether the written file is encrypted. A file in a document, workbook or presentation
    /// format is a copy of the documents the command opened, and an unencrypted copy of an
    /// encrypted one is disclosed; an image or a data file never is.
    /// </param>
    /// <param name="byteOrderMark">Whether the output keeps the UTF-8 byte order mark <paramref name="write"/> starts it with (<see cref="ByteOrderMarks"/>).</param>
    public StagedOutput Stage(string targetPath, FormatDescriptor format, bool overwrite, Action<string> write,
        FileWritePrecondition? inputPrecondition = null, Action<string>? verify = null, bool encrypted = false,
        bool byteOrderMark = false)
    {
        ArgumentNullException.ThrowIfNull(format);
        StagedOutput staged = _transaction.Stage(targetPath, overwrite, inputPrecondition, ByteOrderMarks.Unless(byteOrderMark, write), verify);
        _staged.Add(staged);
        _copies.Add(new OutputPipeline.Copy(format.Id, format.Image, encrypted));
        return staged;
    }

    /// <summary>
    /// Publishes every staged output and returns their facts in stage order. A failed commit is
    /// recovered and reported as a stable publication error.
    /// </summary>
    public IReadOnlyList<PublishedFile> Commit()
    {
        _ = _transaction.Commit();
        foreach (OutputPipeline.Copy copy in _copies)
        {
            _pipeline.RecordCopy(copy);
        }

        return [.. _staged.Select(static staged => staged.Published)];
    }

    /// <inheritdoc />
    public void Dispose() => _transaction.Dispose();
}

/// <summary>What an extraction writes, which decides whether a file keeps the byte order mark it starts with.</summary>
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
