using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Slides;
using static Aspose.Cli.Product.Slides.Engine.SlidesEngineSupport;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>Creates a blank, template-based or Markdown-authored presentation.</summary>
internal static class SlidesCreate
{
    /// <summary>
    /// A template, which opens without a password: an encrypted one is refused with that advice.
    /// New slides go on its layouts, so it opens to be written (SLIDES-PPT-TEMPLATE-LAYOUTS).
    /// </summary>
    private static LoadedPresentation OpenTemplate(SlidesSession session, string path)
    {
        try
        {
            return session.Loader.OpenToWrite(path, password: null);
        }
        catch (CliException error) when (CliErrors.IsPasswordError(error))
        {
            throw CliErrors.WithoutPassword(error,
                "'slides create' cannot open an encrypted template. Ask the user for an unencrypted copy of the template, "
                + "then pass that copy to --template.");
        }
    }

    internal static SlidesCreateResult Run(SlidesSession session, SlidesCreateRequest request)
    {
        LicenseState state = session.Outputs.License;
        string format = request.Output.Format.Id;

        using LoadedPresentation template = request.Template is null
            ? SlidesPresentationLoader.OpenDefaultTemplate()
            : OpenTemplate(session, request.Template.Path);
        Presentation presentation = template.Presentation;
        ApplySlideSize(presentation, request.SlideSize);
        IReadOnlyList<Warning> authoring = [];
        if (request.Markdown is not null)
        {
            authoring = SlidesMarkdownBuilder.Build(
                session.Budgets,
                presentation,
                request.Markdown.Path);
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
            Inputs = FileObjects.Sources(
                (request.Template, template.FormatId, template.Encrypted),
                (request.Markdown, "md", false)),
            License = EnvelopeParts.License(state),
            Warnings = authoring.Count == 0
                ? WrittenWarnings(template)
                : [.. WrittenWarnings(template) ?? [], .. authoring],
        };
    }
}
