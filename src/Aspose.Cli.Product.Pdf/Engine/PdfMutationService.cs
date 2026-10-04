using System.Globalization;
using System.Text.RegularExpressions;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Product.Pdf.Engine.Editing;
using Aspose.Cli.Product.Pdf.Engine.Mapping;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;
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
using PdfColor = Aspose.Pdf.Color;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Owns validated batch execution, atomic persistence and edit verification.</summary>
internal sealed class PdfMutationService
{
    private readonly ILicenseGate _licenseGate;
    private readonly SafeFileWriter _writer;
    private readonly PdfDocumentLoader _loader;
    private readonly InputSource _inputs;
    private readonly OperationDeadline _deadline;

    internal PdfMutationService(
        ILicenseGate licenseGate,
        SafeFileWriter writer,
        PdfDocumentLoader loader,
        InputSource inputs,
        OperationDeadline deadline)
    {
        _licenseGate = licenseGate ?? throw new ArgumentNullException(nameof(licenseGate));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _loader = loader;
        _inputs = inputs;
        _deadline = deadline;
    }

    /// <summary>Opens the document, runs the batch through the mutation handlers and publishes the result.</summary>
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
        Permissions? userPermissions = loaded.PasswordType == PasswordType.User && loaded.Document.IsEncrypted
            ? (Permissions)loaded.Document.Permissions
            : null;
        var touched = new SortedSet<int>();
        var textMoved = new List<string>();
        PdfNavigationCensus navigationBefore = PdfNavigationCensus.Unresolved(loaded.Document);
        PdfEditVerifier? verifier = request.Verify ? new PdfEditVerifier(loaded.Document) : null;
        (IReadOnlyList<BoundedOperationOutcome> outcomes, string? outputPassword) =
            ApplyOperations(loaded.Document, batch, request, touched, textMoved, operationInputs, verifier);
        PdfNavigationCensus navigation = PdfNavigationCensus.Degraded(
            navigationBefore, PdfNavigationCensus.Unresolved(loaded.Document));
        Publication publication;
        try { publication = Publish(loaded.Document, request, outputPassword, precondition, verifier, state); }
        finally { operationInputs.ThrowIfFailed(); }
        List<Warning> warnings = BuildWarnings(state, request.Options.DryRun, signatures, outcomes, textMoved);
        if (UnpermittedChange(userPermissions, outcomes, request.Options.DryRun) is { } protection)
        {
            warnings.Add(protection);
        }

        if (navigation.ToWarning(
                "no longer lead to a page: they targeted deleted pages, or moved pages at a position with a coordinate of 0, which the SDK cannot tell apart from an omitted one",
                "Re-create the affected bookmarks (add_bookmark) and links (add_link) after the page change, or reorder pages before adding navigation.")
            is { } degraded)
        {
            warnings.Add(degraded);
        }

        warnings.AddRange(EnvelopeParts.BackupWarnings(publication.Backup) ?? []);

        return new PdfEditResult
        {
            Input = input,
            Output = publication.Output,
            DryRun = request.Options.DryRun,
            Applied = outcomes,
            Backup = publication.Backup,
            PagesTouched = touched.Count == 0 ? null : touched.ToArray(),
            Verification = publication.Verification,
            License = EnvelopeParts.License(state),
            Warnings = warnings.Count == 0 ? null : warnings,
        };
    }

    private (IReadOnlyList<BoundedOperationOutcome> Outcomes, string? OutputPassword) ApplyOperations(
        Document document,
        PdfOpsBatch batch,
        PdfEditRequest request,
        ISet<int> touched,
        List<string> textMoved,
        InputResourceScope operationInputs,
        PdfEditVerifier? verifier)
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
                var movedPages = new SortedSet<int>();
                long affected = new PdfMutationHandlers(_loader, operationInputs, document, request.OpSecrets, operationPages, movedPages).Run(op);
                verifier?.Record(op, op.Id!, affected, document);
                touched.UnionWith(operationPages);
                if (movedPages.Count > 0)
                {
                    textMoved.Add($"'{op.Id}' ({PdfOp.Catalog.NameOf(op)}) on page{(movedPages.Count == 1 ? "" : "s")} {string.Join(", ", movedPages)}");
                }
                if (op is EncryptPdfOp encrypt)
                {
                    outputPassword = OperationSecrets.Resolve(request.OpSecrets, encrypt.UserPasswordEnv);
                }
                else if (op is DecryptPdfOp)
                {
                    outputPassword = null;
                }
                return new AppliedOperation(affected, OperationTargets(op, operationPages, document));
            },
            (op, _) => OperationTargets(op, [], applied: null));
        return (outcomes, outputPassword);
    }

    /// <summary>
    /// The targets an operation reports. A bookmark operation that succeeded names each bookmark
    /// it added or deleted by its index, a deleted one as it was before the deletion.
    /// </summary>
    private static IReadOnlyList<string> OperationTargets(
        PdfOp operation,
        IReadOnlyCollection<int> pages,
        Document? applied)
    {
        if (applied is not null)
        {
            switch (operation)
            {
                case AddBookmarkOp add:
                    return [$"pdf/bookmark/{Editing.PdfMutationSupport.NewBookmarkIndex(applied, add.Parent)}"];
                case DeleteBookmarksOp { Indexes: { } indexes }:
                    return [.. indexes.Select(static index => $"pdf/bookmark/{index}")];
            }
        }

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
        FileWritePrecondition precondition,
        PdfEditVerifier? verifier,
        LicenseState state)
    {
        OutputInfo? output = null;
        BackupInfo? backup = null;
        PdfEditVerification? verification = null;
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
            backup = write.Backup;
            if (verifier is not null)
            {
                // Issues are reported, not refused: the output is published and the command exits 8.
                verification = write.Read(candidate =>
                {
                    using LoadedPdf reopened = _loader.OpenPublishedCandidate(candidate, outputPassword);
                    return verifier.Verify(reopened.Document, state, _deadline);
                });
            }

            transaction.Commit();
        }

        return new Publication(output, backup, verification);
    }

    private static List<Warning> BuildWarnings(
        LicenseState state,
        bool dryRun,
        bool signatures,
        IReadOnlyCollection<BoundedOperationOutcome> outcomes,
        IReadOnlyList<string> textMoved)
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

        // A redaction that matched nothing passes its read-back check without having removed
        // anything, so it is disclosed. Like a verification issue, it never repeats the pattern.
        string[] unmatched = outcomes
            .Where(static item => item.Op == "redact_text" && item.Status == OpStatuses.Ok && item.ItemsAffected == 0)
            .Select(static item => $"'{item.Id}' (redact_text)")
            .ToArray();
        if (unmatched.Length > 0)
        {
            warnings.Add(new Warning
            {
                Code = PdfDiagnostics.RedactionNoMatch,
                Message = (unmatched.Length == 1 ? "Operation " : "Operations ") + string.Join(", ", unmatched)
                    + " matched no text on the pages searched, so nothing was redacted there.",
                Hint = "Search the pages with 'pdf query search' and the same pattern. When the text shows on the page but is not found, "
                    + "its extracted text differs, for example by spaces between characters: match it with a regular expression "
                    + "that allows them (\\s*), or cover it with redact_area.",
            });
        }

        // PDF-REDACT-TEXT-SHIFT: the text is still in the file, but part of it may now be hidden.
        if (textMoved.Count > 0)
        {
            warnings.Add(new Warning
            {
                Code = PdfDiagnostics.RedactionTextMoved,
                Message = (textMoved.Count == 1 ? "Operation " : "Operations ") + string.Join(", ", textMoved)
                    + " moved the text that followed what was removed on its line to the left, so part of it may now lie under the cover.",
                Hint = "Compare those pages with the input in 'aspose-cli review'. The moved text is still in the file and searchable; "
                    + "to keep the line in place, redact the source document and create the PDF again.",
            });
        }

        return warnings;
    }

    /// <summary>
    /// The engine applies every operation to a document opened with its user password, whatever
    /// its permissions say, so a change they do not allow is disclosed. As the PDF standard
    /// defines them, filling fields is allowed by the fill-forms or annotation permission, page
    /// assembly (inserting, moving, rotating and deleting pages, creating bookmarks) by the
    /// assemble permission, any other change by the modify permission, and changing the
    /// encryption only by the owner password.
    /// </summary>
    private static Warning? UnpermittedChange(
        Permissions? permissions, IReadOnlyCollection<BoundedOperationOutcome> outcomes, bool dryRun)
    {
        if (permissions is not { } granted)
        {
            return null;
        }

        string[] applied = outcomes
            .Where(static item => item.Status == OpStatuses.Ok && item.ItemsAffected > 0)
            .Select(static item => item.Op)
            .ToArray();
        string[] changes = applied
            .Where(op => !Permitted(op, granted))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (changes.Length == 0)
        {
            return null;
        }

        string? encryption = applied.LastOrDefault(static op => op is "encrypt" or "decrypt");
        string output = (encryption, dryRun) switch
        {
            ("decrypt", false) => "The output is not encrypted, so it no longer restricts anyone.",
            ("decrypt", true) => "The output would not be encrypted, so it would no longer restrict anyone.",
            ("encrypt", false) => "The output is encrypted with the passwords and permissions the encrypt operation set.",
            ("encrypt", true) => "The output would be encrypted with the passwords and permissions the encrypt operation set.",
            (_, false) => "The output keeps the input's encryption and permissions; edit with the owner password to change them.",
            (_, true) => "The output would keep the input's encryption and permissions; edit with the owner password to change them.",
        };
        return new Warning
        {
            Code = WarningCodes.ProtectionNotEnforced,
            Message = $"The input was opened with its user password, whose permissions do not allow {string.Join(", ", changes)}; the engine does not enforce them, so "
                + (dryRun ? "the batch would change it if it were not a dry run." : "the batch changed it."),
            Hint = $"Confirm that the document's owner authorized the change. {output}",
        };
    }

    private static bool Permitted(string op, Permissions granted) => op switch
    {
        "encrypt" or "decrypt" => false,
        "set_form_field" => (granted & (Permissions.ModifyContent | Permissions.FillForm | Permissions.ModifyTextAnnotations)) != 0,
        "rotate_pages" or "delete_pages" or "move_pages" or "insert_pages_from" or "insert_blank_page" or "add_bookmark" =>
            (granted & (Permissions.ModifyContent | Permissions.AssembleDocument)) != 0,
        _ => granted.HasFlag(Permissions.ModifyContent),
    };

    private sealed record Publication(
        OutputInfo? Output,
        BackupInfo? Backup,
        PdfEditVerification? Verification);
}
