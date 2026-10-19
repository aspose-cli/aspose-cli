using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.TestKit;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Views;
using Aspose.Slides;
using Aspose.Slides.Export;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// Shared real-engine setup. The test license state is applied before any test authors a
/// presentation, so inputs are written in the state the engine reads them in.
/// </summary>
public sealed class SlidesEngineFixture : IDisposable
{
    public ILicenseGate Gate { get; } = TestLicense.Apply(SlidesActivation.ApplyLicense);
    /// <summary>A fresh session of one invocation, as the product activation creates it.</summary>
    internal SlidesSession Session =>
        ProductTestBudgets.StartEngine<SlidesModule, SlidesSession>(
            (budgets, writer) => SlidesActivation.Session(Outputs(writer), budgets));

    /// <summary>The write pipeline of one invocation, as the product binding creates it.</summary>
    internal OutputPipeline<Presentation> Outputs(SafeFileWriter writer) => new(Gate, new SlidesEvaluationProfile(), writer);

    /// <summary>
    /// Runs one handler as a command does: on its own session and write pipeline, whose
    /// evaluation disclosure the SDK adds to the result.
    /// </summary>
    internal TResult Disclosed<TResult>(Func<SlidesSession, TResult> call)
        where TResult : ResultEnvelope
    {
        SlidesSession session = Session;
        return (TResult)session.Outputs.Disclose(call(session));
    }

    public LicenseState LicenseState => Gate.EnsureApplied();

    /// <summary>
    /// Whether shape text is the watermark evaluation mode saves into every slide: a text box
    /// whose text the SDK itself cuts short with its truncation marker.
    /// </summary>
    internal static bool IsEvaluationWatermark(string? text) =>
        text?.StartsWith("Evalu...", StringComparison.Ordinal) == true
        && text.Contains(SlidesEvaluationMarks.EvaluationTruncationMarker, StringComparison.OrdinalIgnoreCase);

    /// <summary>A blank PNG <paramref name="size"/> pixels square; different sizes give different images.</summary>
    internal static byte[] Png(int size)
    {
        using var source = new Presentation();
        source.SlideSize.SetSize(size, size, SlideSizeScaleType.DoNotScale);
        using IImage image = source.Slides[0].GetImage(1f, 1f);
        using var stream = new MemoryStream();
        image.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    public TempDirectory Temp { get; } = new();
    public string File(string name) => Temp.File(name);

    public string CreatePresentation(string name = "deck.pptx", int slides = 3, string? password = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(slides, 1);
        string path = File(name);
        using var presentation = new Presentation();
        for (int index = 0; index < slides; index++)
        {
            ISlide slide = index == 0
                ? presentation.Slides[0]
                : presentation.Slides.AddEmptySlide(presentation.LayoutSlides[0]);
            IAutoShape title = slide.Shapes.AddAutoShape(ShapeType.Rectangle, 40, 30, 600, 70);
            title.Name = $"Title {index + 1}";
            title.TextFrame.Text = $"Slide {index + 1}";
            slide.Name = $"slide-{index + 1}";
            if (index == 0)
            {
                slide.NotesSlideManager.AddNotesSlide().NotesTextFrame!.Text = "Speaker note";
            }
        }

        if (password is not null)
        {
            presentation.ProtectionManager.Encrypt(password);
        }

        presentation.Save(path, SaveFormat.Pptx);
        return path;
    }

    /// <summary>
    /// Reviews a presentation through the view adapter, as the review command does: it renders
    /// every slide, then assesses the slides it rendered.
    /// </summary>
    internal ProductReviewAssessment Review(string path)
    {
        var adapter = new SlidesViewAdapter();
        var request = new ViewRenderRequest { View = SlidesViews.Slides, MaxPartCount = 100, Purpose = ViewPurpose.Evidence };
        SlidesSession session = Session;
        ViewManifest rendered = adapter.Render(session, path, request, new DiscardedArtifacts());
        return adapter.Assess(session, path, request, rendered);
    }

    /// <summary>A review's coverage metric, or null when the review does not report it.</summary>
    internal static long? Metric(ProductReviewAssessment review, string name) =>
        review.Coverage?.FirstOrDefault(metric => metric.Name == name)?.Value;

    private sealed class DiscardedArtifacts : IViewArtifactSink
    {
        public void Write(string relativePath, Action<Stream> contentWriter)
        {
            using var stream = new MemoryStream();
            contentWriter(stream);
        }

        public void WriteText(string relativePath, string content)
        {
        }
    }

    public void Dispose() => Temp.Dispose();
}
