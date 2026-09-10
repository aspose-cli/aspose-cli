using System.Text;
using Aspose.Cells;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary>
/// Owns content-driven workbook detection, password application and guarded
/// open policy. No query, rendering or mutation behavior belongs here.
/// </summary>
internal sealed class WorkbookLoadService(ResourceBudgetLedger resourceBudgets)
{
    private static readonly HashSet<FileFormatType> OpenableFormats =
    [
        FileFormatType.Xlsx,
        FileFormatType.Xlsm,
        FileFormatType.Xlsb,
        FileFormatType.Xltx,
        FileFormatType.Xltm,
        FileFormatType.Excel97To2003,
        FileFormatType.Excel95,
        FileFormatType.Excel2,
        FileFormatType.Excel3,
        FileFormatType.Excel4,
        FileFormatType.Ods,
        FileFormatType.Ots,
        FileFormatType.SpreadsheetML,
        FileFormatType.Csv,
        FileFormatType.TabDelimited,
        FileFormatType.Html,
        FileFormatType.MHtml,
    ];

    private static readonly HashSet<string> TextExtensions =
        new(StringComparer.Ordinal)
        {
            ".csv",
            ".tsv",
            ".txt",
            ".json",
        };

    internal Workbook Open(string path, string? password)
    {
        InputSizeGuard.Ensure(
            resourceBudgets,
            path,
            InputSizeGuard.ResolveMaxBytes(Environment.GetEnvironmentVariable));
        LoadPlan plan = ResolveLoadPlan(path);
        try
        {
            LoadOptions loadOptions = plan.ToLoadOptions();
            loadOptions.Password = password;
            var workbook = new Workbook(path, loadOptions);
            try
            {
                resourceBudgets.EnsureWithin(
                    CellsBudgetDomains.Sheets,
                    workbook.Worksheets.Count,
                    "items",
                    "post-load");
                resourceBudgets.EnsureWithin(
                    CellsBudgetDomains.Objects,
                    workbook.Worksheets.Cast<Worksheet>()
                        .Sum(static sheet => (long)sheet.Shapes.Count),
                    "items",
                    "post-load");
            }
            catch
            {
                workbook.Dispose();
                throw;
            }

            if (workbook.FileFormat is FileFormatType.Html or FileFormatType.MHtml)
            {
                foreach (Worksheet sheet in workbook.Worksheets)
                {
                    sheet.AutoFitColumns();
                }
            }

            return workbook;
        }
        catch (Exception exception) when (exception is not CliException)
        {
            throw ErrorTranslator.TranslateLoad(
                exception,
                path,
                passwordProvided: password is not null);
        }
    }

    private static LoadPlan ResolveLoadPlan(string path)
    {
        FileFormatInfo detected;
        try
        {
            detected = FileFormatUtil.DetectFileFormat(path);
        }
        catch (Exception exception) when (exception is not CliException)
        {
            throw ErrorTranslator.TranslateLoad(
                exception,
                path,
                passwordProvided: false);
        }

        if (detected.IsEncrypted)
        {
            return LoadPlan.Auto;
        }

        if (OpenableFormats.Contains(detected.FileFormatType))
        {
            if (detected.FileFormatType
                is FileFormatType.Csv or FileFormatType.TabDelimited)
            {
                char? separator = DetectDelimiter(path)
                    ?? (detected.FileFormatType == FileFormatType.TabDelimited
                        ? '\t'
                        : null);
                return new LoadPlan(null, separator);
            }

            return LoadPlan.Auto;
        }

        if (detected.FileFormatType == FileFormatType.Unknown)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (TextExtensions.Contains(extension))
            {
                return extension == ".json"
                    ? LoadPlan.Auto
                    : new LoadPlan(null, DetectDelimiter(path));
            }

            if (ContentLooksLikeHtml(path))
            {
                return new LoadPlan(LoadFormat.Html, null);
            }
        }

        string reason = detected.FileFormatType == FileFormatType.Unknown
            ? "content does not match any supported spreadsheet format"
            : $"the file's content is {detected.FileFormatType}, not a spreadsheet";
        throw CellsErrors.FileCorrupt(path, reason);
    }

    private static char? DetectDelimiter(string path)
    {
        string? line = ReadFirstNonEmptyLine(path);
        if (line is null)
        {
            return null;
        }

        int comma = 0;
        int semicolon = 0;
        int tab = 0;
        int pipe = 0;
        bool inQuotes = false;
        foreach (char character in line)
        {
            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }
            if (inQuotes)
            {
                continue;
            }

            switch (character)
            {
                case ',': comma++; break;
                case ';': semicolon++; break;
                case '\t': tab++; break;
                case '|': pipe++; break;
            }
        }

        (char Character, int Count) best = (';', semicolon);
        if (tab > best.Count)
        {
            best = ('\t', tab);
        }
        if (pipe > best.Count)
        {
            best = ('|', pipe);
        }
        return best.Count > comma ? best.Character : null;
    }

    private static string? ReadFirstNonEmptyLine(string path)
    {
        byte[] prefix;
        try
        {
            using FileStream stream = File.OpenRead(path);
            int length = (int)Math.Min(8192, stream.Length);
            prefix = new byte[length];
            int read = stream.Read(prefix, 0, length);
            if (read < length)
            {
                Array.Resize(ref prefix, read);
            }
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        Encoding encoding = Encoding.UTF8;
        if (prefix.Length >= 2 && prefix[0] == 0xFF && prefix[1] == 0xFE)
        {
            encoding = Encoding.Unicode;
        }
        else if (prefix.Length >= 2 && prefix[0] == 0xFE && prefix[1] == 0xFF)
        {
            encoding = Encoding.BigEndianUnicode;
        }

        string text;
        try
        {
            text = encoding.GetString(prefix);
        }
        catch (DecoderFallbackException)
        {
            text = Encoding.Latin1.GetString(prefix);
        }

        if (text.Length > 0 && text[0] == '\uFEFF')
        {
            text = text[1..];
        }

        return text.Split('\n')
            .Select(static line => line.TrimEnd('\r'))
            .FirstOrDefault(static line => !string.IsNullOrWhiteSpace(line));
    }

    private static bool ContentLooksLikeHtml(string path)
    {
        byte[] prefix;
        try
        {
            using FileStream stream = File.OpenRead(path);
            int length = (int)Math.Min(4096, stream.Length);
            prefix = new byte[length];
            int read = stream.Read(prefix, 0, length);
            if (read < length)
            {
                Array.Resize(ref prefix, read);
            }
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        string text = Encoding.ASCII.GetString(prefix).ToLowerInvariant();
        string[] signatures =
        [
            "<!doctype html",
            "<html",
            "<head",
            "<body",
            "<meta",
            "<table",
            "mso-number-format",
        ];
        return Array.Exists(
            signatures,
            signature => text.Contains(signature, StringComparison.Ordinal));
    }

    private readonly record struct LoadPlan(LoadFormat? Format, char? Separator)
    {
        internal static LoadPlan Auto => default;

        internal LoadOptions ToLoadOptions()
        {
            if (Separator is { } separator)
            {
                return new TxtLoadOptions(LoadFormat.Csv)
                {
                    Separator = separator,
                };
            }
            return Format is { } format
                ? new LoadOptions(format)
                : new LoadOptions();
        }
    }
}
