using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Tables;
using Aspose.Words;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary><c>words create</c>: the engine's new document, or a template saved as a new document.</summary>
internal static class WordsCreate
{
    internal static WordsCreateResult Run(WordsSession session, WordsCreateRequest request)
    {
        // The license is applied before the engine creates or opens anything.
        LicenseState state = session.Outputs.License;
        var log = new EngineWarningLog();
        // A template opens without a password: an encrypted one is refused.
        using LoadedDocument? template = request.Template is null ? null : session.Loader.Open(request.Template.Path, PasswordSource.None(), log);
        Document document = template?.Document ?? new Document();
        OutputProtection protection = OutputProtection.For(request.Output.Format, request.EncryptPassword, new InputProtection(Encrypted: false), inputPassword: null);
        PublishedFile written = session.Outputs.Write(request.Output.Path, request.Output.Format, request.Output.Overwrite, temp =>
        {
            log.During = "save";
            WordsEngineFormats.Save(document, temp, request.Output.Format.Id, protection);
            template?.Resources.ThrowIfFailed();
        }, encrypted: protection.Encrypted);
        return new WordsCreateResult
        {
            Output = FileObjects.Output(written, request.Output.Format, protection.Encrypted),
            Inputs = template is null ? [] : FileObjects.Sources((request.Template!, template.FormatId, template.Protection.Encrypted)),
            EngineWarnings = log.Warnings.Count == 0 ? null : log.Warnings,
            License = EnvelopeParts.License(state),
            Warnings = template is null || template.Warnings.Count == 0 ? null : template.Warnings,
        };
    }
}
