using Aspose.Cli.Product.Pdf.Engine.Editing;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.IO.Output;
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
        // Both set the output's passwords and permissions, and --encrypt-env would replace the
        // operation's without a trace, so the batch is refused before it starts.
        if (request.EncryptPassword is not null && batch.Ops.OfType<EncryptDocumentOp>().FirstOrDefault() is { } encrypt)
        {
            throw CliErrors.OptionInvalid(
                "--encrypt-env",
                $"it encrypts the output and would replace the passwords and permissions that operation {encrypt.Id} (encrypt_document) sets",
                "Remove --encrypt-env (or --encrypt-stdin) to keep the operation's passwords and permissions, or remove the encrypt_document operation to encrypt with --encrypt-env alone.");
        }

        LicenseState state = session.Outputs.License;
        FileWritePrecondition precondition = FileWritePrecondition.Capture(filePath);
        using InputResourceScope operationInputs = session.Budgets.Inputs.CreateScope();
        using LoadedPdf loaded = session.Loader.Open(filePath, request.Password);
        InputInfo input = precondition.Admit("pdf", loaded.Protection, request.Options.IfMatch);
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
        EditPublication<PdfEditVerification> publication;
        // --encrypt replaces whatever encryption the operations leave.
        try { publication = Publish(session, loaded.Document, request, request.EncryptPassword ?? model.OutputPassword, encryptCopy, precondition, model, verifier, applied, state); }
        catch (RedactedTextLeft left)
        {
            publication = new EditPublication<PdfEditVerification>(null, null, new PdfEditVerification
            {
                Ok = false,
                Issues = left.Issues,
                // The output was searched for the text of every redact_text the batch applied.
                CheckedOps = model.Redactions,
            });
        }
        finally { operationInputs.ThrowIfFailed(); }
        List<Warning> warnings = BuildWarnings(signatures, applied, model.TextMoved, model.TextBeside);
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

        warnings.AddRange(publication.BackupWarnings ?? []);

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

    private static EditPublication<PdfEditVerification> Publish(
        PdfSession session,
        Document document,
        PdfEditRequest request,
        Secret? outputPassword,
        EncryptDocumentOp? encryptCopy,
        FileWritePrecondition precondition,
        PdfEditModel model,
        PdfEditVerifier? verifier,
        AppliedBatch applied,
        LicenseState state)
    {
        if (!request.Options.DryRun)
        {
            RemoveUnreferencedObjects(document);
        }

        PdfOutputPasswords? passwords = PdfOutputEncryption.Requested(request.EncryptPassword);
        return session.Outputs.PublishEdit<PdfEditVerification>(
            request.Output,
            document,
            precondition,
            request.Options.DryRun,
            PdfOutputEncryption.Encrypted(document, passwords),
            temp =>
            {
                // The properties as the document holds them before the engine stamps its own on save.
                IReadOnlyDictionary<string, string> properties = PdfRedactionAudit.Properties(document);
                if (passwords is not null)
                {
                    PdfOutputEncryption.Save(document, temp, passwords);
                }
                else
                {
                    Save(document, temp, encryptCopy, request.OpSecrets);
                }

                using LoadedPdf reopened = session.Loader.OpenPublishedCandidate(temp, outputPassword);
                IReadOnlyList<VerificationIssue> left = model.RedactedTextLeft(reopened.Document, properties, state, session.Budgets.Deadline);
                if (left.Count > 0)
                {
                    throw new RedactedTextLeft(left);
                }
            },
            verifier is null ? null : candidate =>
            {
                using LoadedPdf reopened = session.Loader.OpenPublishedCandidate(candidate, outputPassword);
                VerifiedBatch verified = applied.Verified(verifier.Verify(reopened.Document, state, session.Budgets.Deadline));
                return new PdfEditVerification { Ok = verified.Ok, Issues = verified.Issues, CheckedOps = verified.CheckedOps };
            },
            "pdf-edit");
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
        IReadOnlyList<string> textMoved,
        IReadOnlyList<string> textBeside)
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

        if (textBeside.Count > 0)
        {
            warnings.Add(new Warning(PdfDiagnostics.RedactionNearbyTextRemoved,
                "Redaction covers also removed characters of neighbouring lines beside what they selected, not counting spaces: "
                    + string.Join("; ", textBeside) + ". The engine removes the characters of a neighbouring line that a cover overlaps.")
            {
                Hint = "A cover always spans all it selects, so nothing of it shows, also on a scanned image under the text. "
                    + "Render the pages with 'pdf render' to see what else was covered; to keep that text, redact the source document and create the PDF again.",
            });
        }

        return warnings;
    }

    /// <summary>
    /// The edit stops publishing because the reopened output still holds the text of a
    /// <c>redact_text</c>: the staged output is discarded, and the result reports the issues.
    /// </summary>
    private sealed class RedactedTextLeft(IReadOnlyList<VerificationIssue> issues) : Exception("The output still holds redacted text.")
    {
        internal IReadOnlyList<VerificationIssue> Issues { get; } = issues;
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

        string? encryption = encryptOption ? EncryptedOutput.Option : applied.LastOrDefault(static op => op is "encrypt_document" or "decrypt_document");
        string output = encryption switch
        {
            EncryptedOutput.Option => $"The output {(dryRun ? "would open" : "opens")} only with the --encrypt password, which grants every permission.",
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
}
