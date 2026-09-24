using System.CommandLine;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary><c>cells convert</c> — workbook format conversion.</summary>
internal static class ConvertCommand
{
    public static Command Create(IProductCommandHost<IWorkbookEngine> host)
    {
        var sheet = new Option<string?>("--sheet")
        {
            Description = $"Convert only this sheet (supported for {string.Join(", ", CellsFormats.SheetScopedConvertIds)}).",
        }.WithInput(InputKind.None);
        return StandardCommand.Create(
            host,
            "convert",
            "Convert a workbook to another format.",
            new CommandTraits
            {
                Input = CellsCommands.Workbook("Workbook to convert."),
                Output = OutputTarget.File("Output path. Default: the input path with the target extension "
                    + "(with '.out' inserted when that would overwrite the input)."),
                Encrypt = CellsCommands.EncryptedWorkbook,
                UsesFonts = true,
                Target = TargetFormat.Convert(
                    $"Target format: {string.Join(", ", CellsFormats.Definitions.IdsFor(FormatUse.Convert))}.",
                    CellsFormats.Definitions),
            },
            [sheet],
            (parse, standard) =>
            {
                string format = standard.TargetFormat();
                string? sheetName = parse.GetValue(sheet);
                if (sheetName is not null && !CellsFormats.SheetScopedConvertIds.Contains(format))
                {
                    throw CliErrors.OptionInvalid(
                        "--sheet",
                        $"the '{format}' format always converts the whole workbook",
                        $"Drop --sheet, or use one of: {string.Join(", ", CellsFormats.SheetScopedConvertIds)}.");
                }

                string? encryptPassword = standard.EncryptPassword(format);
                string output = standard.OutputPath(CellsFormats.Definitions.ExtensionFor(format));
                return standard.OpenEngine().Convert(standard.Input, new ConvertRequest
                {
                    TargetFormatId = format,
                    OutputPath = output,
                    Overwrite = standard.Overwrite,
                    SheetName = sheetName,
                    Password = standard.InputPassword,
                    EncryptPassword = encryptPassword,
                });
            }).WithExamples(
            [
                "cells convert sales.csv --to xlsx",
                "cells convert book.xlsx --to pdf --out report.pdf",
                "cells convert book.xlsx --to pdf --font-dir fonts --out report.pdf",
            ]);
    }
}
