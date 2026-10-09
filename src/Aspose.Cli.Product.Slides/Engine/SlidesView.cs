using System.Globalization;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Views;
using Aspose.Slides;
using static Aspose.Cli.Product.Slides.Engine.SlidesEngineSupport;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>Renders the slides of the product view.</summary>
internal static class SlidesView
{
    /// <summary>Renders the slides of one view, opening the presentation once.</summary>
    internal static ViewManifest Render(
        SlidesSession session,
        string filePath,
        ViewRenderRequest request,
        IViewArtifactSink artifacts)
    {
        const int evidenceWidth = 1600;
        const int displayWidth = 1920;
        const int cssWidth = 960;
        ArgumentNullException.ThrowIfNull(artifacts);
        LicenseState state = session.Outputs.License;
        using LoadedPresentation loaded = session.Loader.Open(filePath, request.Password);
        Presentation presentation = loaded.Presentation;
        int pixelWidth = request.Purpose == ViewPurpose.Display ? displayWidth : evidenceWidth;
        float scale = (float)(pixelWidth / presentation.SlideSize.Size.Width);
        int total = presentation.Slides.Count;
        int count = Math.Min(total, request.MaxPartCount);
        EnsureRasterFits(
            session.Budgets,
            (long)Math.Ceiling(presentation.SlideSize.Size.Width * scale),
            (long)Math.Ceiling(presentation.SlideSize.Size.Height * scale),
            dpi: null);
        double cssPerPoint = cssWidth / presentation.SlideSize.Size.Width;
        int cssHeight = Math.Max(1, (int)Math.Round(
            presentation.SlideSize.Size.Height * cssPerPoint,
            MidpointRounding.AwayFromZero));
        var parts = new List<ViewPart>(count);
        for (int index = 0; index < count; index++)
        {
            ISlide slide = presentation.Slides[index];
            int number = index + 1;
            string file = string.Create(CultureInfo.InvariantCulture, $"slide-{number:0000}.png");
            using (IImage image = slide.GetImage(scale, scale))
            {
                artifacts.Write(file, stream => image.Save(stream, ImageFormat.Png));
            }
            string? notes = Notes(slide);
            parts.Add(new ViewPart
            {
                Id = SlidesViews.PartId(slide.SlideId),
                Label = Title(slide) ?? string.Create(CultureInfo.InvariantCulture, $"Slide {number}"),
                File = file,
                Kind = ViewPartKinds.Image,
                Width = cssWidth,
                Height = cssHeight,
                Hidden = slide.Hidden,
                Elements = SlidesViewLayout.Elements(slide, cssPerPoint),
                Properties = notes is null
                    ? null
                    : new Dictionary<string, string>(StringComparer.Ordinal) { ["notes"] = notes },
            });
        }

        return new ViewManifest
        {
            View = SlidesViews.Slides,
            SourceFormat = loaded.FormatId,
            SourceSizeBytes = new FileInfo(filePath).Length,
            SourceEncrypted = presentation.ProtectionManager.IsEncrypted,
            TotalPartCount = total,
            Parts = parts,
            Warnings = InputWarnings(state, loaded, textRead: true),
        };
    }
}
