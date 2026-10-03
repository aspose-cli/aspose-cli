using System.Text.RegularExpressions;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Product.Slides.Engine.Editing;
using Aspose.Cli.Product.Slides.Engine.Mapping;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Slides;
using static Aspose.Cli.Product.Slides.Engine.SlidesEngineSupport;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>Owns validated presentation mutation and verification.</summary>
internal sealed class SlidesMutationService
{
    private readonly ILicenseGate _licenseGate;
    private readonly ResourceBudgetLedger _resourceBudgets;
    private readonly SafeFileWriter _writer;
    private readonly SlidesPresentationLoader _loader;

    internal SlidesMutationService(
        ILicenseGate licenseGate,
        ResourceBudgetLedger resourceBudgets,
        SafeFileWriter writer,
        SlidesPresentationLoader loader)
    {
        _licenseGate = licenseGate ?? throw new ArgumentNullException(nameof(licenseGate));
        _resourceBudgets = resourceBudgets;
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _loader = loader;
    }

    public SlidesEditResult ApplyOps(
        string filePath,
        SlidesOpsBatch batch,
        PresentationEditRequest request)
    {
        batch = SlidesOp.Catalog.Prepare(batch);
        string format = SlidesFormats.ForOutput(request.OutputPath);
        if (!SlidesFormats.WriteIds.Contains(format, StringComparer.Ordinal))
        {
            throw CliErrors.FormatUnsupported(format, SlidesFormats.WriteIds);
        }

        LicenseState state = _licenseGate.EnsureApplied();
        FileWritePrecondition precondition = FileWritePrecondition.Capture(filePath);
        using LoadedPresentation loaded = _loader.Open(filePath, request.Password);
        SourceInfo input = Source(filePath, loaded.FormatId);
        FileFingerprints.EnsureUnchanged(filePath, precondition.Fingerprint, input.Fingerprint!);
        FileFingerprints.EnsureMatch(filePath, request.Options.IfMatch, input.Fingerprint!);
        Presentation presentation = loaded.Presentation;
        IReadOnlyList<SlidesMutationHandlers.ResolvedSlidesOp> resolved = SlidesMutationHandlers.ResolveBatch(presentation, batch);
        var touched = new HashSet<uint>();
        IReadOnlyList<BoundedOperationOutcome> outcomes = ApplyOperations(presentation, resolved, request.Options.BestEffort, touched, state);
        EditPublication publication = Publish(loaded, request, format, precondition);

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
                ? InputWarnings(state, loaded, textRead: false)
                : EnvelopeParts.CombineWarnings(OutputWarnings(state, loaded), EnvelopeParts.BackupWarnings(publication.Backup)),
        };
    }

    private IReadOnlyList<BoundedOperationOutcome> ApplyOperations(
        Presentation presentation,
        IReadOnlyList<SlidesMutationHandlers.ResolvedSlidesOp> resolved,
        bool bestEffort,
        ISet<uint> touched,
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
                    _resourceBudgets.Inputs, _loader, presentation, item, operationTouched, state == LicenseState.Evaluation).Run();
                touched.UnionWith(operationTouched);
                return new AppliedOperation(affected, OperationTargets(item, operationTouched));
            },
            (_, index) => OperationTargets(resolved[index], []));

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
        return slideIds.Count is > 0 and <= 100
            ? slideIds.Select(static slideId => $"slide/{slideId}").ToArray()
            : ["presentation"];
    }

    private EditPublication Publish(
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
            using var transaction = new AtomicOutputSetWriter(_writer, Path.GetDirectoryName(request.OutputPath)!, "slides-edit");
            StagedOutput write = transaction.Stage(
                request.OutputPath,
                request.Overwrite,
                request.BackupPath,
                precondition,
                temp =>
                {
                    presentation.Save(temp, SaveFormatFor(format));
                    loaded.Resources.ThrowIfFailed();
                    using LoadedPresentation reopened = _loader.OpenPublishedCandidate(
                        temp,
                        request.EncryptPassword ?? request.Password);
                });
            output = new OutputInfo
            {
                Path = request.OutputPath,
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
