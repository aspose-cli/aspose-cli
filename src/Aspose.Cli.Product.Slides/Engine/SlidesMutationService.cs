using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Product.Slides.Engine.Mapping;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Slides;
using Aspose.Slides.SlideShow;
using static Aspose.Cli.Product.Slides.Engine.SlidesEngineSupport;
using static Aspose.Cli.Product.Slides.Engine.SlidesMutationHandlers;

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
        batch = SlidesOps.Catalog.Prepare(batch);
        string format = Path.GetExtension(request.OutputPath).TrimStart('.').ToLowerInvariant();
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
        IReadOnlyList<SlidesMutationHandlers.ResolvedSlidesOp> resolved = ResolveBatch(presentation, batch);
        var touched = new HashSet<uint>();
        IReadOnlyList<BoundedOperationOutcome> outcomes = ApplyOperations(presentation, resolved, request.Options.BestEffort, touched);
        EditPublication publication = Publish(presentation, request, format, precondition);

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
                ? EvaluationInputWarnings(state, presentation)
                : OutputWarnings(state, presentation),
        };
    }

    private IReadOnlyList<BoundedOperationOutcome> ApplyOperations(
        Presentation presentation,
        IReadOnlyList<SlidesMutationHandlers.ResolvedSlidesOp> resolved,
        bool bestEffort,
        ISet<uint> touched) =>
        BoundedOperationRunner.Run(
            SlidesOps.Catalog,
            resolved.Select(static item => item.Op).ToArray(),
            bestEffort,
            deadline: null,
            (_, index) =>
            {
                SlidesMutationHandlers.ResolvedSlidesOp item = resolved[index];
                var operationTouched = new SortedSet<uint>();
                long affected = ApplyResolved(_resourceBudgets, _loader, presentation, item, operationTouched);
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
        Presentation presentation,
        PresentationEditRequest request,
        string format,
        FileWritePrecondition precondition)
    {
        OutputInfo? output = null;
        BackupInfo? backup = null;
        if (!request.Options.DryRun)
        {
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
            if (write.Backup is not null)
            {
                backup = new BackupInfo
                {
                    Path = write.Backup.Path,
                    Created = write.Backup.Created,
                    SizeBytes = write.Backup.SizeBytes,
                };
            }

            transaction.Commit();
        }

        return new EditPublication(output, backup);
    }

    private sealed record EditPublication(
        OutputInfo? Output,
        BackupInfo? Backup);
}
