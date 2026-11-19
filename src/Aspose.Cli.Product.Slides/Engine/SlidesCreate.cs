using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Tables;
using Aspose.Slides;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary><c>slides create</c>: the engine's new presentation, or a template saved as a new presentation.</summary>
internal static class SlidesCreate
{
    internal static SlidesCreateResult Run(SlidesSession session, SlidesCreateRequest request)
    {
        LicenseState state = session.Outputs.License;
        var log = new EngineWarningLog();
        // A template opens without a password: an encrypted one is refused.
        using LoadedPresentation? template = request.Template is null ? null : session.Loader.Open(request.Template.Path, PasswordSource.None(), log);
        using Presentation? created = template is null ? new Presentation() : null;
        Presentation presentation = template?.Presentation ?? created!;
        OutputProtection protection = OutputProtection.For(request.Output.Format, request.EncryptPassword, new InputProtection(Encrypted: false), inputPassword: null);
        SlidesOutputProtection.Protect(presentation, protection);
        PublishedFile written = session.Outputs.Write(
            request.Output.Path,
            request.Output.Format,
            request.Output.Overwrite,
            presentation,
            temp =>
            {
                log.During = "save";
                SlidesExport.Save(presentation, temp, SlidesEngineFormats.SaveFormatOf(request.Output.Format.Id), log);
                template?.Resources.ThrowIfFailed();
            },
            encrypted: protection.Encrypted);
        return new SlidesCreateResult
        {
            Output = FileObjects.Output(written, request.Output.Format, protection.Encrypted),
            SlideCount = presentation.Slides.Count,
            Inputs = template is null ? [] : FileObjects.Sources((request.Template, template.FormatId, template.Encrypted)),
            EngineWarnings = log.Warnings.Count == 0 ? null : log.Warnings,
            License = EnvelopeParts.License(state),
            Warnings = template is null ? null : SlidesResultParts.InputWarnings(template),
        };
    }
}
