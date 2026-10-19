using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Files;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.Results;
using Aspose.Slides;
using Aspose.Slides.Export;

namespace Aspose.Cli.Product.Slides.Engine.Mapping;

internal sealed class SlidesPresentationLoader
{
    private readonly ResourceBudgetLedger _resourceBudgets;
    private readonly OutputPipeline? _outputs;

    /// <param name="resourceBudgets">The invocation's resource budgets.</param>
    /// <param name="outputs">The write pipeline, which learns whether each opened input is encrypted.</param>
    internal SlidesPresentationLoader(ResourceBudgetLedger resourceBudgets, OutputPipeline outputs)
        : this(resourceBudgets)
    {
        _outputs = outputs ?? throw new ArgumentNullException(nameof(outputs));
    }

    private SlidesPresentationLoader(ResourceBudgetLedger resourceBudgets)
    {
        _resourceBudgets = resourceBudgets ?? throw new ArgumentNullException(nameof(resourceBudgets));
    }

    /// <summary>A loader for font inspection, which publishes nothing.</summary>
    internal static SlidesPresentationLoader ForFontInspection(ResourceBudgetLedger resourceBudgets) => new(resourceBudgets);

    /// <summary>
    /// How a presentation input that does not load is reported: Aspose.Slides raises
    /// <see cref="InvalidPasswordException"/> for a missing or wrong password, and its own
    /// exceptions, <see cref="ArgumentException"/> or <see cref="InvalidOperationException"/>
    /// for bytes it cannot read.
    /// </summary>
    internal static readonly InputLoading Loading = new(
        "supported presentation",
        "Check that the file is complete, not truncated by an interrupted copy or download, and opens in a presentation editor. "
            + $"A file merely renamed to a presentation extension ({string.Join(", ", SlidesFormats.LoadIds)}) does not load either.",
        Classify);

    private static LoadFailureKind Classify(Exception exception) => exception switch
    {
        InvalidPasswordException => LoadFailureKind.Password,
        ArgumentException or InvalidOperationException => LoadFailureKind.Corrupt,
        _ when exception.GetType().Assembly.GetName().Name?.StartsWith("Aspose.Slides", StringComparison.Ordinal) == true
            => LoadFailureKind.Corrupt,
        _ => LoadFailureKind.Other,
    };

    public LoadedPresentation Open(string path, PasswordSource password) =>
        InputOpening.Open(_outputs, _resourceBudgets, path, () => OpenCore(path, password));

    /// <summary>Opens a user input with the password from the command's <c>--password-env</c>, if any.</summary>
    public LoadedPresentation Open(string path, Secret? password) => Open(path, PasswordSource.Option(password));

    /// <summary>
    /// Opens a presentation that new slides are added to on its layouts. A PowerPoint 97-2003
    /// binary file (PPT, PPS or POT) is first saved as PPTX in memory and reopened
    /// (SLIDES-PPT-TEMPLATE-LAYOUTS): the engine lists layouts from such a file that a PPTX save
    /// cannot reference, and that save drops them, as <c>slides convert</c> to PPTX does.
    /// </summary>
    public LoadedPresentation OpenToWrite(string path, PasswordSource password) =>
        InputOpening.Open(_outputs, _resourceBudgets, path, () => OpenCore(path, password, throughPptx: true));

    /// <summary>Opens a presentation to write, with the password from the command's <c>--password-env</c>, if any.</summary>
    public LoadedPresentation OpenToWrite(string path, Secret? password) => OpenToWrite(path, PasswordSource.Option(password));

    // Generated candidates are bounded by publication, not a second user-input admission.
    internal LoadedPresentation OpenPublishedCandidate(string path, Secret? password) =>
        InputOpening.OpenPublished(() => OpenCore(path, PasswordSource.Option(password)));

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

    private LoadedPresentation OpenCore(string path, PasswordSource passwordSource, bool throughPptx = false)
    {
        string? password = passwordSource.Password?.Reveal();
        FileStream? source = null;
        try
        {
            // One read that lets another process replace the file, as an in-place edit publishes;
            // the presentation may keep reading media from it while it is open.
            source = InputFiles.OpenRead(path);
            IPresentationInfo info;
            try
            {
                info = PresentationFactory.Instance.GetPresentationInfo(source);
            }
            catch (Exception exception) when (Classify(exception) == LoadFailureKind.Corrupt)
            {
                // The engine cannot tell the format of what it read, which a package cut short
                // or damaged gives: say so rather than repeat its "Unknown file format".
                throw Loading.Unreadable(path, "its content is incomplete or damaged, so no presentation format can be read from it", exception);
            }

            // Content the engine recognizes as no format at all, such as text or random bytes,
            // is named so rather than by the engine's "Unknown".
            if (info.LoadFormat == LoadFormat.Unknown)
            {
                throw Loading.Unreadable(path, "its content is not in any supported presentation format");
            }

            source.Position = 0;
            string format = SlidesEngineFormats.IdOf(info.LoadFormat);
            if (!SlidesFormats.LoadIds.Contains(format, StringComparer.Ordinal))
            {
                throw Loading.Unloadable(path, info.LoadFormat.ToString());
            }

            if (info.IsPasswordProtected && (password is null || !info.CheckPassword(password)))
            {
                throw InputLoading.PasswordRefused(path, passwordSource);
            }

            // Engine code creates presentations only here or from the default template: without a
            // resource policy, rendering or saving fetches linked media from any address.
            var resources = SlidesResourcePolicy.Beside(path, _resourceBudgets);
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
                _resourceBudgets.EnsureWithin(
                    SlidesBudgetDomains.Slides,
                    presentation.Slides.Count,
                    "items",
                    "post-load");
                _resourceBudgets.EnsureWithin(
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
            bool encrypted = presentation.ProtectionManager.IsEncrypted;
            Stream held = source;
            if (throughPptx && SlidesEngineFormats.IsBinaryPpt(format))
            {
                (presentation, held) = ThroughPptx(presentation, password, resources);
            }
            else
            {
                source = null;
            }

            SlidesCjkFallback.Apply(presentation);
            return new LoadedPresentation(presentation, format, resources)
            {
                Encrypted = encrypted,
                ImplicitTitleCharts = ImplicitTitleCharts(presentation),
                Source = held,
            };
        }
        catch (Exception exception) when (exception is not CliException and not OperationCanceledException)
        {
            throw Loading.Failure(exception, path, passwordSource);
        }
        finally
        {
            source?.Dispose();
        }
    }

    /// <summary>
    /// The presentation saved as PPTX in memory and reopened from it (SLIDES-PPT-TEMPLATE-LAYOUTS),
    /// with the stream it reads from. An evaluation save adds a watermark box to every slide; the
    /// boxes beyond those the slide had are removed, so the copy holds what the file holds.
    /// </summary>
    private static (Presentation Presentation, Stream Source) ThroughPptx(
        Presentation presentation,
        string? password,
        SlidesResourcePolicy resources)
    {
        var pptx = new MemoryStream();
        try
        {
            int[] watermarks;
            using (presentation)
            {
                watermarks = [.. presentation.Slides.Select(static slide => slide.Shapes.Count(SlidesEvaluationMarks.IsEvaluationWatermark))];
                presentation.Save(pptx, SaveFormat.Pptx);
            }

            pptx.Position = 0;
            var reopened = new Presentation(pptx, new LoadOptions { Password = password, ResourceLoadingCallback = resources });
            try
            {
                for (int index = 0; index < reopened.Slides.Count; index++)
                {
                    IShapeCollection shapes = reopened.Slides[index].Shapes;
                    foreach (IShape added in shapes.Where(SlidesEvaluationMarks.IsEvaluationWatermark).Skip(watermarks[index]).ToArray())
                    {
                        shapes.Remove(added);
                    }
                }
            }
            catch
            {
                reopened.Dispose();
                throw;
            }

            return (reopened, pptx);
        }
        catch
        {
            pptx.Dispose();
            resources.Dispose();
            throw;
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
}

internal sealed record LoadedPresentation(Presentation Presentation, string FormatId, SlidesResourcePolicy Resources)
    : IOpenedInput, IDisposable
{
    /// <summary>Whether the file is encrypted, as it was opened; a later encryption does not change it.</summary>
    public bool Encrypted { get; init; } = Presentation.ProtectionManager.IsEncrypted;

    /// <inheritdoc />
    public InputProtection Protection => new(Encrypted);

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
