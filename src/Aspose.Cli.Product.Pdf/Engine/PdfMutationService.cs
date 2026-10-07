using Aspose.Cli.Product.Pdf.Engine.Editing;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Pdf;
using static Aspose.Cli.Product.Pdf.Engine.PdfEngineSupport;

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
        bool openPassword = userPermissions is not null && loaded.HasOpenPassword;
        var touched = new SortedSet<int>();
        var textMoved = new List<string>();
        PdfNavigationCensus navigationBefore = PdfNavigationCensus.Unresolved(loaded.Document);
        PdfEditVerifier? verifier = request.Verify ? new PdfEditVerifier(loaded.Document) : null;
        (IReadOnlyList<BoundedOperationOutcome> outcomes, string? outputPassword, EncryptPdfOp? encryption) =
            ApplyOperations(loaded.Document, batch, request, touched, textMoved, operationInputs, verifier);
        // PDF-ENCRYPTED-INFO-TEXT: document information set in this batch survives only an
        // encryption applied to a reopened copy.
        EncryptPdfOp? encryptCopy = batch.Ops.Any(static op => op is SetMetadataOp) ? encryption : null;
        PdfNavigationCensus navigation = PdfNavigationCensus.Degraded(
            navigationBefore, PdfNavigationCensus.Unresolved(loaded.Document));
        Publication publication;
        try { publication = Publish(loaded.Document, request, outputPassword, encryptCopy, precondition, verifier, state); }
        finally { operationInputs.ThrowIfFailed(); }
        List<Warning> warnings = BuildWarnings(state, request.Options.DryRun, signatures, outcomes, textMoved);
        if (UnpermittedChange(userPermissions, openPassword, outcomes, request.Options.DryRun) is { } protection)
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

    private (IReadOnlyList<BoundedOperationOutcome> Outcomes, string? OutputPassword, EncryptPdfOp? Encryption) ApplyOperations(
        Document document,
        PdfOpsBatch batch,
        PdfEditRequest request,
        ISet<int> touched,
        List<string> textMoved,
        InputResourceScope operationInputs,
        PdfEditVerifier? verifier)
    {
        string? outputPassword = request.Password;
        EncryptPdfOp? encryption = null;
        IReadOnlyList<BoundedOperationOutcome> outcomes = BoundedOperationRunner.Run(
            PdfOp.Catalog,
            batch.Ops,
            request.Options.BestEffort,
            deadline: null,
            (op, index) =>
            {
                // A later encrypt makes the save encrypt a copy, which keeps the text.
                if (op is SetMetadataOp metadata && encryption is null && document.IsEncrypted
                    && !batch.Ops.Skip(index + 1).Any(static later => later is EncryptPdfOp))
                {
                    RefuseTextTheEncryptionGarbles(metadata);
                }

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
                    encryption = encrypt;
                }
                else if (op is DecryptPdfOp)
                {
                    outputPassword = null;
                    encryption = null;
                }
                return new AppliedOperation(affected, OperationTargets(op, operationPages, document));
            },
            (op, _) => OperationTargets(op, [], applied: null));
        return (outcomes, outputPassword, encryption);
    }

    /// <summary>
    /// PDF-ENCRYPTED-INFO-TEXT: the engine writes document information beyond Latin-1 into an
    /// encrypted file garbled, and the encryption the input keeps when no later operation
    /// encrypts cannot be applied to a copy, because its passwords are not all known.
    /// </summary>
    private static void RefuseTextTheEncryptionGarbles(SetMetadataOp metadata)
    {
        string[] values =
        [
            metadata.Title ?? string.Empty,
            metadata.Author ?? string.Empty,
            metadata.Subject ?? string.Empty,
            metadata.Keywords ?? string.Empty,
            .. metadata.Custom?.Values ?? [],
        ];
        if (values.Any(static value => value.Any(static character => character > '\u00FF')))
        {
            throw new OperationInvalidException(
                "the input stays encrypted, and the PDF engine garbles document information with characters beyond Latin-1 that it writes into an encrypted file",
                "Add an encrypt operation after set_metadata in the same batch: the batch then encrypts a copy that keeps the text.");
        }
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
        EncryptPdfOp? encryptCopy,
        FileWritePrecondition precondition,
        PdfEditVerifier? verifier,
        LicenseState state)
    {
        OutputInfo? output = null;
        BackupInfo? backup = null;
        PdfEditVerification? verification = null;
        if (!request.Options.DryRun)
        {
            using var transaction = new AtomicOutputSetWriter(_writer, request.Output.Directory, "pdf-edit");
            StagedOutput write = transaction.Stage(
                request.Output.Path,
                request.Output.Overwrite,
                request.Output.BackupPath,
                precondition,
                temp =>
                {
                    Save(document, temp, encryptCopy, request.OpSecrets);
                    using LoadedPdf reopened = _loader.OpenPublishedCandidate(temp, outputPassword);
                });
            output = BuildOutput(request.Output.Path, "pdf", write.SizeBytes) with
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

    /// <summary>
    /// Saves the edited document. PDF-ENCRYPTED-INFO-TEXT: with an encryption to apply to a
    /// copy, the document is saved without encryption, reopened and encrypted, so the document
    /// information the batch set is written as read from a file.
    /// </summary>
    private static void Save(Document document, string path, EncryptPdfOp? encryptCopy, IReadOnlyDictionary<string, string>? secrets)
    {
        if (encryptCopy is null)
        {
            document.Save(path);
            return;
        }

        document.Decrypt();
        using var plain = new MemoryStream();
        document.Save(plain);
        using var copy = new Document(plain);
        Editing.PdfMutationHandlers.Encrypt(copy, encryptCopy, secrets);
        copy.Save(path);
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
                Hint = "Run 'aspose-cli review' on the output: PDF_TEXT_COVERED names the pages where text lies hidden under a cover. "
                    + "The moved text is still in the file and searchable; to keep the line visible, redact the source document and create the PDF again.",
            });
        }

        return warnings;
    }

    /// <summary>
    /// The engine applies every operation to a document opened without its owner password,
    /// whatever its permissions say, so a change they do not allow is disclosed. The message says
    /// whether the user password opened it or the file has no open password. As the PDF standard
    /// defines them, filling fields is allowed by the fill-forms or annotation permission, page
    /// assembly (inserting, moving, rotating and deleting pages, creating bookmarks) by the
    /// assemble permission, any other change by the modify permission, and changing the
    /// encryption only by the owner password.
    /// </summary>
    private static Warning? UnpermittedChange(
        Permissions? permissions, bool openPassword, IReadOnlyCollection<BoundedOperationOutcome> outcomes, bool dryRun)
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
        string output = encryption switch
        {
            "decrypt" => $"The output {(dryRun ? "would not be encrypted, so it would no longer restrict" : "is not encrypted, so it no longer restricts")} anyone.",
            "encrypt" => $"The output {(dryRun ? "would be" : "is")} encrypted with the passwords and permissions the encrypt operation set.",
            _ => $"The output {(dryRun ? "would keep" : "keeps")} the input's encryption and permissions; edit with the owner password to change them.",
        };
        string opened = openPassword
            ? "The input was opened with its user password, whose permissions"
            : "The input has no open password and was opened without its owner password, so its reader permissions apply; they";
        return new Warning
        {
            Code = WarningCodes.ProtectionNotEnforced,
            Message = $"{opened} do not allow {string.Join(", ", changes)}; the engine does not enforce them, so "
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
