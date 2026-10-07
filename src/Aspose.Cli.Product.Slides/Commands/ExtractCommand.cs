using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class ExtractCommand
{
    public static Command Create(IProductCommandHost<ISlidesEngine> host)
    {
        var what = new Option<string>("--what") { Required = true, Description = "media, notes or text." }.WithInput(InputKind.None);
        what.AcceptOnlyFromAmong([.. PresentationExtractKinds.All]);
        var slides = new Option<string?>("--slides") { Description = "Optional 1-based slide range; only those slides are extracted. Default: every slide." }.WithInput(InputKind.None);
        return StandardCommand.Create(
            host,
            "extract",
            "Extract bounded presentation media, notes or text.",
            new CommandTraits
            {
                Input = SlidesCommands.Presentation,
                Output = OutputTarget.Directory("Safe extraction directory."),
            },
            [what, slides],
            (parse, standard) =>
            {
                string? range = parse.GetValue(slides);
                return standard.OpenEngine().Extract(standard.Input, new PresentationExtractRequest
                {
                    What = parse.GetRequiredValue(what),
                    Output = standard.DirectoryOutput,
                    Slides = range is null ? null : PageRange.Parse(range),
                    Password = standard.InputPassword,
                });
            });
    }
}
