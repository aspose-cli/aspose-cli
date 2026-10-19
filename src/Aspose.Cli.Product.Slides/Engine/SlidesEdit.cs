using Aspose.Cli.Product.Slides.Engine.Editing;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Slides;
using static Aspose.Cli.Product.Slides.Engine.SlidesResultParts;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>
/// Applies a validated, atomic operation batch through the SDK runner, publishes the output and
/// verifies the effect of each operation: <c>slides edit</c>.
/// </summary>
internal static class SlidesEdit
{
    internal static SlidesEditResult Run(SlidesSession session, SlidesEditRequest request)
    {
        string filePath = request.Input;
        SlidesOpsBatch batch = SlidesOp.Catalog.Prepare(request.Batch);
        string format = request.Output.Format.Id;

        LicenseState state = session.Outputs.License;
        FileWritePrecondition precondition = FileWritePrecondition.Capture(filePath);
        using LoadedPresentation loaded = session.Loader.OpenToWrite(filePath, request.Password);
        InputInfo input = precondition.Admit(loaded.FormatId, loaded.Protection, request.Options.IfMatch);
        Presentation presentation = loaded.Presentation;
        bool evaluation = state == LicenseState.Evaluation;
        var model = new SlidesEditModel(
            session.Budgets.Inputs,
            session.Loader,
            presentation,
            evaluation,
            request.OpSecrets,
            () => session.Outputs.ReadInput(presentation));
        SlidesEditVerifier? verifier = request.Verify ? new SlidesEditVerifier(presentation, model.Target, evaluation) : null;
        AppliedBatch applied = BoundedOperationRunner.Run(
            SlidesOp.Catalog, batch.Ops, request.Options.BestEffort, deadline: null, model, verifier, SlidesStateProjection.SlideUnit);
        EditPublication<SlidesEditVerification> publication = Publish(session, loaded, request, format, precondition, verifier, applied);
        IReadOnlyList<Warning> warnings = [.. model.Warnings, .. applied.Warnings];

        return new SlidesEditResult
        {
            Input = input,
            Output = publication.Output,
            DryRun = request.Options.DryRun,
            Checked = request.Options.Checked,
            Applied = applied.Outcomes,
            Backup = publication.Backup,
            SlidesTouched = applied.Pages.Count == 0 ? null : [.. applied.Pages],
            Verification = publication.Verification,
            License = EnvelopeParts.License(state),
            Warnings = request.Options.DryRun
                ? EnvelopeParts.CombineWarnings(InputWarnings(loaded), warnings)
                : EnvelopeParts.CombineWarnings(WrittenWarnings(loaded), warnings, publication.BackupWarnings),
        };
    }

    private static EditPublication<SlidesEditVerification> Publish(
        SlidesSession session,
        LoadedPresentation loaded,
        SlidesEditRequest request,
        string format,
        FileWritePrecondition precondition,
        SlidesEditVerifier? verifier,
        AppliedBatch applied)
    {
        Presentation presentation = loaded.Presentation;
        OutputProtection protection = OutputProtection.For(request.Output.Format, request.EncryptPassword, loaded.Protection, request.Password);
        if (!request.Options.DryRun)
        {
            SlidesOutputProtection.Protect(presentation, protection);
        }

        return session.Outputs.PublishEdit<SlidesEditVerification>(
            request.Output,
            presentation,
            precondition,
            request.Options.DryRun,
            protection.Encrypted,
            temp =>
            {
                presentation.Save(temp, SlidesEngineFormats.SaveFormatOf(format));
                loaded.Resources.ThrowIfFailed();
                using LoadedPresentation reopened = session.Loader.OpenPublishedCandidate(
                    temp,
                    protection.Password);
            },
            verifier is null ? null : candidate =>
            {
                using LoadedPresentation reopened = session.Loader.OpenPublishedCandidate(
                    candidate,
                    protection.Password);
                VerifiedBatch verified = applied.Verified(verifier.Verify(reopened.Presentation));
                return new SlidesEditVerification { Ok = verified.Ok, Issues = verified.Issues, CheckedOps = verified.CheckedOps };
            },
            "slides-edit");
    }
}
