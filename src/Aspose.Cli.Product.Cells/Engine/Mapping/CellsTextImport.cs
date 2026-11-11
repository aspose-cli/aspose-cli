using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Aspose.Cells;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Files;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Decides how a delimited text input is decoded, how its numbers and dates are read, and how a
/// value read from it is written back. Nothing is left to the machine's regional settings: a text
/// input is read with a byte order mark, a named encoding or strict UTF-8, and with a named
/// culture or as written (<see cref="Reads"/>), which a delimited text output writes back as it
/// shows (<see cref="WriteAsShown"/>). An input that UTF-8 reading would change, or that writes
/// decimal commas, is refused with the options that read it.
/// </summary>
internal static partial class CellsTextImport
{
    private const int NumberScanCharacters = 1 << 20;
    private const int NumberScanLines = 10_000;

    static CellsTextImport() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    /// <summary>The input's encoding, and the culture the caller named for it, null when none.</summary>
    internal static (Encoding Encoding, CultureInfo? Culture) Resolve(
        string path,
        char separator,
        CellsTextImportOptions? options,
        ResourceBudgetLedger budgets)
    {
        Encoding encoding = ResolveEncoding(path, options?.Encoding, budgets);
        if (options?.Culture is { } name)
        {
            return (encoding, ResolveCulture(name));
        }

        RejectDecimalCommas(path, separator, encoding);
        return (encoding, null);
    }

    /// <summary>
    /// The engine's options for a text input, loaded within <see cref="Invariant"/>. The engine
    /// types a number or a date as Excel imports text, with a number format that shows the text it
    /// read. With a named <paramref name="culture"/> it reads that culture's numbers and dates;
    /// without one, invariant ones, and a value keeps a type only as written (<see cref="Reads"/>).
    /// </summary>
    internal static TxtLoadOptions LoadOptions(char separator, Encoding? encoding, CultureInfo? culture) => new(LoadFormat.Csv)
    {
        Separator = separator,
        Encoding = encoding,
        CultureInfo = culture ?? CultureInfo.InvariantCulture,
        ConvertNumericData = true,
        ConvertDateTimeData = true,
        LoadStyleStrategy = TxtLoadStyleStrategy.ExactFormat,
        PreferredParsers = culture is null ? [AsWritten.Instance] : null,
    };

    /// <summary>
    /// Runs <paramref name="work"/> with the thread's culture set to the invariant one, then sets
    /// it back: the engine types the values of delimited text, and writes its numbers, by the
    /// thread's culture, which would otherwise be the machine's regional settings.
    /// </summary>
    internal static T Invariant<T>(Func<T> work)
    {
        CultureInfo culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try { return work(); }
        finally { CultureInfo.CurrentCulture = culture; }
    }

    /// <inheritdoc cref="Invariant{T}"/>
    internal static void Invariant(Action work) => Invariant(() => { work(); return true; });

    /// <summary>
    /// The number, date or boolean the engine reads <paramref name="text"/> as when the cell it
    /// types shows <paramref name="text"/> again, so that writing the cell as it shows writes what
    /// was read; null when <paramref name="text"/> is read as text. Two choices keep text the engine
    /// would type: a number written with leading zeros, such as an id (007, $007, (007)), and a
    /// date without a four-digit year, a month and a day in that order (1/2/2026, 1/2, 12:30,
    /// 2026-11), whose order of day and month, or whose day, the text does not say.
    /// </summary>
    internal static object? Reads(string text)
    {
        (CellValueType type, string shows, object? value) = Typed(text);
        return shows == text
            && (type == CellValueType.IsBool
                || type == CellValueType.IsNumeric && !LeadingZeros().IsMatch(text)
                || type == CellValueType.IsDateTime && YearFirst().IsMatch(text))
                ? value
                : null;
    }

    /// <summary>The type the engine reads <paramref name="text"/> as, whatever the cell it types shows.</summary>
    internal static CellValueType ReadType(string text) => Typed(text).Type;

    /// <summary>
    /// Writes each boolean of <paramref name="sheet"/>, and each number and date that has a number
    /// format, as the text it shows, when that text reads back as its value (<see cref="Reads"/>),
    /// as a value read as written shows the text it was read from; any other value is written as
    /// stored. A save of a workbook read as written calls it, within <see cref="Invariant"/>,
    /// before it writes the sheet as delimited text.
    /// </summary>
    internal static void WriteAsShown(Worksheet sheet)
    {
        var shown = new List<(Cell Cell, string Text)>();
        foreach (Cell cell in sheet.Cells)
        {
            if ((cell.Type == CellValueType.IsBool
                    || cell.Type is CellValueType.IsNumeric or CellValueType.IsDateTime
                        && cell.GetStyle() is not { Number: 0, Custom: null or "" })
                && cell.StringValue is var text
                && Reads(text) is { } value
                && (value, cell.Value) switch
                {
                    (DateTime read, DateTime stored) => read == stored,
                    (bool read, bool stored) => read == stored,
                    (DateTime or bool, _) or (_, DateTime or bool) or (_, null) => false,
                    _ => Convert.ToDouble(value, CultureInfo.InvariantCulture) == Convert.ToDouble(cell.Value, CultureInfo.InvariantCulture),
                })
            {
                shown.Add((cell, text));
            }
        }

        foreach ((Cell cell, string text) in shown)
        {
            cell.PutValue(text);
        }
    }

    // A cell of a workbook of its own, on which the engine types a text as a load does.
    private static readonly Lazy<(Cell Cell, Style Blank)> Scratch = new(static () =>
    {
        var workbook = new Workbook();
        workbook.Settings.CultureInfo = CultureInfo.InvariantCulture;
        return (workbook.Worksheets[0].Cells["A1"], workbook.CreateStyle());
    });

    private static (CellValueType Type, string Shows, object? Value) Typed(string text) => Invariant(() =>
    {
        lock (Scratch)
        {
            (Cell cell, Style blank) = Scratch.Value;
            cell.SetStyle(blank);
            cell.PutValue(text, isConverted: true, setStyle: true);
            // The engine types a boolean as it loads but not on a cell, so the cell is given the
            // boolean, to show how the engine shows it.
            if (cell.Type == CellValueType.IsString && bool.TryParse(text, out bool flag))
            {
                cell.PutValue(flag);
            }

            return (cell.Type, cell.StringValue, cell.Value);
        }
    });

    /// <summary>
    /// Reads a value as the number, date or boolean <see cref="Reads"/> gives it, which the engine
    /// types as it loads; any other value is text.
    /// </summary>
    private sealed class AsWritten : ICustomParser
    {
        internal static readonly AsWritten Instance = new();

        public object? ParseObject(string value) => Reads(value) is null ? value : null;

        public string GetFormat() => string.Empty;
    }

    // A zero followed by a digit after any leading spaces, currency symbols, parentheses and signs.
    [GeneratedRegex(@"^[\s(+\-\p{Sc}]*0\d", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingZeros();

    // A four-digit year, a month and a day, alone or before a time.
    [GeneratedRegex(@"^\d{4}[-/]\d{1,2}[-/]\d{1,2}(?:$|[ T])", RegexOptions.CultureInvariant)]
    private static partial Regex YearFirst();

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
                CellsOptionNames.Encoding,
                $"'{name}' is not a known text encoding",
                "Use an encoding name such as utf-8, gb18030, big5, shift_jis or windows-1252.");
        }

        if (marked is not null && marked.CodePage != named.CodePage)
        {
            throw CliErrors.OptionInvalid(
                CellsOptionNames.Encoding,
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
                CellsOptionNames.Culture,
                $"'{name}' is not a known culture",
                "Use a culture name such as de-DE, fr-FR, en-US or zh-CN.");
        }
    }

    private static Encoding? ByteOrderMark(string path)
    {
        Span<byte> prefix = stackalloc byte[3];
        using FileStream stream = InputFiles.OpenRead(path);
        int read = stream.ReadAtLeast(prefix, prefix.Length, throwOnEndOfStream: false);
        return prefix[..read] switch
        {
            [0xEF, 0xBB, 0xBF, ..] => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            [0xFF, 0xFE, ..] => Encoding.Unicode,
            [0xFE, 0xFF, ..] => Encoding.BigEndianUnicode,
            _ => null,
        };
    }

    /// <summary>
    /// Decodes the whole file in blocks with one stateful decoder, so a character whose bytes
    /// straddle two blocks is decoded across them. GetCharCount keeps no state between calls and
    /// would refuse such a character as invalid.
    /// </summary>
    private static void EnsureUtf8(string path, ResourceBudgetLedger budgets)
    {
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        Decoder decoder = encoding.GetDecoder();
        byte[] buffer = new byte[1 << 16];
        char[] characters = new char[encoding.GetMaxCharCount(buffer.Length)];
        long offset = 0;
        using FileStream stream = InputFiles.OpenRead(path);
        try
        {
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                budgets.Deadline.ThrowIfExpired("text-encoding");
                decoder.GetChars(buffer, 0, read, characters, 0, flush: false);
                offset += read;
            }

            decoder.GetChars([], 0, 0, characters, 0, flush: true);
        }
        catch (DecoderFallbackException exception)
        {
            // Index is relative to the current block and is negative when the invalid sequence
            // began with bytes the decoder carried over from the previous block.
            throw CellsErrors.TextEncodingInvalid(path, Math.Max(0, offset + exception.Index));
        }
    }

    /// <summary>
    /// Refuses a number that invariant parsing would change: a decimal comma ("2,71"), alone or
    /// after dot thousands groups ("1.253,96"). A comma followed by exactly three digits stays
    /// readable as a thousands separator, so "1,234" is not refused.
    /// </summary>
    private static void RejectDecimalCommas(string path, char separator, Encoding encoding)
    {
        using var reader = new StreamReader(InputFiles.OpenRead(path), encoding, detectEncodingFromByteOrderMarks: true);
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
