using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Decides how a delimited text input is decoded and how its numbers and dates are parsed.
/// Nothing is left to the machine's regional settings: a text input is read with a byte order
/// mark, a named encoding or strict UTF-8, and with a named culture or invariant formats. An
/// input that invariant UTF-8 reading would change is refused with the options that read it.
/// </summary>
internal static partial class CellsTextImport
{
    private const int NumberScanCharacters = 1 << 20;
    private const int NumberScanLines = 10_000;

    static CellsTextImport() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    internal static (Encoding Encoding, CultureInfo Culture) Resolve(
        string path,
        char separator,
        TextImportOptions? options,
        ResourceBudgetLedger budgets)
    {
        Encoding encoding = ResolveEncoding(path, options?.Encoding, budgets);
        if (options?.Culture is { } name)
        {
            return (encoding, ResolveCulture(name));
        }

        RejectDecimalCommas(path, separator, encoding);
        return (encoding, CultureInfo.InvariantCulture);
    }

    private static Encoding ResolveEncoding(string path, string? name, ResourceBudgetLedger budgets)
    {
        Encoding? marked = ByteOrderMark(path);
        if (name is null)
        {
            if (marked is null)
            {
                EnsureUtf8(path, budgets);
            }

            return marked ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }

        Encoding named;
        try
        {
            named = Encoding.GetEncoding(name);
        }
        catch (ArgumentException)
        {
            throw CliErrors.OptionInvalid(
                "--encoding",
                $"'{name}' is not a known text encoding",
                "Use an encoding name such as utf-8, gb18030, big5, shift_jis or windows-1252.");
        }

        if (marked is not null && marked.CodePage != named.CodePage)
        {
            throw CliErrors.OptionInvalid(
                "--encoding",
                $"the file starts with a {marked.WebName} byte order mark",
                "Drop --encoding; the byte order mark already names the encoding.");
        }

        return named;
    }

    private static CultureInfo ResolveCulture(string name)
    {
        try
        {
            return CultureInfo.GetCultureInfo(name, predefinedOnly: true);
        }
        catch (CultureNotFoundException)
        {
            throw CliErrors.OptionInvalid(
                "--culture",
                $"'{name}' is not a known culture",
                "Use a culture name such as de-DE, fr-FR, en-US or zh-CN.");
        }
    }

    private static Encoding? ByteOrderMark(string path)
    {
        Span<byte> prefix = stackalloc byte[3];
        using FileStream stream = File.OpenRead(path);
        int read = stream.ReadAtLeast(prefix, prefix.Length, throwOnEndOfStream: false);
        return prefix[..read] switch
        {
            [0xEF, 0xBB, 0xBF, ..] => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            [0xFF, 0xFE, ..] => Encoding.Unicode,
            [0xFE, 0xFF, ..] => Encoding.BigEndianUnicode,
            _ => null,
        };
    }

    private static void EnsureUtf8(string path, ResourceBudgetLedger budgets)
    {
        Decoder decoder = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetDecoder();
        byte[] buffer = new byte[1 << 16];
        long offset = 0;
        using FileStream stream = File.OpenRead(path);
        try
        {
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                budgets.Deadline.ThrowIfExpired("text-encoding");
                decoder.GetCharCount(buffer.AsSpan(0, read), flush: false);
                offset += read;
            }

            decoder.GetCharCount([], flush: true);
        }
        catch (DecoderFallbackException exception)
        {
            throw CellsErrors.TextEncodingInvalid(path, offset + Math.Max(0, exception.Index));
        }
    }

    /// <summary>
    /// Refuses a number that invariant parsing would change: a decimal comma ("2,71"), alone or
    /// after dot thousands groups ("1.253,96"). A comma followed by exactly three digits stays
    /// readable as a thousands separator, so "1,234" is not refused.
    /// </summary>
    private static void RejectDecimalCommas(string path, char separator, Encoding encoding)
    {
        using var reader = new StreamReader(path, encoding, detectEncodingFromByteOrderMarks: true);
        char[] window = new char[NumberScanCharacters];
        int length = reader.ReadBlock(window, 0, window.Length);
        int lineNumber = 0;
        foreach (string line in new string(window, 0, length).Split('\n').Take(NumberScanLines))
        {
            lineNumber++;
            foreach (string field in Fields(line.TrimEnd('\r'), separator))
            {
                if (DecimalComma().IsMatch(field))
                {
                    throw CellsErrors.TextNumbersAmbiguous(path, field, lineNumber);
                }
            }
        }
    }

    private static IEnumerable<string> Fields(string line, char separator)
    {
        var field = new StringBuilder();
        bool quoted = false;
        foreach (char character in line)
        {
            if (character == '"')
            {
                quoted = !quoted;
            }
            else if (character == separator && !quoted)
            {
                yield return field.ToString().Trim();
                field.Clear();
            }
            else
            {
                field.Append(character);
            }
        }

        yield return field.ToString().Trim();
    }

    [GeneratedRegex(@"^[-+]?(?:\d+,\d{1,2}|\d+,\d{4,}|\d{1,3}(?:\.\d{3})+,\d+)$", RegexOptions.CultureInvariant)]
    private static partial Regex DecimalComma();
}
