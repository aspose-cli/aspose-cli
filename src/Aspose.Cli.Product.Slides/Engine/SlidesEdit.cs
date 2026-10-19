using Aspose.Cli.Product.Slides.Engine.Editing;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Slides;
using static Aspose.Cli.Product.Slides.Engine.SlidesEngineSupport;

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
        InputInfo input = Source(filePath, loaded);
        FileFingerprints.EnsureUnchanged(filePath, precondition.Fingerprint, input.Fingerprint);
        FileFingerprints.EnsureMatch(filePath, request.Options.IfMatch, input.Fingerprint);
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
        EditPublication publication = Publish(session, loaded, request, format, precondition, verifier, applied);
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
                : EnvelopeParts.CombineWarnings(WrittenWarnings(loaded), warnings, EnvelopeParts.BackupWarnings(publication.Backup)),
        };
    }

    private static EditPublication Publish(
        SlidesSession session,
        LoadedPresentation loaded,
        SlidesEditRequest request,
        string format,
        FileWritePrecondition precondition,
        SlidesEditVerifier? verifier,
        AppliedBatch applied)
    {
        OutputInfo? output = null;
        BackupInfo? backup = null;
        SlidesEditVerification? verification = null;
        if (!request.Options.DryRun)
        {
            Presentation presentation = loaded.Presentation;
            // PPTX and PPTM, the formats an edit writes, keep the input's password unless
            // --encrypt replaces it.
            Encrypt(presentation, request.EncryptPassword);
            bool encrypted = presentation.ProtectionManager.IsEncrypted;
            using OutputSet<Presentation> transaction = session.Outputs.BeginSet([request.Output.Directory], "slides-edit");
            StagedOutput write = transaction.Stage(
                request.Output.Path,
                request.Output.Overwrite,
                presentation,
                temp =>
                {
                    presentation.Save(temp, SlidesEngineFormats.SaveFormatOf(format));
                    loaded.Resources.ThrowIfFailed();
                    using LoadedPresentation reopened = session.Loader.OpenPublishedCandidate(
                        temp,
                        request.EncryptPassword ?? request.Password);
                },
                backupPath: request.Output.BackupPath,
                inputPrecondition: precondition,
                encrypted: encrypted);
            backup = write.Backup;
            if (verifier is not null)
            {
                // Issues are reported, not refused: the output is published and the command exits 8.
                verification = write.Read(candidate =>
                {
                    using LoadedPresentation reopened = session.Loader.OpenPublishedCandidate(
                        candidate,
                        request.EncryptPassword ?? request.Password);
                    VerifiedBatch verified = applied.Verified(verifier.Verify(reopened.Presentation));
                    return new SlidesEditVerification { Ok = verified.Ok, Issues = verified.Issues, CheckedOps = verified.CheckedOps };
                });
            }

            transaction.Commit();
            output = FileObjects.Output(write.Published, request.Output.Format, encrypted);
        }

        return new EditPublication(output, backup, verification);
    }

    private sealed record EditPublication(
        OutputInfo? Output,
        BackupInfo? Backup,
        SlidesEditVerification? Verification);
}
