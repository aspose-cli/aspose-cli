using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class ReadCommand
{
    public static CommandDefinition<SlidesReadRequest, SlidesReadResult> Create()
    {
        var slides = new PartRangeOption("slide", "the first 10 slides");
        var content = new Option<string>("--content")
        {
            Description = "Projection depth: how much of each slide to return.",
            DefaultValueFactory = _ => SlidesReadContents.Shapes,
        }.WithInput(InputKind.None);
        content.AcceptOnlyFromAmong([.. SlidesReadContents.All]);
        var maxChars = new MaxCharactersOption(
            "every returned title, text, run, note and comment, repeats included");
        var notes = new Option<bool>("--notes") { Description = "Include speaker notes for returned slides." };
        return new(
            "read",
            "Read a bounded slide-content window.",
            new CommandTraits { Input = SlidesInputs.Presentation },
            [.. slides.Options, content, .. maxChars.Options, notes],
            (parse, standard) =>
            {
                int characters = maxChars.Read(parse);
                return new SlidesReadRequest
                {
                    Input = standard.Input,
                    Slides = slides.ReadRange(parse),
                    Content = parse.GetValue(content) ?? SlidesReadContents.Shapes,
                    IncludeNotes = parse.GetValue(notes),
                    MaxCharacters = characters,
                    Password = standard.InputPassword,
                };
            },
            Table)
        {
            Examples =
            [
                "slides read deck.pptx --slides 1-5 --output-format json",
                "slides read deck.pptx --slides 2 --content full --notes --output-format json",
            ],
            Links =
            [
                CommandHelpLink.Docs(SlidesModule.Manifest, "verification", "slide read-back and visual review"),
                new("aspose-cli docs reading", "reading every product within bounded windows"),
            ],
            Finish = (_, request, result, standard) =>
                result with { Window = result.Window with { Next = Next(standard.Continuation(), slides.Name, request, result) } },
        };
    }

    internal static void Table(SlidesReadResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"{result.Input.Path}: {result.SlideCount} slide(s) ({result.Content})");
        foreach (SlidesSlideData slide in result.Slides)
        {
            surface.Out.WriteLine();
            surface.Out.WriteLine($"--- slide {slide.Slide} [{slide.SlideId}] {slide.Title ?? slide.Name ?? string.Empty} ---");
            foreach (string text in slide.Text ?? [])
            {
                surface.Out.WriteLine(text);
            }

            foreach (SlidesShapeData shape in slide.Shapes)
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
    private static string? Next(ContinuationCommand resume, string slidesOption, SlidesReadRequest request, SlidesReadResult result)
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
                .Option(slidesOption, continuation.Parts)
                .Option("--content", request.Content)
                .Flag("--notes", request.IncludeNotes)
                .Option(MaxCharactersOption.Name, continuation.MaxCharacters)
                .ToString()
            : null;
    }
}
