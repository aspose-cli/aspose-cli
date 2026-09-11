using System.CommandLine;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class InfoCommand
{
    private static readonly string[] Details =
        ["masters", "layouts", "media", "fonts", "notes", "comments", "sections", "properties"];

    public static Command Create(IProductCommandHost<IPresentationEngine> host)
    {
        Argument<string> file = SlidesOptions.File();
        var preview = new Option<bool>("--preview") { Description = "Include bounded slide titles." };
        var detail = new Option<string[]>("--detail")
        {
            Description = "Extra structural projections; repeatable.",
            AllowMultipleArgumentsPerToken = true,
        }.WithInput(InputKind.None);
        detail.AcceptOnlyFromAmong(Details);
        var password = new PasswordOptions("--password", "the presentation");
        var command = new Command("inspect", "Show presentation structure, stable slide ids and metadata.");
        command.Arguments.Add(file);
        command.Options.Add(preview);
        command.Options.Add(detail);
        password.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
            context.Port.GetInfo(
                context.Paths.ResolveInput(parse.GetRequiredValue(file)),
                new PresentationInfoRequest
                {
                    IncludePreview = parse.GetValue(preview),
                    Details = parse.GetValue(detail),
                    Password = password.Resolve(parse, context.Inputs),
                })));
        return command;
    }
}
