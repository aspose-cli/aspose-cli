using System.CommandLine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility.Output;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary><c>cells convert</c> — workbook format conversion.</summary>
internal static class ConvertCommand
{
    public static CommandDefinition<ConvertRequest, ConvertResult> Create()
    {
        var sheet = new Option<string?>("--sheet")
        {
            Description = $"Convert only this sheet (supported for {string.Join(", ", CellsFormats.SheetScopedConvertIds)}).",
        }.WithInput(InputKind.None);
        var encoding = new Option<string?>("--encoding")
        {
            Description = "Text encoding of a CSV or TSV input, such as gb18030, big5, shift_jis or windows-1252. "
                + "Default: its byte order mark, else UTF-8 (other bytes are refused).",
        }.WithInput(InputKind.None);
        var culture = new Option<string?>("--culture")
        {
            Description = "Culture whose number and date formats a CSV or TSV input uses, such as de-DE for '1.234,56'. "
                + "Default: invariant formats (a decimal comma is refused).",
        }.WithInput(InputKind.None);
        var bom = new Option<bool>("--bom")
        {
            Description = "Start a CSV or TSV output with a UTF-8 byte order mark, so Excel reads its non-English text "
                + "correctly. Default: UTF-8 without one.",
        };
        return new(
            "convert",
            "Convert a workbook to another format.",
            new CommandTraits
            {
                Input = CellsTraits.Workbook("Workbook to convert."),
                Output = OutputTarget.File("Output path. Default: the input path with the target extension "
                    + "(with '.out' inserted when that would overwrite the input)."),
                Encrypt = CellsTraits.EncryptedWorkbook,
                UsesFonts = true,
                Target = TargetFormat.Convert(
                    $"Target format: {string.Join(", ", CellsFormats.Definitions.IdsFor(FormatUse.Convert))}.",
                    CellsFormats.Definitions),
            },
            [sheet, encoding, culture, bom],
            (parse, standard) =>
            {
                ResolvedOutput output = standard.Output;
                string format = output.Format.Id;
                string? encodingName = parse.GetValue(encoding);
                string? cultureName = parse.GetValue(culture);
                string? sheetName = parse.GetValue(sheet);
                if (sheetName is not null && !CellsFormats.SheetScopedConvertIds.Contains(format))
                {
                    throw CliErrors.OptionInvalid(
                        "--sheet",
                        $"the '{format}' format always converts the whole workbook",
                        $"Drop --sheet, or use one of: {string.Join(", ", CellsFormats.SheetScopedConvertIds)}.");
                }

                bool byteOrderMark = parse.GetValue(bom);
                if (byteOrderMark && format is not ("csv" or "tsv"))
                {
                    throw CliErrors.OptionInvalid("--bom", $"a '{format}' output is not CSV or TSV text", "Drop --bom.");
                }

                Secret? encryptPassword = standard.EncryptPassword();
                return new ConvertRequest
                {
                    Input = standard.Input,
                    Output = output,
                    SheetName = sheetName,
                    Password = standard.InputPassword,
                    EncryptPassword = encryptPassword,
                    TextImport = encodingName is null && cultureName is null
                        ? null
                        : new TextImportOptions { Encoding = encodingName, Culture = cultureName },
                    ByteOrderMark = byteOrderMark,
                };
            },
            Table)
        {
            Examples =
            [
                "cells convert sales.csv --to xlsx",
                "cells convert erp-export.csv --to xlsx --encoding gb18030",
                "cells convert partner-orders.csv --to xlsx --culture de-DE",
                "cells convert book.xlsx --to csv --bom --out for-excel.csv",
                "cells convert book.xlsx --to pdf --out report.pdf",
                "cells convert book.xlsx --to pdf --font-dir fonts --out report.pdf",
            ],
        };
    }

    internal static void Table(ConvertResult convert, TableSurface surface) =>
        ResultText.Produced(surface, convert.Output, convert.Sheet is null ? null : $"sheet {convert.Sheet}");
}
