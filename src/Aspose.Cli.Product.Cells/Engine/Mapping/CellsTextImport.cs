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
/// shows (<see cref="WriteAsShown"/>); in a file that writes a decimal comma, numbers with a comma
/// or a point stay text (<see cref="KeepSeparatedNumbersAsText"/>), and an error value is always
/// text (<see cref="KeepErrorsAsText"/>). An input that UTF-8 reading would change is refused with
/// the encoding that reads it.
/// </summary>
internal static partial class CellsTextImport
{
    static CellsTextImport() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    /// <summary>
    /// The input's encoding, whether it starts with a UTF-8 byte order mark, and the culture the
    /// caller named for it, null when none.
    /// </summary>
    internal static (Encoding Encoding, bool Utf8ByteOrderMark, CultureInfo? Culture) Resolve(
        string path,
        CellsTextImportOptions? options,
        ResourceBudgetLedger budgets)
    {
        Encoding? marked = ByteOrderMark(path);
        return (ResolveEncoding(path, marked, options?.Encoding, budgets), marked is UTF8Encoding,
            options?.Culture is { } name ? ResolveCulture(name) : null);
    }

    /// <summary>
    /// The engine's options for a text input, loaded within <see cref="Invariant"/>. The engine
    /// types a number or a date as Excel imports text, with a number format that shows the text it
    /// read. With a named <paramref name="culture"/> it reads that culture's numbers and dates;
    /// without one, invariant ones, and a value keeps a type only as written (<see cref="Reads"/>),
    /// which keeps a number of more than 15 digits as text, as KeepPrecision does not (known issue
    /// CELLS-CSV-KEEP-PRECISION, KNOWN-ISSUES.md).
    /// </summary>
    internal static TxtLoadOptions LoadOptions(char separator, Encoding? encoding, CultureInfo? culture) => new(LoadFormat.Csv)
    {
        Separator = separator,
        Encoding = encoding,
        CultureInfo = culture ?? CultureInfo.InvariantCulture,
        ConvertNumericData = true,
        ConvertDateTimeData = true,
        LoadStyleStrategy = TxtLoadStyleStrategy.ExactFormat,
        PreferredParsers = culture is null ? [new AsWritten(separator)] : null,
    };

    /// <summary>
    /// Keeps as text, once a text input loaded with <paramref name="options"/> as written wrote a
    /// decimal comma anywhere (2,71, 1.253,96, or in a file of semicolons any number with one comma
    /// and no point, such as 1,234, unless the file also writes a decimal point, such as 19.90),
    /// every number of <paramref name="sheet"/> it wrote
    /// with a comma or a point, whose meaning such a file leaves open: 1,234 may be a decimal number
    /// and 1.234 a thousand. Returns the warning that says so, with the conversion that reads the
    /// numbers in the culture that wrote them, for <paramref name="input"/>, a user's input; null
    /// for a read-back or a file without a decimal comma.
    /// </summary>
    internal static Warning? KeepSeparatedNumbersAsText(LoadOptions options, Worksheet sheet, string? input) => Invariant(() =>
    {
        if (options is not TxtLoadOptions { PreferredParsers: [AsWritten { DecimalCommas: true }] })
        {
            return null;
        }

        var separated = new List<Cell>();
        Cell? sample = null;
        int count = 0;
        foreach (Cell cell in sheet.Cells)
        {
            if (cell.Type == CellValueType.IsNumeric && cell.StringValue.IndexOfAny([',', '.']) >= 0)
            {
                separated.Add(cell);
            }
            else if (cell.Type == CellValueType.IsString && DecimalComma().IsMatch(cell.StringValue.Trim()))
            {
                sample ??= cell;
                count++;
            }
        }

        foreach (Cell cell in separated)
        {
            cell.PutValue(cell.StringValue);
        }

        sample ??= separated.FirstOrDefault();

        return input is null || sample is null ? null : new Warning(CellsDiagnostics.NumbersReadAsText,
            $"'{Path.GetFileName(input)}' writes numbers with a decimal comma, such as '{sample.StringValue}' in {sample.Name}, "
                + $"so its {count + separated.Count} number(s) written with a comma or a point are kept as text rather than read by a guess.")
        {
            Hint = $"Read them as numbers with the culture that wrote them: {CellsErrors.Import(input).Option("--culture", "de-DE")}. "
                + "That fits a file whose decimals all have a comma; if it also writes a decimal point, such as 19.90, "
                + "a culture like en-US wrote it, and de-DE would read 19.90 as 1990.",
            Docs = "cells/troubleshooting",
            Location = CellsAddresses.At("cell", sheet.Name, sample.Name),
        };
    });

    /// <summary>
    /// Keeps as text each error value of <paramref name="sheet"/>, such as #N/A, which a delimited
    /// text input, holding no formula, can only have written as text: the engine types it as the
    /// error whatever the load's parser returns (known issue CELLS-CSV-ERROR-TEXT, KNOWN-ISSUES.md),
    /// so it is put back with a quote prefix, which keeps it text and which a text output omits.
    /// </summary>
    internal static void KeepErrorsAsText(Worksheet sheet) =>
        PutValues(sheet, static cell => cell is { Type: CellValueType.IsError, IsFormula: false } ? "'" + cell.StringValue : null);

    /// <summary>
    /// Types as the error it writes each text of <paramref name="table"/>, a text table an edit
    /// wrote and a load read back, that writes an error value, such as #DIV/0!, unless
    /// <paramref name="before"/>, the sheet it was written from as the edit found it, held that
    /// text: a text table holds an error value as its text, which a load keeps as text.
    /// </summary>
    internal static void TypeErrors(Worksheet table, Worksheet? before) =>
        PutValues(table, cell => cell.Type == CellValueType.IsString && cell.StringValue.StartsWith('#')
            && ReadType(cell.StringValue) == CellValueType.IsError
            && !(before?.Cells.CheckCell(cell.Row, cell.Column) is { Type: CellValueType.IsString } held && held.StringValue == cell.StringValue)
            ? cell.StringValue : null);

    // Puts into each cell of the sheet the value the function gives it, if any, once all are read.
    private static void PutValues(Worksheet sheet, Func<Cell, string?> value)
    {
        var put = new List<(Cell Cell, string Value)>();
        foreach (Cell cell in sheet.Cells)
        {
            if (value(cell) is { } text)
            {
                put.Add((cell, text));
            }
        }

        foreach ((Cell cell, string text) in put)
        {
            cell.PutValue(text);
        }
    }

    /// <summary>
    /// Runs <paramref name="work"/> with the thread's culture set to the invariant one, then sets
    /// it back: the engine types the values of delimited text, and writes its numbers, by the
    /// thread's culture, which would otherwise be the machine's regional settings, whatever
    /// culture its options name (known issues CELLS-CSV-SAVE-CULTURE and
    /// CELLS-CSV-FRACTION-CULTURE, KNOWN-ISSUES.md).
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
    /// would type: a number written with leading zeros (<see cref="NumberText.HasLeadingZeros"/>),
    /// and a date without a four-digit year, a month and a day in that order (1/2/2026, 1/2, 12:30,
    /// 2026-11), whose order of day and month, or whose day, the text does not say.
    /// </summary>
    internal static object? Reads(string text)
    {
        (CellValueType type, string shows, object? value) = Typed(text);
        return shows == text
            && (type == CellValueType.IsBool
                || type == CellValueType.IsNumeric && !NumberText.HasLeadingZeros(text)
                || type == CellValueType.IsDateTime && YearFirst().IsMatch(text))
                ? value
                : null;
    }

    /// <summary>
    /// Writes <paramref name="text"/> into <paramref name="cell"/> as a load as written reads it:
    /// the number, date or boolean that shows it again (<see cref="Reads"/>), with the number format
    /// that shows it, and as text otherwise.
    /// </summary>
    internal static void PutAsWritten(Cell cell, string text) => Invariant(() =>
    {
        if (Reads(text) is null)
        {
            cell.PutValue(text);
        }
        else
        {
            cell.PutValue(text, isConverted: true, setStyle: true);
        }
    });

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
    /// types as it loads, and any other value as text, and notes whether the input writes a decimal
    /// comma (<see cref="KeepSeparatedNumbersAsText"/>). One load uses one.
    /// </summary>
    private sealed class AsWritten(char separator) : ICustomParser
    {
        private bool _decimalComma, _commaNumber, _decimalPoint;

        internal bool DecimalCommas => _decimalComma || _commaNumber && !_decimalPoint;

        public object? ParseObject(string value)
        {
            string trimmed = value.Trim();
            _decimalComma = _decimalComma || DecimalComma().IsMatch(trimmed);
            _commaNumber = _commaNumber || separator == ';' && CommaNumber().IsMatch(trimmed);
            _decimalPoint = _decimalPoint || DecimalPoint().IsMatch(trimmed);
            return Reads(value) is null ? value : null;
        }

        public string GetFormat() => string.Empty;
    }

    // A number written with a decimal comma: a comma before one, two, or four and more digits, or
    // after dot thousands groups. A comma before exactly three digits may be a thousands separator.
    [GeneratedRegex(@"^[-+]?(?:\d+,\d{1,2}|\d+,\d{4,}|\d{1,3}(?:\.\d{3})+,\d+)$", RegexOptions.CultureInvariant)]
    private static partial Regex DecimalComma();

    // A number with one comma and no point, such as 1,234, which a file of semicolons writes with a decimal comma.
    [GeneratedRegex(@"^[-+]?\d+,\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex CommaNumber();

    // A number with a decimal point and no comma: a point before one, two, or four and more digits.
    [GeneratedRegex(@"^[-+]?\d+\.(?:\d{1,2}|\d{4,})$", RegexOptions.CultureInvariant)]
    private static partial Regex DecimalPoint();

    // A four-digit year, a month and a day, alone or before a time.
    [GeneratedRegex(@"^\d{4}[-/]\d{1,2}[-/]\d{1,2}(?:$|[ T])", RegexOptions.CultureInvariant)]
    private static partial Regex YearFirst();

    private static Encoding ResolveEncoding(string path, Encoding? marked, string? name, ResourceBudgetLedger budgets)
    {
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
}
