using System.CommandLine;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary><c>aspose-cli cells convert</c> — document format conversion.</summary>
internal static class ConvertCommand
{
    public static Command Create(IProductCommandHost<IWorkbookEngine> host)
    {
        var fileArgument = new Argument<string>("file")
        {
            Description = "Workbook to convert.",
        }.WithInput(InputKind.File);

        var toOption = new Option<string>("--to")
        {
            Description = $"Target format: {string.Join(", ", CellsModule.Formats.IdsFor(FormatUse.Convert))}.",
            Required = true,
        }.WithInput(InputKind.None);

        var output = new OutputFileOptions(
            "Output path. Default: the input path with the target extension " +
            "(with '.out' inserted when that would overwrite the input).");

        var sheetOption = new Option<string?>("--sheet")
        {
            Description = $"Convert only this sheet (supported for {string.Join(", ", CellsFormats.SheetScopedConvertIds)}).",
        }.WithInput(InputKind.None);

        var password = new PasswordOptions("--password", "the workbook");

        var convert = new Command("convert", "Convert a workbook to another format.");
        convert.Arguments.Add(fileArgument);
        convert.Options.Add(toOption);
        convert.Options.Add(sheetOption);
        output.AddTo(convert);
        password.AddTo(convert);

        convert.SetAction(parseResult => host.Run(parseResult, context =>
        {
            FormatInfo format = CellsFormats.ResolveConvert(parseResult.GetRequiredValue(toOption));

            string? sheetName = parseResult.GetValue(sheetOption);
            if (sheetName is not null && !CellsFormats.SheetScopedConvertIds.Contains(format.Id))
            {
                throw CliErrors.OptionInvalid(
                    "--sheet",
                    $"the '{format.Id}' format always converts the whole workbook",
                    $"Drop --sheet, or use one of: {string.Join(", ", CellsFormats.SheetScopedConvertIds)}.");
            }

            string inputPath = context.Paths.ResolveInput(parseResult.GetRequiredValue(fileArgument));
            string outputPath = output.ResolvePath(parseResult, context.Paths, inputPath, format.Extension);

            return context.Port.Convert(inputPath, new ConvertRequest
            {
                TargetFormatId = format.Id,
                OutputPath = outputPath,
                Overwrite = output.Overwrite(parseResult),
                SheetName = sheetName,
                Password = password.Resolve(parseResult, context.Inputs, context.ReadEnvironment),
            });
        }));

        return convert;
    }
}
