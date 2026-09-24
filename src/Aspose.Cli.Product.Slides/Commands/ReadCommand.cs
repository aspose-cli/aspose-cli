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
        var slides = new Option<string?>("--slides") { Description = "1-based slide range, e.g. 1-3,7,9-. Default: the first 10 slides." }.WithInput(InputKind.None);
        var scope = new Option<string>("--scope")
        {
            Description = "Projection scope: text, shapes or full.",
            DefaultValueFactory = _ => PresentationReadScopes.Shapes,
        }.WithInput(InputKind.None);
        scope.AcceptOnlyFromAmong([.. PresentationReadScopes.All]);
        var maxChars = new MaxCharactersOption(
            "Maximum returned title/text/run/notes/comment characters, including repeated projections.");
        var notes = new Option<bool>("--notes") { Description = "Include speaker notes for returned slides." };
        return StandardCommand.Create(
            host,
            "slides",
            "Read a bounded slide-content window.",
            new CommandTraits { Input = SlidesCommands.Presentation },
            [slides, scope, .. maxChars.Options, notes],
            (parse, standard) =>
            {
                int characters = maxChars.Read(parse);
                string? range = parse.GetValue(slides);
                string input = standard.Input;
                var request = new PresentationReadRequest
                {
                    Slides = range is null ? null : PageRange.Parse(range),
                    Scope = parse.GetValue(scope) ?? PresentationReadScopes.Shapes,
                    IncludeNotes = parse.GetValue(notes),
                    MaxCharacters = characters,
                    Password = standard.InputPassword,
                };
                PresentationReadResult result = standard.Port.Read(input, request);
                return result with { Next = Next(input, request, result) };
            });
    }

    /// <summary>
    /// The read that resumes where this one stopped, or null when it covered the selection.
    /// Without --slides the selection is every slide, read ten at a time.
    /// </summary>
    private static string? Next(string input, PresentationReadRequest request, PresentationReadResult result)
    {
        if (!result.Window.Truncated || result.Window.Of == 0)
        {
            return null;
        }

        IReadOnlyList<int> selection = request.Slides?.Resolve(result.Window.Of)
            ?? Enumerable.Range(1, result.Window.Of).ToArray();
        return ReadContinuation.After(
                selection,
                [.. result.Slides.Select(static slide => new ReadPart(slide.Number, slide.ContentTruncated))],
                request.MaxCharacters) is { } continuation
            ? new ContinuationCommand("slides", "query", "slides")
                .Argument(input)
                .Option("--slides", continuation.Parts)
                .Option("--scope", request.Scope)
                .Flag("--notes", request.IncludeNotes)
                .Option(MaxCharactersOption.Name, continuation.MaxCharacters)
                .ToString()
            : null;
    }
}
