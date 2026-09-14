using System.Drawing;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Product.Slides.Engine.Mapping;
using Aspose.Cli.Product.Slides.Operations;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Text;
using Aspose.Slides;
using Aspose.Slides.Charts;
using Aspose.Slides.SlideShow;
using static Aspose.Cli.Product.Slides.Engine.SlidesEngineSupport;
using static Aspose.Cli.Product.Slides.Engine.SlidesMutationHandlers;
using static Aspose.Cli.Product.Slides.Engine.SlidesMutationSupport;

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
        SlidesFontCatalog.EnsureInitialized();
    }

    /// <inheritdoc />
    public SlidesEditResult ApplyOps(
        string filePath,
        SlidesOpsBatch batch,
        PresentationEditRequest request) =>
        SlidesErrorTranslator.Execute("edit", () => ApplyOpsCore(filePath, batch, request));

    private SlidesEditResult ApplyOpsCore(
        string filePath,
        SlidesOpsBatch batch,
        PresentationEditRequest request)
    {
        batch = SlidesOpsParser.Prepare(batch);
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
        List<BoundedOperationOutcome> outcomes = ApplyOperations(presentation, resolved, request.Options.BestEffort, touched);
        EditPublication publication = Publish(presentation, request, format, touched, precondition);

        return new SlidesEditResult
        {
            Input = input,
            Output = publication.Output,
            DryRun = request.Options.DryRun,
            Applied = outcomes,
            Backup = publication.Backup,
            SlidesTouched = touched.Count == 0 ? null : touched.Order().ToArray(),
            Verification = publication.Verification,
            License = EnvelopeParts.License(state),
            Warnings = request.Options.DryRun
                ? EvaluationInputWarnings(state, presentation)
                : OutputWarnings(state, presentation),
        };
    }

    private List<BoundedOperationOutcome> ApplyOperations(
        Presentation presentation,
        IReadOnlyList<SlidesMutationHandlers.ResolvedSlidesOp> resolved,
        bool continueOnError,
        ISet<uint> touched)
    {
        var outcomes = new List<BoundedOperationOutcome>(resolved.Count);
        for (int index = 0; index < resolved.Count; index++)
        {
            SlidesMutationHandlers.ResolvedSlidesOp item = resolved[index];
            var operationTouched = new SortedSet<uint>();
            try
            {
                long affected = ApplyResolved(
                    _resourceBudgets,
                    _loader,
                    presentation,
                    item,
                    operationTouched);
                touched.UnionWith(operationTouched);
                outcomes.Add(new BoundedOperationOutcome
                {
                    Id = item.Op.Id!,
                    Index = index,
                    Op = item.Op.OpName,
                    Status = "ok",
                    ItemsAffected = affected,
                    Targets = OperationTargets(item, operationTouched),
                });
            }
            catch (Exception exception) when (
                exception is CliException or EngineOpException or InvalidOperationException
                or ArgumentException or IndexOutOfRangeException or IOException
                or UnauthorizedAccessException)
            {
                touched.UnionWith(operationTouched);
                CliException translated = exception as CliException
                    ?? InvalidOp(index, item.Op.OpName, exception.Message, exception);
                if (!continueOnError)
                {
                    throw translated;
                }

                outcomes.Add(new BoundedOperationOutcome
                {
                    Id = item.Op.Id!,
                    Index = index,
                    Op = item.Op.OpName,
                    Status = "failed",
                    ItemsAffected = 0,
                    Targets = OperationTargets(item, operationTouched),
                    Error = new OpError
                    {
                        Code = translated.Code.Name,
                        Message = translated.Message,
                        Hint = translated.Hint ?? "Fix the operation target or value, then retry the batch.",
                    },
                });
            }
        }

        return outcomes;
    }

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
        IReadOnlySet<uint> touched,
        FileWritePrecondition precondition)
    {
        OutputInfo? output = null;
        BackupInfo? backup = null;
        SlidesEditVerification? verification = null;
        if (!request.Options.DryRun)
        {
            Encrypt(presentation, request.EncryptPassword);
            using var transaction = new AtomicOutputSetWriter(_writer, Path.GetDirectoryName(request.OutputPath)!, "slides-edit");
            StagedOutput write = transaction.Stage(
                request.OutputPath,
                request.Overwrite,
                request.BackupPath,
                precondition,
                temp => presentation.Save(temp, SaveFormatFor(format)));
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

            if (request.Verify)
            {
                verification = write.Read(candidate => VerifyEdit(
                    candidate,
                    request.OutputPath,
                    request.EncryptPassword ?? request.Password,
                    touched,
                    transaction,
                    request.OverwriteArtifacts));
            }
            transaction.Commit();
        }

        return new EditPublication(output, backup, verification);
    }

    private sealed record EditPublication(
        OutputInfo? Output,
        BackupInfo? Backup,
        SlidesEditVerification? Verification);

    private SlidesEditVerification VerifyEdit(
        string candidatePath,
        string outputPath,
        string? password,
        IReadOnlyCollection<uint> touched,
        AtomicOutputSetWriter transaction,
        bool overwriteArtifacts)
    {
        using LoadedPresentation reopened = _loader.OpenPublishedCandidate(candidatePath, password);
        ISlide[] selected = (touched.Count == 0
                ? reopened.Presentation.Slides.Take(1)
                : reopened.Presentation.Slides.Where(slide => touched.Contains(slide.SlideId)))
            .Take(12)
            .ToArray();
        EnsureRasterBudget(_resourceBudgets,
            (long)Math.Ceiling(reopened.Presentation.SlideSize.Size.Width * 2d),
            (long)Math.Ceiling(reopened.Presentation.SlideSize.Size.Height * 2d), selected.Length, 144);
        var renders = new List<SlideRenderOutput>();
        var issues = new List<string>();
        foreach (ISlide slide in selected)
        {
            int number = FindSlideNumber(reopened.Presentation, slide);
            string path = $"{outputPath}.verify.s{number}.png";
            long size = transaction.Stage(path, overwriteArtifacts, temp =>
            {
                using IImage image = slide.GetImage(2f, 2f);
                using FileStream outputStream = File.Create(temp);
                image.Save(outputStream, ImageFormat.Png);
            }).SizeBytes;
            renders.Add(new SlideRenderOutput
            {
                Slide = number,
                SlideId = slide.SlideId,
                Output = new OutputInfo { Path = path, Format = "png", SizeBytes = size },
            });
        }

        return new SlidesEditVerification
        {
            Ok = issues.Count == 0,
            VisualReviewRequired = reopened.Presentation.Slides.Count > selected.Length
                || touched.Count > selected.Length,
            Slides = reopened.Presentation.Slides.Count,
            ReadBackSlideIds = selected.Select(static slide => slide.SlideId).ToArray(),
            Renders = renders,
            Issues = issues,
        };
    }

}
