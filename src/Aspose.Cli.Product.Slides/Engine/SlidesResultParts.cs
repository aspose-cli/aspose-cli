using System.Globalization;
using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>
/// The parts every handler's result takes from the loaded presentation: its input and the
/// warnings about what a result read from it or written from it may be missing.
/// </summary>
internal static class SlidesResultParts
{
    /// <summary>
    /// Warnings about what a result read from the loaded presentation may be missing. A result
    /// that reads the presentation's text tells the write pipeline
    /// (<c>ReadInput</c>), which discloses the text evaluation mode cut short.
    /// </summary>
    internal static IReadOnlyList<Warning>? InputWarnings(LoadedPresentation loaded) =>
        Warnings(loaded, output: false);

    /// <summary>
    /// The warnings of an output produced from the presentation; the write pipeline adds the
    /// evaluation disclosure. Evaluation mode cuts text short only where it is read: the
    /// pipeline finds it in a Markdown output (<see cref="SlidesEvaluationProfile"/>), and an
    /// extraction of text tells the pipeline it read the presentation; saved presentations,
    /// PDFs and images keep the full text.
    /// </summary>
    internal static IReadOnlyList<Warning>? WrittenWarnings(LoadedPresentation loaded) =>
        Warnings(loaded, output: true);

    private static IReadOnlyList<Warning>? Warnings(LoadedPresentation loaded, bool output)
    {
        var warnings = new List<Warning>();
        if (loaded.Resources.Warning is { } omitted)
        {
            warnings.Add(omitted);
        }

        if (output)
        {
            foreach ((int slide, string chart) in loaded.ImplicitTitleCharts)
            {
                warnings.Add(new Warning(SlidesDiagnostics.ChartTitleOverlaid, $"The chart '{chart}' on slide {slide} has an implicit automatic title, which this output draws over the plot, enlarging the plot area.")
                {
                    Hint = "Inspect the chart in PowerPoint and restore its title layout there if it changed.",
                    Location = string.Create(CultureInfo.InvariantCulture, $"slide {slide}"),
                    AffectsCompleteness = true,
                });
            }
        }

        return warnings.Count == 0 ? null : warnings;
    }

    internal static InputInfo Source(string path, LoadedPresentation loaded) =>
        FileObjects.Input(path, loaded.FormatId, loaded.Encrypted);
}
