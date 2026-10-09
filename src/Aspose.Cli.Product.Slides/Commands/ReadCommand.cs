using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class ReadCommand
{
    public static CommandDefinition<PresentationReadRequest, PresentationReadResult> Create()
    {
        var slides = new Option<string?>("--slides") { Description = "1-based slide range, e.g. 1-3,7,9-. Default: the first 10 slides." }.WithInput(InputKind.None);
        var scope = new Option<string>("--scope")
        {
            Description = "Projection scope: text, shapes or full.",
            DefaultValueFactory = _ => PresentationReadScopes.Shapes,
        }.WithInput(InputKind.None);
        scope.AcceptOnlyFromAmong([.. PresentationReadScopes.All]);
        var maxChars = new MaxCharactersOption(
            "title, text, run, notes and comment characters, including repeated projections");
        var notes = new Option<bool>("--notes") { Description = "Include speaker notes for returned slides." };
        return new(
            "slides",
            "Read a bounded slide-content window.",
            new CommandTraits { Input = SlidesInputs.Presentation },
            [slides, scope, .. maxChars.Options, notes],
            (parse, standard) =>
            {
                int characters = maxChars.Read(parse);
                string? range = parse.GetValue(slides);
                return new PresentationReadRequest
                {
                    Input = standard.Input,
                    Slides = range is null ? null : PageRange.Parse(range),
                    Scope = parse.GetValue(scope) ?? PresentationReadScopes.Shapes,
                    IncludeNotes = parse.GetValue(notes),
                    MaxCharacters = characters,
                    Password = standard.InputPassword,
                };
            },
            Table)
        {
            Finish = static (_, request, result, standard) =>
                result with { Window = result.Window with { Next = Next(standard.Continuation(), request, result) } },
        };
    }

    internal static void Table(PresentationReadResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"{result.Source.Path}: {result.SlideCount} slide(s) ({result.Scope})");
        foreach (SlideData slide in result.Slides)
        {
            surface.Out.WriteLine();
            surface.Out.WriteLine($"--- slide {slide.Slide} [{slide.SlideId}] {slide.Title ?? slide.Name ?? string.Empty} ---");
            foreach (string text in slide.Text ?? [])
            {
                surface.Out.WriteLine(text);
            }

            foreach (SlideShapeData shape in slide.Shapes)
            {
                if (shape.Text is not null)
                {
                    surface.Out.WriteLine(shape.Text);
                }
            }

            if (slide.Notes is not null)
            {
                surface.Out.WriteLine($"notes: {slide.Notes}");
            }
        }
    }

    /// <summary>
    /// The read that resumes where this one stopped, or null when it covered the selection.
    /// Without --slides the selection is every slide, read ten at a time.
    /// </summary>
    private static string? Next(ContinuationCommand resume, PresentationReadRequest request, PresentationReadResult result)
    {
        if (!result.Window.Truncated || result.SlideCount == 0)
        {
            return null;
        }

        IReadOnlyList<int> selection = request.Slides?.Resolve(result.SlideCount)
            ?? Enumerable.Range(1, result.SlideCount).ToArray();
        return ReadContinuation.After(
                selection,
                [.. result.Slides.Select(static slide => new ReadPart(slide.Slide, slide.ContentTruncated))],
                request.MaxCharacters) is { } continuation
            ? resume
                .Option("--slides", continuation.Parts)
                .Option("--scope", request.Scope)
                .Flag("--notes", request.IncludeNotes)
                .Option(MaxCharactersOption.Name, continuation.MaxCharacters)
                .ToString()
            : null;
    }
}
