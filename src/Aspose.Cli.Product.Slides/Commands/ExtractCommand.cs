using System.CommandLine;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class ExtractCommand
{
    public static Command Create(IProductCommandHost<IPresentationEngine> host)
    {
        Argument<string> file = SlidesOptions.File();
        var what = new Option<string>("--what") { Required = true, Description = "media, notes or text." };
        what.AcceptOnlyFromAmong([.. PresentationExtractKinds.All]);
        var slides = new Option<string?>("--slides") { Description = "Optional slide range for notes or text." };
        var outDirectory = new Option<string>("--out-dir", "--out") { Required = true, Description = "Safe extraction directory." };
        Option<bool> overwrite = OutputOptions.Overwrite();
        var password = new PasswordOptions("--password", "the presentation");
        var command = new Command("extract", "Extract bounded presentation media, notes or text.");
        command.Arguments.Add(file);
        command.Options.Add(what);
        command.Options.Add(slides);
        command.Options.Add(outDirectory);
        command.Options.Add(overwrite);
        password.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
        {
            string? range = parse.GetValue(slides);
            return context.Port.Extract(
                context.Paths.ResolveInput(parse.GetRequiredValue(file)),
                new PresentationExtractRequest
                {
                    What = parse.GetRequiredValue(what),
                    OutputDirectory = context.Paths.ResolveOutput(parse.GetRequiredValue(outDirectory)),
                    Overwrite = parse.GetValue(overwrite),
                    Slides = range is null ? null : PageRange.Parse(range),
                    Password = password.Resolve(parse, context.Inputs),
                });
        }));
        return command;
    }
}
