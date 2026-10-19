using System.Runtime.ExceptionServices;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.IO;

/// <summary>What an exception a document engine raised while it loaded an input means.</summary>
public enum LoadFailureKind
{
    /// <summary>Not a failure of the input; it propagates unchanged.</summary>
    Other,

    /// <summary>The input is encrypted and the password is missing or does not open it.</summary>
    Password,

    /// <summary>
    /// The input is damaged, truncated, not the document it claims to be or in a format the
    /// engine cannot load.
    /// </summary>
    Corrupt,
}

/// <summary>
/// The one translation of the failures of loading an input document. A product supplies the
/// engine call and a classifier of its engine's exceptions; this decides between a required and
/// an invalid password, turns an I/O failure into <c>FILE_NOT_FOUND</c>, <c>FILE_LOCKED</c>,
/// <c>FILE_ACCESS_DENIED</c> or a damaged input, and reports a damaged input or a format the
/// engine recognizes but cannot load with one code, <c>FILE_CORRUPT</c>.
/// </summary>
public sealed class InputLoading
{
    private readonly Func<Exception, LoadFailureKind> _classify;

    /// <param name="document">What an input is, completing "not a valid ...", such as <c>spreadsheet</c>.</param>
    /// <param name="hint">How to check a file that does not load, in the product's terms.</param>
    /// <param name="classify">
    /// What an exception the engine raised while loading means, decided by its type or code,
    /// never by its message. I/O exceptions never reach it.
    /// </param>
    public InputLoading(string document, string hint, Func<Exception, LoadFailureKind> classify)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(hint);
        ArgumentNullException.ThrowIfNull(classify);
        Document = document;
        Hint = hint;
        _classify = classify;
    }

    /// <summary>What an input is, such as <c>spreadsheet</c>.</summary>
    public string Document { get; }

    /// <summary>How to check a file that does not load.</summary>
    public string Hint { get; }

    /// <summary>
    /// Runs one engine call that reads <paramref name="path"/> and translates its failure.
    /// A <see cref="CliException"/> and a cancellation pass unchanged.
    /// </summary>
    /// <param name="path">The input file.</param>
    /// <param name="password">The password the input was opened with and where it comes from.</param>
    /// <param name="load">The engine call.</param>
    public T Load<T>(string path, PasswordSource password, Func<T> load)
    {
        ArgumentNullException.ThrowIfNull(load);
        try
        {
            return load();
        }
        catch (Exception exception) when (exception is not CliException and not OperationCanceledException)
        {
            throw Failure(exception, path, password);
        }
    }

    /// <summary>
    /// The error for an exception raised while <paramref name="path"/> loaded. An exception the
    /// classifier calls <see cref="LoadFailureKind.Other"/> is rethrown with its stack intact,
    /// so the Host can still tell an engine failure from a defect of the CLI.
    /// </summary>
    public CliException Failure(Exception exception, string path, PasswordSource password) =>
        Translate(exception, path, password, onDisk: true);

    /// <summary>
    /// The error for an exception raised while an input held in memory, such as inline Markdown,
    /// loaded. No file is read, so an I/O exception is the content's, never a missing or locked
    /// file; <paramref name="source"/> names the input in the error.
    /// </summary>
    public CliException InMemoryFailure(Exception exception, string source) =>
        Translate(exception, source, password: default, onDisk: false);

    private CliException Translate(Exception exception, string path, PasswordSource password, bool onDisk)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (exception is CliException error)
        {
            return error;
        }
        if (onDisk && exception is not OperationCanceledException && InputFailures.FromAccess(exception, path) is { } access)
        {
            return access;
        }

        LoadFailureKind kind = exception is IOException ? LoadFailureKind.Corrupt : _classify(exception);
        switch (kind)
        {
            case LoadFailureKind.Password:
                return PasswordRefused(path, password);
            case LoadFailureKind.Corrupt:
                return Unreadable(path, FirstLine(exception.Message), exception);
            default:
                ExceptionDispatchInfo.Throw(exception);
                throw new InvalidOperationException("Unreachable.", exception);
        }
    }

    /// <summary>
    /// An encrypted input that <paramref name="password"/> does not open: <c>PASSWORD_REQUIRED</c>
    /// without a password, <c>PASSWORD_INVALID</c> with one, with the hint of where the password
    /// comes from (<see cref="PasswordSource.Refused"/>).
    /// </summary>
    public static CliException PasswordRefused(string path, PasswordSource password) => password.Refused(path);

    /// <summary>A damaged input: <c>FILE_CORRUPT</c>, with why it is not a valid document.</summary>
    /// <param name="path">The input file.</param>
    /// <param name="reason">Why it is not one, completing a sentence.</param>
    /// <param name="innerException">The engine failure, if any.</param>
    public CliException Unreadable(string path, string reason, Exception? innerException = null) =>
        CliErrors.InputUnreadable(path, Document, reason, Hint, innerException);

    /// <summary>
    /// An input whose format is recognized but cannot be loaded as this product's document,
    /// such as another product's file: <c>FILE_CORRUPT</c>, as a damaged input is, so every
    /// product refuses it alike and the Host can name the product that reads it.
    /// </summary>
    /// <param name="path">The input file.</param>
    /// <param name="format">The format the content has, as the engine names it.</param>
    /// <param name="innerException">The engine failure, if any.</param>
    public CliException Unloadable(string path, string format, Exception? innerException = null) =>
        Unreadable(path, $"its content is {format}, not a {Document} this command reads", innerException);

    /// <summary>
    /// An input whose content is of a kind the product imports, but whose name carries the
    /// extension of none of its input formats: <c>FILE_CORRUPT</c>, naming the extension and the
    /// extensions of <paramref name="importing"/>, in ordinal order, under which it imports.
    /// </summary>
    /// <param name="path">The input file.</param>
    /// <param name="content">What its content is, such as <c>delimited text</c>.</param>
    /// <param name="importing">The formats a file of this content is imported as.</param>
    /// <param name="importedAs">How it is imported, completing "to import it ...", such as <c>as text</c>.</param>
    public CliException NotNamedAsInput(string path, string content, IEnumerable<FormatDescriptor> importing, string importedAs)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(importing);
        string extension = Path.GetExtension(path);
        string named = extension.Length > 1 ? $"its {extension} extension names no" : "it has no extension that names a";
        string extensions = string.Join(", ", importing.SelectMany(static format => format.Extensions)
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal));
        return Unreadable(path, $"its content is {content}, but {named} {Document} input; give it a {extensions} extension to import it {importedAs}");
    }

    // Engine messages can be multi-line; the first line carries the fact.
    private static string FirstLine(string message)
    {
        string text = message.TrimStart();
        int newline = text.AsSpan().IndexOfAny('\r', '\n');
        return (newline > 0 ? text[..newline] : text).TrimEnd();
    }
}

/// <summary>
/// The one translation of an operating-system failure to read an input file, the document a
/// command opens or an auxiliary input such as an image or a certificate.
/// </summary>
public static class InputFailures
{
    /// <summary>
    /// The error for a failure to access <paramref name="path"/>: <c>FILE_NOT_FOUND</c>,
    /// <c>FILE_ACCESS_DENIED</c> or <c>FILE_LOCKED</c>; null for any other failure, such as an
    /// I/O exception a parser raised on the file's bytes.
    /// </summary>
    public static CliException? FromAccess(Exception exception, string path)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception switch
        {
            FileNotFoundException or DirectoryNotFoundException or DriveNotFoundException => CliErrors.FileNotFound(path),
            UnauthorizedAccessException => CliErrors.FileAccessDenied(path),
            IOException io when FileAccessProbe.IsSharingViolation(io) => CliErrors.FileLocked(path),
            IOException when !File.Exists(path) => CliErrors.FileNotFound(path),
            IOException when !FileAccessProbe.CanOpenForRead(path) => CliErrors.FileLocked(path),
            _ => null,
        };
    }

    /// <summary>
    /// An auxiliary input file, such as a signing certificate, whose name carries the extension
    /// of none of the formats in <paramref name="accepted"/>: <c>FORMAT_UNSUPPORTED</c> for its
    /// extension, as <see cref="CliErrors.FormatUnsupported"/> reports a format id, with their ids.
    /// </summary>
    public static CliException ExtensionUnsupported(string path, IReadOnlyList<FormatDescriptor> accepted)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(accepted);
        return CliErrors.FormatUnsupported(Path.GetExtension(path).TrimStart('.'), [.. accepted.Select(static format => format.Id)]);
    }

    /// <summary>
    /// Runs a read of an auxiliary input file and turns an operating-system failure into the
    /// error <see cref="FromAccess"/> names; any other failure propagates unchanged.
    /// </summary>
    public static T Read<T>(string path, Func<T> read)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(read);
        try
        {
            return read();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            if (FromAccess(exception, path) is { } error)
            {
                throw error;
            }
            throw;
        }
    }
}
