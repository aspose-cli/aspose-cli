using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

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
            "every returned title, text, run, paragraph, table cell, chart label, footer, note and comment, repeats included");
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
            })
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
            Finish = (_, request, result, _) =>
                result with { Window = result.Window with { Next = Next(slides.Name, request, result) } },
        };
    }

    /// <summary>
    /// The option values of the read that resumes where this one stopped, or null when it covered
    /// the selection. Without --slides the selection is every slide, read ten at a time.
    /// </summary>
    private static IReadOnlyDictionary<string, string>? Next(string slidesOption, SlidesReadRequest request, SlidesReadResult result)
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
                request.MaxCharacters)?.Next(slidesOption);
    }
}
