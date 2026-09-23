using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.TestKit;
using Aspose.Cli.Sdk.Configuration;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Slides;
using Aspose.Slides.Export;

namespace Aspose.Cli.Product.Slides.Tests;

public sealed class SlidesEngineFixture : IDisposable
{
    public SlidesEngineFixture()
    {
        LicenseResolution resolution = LicenseResolver.Resolve(
            flagPath: null,
            productId: "slides",
            Environment.GetEnvironmentVariable,
            Directory.GetCurrentDirectory(),
            ConfigurationPaths.UserDirectory());
        Gate = new SlidesLicenseGate(resolution, Environment.GetEnvironmentVariable);
    }

    public ILicenseGate Gate { get; }
    internal SlidesPresentationEngine Engine =>
        ProductTestBudgets.StartEngine<SlidesModule, SlidesPresentationEngine>(
            (budgets, writer) => new SlidesPresentationEngine(Gate, budgets, writer));
    public LicenseState LicenseState => Gate.EnsureApplied();
    public TempDirectory Temp { get; } = new();
    public string File(string name) => Temp.File(name);

    public string CreatePresentation(string name = "deck.pptx", int slides = 3, string? password = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(slides, 1);
        Gate.EnsureApplied();
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

    public void Dispose() => Temp.Dispose();
}
