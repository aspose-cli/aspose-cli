using System.CommandLine;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class InfoCommand
{
    private static readonly string[] Details =
        ["outline", "forms", "attachments", "fonts", "permissions", "signatures", "layers", "metadata"];

    public static Command Create(IProductCommandHost<IPdfEngine> host)
    {
        Argument<string> file = PdfOptions.File();
        var preview = new Option<bool>("--preview") { Description = "Include bounded per-page geometry." };
        var detail = new Option<string[]>("--detail")
        {
            Description = "Extra structural projections; repeatable.",
            AllowMultipleArgumentsPerToken = true,
        }.WithInput(InputKind.None);
        detail.AcceptOnlyFromAmong(Details);
        var password = new PasswordOptions("--password", "the PDF");
        var command = new Command("inspect", "Show PDF structure, security state and metadata.");
        command.Arguments.Add(file);
        command.Options.Add(preview);
        command.Options.Add(detail);
        password.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
            context.Port.GetInfo(
                context.Paths.ResolveInput(parse.GetRequiredValue(file)),
                new PdfInfoRequest
                {
                    IncludePreview = parse.GetValue(preview),
                    Details = parse.GetValue(detail),
                    Password = password.Resolve(parse, context.Inputs, context.ReadEnvironment),
                })));
        return command;
    }
}
