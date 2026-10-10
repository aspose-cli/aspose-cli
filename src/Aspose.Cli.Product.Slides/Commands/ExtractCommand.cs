using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class ExtractCommand
{
    public static CommandDefinition<PresentationExtractRequest, SlidesExtractResult> Create()
    {
        var what = new Option<string>("--what") { Required = true, Description = "media, notes or text." }.WithInput(InputKind.None);
        what.AcceptOnlyFromAmong([.. PresentationExtractKinds.All]);
        var slides = new PartRangeOption("slide", "every slide");
        return new(
            "extract",
            "Extract bounded presentation media, notes or text.",
            new CommandTraits
            {
                Input = SlidesInputs.Presentation,
                Output = OutputTarget.Directory("Safe extraction directory."),
            },
            [what, .. slides.Options],
            (parse, standard) =>
            {
                return new PresentationExtractRequest
                {
                    Input = standard.Input,
                    What = parse.GetRequiredValue(what),
                    Output = standard.DirectoryOutput,
                    Slides = slides.ReadRange(parse),
                    Password = standard.InputPassword,
                };
            },
            Table);
    }

    internal static void Table(SlidesExtractResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"extracted {result.Items.Count} {result.What} item(s)");
        foreach (SlidesExtractedItem item in result.Items)
        {
            surface.Out.WriteLine($"{item.Path} ({item.Kind}, {TableText.Bytes(item.SizeBytes)})");
        }
    }
}
