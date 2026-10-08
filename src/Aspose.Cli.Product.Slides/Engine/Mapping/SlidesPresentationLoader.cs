using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Slides;

namespace Aspose.Cli.Product.Slides.Engine.Mapping;

internal sealed class SlidesPresentationLoader(
    ResourceBudgetLedger resourceBudgets)
{
    /// <summary>
    /// How a presentation input that does not load is reported: Aspose.Slides raises
    /// <see cref="InvalidPasswordException"/> for a missing or wrong password, and its own
    /// exceptions, <see cref="ArgumentException"/> or <see cref="InvalidOperationException"/>
    /// for bytes it cannot read.
    /// </summary>
    internal static readonly InputLoading Loading = new(
        "supported presentation",
        $"Use one of: {string.Join(", ", SlidesFormats.LoadIds)}. Verify the file opens in a presentation editor and is not merely renamed.",
        static exception => exception switch
        {
            InvalidPasswordException => LoadFailureKind.Password,
            ArgumentException or InvalidOperationException => LoadFailureKind.Corrupt,
            _ when exception.GetType().Assembly.GetName().Name?.StartsWith("Aspose.Slides", StringComparison.Ordinal) == true
                => LoadFailureKind.Corrupt,
            _ => LoadFailureKind.Other,
        });

    public LoadedPresentation Open(string path, Secret? password)
    {
        InputSizeGuard.Ensure(resourceBudgets, path);
        return OpenCore(path, password);
    }

    // Generated candidates are bounded by publication, not a second user-input admission.
    internal LoadedPresentation OpenPublishedCandidate(string path, Secret? password) => OpenCore(path, password);

    /// <summary>
    /// Opens the built-in 16:9 design that new presentations use without a template. It has no
    /// source directory, so it loads under a policy that denies every external resource.
    /// </summary>
    internal static LoadedPresentation OpenDefaultTemplate()
    {
        using Stream stream = typeof(SlidesPresentationLoader).Assembly.GetManifestResourceStream(DefaultTemplateResource)
            ?? throw new InvalidOperationException($"The built-in resource {DefaultTemplateResource} is missing.");
        var resources = SlidesResourcePolicy.DenyAll();
        var presentation = new Presentation(stream, new LoadOptions { ResourceLoadingCallback = resources });
        SlidesCjkFallback.Apply(presentation);
        return new LoadedPresentation(presentation, "pptx", resources);
    }

    private const string DefaultTemplateResource = "Templates/default-16x9.pptx";

    private LoadedPresentation OpenCore(string path, Secret? secret)
    {
        string? password = secret?.Reveal();
        FileStream? source = null;
        try
        {
            // One read that lets another process replace the file, as an in-place edit publishes;
            // the presentation may keep reading media from it while it is open.
            source = InputFiles.OpenRead(path);
            IPresentationInfo info = PresentationFactory.Instance.GetPresentationInfo(source);
            source.Position = 0;
            string format = FormatId(info.LoadFormat);
            if (!SlidesFormats.LoadIds.Contains(format, StringComparer.Ordinal))
            {
                throw Loading.Unloadable(path, info.LoadFormat.ToString());
            }

            if (info.IsPasswordProtected && (password is null || !info.CheckPassword(password)))
            {
                throw InputLoading.PasswordRefused(path, secret);
            }

            // Engine code creates presentations only here or from the default template: without a
            // resource policy, rendering or saving fetches linked media from any address.
            var resources = SlidesResourcePolicy.Beside(path, resourceBudgets);
            Presentation presentation;
            try
            {
                presentation = new Presentation(source, new LoadOptions { Password = password, ResourceLoadingCallback = resources });
            }
            catch
            {
                resources.Dispose();
                throw;
            }
            try
            {
                resourceBudgets.EnsureWithin(
                    SlidesBudgetDomains.Slides,
                    presentation.Slides.Count,
                    "items",
                    "post-load");
                resourceBudgets.EnsureWithin(
                    SlidesBudgetDomains.Shapes,
                    presentation.Slides.Sum(
                        static slide => (long)slide.Shapes.Count),
                    "items",
                    "projection");
            }
            catch
            {
                presentation.Dispose();
                resources.Dispose();
                throw;
            }
            SlidesCjkFallback.Apply(presentation);
            var loaded = new LoadedPresentation(presentation, format, resources)
            {
                ImplicitTitleCharts = ImplicitTitleCharts(presentation),
                Source = source,
            };
            source = null;
            return loaded;
        }
        catch (Exception exception) when (exception is not CliException and not OperationCanceledException)
        {
            throw Loading.Failure(exception, path, secret);
        }
        finally
        {
            source?.Dispose();
        }
    }

    /// <summary>
    /// The charts, as loaded, whose implicit automatic title the engine turns into one drawn over
    /// the plot (known issue SLIDES-CHART-TITLE in KNOWN-ISSUES.md): it reports a title that
    /// overlays the plot and has no text of its own.
    /// </summary>
    private static IReadOnlyList<(int Slide, string Chart)> ImplicitTitleCharts(Presentation presentation)
    {
        var charts = new List<(int Slide, string Chart)>();
        for (int index = 0; index < presentation.Slides.Count; index++)
        {
            Collect(presentation.Slides[index].Shapes, index + 1);
        }

        return charts;

        void Collect(IShapeCollection shapes, int slide)
        {
            foreach (IShape shape in shapes)
            {
                if (shape is IGroupShape group)
                {
                    Collect(group.Shapes, slide);
                }
                else if (shape is Aspose.Slides.Charts.IChart { HasTitle: true } chart
                    && chart.ChartTitle.Overlay
                    && chart.ChartTitle.TextFrameForOverriding is null)
                {
                    charts.Add((slide, chart.Name));
                }
            }
        }
    }

    private static string FormatId(LoadFormat format) => format switch
    {
        LoadFormat.Ppt => "ppt",
        LoadFormat.Pps => "pps",
        LoadFormat.Pptx => "pptx",
        LoadFormat.Ppsx => "ppsx",
        LoadFormat.Odp => "odp",
        LoadFormat.Potx => "potx",
        LoadFormat.Pptm => "pptm",
        LoadFormat.Ppsm => "ppsm",
        LoadFormat.Potm => "potm",
        LoadFormat.Otp => "otp",
        LoadFormat.Ppt95 => "ppt",
        LoadFormat.Pot => "pot",
        LoadFormat.Fodp => "fodp",
        _ => "unknown",
    };
}

internal sealed record LoadedPresentation(Presentation Presentation, string FormatId, SlidesResourcePolicy Resources)
    : IDisposable
{
    /// <summary>The charts whose implicit automatic title an output draws over the plot.</summary>
    public IReadOnlyList<(int Slide, string Chart)> ImplicitTitleCharts { get; init; } = [];

    /// <summary>The input file the presentation was read from, held until it is disposed.</summary>
    public Stream? Source { get; init; }

    public void Dispose()
    {
        try { Presentation.Dispose(); }
        finally
        {
            Resources.Dispose();
            Source?.Dispose();
        }
    }
}
