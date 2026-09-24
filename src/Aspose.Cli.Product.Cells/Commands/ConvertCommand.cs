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
        var to = new Option<string>("--to")
        {
            Description = $"Target format: {string.Join(", ", CellsFormats.Definitions.IdsFor(FormatUse.Convert))}.",
            Required = true,
        }.WithInput(InputKind.None);
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
            },
            [to, sheet],
            (parse, standard) =>
            {
                FormatInfo format = CellsFormats.ResolveConvert(parse.GetRequiredValue(to));
                string? sheetName = parse.GetValue(sheet);
                if (sheetName is not null && !CellsFormats.SheetScopedConvertIds.Contains(format.Id))
                {
                    throw CliErrors.OptionInvalid(
                        "--sheet",
                        $"the '{format.Id}' format always converts the whole workbook",
                        $"Drop --sheet, or use one of: {string.Join(", ", CellsFormats.SheetScopedConvertIds)}.");
                }

                string output = standard.OutputPath(format.Extension);
                return standard.Port.Convert(standard.Input, new ConvertRequest
                {
                    TargetFormatId = format.Id,
                    OutputPath = output,
                    Overwrite = standard.Overwrite,
                    SheetName = sheetName,
                    Password = standard.InputPassword,
                    EncryptPassword = standard.EncryptPassword(format.Id),
                });
            }).WithExamples(
            [
                "cells convert sales.csv --to xlsx",
                "cells convert book.xlsx --to pdf --out report.pdf",
                "cells convert book.xlsx --to pdf --font-dir fonts --out report.pdf",
            ]);
    }
}
