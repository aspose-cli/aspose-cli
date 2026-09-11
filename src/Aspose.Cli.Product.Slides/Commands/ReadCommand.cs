using System.CommandLine;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class ReadCommand
{
    public static Command Create(IProductCommandHost<IPresentationEngine> host)
    {
        Argument<string> file = SlidesOptions.File();
        var slides = new Option<string?>("--slides") { Description = "1-based slide range, e.g. 1-3,7,9-." }.WithInput(InputKind.None);
        var scope = new Option<string>("--scope")
        {
            Description = "Projection scope: text, shapes or full.",
            DefaultValueFactory = _ => PresentationReadScopes.Shapes,
        }.WithInput(InputKind.None);
        scope.AcceptOnlyFromAmong([.. PresentationReadScopes.All]);
        var maxChars = new Option<int>("--max-chars")
        {
            Description = "Maximum projected text characters.",
            DefaultValueFactory = _ => 20_000,
        };
        var notes = new Option<bool>("--notes") { Description = "Include speaker notes for returned slides." };
        var password = new PasswordOptions("--password", "the presentation");
        var command = new Command("slides", "Read a bounded slide-content window.");
        command.Arguments.Add(file);
        command.Options.Add(slides);
        command.Options.Add(scope);
        command.Options.Add(maxChars);
        command.Options.Add(notes);
        password.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
        {
            int characters = parse.GetValue(maxChars);
            OptionGuards.EnsureInRange(
                "--max-chars", characters, 1, 10_000_000,
                "Use a positive bounded character budget.");
            string? range = parse.GetValue(slides);
            return context.Port.Read(
                context.Paths.ResolveInput(parse.GetRequiredValue(file)),
                new PresentationReadRequest
                {
                    Slides = range is null ? null : PageRange.Parse(range),
                    Scope = parse.GetValue(scope) ?? PresentationReadScopes.Shapes,
                    IncludeNotes = parse.GetValue(notes),
                    MaxCharacters = characters,
                    Password = password.Resolve(parse, context.Inputs),
                });
        }));
        return command;
    }
}
