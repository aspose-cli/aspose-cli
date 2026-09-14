using System.Globalization;
using System.Text.RegularExpressions;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Product.Pdf.Engine.Mapping;
using Aspose.Cli.Product.Pdf.Operations;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Text;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Devices;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Optimization;
using Aspose.Pdf.Text;
using static Aspose.Cli.Product.Pdf.Engine.PdfArtifactSupport;
using static Aspose.Cli.Product.Pdf.Engine.PdfEngineSupport;
using static Aspose.Cli.Product.Pdf.Engine.PdfMutationSupport;
using PdfColor = Aspose.Pdf.Color;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Owns validated batch execution, atomic persistence and edit verification.</summary>
internal sealed class PdfMutationService
{
    private readonly ILicenseGate _licenseGate;
    private readonly SafeFileWriter _writer;
    private readonly PdfDocumentLoader _loader;

    internal PdfMutationService(
        ILicenseGate licenseGate,
        SafeFileWriter writer,
        PdfDocumentLoader loader)
    {
        _licenseGate = licenseGate ?? throw new ArgumentNullException(nameof(licenseGate));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _loader = loader;
    }

    public PdfEditResult ApplyOps(string filePath, PdfOpsBatch batch, PdfEditRequest request) =>
        PdfErrorTranslator.Execute("edit", () => ApplyOpsCore(filePath, batch, request));

    /// <inheritdoc />

    private PdfEditResult ApplyOpsCore(string filePath, PdfOpsBatch batch, PdfEditRequest request)
    {
        batch = PdfOpsParser.Prepare(batch);
        EnsurePdfOutput(request.OutputPath);
        LicenseState state = _licenseGate.EnsureApplied();
        FileWritePrecondition precondition = FileWritePrecondition.Capture(filePath);
        using LoadedPdf loaded = _loader.Open(filePath, request.Password);
        SourceInfo input = PdfInfoProjection.Source(filePath, includeFingerprint: true);
        FileFingerprints.EnsureUnchanged(filePath, precondition.Fingerprint, input.Fingerprint!);
        FileFingerprints.EnsureMatch(filePath, request.Options.IfMatch, input.Fingerprint!);
        bool signatures = loaded.Document.Form.SignaturesExist;
        var touched = new SortedSet<int>();
        (List<BoundedOperationOutcome> outcomes, string? outputPassword) =
            ApplyOperations(loaded.Document, batch, request, touched);
        Publication publication = Publish(loaded.Document, request, outputPassword, touched, precondition);
        List<Warning> warnings = BuildWarnings(state, request.Options.DryRun, signatures, outcomes);

        return new PdfEditResult
        {
            Input = input,
            Output = publication.Output,
            DryRun = request.Options.DryRun,
            Applied = outcomes,
            Backup = publication.Backup,
            Mutation = publication.Mutation,
            PagesTouched = touched.Count == 0 ? null : touched.ToArray(),
            Verification = publication.Verification,
            License = EnvelopeParts.License(state),
            Warnings = warnings.Count == 0 ? null : warnings,
        };
    }

    private (List<BoundedOperationOutcome> Outcomes, string? OutputPassword) ApplyOperations(
        Document document,
        PdfOpsBatch batch,
        PdfEditRequest request,
        ISet<int> touched)
    {
        var outcomes = new List<BoundedOperationOutcome>(batch.Ops.Count);
        string? outputPassword = request.Password;
        for (int index = 0; index < batch.Ops.Count; index++)
        {
            PdfOp op = batch.Ops[index];
            var operationPages = new SortedSet<int>();
            try
            {
                IReadOnlyDictionary<string, string>? secrets = null;
                _ = request.OpSecrets?.TryGetValue(index, out secrets);
                long affected = PdfMutationHandlers.ApplyOp(
                    _loader,
                    document,
                    op,
                    secrets,
                    operationPages);
                touched.UnionWith(operationPages);
                if (op is EncryptPdfOp)
                {
                    outputPassword = Secret(secrets, "userPassword", required: false);
                }
                else if (op is DecryptPdfOp)
                {
                    outputPassword = null;
                }

                outcomes.Add(new BoundedOperationOutcome
                {
                    Id = op.Id!,
                    Index = index,
                    Op = op.OpName,
                    Status = OpStatuses.Ok,
                    ItemsAffected = affected,
                    Targets = OperationTargets(op, operationPages),
                });
            }
            catch (Exception exception) when (
                exception is CliException or EngineOpException or InvalidOperationException
                or ArgumentException or IndexOutOfRangeException)
            {
                touched.UnionWith(operationPages);
                CliException translated = exception as CliException ?? InvalidOp(index, op.OpName, exception.Message, exception);
                if (!request.Options.BestEffort)
                {
                    throw translated;
                }

                outcomes.Add(new BoundedOperationOutcome
                {
                    Id = op.Id!,
                    Index = index,
                    Op = op.OpName,
                    Status = OpStatuses.Failed,
                    ItemsAffected = 0,
                    Targets = OperationTargets(op, operationPages),
                    Error = new OpError
                    {
                        Code = translated.Code.Name,
                        Message = translated.Message,
                        Hint = translated.Hint ?? "Fix the operation target or value, then retry the batch.",
                    },
                });
            }
        }

        return (outcomes, outputPassword);
    }

    private static IReadOnlyList<string> OperationTargets(
        PdfOp operation,
        IReadOnlyCollection<int> pages)
    {
        if (pages.Count is > 0 and <= 100)
        {
            return pages
                .Order()
                .Select(static page => $"pdf/page/{page}")
                .ToArray();
        }
        return [operation switch
        {
            SetMetadataOp or RemoveMetadataOp => "pdf/metadata",
            SetFormFieldOp or FlattenFormsOp => "pdf/form",
            EncryptPdfOp or DecryptPdfOp => "pdf/security",
            RedactTextOp or RedactAreaOp => "pdf/text",
            AddBookmarkOp or DeleteBookmarksOp => "pdf/bookmark",
            AddAttachmentOp or RemoveAttachmentOp => "pdf/attachment",
            SetPageLabelsOp => "pdf/pages",
            _ => "pdf",
        }];
    }

    private Publication Publish(
        Document document,
        PdfEditRequest request,
        string? outputPassword,
        IReadOnlySet<int> touched,
        FileWritePrecondition precondition)
    {
        OutputInfo? output = null;
        BackupInfo? backup = null;
        PdfVerification? verification = null;
        MutationReceipt? mutation = null;
        if (!request.Options.DryRun)
        {
            using var transaction = new AtomicOutputSetWriter(_writer, Path.GetDirectoryName(request.OutputPath)!, "pdf-edit");
            StagedOutput write = transaction.Stage(
                request.OutputPath,
                request.Overwrite,
                request.BackupPath,
                precondition,
                temp =>
                {
                    document.Save(temp);
                    using LoadedPdf reopened = _loader.OpenPublishedCandidate(temp, outputPassword);
                });
            output = BuildOutput(request.OutputPath, "pdf", write.SizeBytes) with
            {
                Fingerprint = write.Fingerprint,
            };
            mutation = new MutationReceipt { Verification = "reopened" };
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
                    _loader,
                    candidate,
                    request.OutputPath,
                    outputPassword,
                    touched,
                    transaction,
                    request.OverwriteArtifacts));
            }
            transaction.Commit();
        }

        return new Publication(output, backup, verification, mutation);
    }

    private static List<Warning> BuildWarnings(
        LicenseState state,
        bool dryRun,
        bool signatures,
        IReadOnlyCollection<BoundedOperationOutcome> outcomes)
    {
        var warnings = new List<Warning>();
        if (state == LicenseState.Evaluation && !dryRun)
        {
            warnings.Add(EnvelopeParts.EvaluationWatermark);
        }

        if (signatures && outcomes.Any(static item => item.Status == OpStatuses.Ok && item.ItemsAffected > 0))
        {
            warnings.Add(new Warning
            {
                Code = WarningCodes.SignatureInvalidated,
                Message = "Editing a signed PDF invalidates or changes its existing signature state.",
                Hint = "Validate signatures again and apply any required signature only after the final edit.",
            });
        }

        return warnings;
    }

    private sealed record Publication(
        OutputInfo? Output,
        BackupInfo? Backup,
        PdfVerification? Verification,
        MutationReceipt? Mutation);
}
