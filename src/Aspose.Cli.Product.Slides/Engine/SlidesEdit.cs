using Aspose.Cli.Product.Slides.Engine.Editing;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Slides;
using static Aspose.Cli.Product.Slides.Engine.SlidesEngineSupport;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>Validated, atomic presentation mutation and verification.</summary>
internal static class SlidesEdit
{
    internal static SlidesEditResult Run(SlidesSession session, PresentationEditRequest request)
    {
        string filePath = request.Input;
        SlidesOpsBatch batch = SlidesOp.Catalog.Prepare(request.Batch);
        string format = request.Output.Format.Id;

        LicenseState state = session.Outputs.License;
        FileWritePrecondition precondition = FileWritePrecondition.Capture(filePath);
        using LoadedPresentation loaded = session.Loader.Open(filePath, request.Password);
        SourceInfo input = Source(filePath, loaded.FormatId);
        FileFingerprints.EnsureUnchanged(filePath, precondition.Fingerprint, input.Fingerprint!);
        FileFingerprints.EnsureMatch(filePath, request.Options.IfMatch, input.Fingerprint!);
        Presentation presentation = loaded.Presentation;
        IReadOnlyList<SlidesMutationHandlers.ResolvedSlidesOp> resolved = SlidesMutationHandlers.ResolveBatch(presentation, batch);
        var touched = new HashSet<uint>();
        var warnings = new List<Warning>();
        IReadOnlyList<BoundedOperationOutcome> outcomes = ApplyOperations(session, presentation, resolved, request.Options.BestEffort, touched, warnings, state);
        EditPublication publication = Publish(session, loaded, request, format, precondition);

        return new SlidesEditResult
        {
            Input = input,
            Output = publication.Output,
            DryRun = request.Options.DryRun,
            Applied = outcomes,
            Backup = publication.Backup,
            SlidesTouched = touched.Count == 0 ? null : touched.Order().ToArray(),
            License = EnvelopeParts.License(state),
            Warnings = request.Options.DryRun
                ? EnvelopeParts.CombineWarnings(InputWarnings(state, loaded, textRead: false), warnings)
                : EnvelopeParts.CombineWarnings(WrittenWarnings(state, loaded, textRead: false), warnings, EnvelopeParts.BackupWarnings(publication.Backup)),
        };
    }

    private static IReadOnlyList<BoundedOperationOutcome> ApplyOperations(
        SlidesSession session,
        Presentation presentation,
        IReadOnlyList<SlidesMutationHandlers.ResolvedSlidesOp> resolved,
        bool bestEffort,
        ISet<uint> touched,
        ICollection<Warning> warnings,
        LicenseState state) =>
        BoundedOperationRunner.Run(
            SlidesOp.Catalog,
            resolved.Select(static item => item.Op).ToArray(),
            bestEffort,
            deadline: null,
            (_, index) =>
            {
                SlidesMutationHandlers.ResolvedSlidesOp item = resolved[index];
                var operationTouched = new SortedSet<uint>();
                long affected = new SlidesMutationHandlers(
                    session.Budgets.Inputs, session.Loader, presentation, item, operationTouched, warnings, state == LicenseState.Evaluation).Run();
                touched.UnionWith(operationTouched);
                return new AppliedOperation(affected, OperationTargets(item, operationTouched));
            },
            (_, index) => OperationTargets(resolved[index], []),
            static (_, _) => [PresentationTarget]);

    /// <summary>The whole presentation, the target of an operation that names no slide or changes more than an outcome lists.</summary>
    private const string PresentationTarget = "presentation";

    private static IReadOnlyList<string> OperationTargets(
        SlidesMutationHandlers.ResolvedSlidesOp item,
        IReadOnlyCollection<uint> touched)
    {
        if (item.ShapeId is long shapeId && item.Slide is not null)
        {
            return [$"slide/{item.Slide.SlideId}/shape/{shapeId}"];
        }

        var slideIds = new SortedSet<uint>(touched);
        if (slideIds.Count == 0 && item.Slides is not null)
        {
            foreach (ISlide slide in item.Slides)
            {
                slideIds.Add(slide.SlideId);
            }
        }
        if (slideIds.Count == 0 && item.Slide is not null)
        {
            slideIds.Add(item.Slide.SlideId);
        }
        return slideIds.Count > 0
            ? slideIds.Select(static slideId => $"slide/{slideId}").ToArray()
            : [PresentationTarget];
    }

    private static EditPublication Publish(
        SlidesSession session,
        LoadedPresentation loaded,
        PresentationEditRequest request,
        string format,
        FileWritePrecondition precondition)
    {
        OutputInfo? output = null;
        BackupInfo? backup = null;
        if (!request.Options.DryRun)
        {
            Presentation presentation = loaded.Presentation;
            Encrypt(presentation, request.EncryptPassword);
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
                inputPrecondition: precondition);
            output = new OutputInfo
            {
                Path = request.Output.Path,
                Format = format,
                SizeBytes = write.SizeBytes,
            };
            backup = write.Backup;
            transaction.Commit();
        }

        return new EditPublication(output, backup);
    }

    private sealed record EditPublication(
        OutputInfo? Output,
        BackupInfo? Backup);
}
