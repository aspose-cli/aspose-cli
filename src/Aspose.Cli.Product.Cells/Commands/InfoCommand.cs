using System.CommandLine;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>
/// <c>aspose-cli cells inspect</c> — the first step of the projection ladder:
/// structure and metadata, never bulk data.
/// </summary>
internal static class InfoCommand
{
    private const int MaxPreviewRows = 100;

    public static Command Create(IProductCommandHost<IWorkbookEngine> host)
    {
        var fileArgument = new Argument<string>("file")
        {
            Description = "Workbook to inspect (xlsx, xlsm, xlsb, xls, ods, csv, ...).",
        }.WithInput(InputKind.File);

        var previewOption = new Option<bool>("--preview")
        {
            Description = "Include a small sample of display values for each sheet.",
        };

        var previewRowsOption = new Option<int>("--preview-rows")
        {
            Description = $"Number of preview rows per sheet (1-{MaxPreviewRows}).",
            DefaultValueFactory = _ => 5,
        };

        var password = new PasswordOptions("--password", "the workbook");

        var detailOption = new Option<string[]>("--detail")
        {
            Description = "Extra sections: names (defined names), errors (formula-error scan), "
                + "fonts (fonts used), tables, charts, pivots, validation. Repeatable.",
            AllowMultipleArgumentsPerToken = true,
        }.WithInput(InputKind.None);
        detailOption.AcceptOnlyFromAmong([.. InfoDetails.All]);

        var info = new Command("inspect", "Show structure and metadata of a workbook.");
        info.Arguments.Add(fileArgument);
        info.Options.Add(previewOption);
        info.Options.Add(previewRowsOption);
        password.AddTo(info);
        info.Options.Add(detailOption);

        info.SetAction(parseResult => host.Run(parseResult, context =>
        {
            int previewRows = parseResult.GetValue(previewRowsOption);
            OptionGuards.EnsureInRange("--preview-rows", previewRows, 1, MaxPreviewRows,
                "Pass a smaller sample size; previews are meant to be cheap to read.");

            string path = context.Paths.ResolveInput(parseResult.GetRequiredValue(fileArgument));
            return context.Port.GetInfo(path, new InfoRequest
            {
                IncludePreview = parseResult.GetValue(previewOption),
                PreviewRows = previewRows,
                Details = parseResult.GetValue(detailOption),
                Password = password.Resolve(parseResult, context.Inputs),
            });
        }));

        return info;
    }
}
