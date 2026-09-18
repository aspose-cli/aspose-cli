using System.Text;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.IO;

/// <summary>Single bounded entry point for user-controlled file, stdin, and text input.</summary>
public sealed class InputSource
{
    private static readonly Encoding StrictUtf8 =
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private readonly ResourceBudgetLedger _budgets;

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

    public Stream OpenFile(string path)
    {
        _budgets.ThrowIfFailed();
        _budgets.Deadline.ThrowIfExpired("file-open");
        string full = Path.GetFullPath(path);
        _budgets.VerifyAdmission(full);
        var stream = new FileStream(
            full,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);
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

    /// <summary>
    /// Reads derived user-controlled content (for example a package part)
    /// through both the shared memory-byte and decoded-character budgets.
    /// </summary>
    public string ReadDerivedText(Stream stream, string phase)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(phase);
        var bounded = new BoundedReadStream(
            stream,
            _budgets,
            ResourceBudgetKinds.MemoryBufferBytes,
            phase);
        return ReadText(
            bounded,
            ResourceBudgetKinds.DecodedTextCharacters,
            phase);
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

    private string ReadText(
        Stream stream,
        string characterBudget,
        string phase)
    {
        using (stream)
        using (var reader = new StreamReader(
                   stream,
                   StrictUtf8,
                   detectEncodingFromByteOrderMarks: true,
                   bufferSize: 4096,
                   leaveOpen: false))
        {
            long limit = _budgets.Limit(characterBudget);
            var result = new StringBuilder((int)Math.Min(limit, 8192));
            char[] buffer = new char[4096];
            try
            {
                while (true)
                {
                    _budgets.Deadline.ThrowIfExpired(phase);
                    int read = reader.Read(buffer, 0, buffer.Length);
                    if (read == 0)
                    {
                        return result.ToString();
                    }
                    _budgets.Consume(
                        characterBudget,
                        read,
                        "characters",
                        phase);
                    result.Append(buffer, 0, read);
                }
            }
            catch (DecoderFallbackException exception)
            {
                throw CliErrors.InputEncodingInvalid(phase, exception);
            }
        }
    }
}
