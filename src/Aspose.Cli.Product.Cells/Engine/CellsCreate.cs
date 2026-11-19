using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary><c>cells create</c>: the engine's new workbook, or a template saved as a new workbook.</summary>
internal static class CellsCreate
{
    internal static CellsCreateResult Run(CellsSession session, CellsCreateRequest request)
    {
        // The license is applied before the engine creates or opens anything.
        LicenseState state = session.Outputs.License;
        var log = new EngineWarningLog();
        // A template opens without a password: an encrypted one is refused.
        using LoadedWorkbook? template = request.Template is null ? null : session.Loader.Open(request.Template.Path, PasswordSource.None(), log);
        using Workbook? created = template is null ? new Workbook() : null;
        Workbook workbook = template?.Workbook ?? created!;
        OutputProtection protection = OutputProtection.For(request.Output.Format, request.EncryptPassword, new InputProtection(Encrypted: false), inputPassword: null);
        PublishedFile written = session.Outputs.Write(
            request.Output.Path,
            request.Output.Format,
            request.Output.Overwrite,
            workbook,
            temp =>
            {
                log.During = "save";
                CellsDocuments.Save(workbook, temp, request.Output.Format.Id, protection, log);
                template?.Resources.ThrowIfFailed();
            },
            encrypted: protection.Encrypted);
        return new CellsCreateResult
        {
            Output = FileObjects.Output(written, request.Output.Format, protection.Encrypted),
            Inputs = template is null ? [] : FileObjects.Sources((request.Template!, template.FormatId, template.Encrypted)),
            EngineWarnings = log.Warnings.Count == 0 ? null : log.Warnings,
            License = EnvelopeParts.License(state),
            Warnings = template is null || template.Warnings.Count == 0 ? null : template.Warnings,
        };
    }
}
