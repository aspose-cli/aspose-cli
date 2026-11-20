using System.CommandLine;
using System.Globalization;
using System.Text;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Formats;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>
/// <c>cells convert</c>: a workbook, or CSV or TSV text, saved in another format. How CSV or TSV
/// text is read, the sheet a one-sheet output writes and how a text output writes its values are
/// stated by the caller; nothing is detected.
/// </summary>
internal static class ConvertCommand
{

    public static CommandDefinition<CellsConvertRequest, CellsConvertResult> Create()
    {
        var sheets = new Option<string[]>(CellsOptionNames.Sheet)
        {
            Description = "A sheet to write, by its name: one for a csv, tsv or md output, which otherwise holds the sheet the engine writes, the active sheet; any for pdf, repeated. Other formats write the whole workbook.",
        }.WithInput(InputKind.None);
        var formatStrategy = new Option<string?>(CellsOptionNames.FormatStrategy)
        {
            Description = "How a csv, tsv or md output writes cell values, a member name of the engine's CellValueFormatStrategy: None writes the values as they are stored, DisplayString as the number formats show them. Default: the engine's.",
        }.WithInput(InputKind.None);
        var separator = new Option<string?>(CellsOptionNames.Separator)
        {
            Description = "The separator of a .csv or .txt input, one character (TxtLoadOptions.Separator). Required for such an input; a .tsv input is tab-separated.",
        }.WithInput(InputKind.None);
        var encoding = new Option<string?>(CellsOptionNames.Encoding)
        {
            Description = "The text encoding of a .csv, .tsv or .txt input, such as utf-8, gb18030 or windows-1252 (TxtLoadOptions.Encoding). Required for such an input.",
        }.WithInput(InputKind.None);
        var convertNumbers = new Option<bool?>(CellsOptionNames.ConvertNumbers)
        {
            Description = "true or false: whether a .csv, .tsv or .txt input's numbers become numbers (TxtLoadOptions.ConvertNumericData). Required for such an input.",
        }.WithInput(InputKind.None);
        var convertDates = new Option<bool?>(CellsOptionNames.ConvertDates)
        {
            Description = "true or false: whether a .csv, .tsv or .txt input's dates become dates (TxtLoadOptions.ConvertDateTimeData). Required for such an input.",
        }.WithInput(InputKind.None);
        var culture = new Option<string?>(CellsOptionNames.Culture)
        {
            Description = "The culture a text input's numbers and dates are read in, such as en-US or de-DE (LoadOptions.CultureInfo). Required when --convert-numbers or --convert-dates is true.",
        }.WithInput(InputKind.None);
        var bom = new BomOption("a csv or tsv output");
        return new(
            "convert",
            "Convert a workbook, or CSV or TSV text, to another format with the engine's options for it. An html, mhtml or md output is one file: it holds its images itself (SaveAsSingleFile, ExportImagesAsBase64).",
            new CommandTraits
            {
                Input = CellsInputs.Workbook,
                Output = OutputTarget.File("Output path. Default: the input path with the target extension "
                    + "(with '.out' inserted when that would overwrite the input)."),
                Encrypt = CellsInputs.EncryptedWorkbook,
                UsesFonts = true,
                Target = TargetFormat.Convert(
                    $"Target format: {string.Join(", ", CellsFormats.Definitions.IdsFor(FormatUse.Convert))}.",
                    CellsFormats.Definitions),
            },
            [sheets, formatStrategy, separator, encoding, convertNumbers, convertDates, culture, .. bom.Options],
            (parse, standard) =>
            {
                string format = standard.Output.Format.Id;
                string[] named = parse.GetValue(sheets) ?? [];
                bool oneSheet = CellsFormats.OneSheet.Contains(format, StringComparer.Ordinal);
                if (oneSheet && named.Length > 1)
                {
                    throw CliErrors.OptionInvalid(CellsOptionNames.Sheet, $"a {format} output holds one sheet, and --sheet named {named.Length}");
                }

                if (!CellsFormats.SheetSelected.Contains(format, StringComparer.Ordinal) && named.Length > 0)
                {
                    throw CliErrors.OptionInvalid(CellsOptionNames.Sheet, $"a {format} output holds the whole workbook; --sheet applies to {string.Join(", ", CellsFormats.SheetSelected)}");
                }

                string? strategy = parse.GetValue(formatStrategy);
                if (!oneSheet && strategy is not null)
                {
                    throw CliErrors.OptionInvalid(CellsOptionNames.FormatStrategy, $"a {format} output writes no text table; --format-strategy applies to csv, tsv and md");
                }

                bool byteOrderMark = bom.Read(parse, CellsFormats.Delimited.Contains(format, StringComparer.Ordinal) ? null : $"'{format}' output is not csv or tsv text");
                return new CellsConvertRequest
                {
                    Input = standard.Input,
                    Output = standard.Output,
                    Sheets = named.Length == 0 ? null : named,
                    Password = standard.InputPassword,
                    EncryptPassword = standard.EncryptPassword(),
                    TextInput = TextInput(standard.Input, parse, separator, encoding, convertNumbers, convertDates, culture),
                    FormatStrategy = strategy,
                    ByteOrderMark = byteOrderMark,
                };
            })
        {
            Examples =
            [
                "cells convert book.xlsx --out report.pdf",
                "cells convert book.xlsx --to csv --sheet Sales --format-strategy None --out sales.csv",
                "cells convert sales.csv --to xlsx --separator , --encoding utf-8 --convert-numbers true --convert-dates false",
                "cells convert export.csv --to xlsx --separator ; --encoding windows-1252 --convert-numbers true --convert-dates true --culture de-DE",
            ],
            Links =
            [
                CommandHelpLink.Schema(CellsModule.Manifest, "convert-result", "the fields of the convert result"),
            ],
        };
    }

    /// <summary>
    /// How a text input is read: every option stated for a .csv, .tsv or .txt input, none for a
    /// workbook. A .tsv input is tab-separated; any other text input names its separator.
    /// </summary>
    private static CellsTextInput? TextInput(
        string input, ParseResult parse, Option<string?> separator, Option<string?> encoding,
        Option<bool?> convertNumbers, Option<bool?> convertDates, Option<string?> culture)
    {
        string? separatorText = parse.GetValue(separator);
        string? encodingName = parse.GetValue(encoding);
        bool? numbers = parse.GetValue(convertNumbers);
        bool? dates = parse.GetValue(convertDates);
        string? cultureName = parse.GetValue(culture);
        if (CellsFormats.TextFormatOf(input) is not { } text)
        {
            (string Option, bool Given)[] given =
            [
                (CellsOptionNames.Separator, separatorText is not null), (CellsOptionNames.Encoding, encodingName is not null),
                (CellsOptionNames.ConvertNumbers, numbers is not null), (CellsOptionNames.ConvertDates, dates is not null),
                (CellsOptionNames.Culture, cultureName is not null),
            ];
            return given.FirstOrDefault(static option => option.Given) is { Option: { } stated }
                ? throw CliErrors.OptionInvalid(stated, $"it applies to a csv or tsv text input, and {Path.GetFileName(input)} is none")
                : null;
        }

        bool fixedSeparator = CellsFormats.FixedSeparators.TryGetValue(text.Id, out char own);
        if (fixedSeparator && separatorText is not null)
        {
            throw CliErrors.OptionInvalid(CellsOptionNames.Separator, $"a {text.Id} input has its own separator");
        }

        char character = fixedSeparator ? own
            : separatorText is { Length: 1 } one ? one[0]
            : throw CliErrors.OptionInvalid(CellsOptionNames.Separator, separatorText is null ? $"a {text.Id} input needs --separator, one character" : $"'{separatorText}' is not one character");
        Encoding textEncoding = encodingName is null
            ? throw CliErrors.OptionInvalid(CellsOptionNames.Encoding, $"a {text.Id} input needs --encoding")
            : Named(encodingName);
        bool numbersValue = numbers ?? throw CliErrors.OptionInvalid(CellsOptionNames.ConvertNumbers, $"a {text.Id} input needs --convert-numbers true or false");
        bool datesValue = dates ?? throw CliErrors.OptionInvalid(CellsOptionNames.ConvertDates, $"a {text.Id} input needs --convert-dates true or false");
        if ((numbersValue || datesValue) != (cultureName is not null))
        {
            throw CliErrors.OptionInvalid(CellsOptionNames.Culture, cultureName is null
                ? "converting numbers or dates needs --culture, the culture they are written in"
                : "it applies when --convert-numbers or --convert-dates is true");
        }

        CultureInfo? textCulture = null;
        if (cultureName is not null)
        {
            try
            {
                textCulture = CultureInfo.GetCultureInfo(cultureName, predefinedOnly: true);
            }
            catch (CultureNotFoundException)
            {
                throw CliErrors.OptionInvalid(CellsOptionNames.Culture, $"'{cultureName}' is no culture name");
            }
        }

        return new CellsTextInput(text.Id, character, textEncoding, numbersValue, datesValue, textCulture);
    }

    private static Encoding Named(string name)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        try
        {
            return Encoding.GetEncoding(name);
        }
        catch (ArgumentException)
        {
            throw CliErrors.OptionInvalid(CellsOptionNames.Encoding, $"'{name}' is no encoding name");
        }
    }
}
