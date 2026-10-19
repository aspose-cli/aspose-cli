using Aspose.Cli.Product.Pdf.Engine.Editing;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Pdf;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>
/// Applies a validated, atomic operation batch, publishes the output and verifies the effect of
/// each operation: <c>pdf edit</c>.
/// </summary>
internal static class PdfEdit
{
    /// <summary>Opens the document, runs the batch through the mutation handlers and publishes the result.</summary>
    internal static PdfEditResult Run(PdfSession session, PdfEditRequest request)
    {
        string filePath = request.Input;
        PdfOpsBatch batch = PdfOp.Catalog.Prepare(request.Batch);
        LicenseState state = session.Outputs.License;
        FileWritePrecondition precondition = FileWritePrecondition.Capture(filePath);
        using InputResourceScope operationInputs = session.Budgets.Inputs.CreateScope();
        using LoadedPdf loaded = session.Loader.Open(filePath, request.Password);
        InputInfo input = PdfInfoProjection.Source(filePath, loaded);
        FileFingerprints.EnsureUnchanged(filePath, precondition.Fingerprint, input.Fingerprint!);
        FileFingerprints.EnsureMatch(filePath, request.Options.IfMatch, input.Fingerprint!);
        bool signatures = loaded.Document.Form.SignaturesExist;
        Permissions? userPermissions = loaded.PasswordType == PasswordType.User && loaded.Document.IsEncrypted
            ? (Permissions)loaded.Document.Permissions
            : null;
        bool openPassword = userPermissions is not null && loaded.HasOpenPassword;
        PdfNavigationCensus navigationBefore = PdfNavigationCensus.Unresolved(loaded.Document);
        PdfEditVerifier? verifier = request.Verify ? new PdfEditVerifier(loaded.Document) : null;
        var model = new PdfEditModel(
            session.Loader, operationInputs, loaded.Document, batch.Ops, request.Password, request.EncryptPassword is not null, request.OpSecrets);
        AppliedBatch applied = BoundedOperationRunner.Run(
            PdfOp.Catalog, batch.Ops, request.Options.BestEffort, deadline: null, model, verifier, PdfSnapshots.PageUnit);
        if (batch.Ops.Any(static op => op is DecryptDocumentOp))
        {
            session.Outputs.Decrypted();
        }

        // PDF-ENCRYPTED-INFO-TEXT: document information beyond Latin-1 set in this batch survives
        // only an encryption applied to a reopened copy.
        EncryptDocumentOp? encryptCopy = batch.Ops.Any(static op => op is SetPropertiesOp)
            && PdfOutputEncryption.InformationBeyondLatin1(loaded.Document) ? model.Encryption : null;
        PdfNavigationCensus navigation = PdfNavigationCensus.Degraded(
            navigationBefore, PdfNavigationCensus.Unresolved(loaded.Document));
        Publication publication;
        // --encrypt replaces whatever encryption the operations leave.
        try { publication = Publish(session, loaded.Document, request, request.EncryptPassword ?? model.OutputPassword, encryptCopy, precondition, verifier, applied, state); }
        finally { operationInputs.ThrowIfFailed(); }
        List<Warning> warnings = BuildWarnings(signatures, applied, model.TextMoved);
        if (MetadataRewritten(batch.Ops, applied.Outcomes) is { } rewritten)
        {
            warnings.Add(rewritten);
        }

        if (UnpermittedChange(userPermissions, openPassword, applied.Outcomes, request.Options.DryRun, request.EncryptPassword is not null) is { } protection)
        {
            warnings.Add(protection);
        }

        if (navigation.ToWarning(
                "no longer lead to a page: they targeted deleted pages, or moved pages at a position with a coordinate of 0, which the SDK cannot tell apart from an omitted one",
                "Re-create the affected bookmarks (add_bookmark) and links (add_hyperlink) after the page change, or reorder pages before adding navigation.")
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
            Checked = request.Options.Checked,
            Applied = applied.Outcomes,
            Backup = publication.Backup,
            PagesTouched = applied.Pages.Count == 0 ? null : [.. applied.Pages],
            Verification = publication.Verification,
            License = EnvelopeParts.License(state),
            Warnings = warnings.Count == 0 ? null : warnings,
        };
    }

    private static Publication Publish(
        PdfSession session,
        Document document,
        PdfEditRequest request,
        Secret? outputPassword,
        EncryptDocumentOp? encryptCopy,
        FileWritePrecondition precondition,
        PdfEditVerifier? verifier,
        AppliedBatch applied,
        LicenseState state)
    {
        OutputInfo? output = null;
        BackupInfo? backup = null;
        PdfEditVerification? verification = null;
        if (!request.Options.DryRun)
        {
            using OutputSet<Document> transaction = session.Outputs.BeginSet([request.Output.Directory], "pdf-edit");
            RemoveUnreferencedObjects(document);
            PdfOutputPasswords? passwords = PdfOutputEncryption.Requested(request.EncryptPassword);
            bool encrypted = PdfOutputEncryption.Encrypted(document, passwords);
            StagedOutput write = transaction.Stage(
                request.Output.Path,
                request.Output.Overwrite,
                document,
                temp =>
                {
                    if (passwords is not null)
                    {
                        PdfOutputEncryption.Save(document, temp, passwords);
                    }
                    else
                    {
                        Save(document, temp, encryptCopy, request.OpSecrets);
                    }

                    using LoadedPdf reopened = session.Loader.OpenPublishedCandidate(temp, outputPassword);
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
                    using LoadedPdf reopened = session.Loader.OpenPublishedCandidate(candidate, outputPassword);
                    VerifiedBatch verified = applied.Verified(verifier.Verify(reopened.Document, state, session.Budgets.Deadline));
                    return new PdfEditVerification { Ok = verified.Ok, Issues = verified.Issues, CheckedOps = verified.CheckedOps };
                });
            }

            transaction.Commit();
            output = FileObjects.Output(write.Published, request.Output.Format, encrypted);
        }

        return new Publication(output, backup, verification);
    }

    /// <summary>
    /// The engine writes every object the input held, including those an operation stopped
    /// referencing: a flattened field with its name and value, a deleted attachment's content.
    /// Removing them keeps what an edit took out of the document out of the file's bytes.
    /// </summary>
    private static void RemoveUnreferencedObjects(Document document) =>
        document.OptimizeResources(new Aspose.Pdf.Optimization.OptimizationOptions
        {
            RemoveUnusedObjects = true,
            RemoveUnusedStreams = false,
            LinkDuplicateStreams = false,
            AllowReusePageContent = false,
            CompressObjects = false,
            CompressAllContentStreams = false,
            UnembedFonts = false,
            SubsetFonts = false,
            RemovePrivateInfo = false,
        });

    /// <summary>
    /// Saves the edited document. PDF-ENCRYPTED-INFO-TEXT: with an encryption to apply to a
    /// copy, the document is saved without encryption, reopened and encrypted, so the document
    /// information the batch set is written as read from a file.
    /// </summary>
    private static void Save(Document document, string path, EncryptDocumentOp? encryptCopy, IReadOnlyDictionary<string, Secret>? secrets)
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

    /// <summary>
    /// Aspose.PDF writes the creator, producer and modification date into every PDF it saves
    /// (<see cref="PdfSnapshots.SaveStamped"/>), so an operation that removed the document
    /// information leaves them in the output; its targets leave them out, and this says why they
    /// remain.
    /// </summary>
    private static Warning? MetadataRewritten(IReadOnlyList<PdfOp> ops, IReadOnlyList<BoundedOperationOutcome> outcomes)
    {
        HashSet<string> applied = [.. outcomes.Where(static outcome => outcome.Status == OpStatuses.Ok).Select(static outcome => outcome.Id)];
        string[] ids = [.. ops
            .Where(op => op.Id is { } id && applied.Contains(id) && op switch
            {
                ClearPropertiesOp remove => remove.DocumentInfo,
                _ => false,
            })
            .Select(static op => $"'{op.Id}'")];
        return ids.Length == 0
            ? null
            : new Warning(PdfDiagnostics.MetadataRewritten, $"Aspose.PDF writes the creator, producer and modificationDate document information into every PDF it saves, "
                + $"so the output holds them although {(ids.Length == 1 ? "operation " : "operations ")}{string.Join(", ", ids)} removed or set them.")
            {
                Hint = "They name the PDF library and the time of this save, and no operation removes them; the operation's targets name only the entries the output changed.",
            };
    }

    private static List<Warning> BuildWarnings(
        bool signatures,
        AppliedBatch applied,
        IReadOnlyList<string> textMoved)
    {
        var warnings = new List<Warning>();
        if (signatures && applied.Outcomes.Any(static item => item.Status == OpStatuses.Ok && item.ItemsAffected > 0))
        {
            warnings.Add(new Warning(WarningCodes.SignatureInvalidated, "Editing a signed PDF invalidates or changes its existing signature state.")
            {
                Hint = "Validate signatures again and apply any required signature only after the final edit.",
            });
        }

        // An operation that changed nothing, such as a redaction that matched nothing, passes its
        // read-back check without having changed anything, so it is disclosed.
        warnings.AddRange(applied.Warnings);

        // PDF-REDACT-TEXT-SHIFT: the text is still in the file, but part of it may now be hidden.
        if (textMoved.Count > 0)
        {
            warnings.Add(new Warning(PdfDiagnostics.RedactionTextMoved, (textMoved.Count == 1 ? "Operation " : "Operations ") + string.Join(", ", textMoved)
                    + " moved the text that followed what was removed on its line to the left, so part of it may now lie under the cover.")
            {
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
        Permissions? permissions, bool openPassword, IReadOnlyCollection<BoundedOperationOutcome> outcomes, bool dryRun, bool encryptOption)
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

        string? encryption = encryptOption ? "--encrypt" : applied.LastOrDefault(static op => op is "encrypt_document" or "decrypt_document");
        string output = encryption switch
        {
            "--encrypt" => $"The output {(dryRun ? "would open" : "opens")} only with the --encrypt password, which grants every permission.",
            "decrypt_document" => $"The output {(dryRun ? "would not be encrypted, so it would no longer restrict" : "is not encrypted, so it no longer restricts")} anyone.",
            "encrypt_document" => $"The output {(dryRun ? "would be" : "is")} encrypted with the passwords and permissions the encrypt operation set.",
            _ => $"The output {(dryRun ? "would keep" : "keeps")} the input's encryption and permissions; edit with the owner password to change them.",
        };
        string opened = openPassword
            ? "The input was opened with its user password, whose permissions"
            : "The input has no open password and was opened without its owner password, so its reader permissions apply; they";
        return new Warning(WarningCodes.ProtectionNotEnforced, $"{opened} do not allow {string.Join(", ", changes)}; the engine does not enforce them, so "
                + (dryRun ? "the batch would change it if it were not a dry run." : "the batch changed it."))
        {
            Hint = $"Confirm that the document's owner authorized the change. {output}",
        };
    }

    private static bool Permitted(string op, Permissions granted) => op switch
    {
        "encrypt_document" or "decrypt_document" => false,
        "set_form_field" => (granted & (Permissions.ModifyContent | Permissions.FillForm | Permissions.ModifyTextAnnotations)) != 0,
        "rotate_pages" or "delete_pages" or "move_pages" or "import_pages" or "insert_page" or "add_bookmark" =>
            (granted & (Permissions.ModifyContent | Permissions.AssembleDocument)) != 0,
        _ => granted.HasFlag(Permissions.ModifyContent),
    };

    private sealed record Publication(
        OutputInfo? Output,
        BackupInfo? Backup,
        PdfEditVerification? Verification);
}
