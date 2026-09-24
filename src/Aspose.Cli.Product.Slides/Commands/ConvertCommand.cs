using System.CommandLine;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class ConvertCommand
{
    public static Command Create(IProductCommandHost<IPresentationEngine> host)
    {
        Argument<string> file = SlidesOptions.File();
        var to = new Option<string>("--to") { Required = true, Description = "Target presentation export format. PNG and JPEG use 192 DPI; use slides render for custom dimensions." }.WithInput(InputKind.None);
        to.AcceptOnlyFromAmong([.. SlidesFormats.Definitions.IdsFor(FormatUse.Convert)]);
        var slides = new Option<string?>("--slides") { Description = "Optional 1-based slide range." }.WithInput(InputKind.None);
        var output = new OutputFileOptions(
            "Output path; defaults to a sibling using the target extension.");
        var password = new PasswordOptions("--password", "the presentation");
        var encrypt = new PasswordOptions("--encrypt", "the output presentation", allowStdin: false);
        var fonts = new FontDirectoryOptions();
        var command = new Command("convert", "Convert a presentation or selected slides.");
        command.Arguments.Add(file);
        command.Options.Add(to);
        command.Options.Add(slides);
        output.AddTo(command);
        password.AddTo(command);
        encrypt.AddTo(command);
        fonts.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
        {
            string input = context.Paths.ResolveInput(parse.GetRequiredValue(file));
            string format = parse.GetRequiredValue(to);
            string? range = parse.GetValue(slides);
            using IDisposable fontScope = fonts.Use(parse, context);
            return context.Port.Convert(input, new PresentationConvertRequest
            {
                TargetFormatId = format,
                OutputPath = output.ResolvePath(parse, context.Paths, input, SlidesFormats.Definitions.ExtensionFor(format)),
                Overwrite = output.Overwrite(parse),
                Slides = range is null ? null : PageRange.Parse(range),
                Password = password.Resolve(parse, context.Inputs, context.ReadEnvironment),
                EncryptPassword = encrypt.Resolve(parse, context.Inputs, context.ReadEnvironment),
            });
        }));
        return command;
    }
}
