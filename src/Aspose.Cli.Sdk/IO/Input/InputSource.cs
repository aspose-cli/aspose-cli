using System.Text;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Files;

namespace Aspose.Cli.Sdk.IO.Input;

/// <summary>Single bounded entry point for user-controlled file, stdin, and text input.</summary>
public sealed class InputSource
{
    private static readonly Encoding StrictUtf8 =
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    // Its preamble lets a reader drop a leading byte order mark and stay strict after it.
    private static readonly Encoding MarkedStrictUtf8 =
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true);
    private readonly ResourceBudgetLedger _budgets;
    private (string Snapshot, string Original)? _snapshotOrigin;

    /// <summary>Preserves the resource origin of one host-owned document snapshot for this invocation.</summary>
    public void SetSnapshotOrigin(string snapshot, string original)
    {
        if (_snapshotOrigin is not null) { throw new InvalidOperationException("The input snapshot origin is already set."); }
        _snapshotOrigin = (Path.GetFullPath(snapshot), Path.GetFullPath(original));
    }

    internal string ResourceOriginFor(string documentPath)
    {
        string full = Path.GetFullPath(documentPath);
        return _snapshotOrigin is { } origin && FilePaths.Same(full, origin.Snapshot) ? origin.Original : full;
    }

    internal InputSource(ResourceBudgetLedger budgets)
    {
        _budgets = budgets;
    }

    /// <summary>Charges already-decoded inline text to this invocation.</summary>
    public string ReadInlineText(string value, string phase)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(phase);
        _budgets.Consume(
            ResourceBudgetKinds.DecodedTextCharacters,
            value.Length,
            "characters",
            phase);
        return value;
    }

    /// <summary>Owns bounded streams retained by a document engine through its save phase.</summary>
    public InputResourceScope CreateScope() => new(this);

    internal void ThrowIfFailed() => _budgets.ThrowIfFailed();

    internal ResourceBudgetLedger Budgets => _budgets;

    /// <summary>
    /// Opens a bounded read of a file with <see cref="InputFiles.OpenRead"/>; a file that cannot
    /// be opened is the error <see cref="InputFailures.FromAccess"/> names.
    /// </summary>
    public Stream OpenFile(string path)
    {
        _budgets.ThrowIfFailed();
        _budgets.Deadline.ThrowIfExpired("file-open");
        string full = Path.GetFullPath(path);
        _budgets.VerifyAdmission(full);
        FileStream stream = InputFailures.Read(full, () => InputFiles.OpenRead(full));
        long observedLength = stream.Length;
        if (observedLength > _budgets.Limit(ResourceBudgetKinds.InputBytes))
        {
            stream.Dispose();
            throw CliErrors.FileTooLarge(
                observedLength,
                _budgets.Limit(ResourceBudgetKinds.InputBytes));
        }
        return new BoundedReadStream(
            stream,
            _budgets,
            ResourceBudgetKinds.InputBytes,
            "file-read");
    }

    public byte[] ReadAllBytes(string path)
    {
        using Stream stream = OpenFile(path);
        long memoryLimit = _budgets.Limit(ResourceBudgetKinds.MemoryBufferBytes);
        if (stream.Length > memoryLimit || stream.Length > Array.MaxLength)
        {
            throw CliErrors.InputBudgetExceeded(
                ResourceBudgetKinds.MemoryBufferBytes,
                stream.Length,
                memoryLimit,
                "bytes",
                "buffer-allocation");
        }

        int length = checked((int)stream.Length);
        _budgets.Consume(
            ResourceBudgetKinds.MemoryBufferBytes,
            length,
            "bytes",
            "buffer-allocation");
        byte[] bytes = new byte[length];
        int offset = 0;
        while (offset < bytes.Length)
        {
            int read = stream.Read(bytes, offset, bytes.Length - offset);
            if (read == 0)
            {
                throw CliErrors.InputChanged(path, length, offset);
            }
            offset += read;
        }
        if (stream.ReadByte() >= 0)
        {
            throw CliErrors.InputChanged(path, length, length + 1L);
        }
        return bytes;
    }

    public string ReadTextFile(string path) =>
        ReadText(
            OpenFile(path),
            ResourceBudgetKinds.DecodedTextCharacters,
            "text-file-decode");

    public string ReadStandardInputText(Stream standardInput)
    {
        ArgumentNullException.ThrowIfNull(standardInput);
        using var bounded = new BoundedReadStream(
            standardInput,
            _budgets,
            ResourceBudgetKinds.StandardInputBytes,
            "stdin-read",
            leaveOpen: true);
        return ReadText(
            bounded,
            ResourceBudgetKinds.DecodedTextCharacters,
            "stdin-decode");
    }

    public string ReadSecretLine(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        long limit = _budgets.Limit(ResourceBudgetKinds.SecretCharacters);
        var value = new StringBuilder((int)Math.Min(limit, 256));
        while (true)
        {
            _budgets.Deadline.ThrowIfExpired("secret-read");
            int next = reader.Read();
            if (next is -1 or '\n')
            {
                break;
            }
            if (next == '\r')
            {
                continue;
            }
            _budgets.Consume(
                ResourceBudgetKinds.SecretCharacters,
                1,
                "characters",
                "secret-read");
            value.Append((char)next);
        }
        return value.ToString();
    }

    /// <summary>
    /// <see cref="ReadSecretLine(TextReader)"/> of bytes, such as piped standard input: UTF-8 text
    /// after a UTF-8 byte order mark, if there is one, whatever the console's code page, so a secret
    /// reads the same from stdin as from an environment variable. Bytes that are not UTF-8 are
    /// refused as the error of <paramref name="option"/>, such as <c>--password-stdin</c>, whose hint
    /// offers <paramref name="alternative"/>, the option that reads the secret from an environment
    /// variable; the error repeats none of the bytes.
    /// </summary>
    internal string ReadSecretLine(Stream input, string option, string alternative)
    {
        using var reader = new StreamReader(input, MarkedStrictUtf8, detectEncodingFromByteOrderMarks: false, bufferSize: -1, leaveOpen: true);
        try { return ReadSecretLine(reader); }
        catch (DecoderFallbackException)
        {
            throw CliErrors.OptionInvalid(option, "stdin is read as UTF-8 text, and the password on it is not",
                $"Pipe the password as UTF-8 text, or name an environment variable that holds it with {alternative}.");
        }
    }

    /// <summary>
    /// Decodes text in the encoding its byte order mark names, UTF-8 without one, and refuses the
    /// bytes that encoding cannot decode rather than replace them; the mark is not part of the text.
    /// </summary>
    private string ReadText(
        Stream stream,
        string characterBudget,
        string phase)
    {
        using (stream)
        {
            byte[] bytes = new byte[4096];
            int count = stream.ReadAtLeast(bytes, 4, throwOnEndOfStream: false);
            (Encoding encoding, int start) = MarkedEncoding(bytes.AsSpan(0, count));
            Decoder decoder = encoding.GetDecoder();
            char[] buffer = new char[encoding.GetMaxCharCount(bytes.Length)];
            long limit = _budgets.Limit(characterBudget);
            var result = new StringBuilder((int)Math.Min(limit, 8192));
            try
            {
                while (true)
                {
                    _budgets.Deadline.ThrowIfExpired(phase);
                    int read = decoder.GetChars(bytes, start, count - start, buffer, 0, flush: count == 0);
                    if (read > 0)
                    {
                        _budgets.Consume(characterBudget, read, "characters", phase);
                        result.Append(buffer, 0, read);
                    }
                    if (count == 0)
                    {
                        return result.ToString();
                    }
                    (start, count) = (0, stream.Read(bytes, 0, bytes.Length));
                }
            }
            catch (DecoderFallbackException exception)
            {
                throw CliErrors.InputEncodingInvalid(phase, exception);
            }
        }
    }

    /// <summary>The strict encoding of the byte order mark that starts <paramref name="start"/>, as a StreamReader detects one, and the mark's length.</summary>
    private static (Encoding Encoding, int Length) MarkedEncoding(ReadOnlySpan<byte> start) => start switch
    {
        [0xEF, 0xBB, 0xBF, ..] => (StrictUtf8, 3),
        [0xFF, 0xFE, 0, 0, ..] => (new UTF32Encoding(bigEndian: false, byteOrderMark: true, throwOnInvalidCharacters: true), 4),
        [0, 0, 0xFE, 0xFF, ..] => (new UTF32Encoding(bigEndian: true, byteOrderMark: true, throwOnInvalidCharacters: true), 4),
        [0xFF, 0xFE, ..] => (new UnicodeEncoding(bigEndian: false, byteOrderMark: true, throwOnInvalidBytes: true), 2),
        [0xFE, 0xFF, ..] => (new UnicodeEncoding(bigEndian: true, byteOrderMark: true, throwOnInvalidBytes: true), 2),
        _ => (StrictUtf8, 0),
    };
}
