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
        var preview = new Option<bool>("--preview") { Description = "Include bounded slide titles." };
        var detail = new Option<string[]>("--detail")
        {
            Description = "Extra structural projections; repeatable.",
            AllowMultipleArgumentsPerToken = true,
        }.WithInput(InputKind.None);
        detail.AcceptOnlyFromAmong(Details);
        return StandardCommand.Create(
            host,
            "inspect",
            "Show presentation structure, stable slide ids and metadata.",
            new CommandTraits { Input = SlidesCommands.Presentation },
            [preview, detail],
            (parse, standard) => standard.Port.GetInfo(standard.Input, new PresentationInfoRequest
            {
                IncludePreview = parse.GetValue(preview),
                Details = parse.GetValue(detail),
                Password = standard.InputPassword,
            }));
    }
}
