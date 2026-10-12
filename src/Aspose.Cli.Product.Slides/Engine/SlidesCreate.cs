using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Slides;
using static Aspose.Cli.Product.Slides.Engine.SlidesEngineSupport;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>Creates a blank, template-based or Markdown-authored presentation.</summary>
internal static class SlidesCreate
{
    internal static SlidesCreateResult Run(SlidesSession session, SlidesCreateRequest request)
    {
        LicenseState state = session.Outputs.License;
        string format = request.Output.Format.Id;

        using LoadedPresentation template = request.TemplatePath is null
            ? SlidesPresentationLoader.OpenDefaultTemplate()
            : session.Loader.Open(request.TemplatePath, password: null);
        Presentation presentation = template.Presentation;
        ApplySlideSize(presentation, request.Size);
        IReadOnlyList<Warning> authoring = [];
        if (request.MarkdownPath is not null)
        {
            authoring = SlidesMarkdownBuilder.Build(
                session.Budgets,
                presentation,
                request.MarkdownPath);
        }
        else if (presentation.Slides.Count == 0)
        {
            // A design template may hold only masters and layouts; a presentation needs a slide.
            presentation.Slides.AddEmptySlide(SlidesPlaceholders.Layout(presentation, SlideLayoutType.Title));
        }

        Encrypt(presentation, request.EncryptPassword);
        PublishedFile written = session.Outputs.Write(
            request.Output.Path,
            request.Output.Overwrite,
            presentation,
            temp =>
            {
                presentation.Save(temp, SlidesEngineFormats.SaveFormatOf(format));
                template.Resources.ThrowIfFailed();
            },
            encrypted: presentation.ProtectionManager.IsEncrypted);
        return new SlidesCreateResult
        {
            Output = FileObjects.Output(written, request.Output.Format, presentation.ProtectionManager.IsEncrypted),
            SlideCount = presentation.Slides.Count,
            Template = request.TemplatePath is null
                ? null
                : Source(request.TemplatePath, template),
            Markdown = request.MarkdownPath is null
                ? null
                : FileObjects.Input(request.MarkdownPath, "md", encrypted: false),
            License = EnvelopeParts.License(state),
            Warnings = authoring.Count == 0
                ? WrittenWarnings(template)
                : [.. WrittenWarnings(template) ?? [], .. authoring],
        };
    }
}
