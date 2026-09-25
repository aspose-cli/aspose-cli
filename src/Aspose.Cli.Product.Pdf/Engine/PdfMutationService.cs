using System.Globalization;
using System.Text.RegularExpressions;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Product.Pdf.Engine.Mapping;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Operations;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Text;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Devices;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Optimization;
using Aspose.Pdf.Text;
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
    private readonly InputSource _inputs;

    internal PdfMutationService(
        ILicenseGate licenseGate,
        SafeFileWriter writer,
        PdfDocumentLoader loader,
        InputSource inputs)
    {
        _licenseGate = licenseGate ?? throw new ArgumentNullException(nameof(licenseGate));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _loader = loader;
        _inputs = inputs;
    }

    /// <inheritdoc />

    public PdfEditResult ApplyOps(string filePath, PdfOpsBatch batch, PdfEditRequest request)
    {
        batch = PdfOp.Catalog.Prepare(batch);
        EnsurePdfOutput(request.OutputPath);
        LicenseState state = _licenseGate.EnsureApplied();
        FileWritePrecondition precondition = FileWritePrecondition.Capture(filePath);
        using InputResourceScope operationInputs = _inputs.CreateScope();
        using LoadedPdf loaded = _loader.Open(filePath, request.Password);
        SourceInfo input = PdfInfoProjection.Source(filePath, includeFingerprint: true);
        FileFingerprints.EnsureUnchanged(filePath, precondition.Fingerprint, input.Fingerprint!);
        FileFingerprints.EnsureMatch(filePath, request.Options.IfMatch, input.Fingerprint!);
        bool signatures = loaded.Document.Form.SignaturesExist;
        var touched = new SortedSet<int>();
        PdfNavigationCensus navigationBefore = PdfNavigationCensus.Unresolved(loaded.Document);
        (IReadOnlyList<BoundedOperationOutcome> outcomes, string? outputPassword) =
            ApplyOperations(loaded.Document, batch, request, touched, operationInputs);
        PdfNavigationCensus navigation = PdfNavigationCensus.Degraded(
            navigationBefore, PdfNavigationCensus.Unresolved(loaded.Document));
        Publication publication;
        try { publication = Publish(loaded.Document, request, outputPassword, precondition); }
        finally { operationInputs.ThrowIfFailed(); }
        List<Warning> warnings = BuildWarnings(state, request.Options.DryRun, signatures, outcomes);
        if (navigation.ToWarning(
                "no longer lead to a page: moving or deleting pages leaves destinations pointing at pages the document no longer has",
                "Re-create the affected bookmarks (add_bookmark) and links (add_link) after the page change, or reorder pages before adding navigation.")
            is { } degraded)
        {
            warnings.Add(degraded);
        }

        return new PdfEditResult
        {
            Input = input,
            Output = publication.Output,
            DryRun = request.Options.DryRun,
            Applied = outcomes,
            Backup = publication.Backup,
            Mutation = publication.Mutation,
            PagesTouched = touched.Count == 0 ? null : touched.ToArray(),
            License = EnvelopeParts.License(state),
            Warnings = warnings.Count == 0 ? null : warnings,
        };
    }

    private (IReadOnlyList<BoundedOperationOutcome> Outcomes, string? OutputPassword) ApplyOperations(
        Document document,
        PdfOpsBatch batch,
        PdfEditRequest request,
        ISet<int> touched,
        InputResourceScope operationInputs)
    {
        string? outputPassword = request.Password;
        IReadOnlyList<BoundedOperationOutcome> outcomes = BoundedOperationRunner.Run(
            PdfOp.Catalog,
            batch.Ops,
            request.Options.BestEffort,
            deadline: null,
            (op, _) =>
            {
                var operationPages = new SortedSet<int>();
                long affected = new PdfMutationHandlers(_loader, operationInputs, document, request.OpSecrets, operationPages).Run(op);
                touched.UnionWith(operationPages);
                if (op is EncryptPdfOp encrypt)
                {
                    outputPassword = OperationSecrets.Resolve(request.OpSecrets, encrypt.UserPasswordEnv);
                }
                else if (op is DecryptPdfOp)
                {
                    outputPassword = null;
                }
                return new AppliedOperation(affected, OperationTargets(op, operationPages));
            },
            (op, _) => OperationTargets(op, []));
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
        FileWritePrecondition precondition)
    {
        OutputInfo? output = null;
        BackupInfo? backup = null;
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

            transaction.Commit();
        }

        return new Publication(output, backup, mutation);
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
        MutationReceipt? Mutation);
}
