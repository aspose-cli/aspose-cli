using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class InfoCommand
{
    private static readonly string[] Details =
        ["outline", "forms", "attachments", "fonts", "permissions", "signatures", "layers", "metadata"];

    public static Command Create(IProductCommandHost<IPdfEngine> host)
    {
        var preview = new Option<bool>("--preview") { Description = "Include bounded per-page geometry." };
        var detail = new Option<string[]>("--detail")
        {
            Description = "Extra structural projections; repeatable.",
            AllowMultipleArgumentsPerToken = true,
        }.WithInput(InputKind.None);
        detail.AcceptOnlyFromAmong(Details);
        return StandardCommand.Create(
            host,
            "inspect",
            "Show PDF structure, security state and metadata.",
            new CommandTraits { Input = PdfCommands.Document },
            [preview, detail],
            (parse, standard) => standard.OpenEngine().GetInfo(standard.Input, new PdfInfoRequest
            {
                IncludePreview = parse.GetValue(preview),
                Details = parse.GetValue(detail),
                Password = standard.InputPassword,
            }));
    }
}
