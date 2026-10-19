using System.CommandLine;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Output;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary><c>cells convert</c> — workbook format conversion.</summary>
internal static class ConvertCommand
{
    public static CommandDefinition<CellsConvertRequest, CellsConvertResult> Create()
    {
        var sheets = new PartRangeOption("sheet", "every sheet, except a CSV, TSV or Markdown output, which holds "
            + "the active sheet and takes one selected sheet; a PDF takes any selection")
        {
        };
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
                Input = CellsInputs.Workbook("Workbook to convert."),
                Output = OutputTarget.File("Output path. Default: the input path with the target extension "
                    + "(with '.out' inserted when that would overwrite the input)."),
                Encrypt = CellsInputs.EncryptedWorkbook,
                UsesFonts = true,
                Target = TargetFormat.Convert(
                    $"Target format: {string.Join(", ", CellsFormats.Definitions.IdsFor(FormatUse.Convert))}.",
                    CellsFormats.Definitions),
            },
            [.. sheets.Options, encoding, culture, bom],
            (parse, standard) =>
            {
                ResolvedOutput output = standard.Output;
                string format = output.Format.Id;
                string? encodingName = parse.GetValue(encoding);
                string? cultureName = parse.GetValue(culture);
                (PageRange? read, string? text) = CellsInputs.SheetSelection(sheets, parse);
                PageRange? selection = read is { } range && !ReferenceEquals(range, PageRange.All) ? range : null;
                if ((selection is not null || text is not null) && !CellsFormats.SheetScoped.Contains(format))
                {
                    throw CliErrors.OptionInvalid(
                        sheets.Name,
                        $"the '{format}' format always converts the whole workbook",
                        $"Drop {sheets.Name}, or use one of: {string.Join(", ", CellsFormats.SheetScoped)}.");
                }

                bool byteOrderMark = parse.GetValue(bom);
                if (byteOrderMark && !CellsFormats.IsDelimited(format))
                {
                    throw CliErrors.OptionInvalid("--bom", $"a '{format}' output is not CSV or TSV text", "Drop --bom.");
                }

                Secret? encryptPassword = standard.EncryptPassword();
                return new CellsConvertRequest
                {
                    Input = standard.Input,
                    Output = output,
                    Sheets = selection,
                    SheetsText = text,
                    Password = standard.InputPassword,
                    EncryptPassword = encryptPassword,
                    TextImport = encodingName is null && cultureName is null
                        ? null
                        : new CellsTextImportOptions { Encoding = encodingName, Culture = cultureName },
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
                "cells convert book.xlsx --out report.pdf",
                "cells convert book.xlsx --to pdf --font-dir fonts --out report.pdf",
            ],
            Links =
            [
                CommandHelpLink.Docs(CellsModule.Manifest, "report-from-csv", "importing CSV into a workbook"),
                CommandHelpLink.Docs(CellsModule.Manifest, "overview", "what each Cells command is for"),
            ],
        };
    }

    internal static void Table(CellsConvertResult convert, TableSurface surface) =>
        ResultText.Files(surface, "wrote", "file", [.. convert.Outputs.Select(output =>
            new ProducedFile(null, output.Path, output.Format, output.SizeBytes, convert.Sheets is null ? null : $"sheets {string.Join(", ", convert.Sheets)}"))]);
}
