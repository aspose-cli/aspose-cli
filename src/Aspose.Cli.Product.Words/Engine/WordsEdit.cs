using System.Collections.Frozen;
using Aspose.Cli.Product.Words.Engine.Editing;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Words;
using Aspose.Words.Fields;
using Aspose.Words.Layout;
using Aspose.Words.Saving;
using static Aspose.Cli.Product.Words.Engine.WordsEngineSupport;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>
/// Applies one Words operation batch: opens the document, resolves every anchor before the
/// first operation runs, runs the handlers, and commits the result through the atomic writer
/// with optional save-and-reopen verification.
/// </summary>
internal static class WordsEdit
{
    /// <summary>The operations whose effect is a field, protection or revision count the verification compares.</summary>
    private static readonly FrozenSet<string> StateOps = FrozenSet.Create(
        StringComparer.Ordinal,
        "insert_toc", "insert_hyperlink", "insert_field", "set_page_numbers", "refresh_fields", "mail_merge",
        "protect_document", "unprotect_document", "accept_revisions", "reject_revisions");

    /// <summary>Applies a validated operation batch and commits it atomically.</summary>
    internal static WordsEditResult Run(WordsSession session, WordsEditRequest request)
    {
        WordsOpsBatch batch = WordsOp.Catalog.Prepare(request.Batch);
        LicenseState state = session.Outputs.License;
        FileWritePrecondition precondition = FileWritePrecondition.Capture(request.Input);
        using LoadedDocument loaded = session.Loader.Open(request.Input, request.Password);
        bool inputHadRevisions = loaded.Document.Revisions.Count > 0;
        bool inputWasSigned = loaded.Format.HasDigitalSignature;
        ProtectionType inputProtection = loaded.Document.ProtectionType;
        using InputResourceScope operationInputs = session.Budgets.Inputs.CreateScope();
        ValidateRequest(request, batch);
        FormatDescriptor written = request.Output.Keeping(loaded.FormatId);
        string format = written.Id;
        Secret? outputPassword = request.EncryptPassword
            ?? (loaded.Format.IsEncrypted && written.Protectable ? request.Password : null);
        SaveOptions saveOptions = WordsSavePipeline.Options(format, outputPassword);
        InputInfo input = InfoProjection.Source(request.Input, loaded);
        FileFingerprints.EnsureUnchanged(request.Input, precondition.Fingerprint, input.Fingerprint);
        FileFingerprints.EnsureMatch(request.Input, request.Options.IfMatch, input.Fingerprint);
        Node[] blocksBefore = BodyBlocks(loaded.Document).ToArray();
        var blocks = new DocumentBlockIndex(loaded.Document, loaded.Evaluation);
        IReadOnlyList<ResolvedWordsOp> resolved = WordsAnchorResolver.Resolve(loaded.Document, blocks, batch);
        Document? baseline = request.Verify ? loaded.Document.Clone() : null;
        WordsRevisionTracking? tracking = request.TrackChanges
            ? new WordsRevisionTracking(loaded.Document, request.Author!)
            : null;
        tracking?.Start();
        var operationWarnings = new List<Warning>();
        var pageFields = new List<Field>();
        AppliedBatch applied = ApplyOperations(session, loaded, resolved, request, operationInputs, tracking, operationWarnings, pageFields);
        IReadOnlyList<BoundedOperationOutcome> outcomes = applied.Outcomes;
        tracking?.Stop();
        UpdatePageFields(loaded.Document, pageFields);
        IReadOnlyList<int> pagesTouched = TouchedPages(loaded.Document, blocksBefore, applied.Changed);

        session.Loader.EnsureWithinBudgets(loaded.Document, loaded.Resources);
        WordsSavePipeline.RemoveMacrosUnlessKept(loaded.Document, format);

        (OutputInfo? output, BackupInfo? backup, WordsEditVerification? verification) =
            Persist(
                session,
                loaded.Document,
                request,
                baseline,
                VerifiedOps(outcomes, request.TrackChanges),
                precondition,
                loaded.Resources,
                written,
                saveOptions,
                outputPassword);
        baseline?.Cleanup();
        var outputWarnings = new List<Warning>();
        outputWarnings.AddRange(SaveWarnings(loaded, format));

        return new WordsEditResult
        {
            Input = input,
            Output = output,
            DryRun = request.Options.DryRun,
            Checked = request.Options.Checked,
            Applied = outcomes,
            Backup = backup,
            PagesTouched = pagesTouched.Count == 0 ? null : pagesTouched,
            Verification = verification,
            License = EnvelopeParts.License(state),
            Warnings = EnvelopeParts.CombineWarnings(outputWarnings, EnvelopeParts.BackupWarnings(backup), operationWarnings, PdfInputWarnings(loaded), MutationWarnings(
                loaded.Document,
                format,
                // The input's revisions are disclosed while the output still contains revisions;
                // LOSSY_CONVERSION covers the formats that cannot store them.
                inputHadRevisions && KeepsRevisions(loaded.Document, format),
                inputWasSigned,
                inputProtection,
                loaded.RemoteResourcesBlocked)),
        };
    }

    private static void ValidateRequest(WordsEditRequest request, WordsOpsBatch batch)
    {
        if (!request.TrackChanges)
        {
            return;
        }

        string[] untracked = batch.Ops
            .Where(static op => !IsTrackable(op))
            .Select(static op => WordsOp.Catalog.NameOf(op))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (untracked.Length > 0)
        {
            throw CliErrors.OptionInvalid(
                "--track-changes",
                $"{string.Join(", ", untracked)} cannot be recorded as tracked changes",
                "Apply these operations in a separate batch without --track-changes; only content insertions and deletions are tracked.");
        }
    }

    /// <summary>
    /// Whether Word can record the operation as tracked changes. Aspose.Words tracks the
    /// insertion and deletion of content only; formatting, styles, lists, page setup,
    /// properties, protection, merges, field updates, header replacement and section
    /// structure would change silently, and resolving revisions is not itself an edit. Comments
    /// are review annotations rather than revisions, so their operations apply untracked.
    /// </summary>
    private static bool IsTrackable(WordsOp op) => op is ReplaceTextOp or SetTextOp or InsertParagraphsOp
        or InsertMarkdownOp or DeleteBlocksOp or InsertBreakOp { Kind: "page" } or InsertImageOp or InsertTableOp
        or SetTableCellOp or RepeatTableRowOp or InsertTocOp or InsertBookmarkOp or InsertHyperlinkOp or InsertFieldOp
        or AddCommentOp or DeleteCommentsOp or ImportSectionsOp;

    private static AppliedBatch ApplyOperations(
        WordsSession session,
        LoadedDocument loaded,
        IReadOnlyList<ResolvedWordsOp> resolved,
        WordsEditRequest request,
        InputResourceScope operationInputs,
        WordsRevisionTracking? tracking,
        List<Warning> warnings,
        List<Field> pageFields)
    {
        // The SDK snapshots what each operation can change before and after it, and derives its
        // outcome and the OP_NO_EFFECT of one that changed nothing; the handlers count nothing.
        var model = new WordsEditModel(loaded, resolved, item => new WordsMutationHandlers(
            loaded, item, session.Outputs, session.Loader, session.Budgets.Inputs, operationInputs, request.OpSecrets, tracking, warnings, pageFields));
        AppliedBatch applied = BoundedOperationRunner.Run(
            WordsOp.Catalog,
            resolved.Select(static item => item.Op).ToArray(),
            request.Options.BestEffort,
            deadline: null,
            model);
        warnings.AddRange(applied.Warnings);
        return applied;
    }

    /// <summary>
    /// Saves, verifies and publishes the edited document. Aspose.Words keeps only the start of
    /// a long document it lays out or saves in evaluation mode and ends it with its truncation
    /// notice; the write pipeline finds the notice in the saved output and discloses it.
    /// </summary>
    private static (
        OutputInfo? Output,
        BackupInfo? Backup,
        WordsEditVerification? Verification) Persist(
        WordsSession session,
        Document document,
        WordsEditRequest request,
        Document? baseline,
        IReadOnlyList<string> checkedOps,
        FileWritePrecondition precondition,
        LocalDocumentResourceLoader resources,
        FormatDescriptor written,
        SaveOptions saveOptions,
        Secret? outputPassword)
    {
        string format = written.Id;
        OutputInfo? output = null;
        BackupInfo? backup = null;
        WordsEditVerification? verification = null;
        if (!request.Options.DryRun)
        {
            using OutputSet<Document> transaction = session.Outputs.BeginSet([request.Output.Directory], "words-edit");
            StagedOutput write = transaction.Stage(
                request.Output.Path,
                request.Output.Overwrite,
                document,
                temp =>
                {
                    try { document.Save(temp, saveOptions); }
                    finally { resources.ThrowIfFailed(); }
                    // Every format edit writes is one the product loads (WordsFormats.Editable).
                    if (!request.Verify)
                    {
                        using LoadedDocument reopened = session.Loader.OpenPublishedCandidate(temp, outputPassword);
                    }
                },
                backupPath: request.Output.BackupPath,
                inputPrecondition: precondition,
                encrypted: outputPassword is not null);
            backup = write.Backup;

            if (request.Verify)
            {
                WordsSavedState expected = WordsReadBack.Saved(document);
                verification = write.Read(
                    candidate => Verify(session, candidate, format, outputPassword, baseline!, expected, checkedOps));
            }
            transaction.Commit();
            output = FileObjects.Output(write.Published, written, encrypted: outputPassword is not null);
        }
        return (output, backup, verification);
    }

    /// <summary>
    /// The pages of the output that hold what the batch changed (<see cref="AppliedBatch.Changed"/>):
    /// the pages of each changed block, and for a block it removed, the page of the next original
    /// block that remains; every page of a section whose page setup, headers or footers changed;
    /// every page for the watermark; and the pages of the text in a changed style or a style based
    /// on it. Document properties and the editing restriction are on no page, so a batch that
    /// changed nothing, or only those, touches none.
    /// </summary>
    private static IReadOnlyList<int> TouchedPages(Document document, IReadOnlyList<Node> blocksBefore, IReadOnlyList<ChangedObject> changed)
    {
        var pages = new SortedSet<int>();
        if (changed.Count == 0)
        {
            return [];
        }

        document.UpdatePageLayout();
        var collector = new LayoutCollector(document);
        var styles = new HashSet<string>(StringComparer.Ordinal);
        foreach (ChangedObject item in changed)
        {
            if (item.Identity is Node node && item.InOutput && Attached(document, node))
            {
                AddPages(collector, node, pages);
            }
            else if (item.Identity is Node removed && IndexOf(blocksBefore, removed) is int index and >= 0)
            {
                Node? next = blocksBefore.Skip(index + 1).FirstOrDefault(block => Attached(document, block));
                if ((next is null ? document.PageCount : collector.GetStartPageIndex(next)) is > 0 and int page)
                {
                    pages.Add(page);
                }
            }
            else if (item.Address == WordsSnapshots.WatermarkAddress)
            {
                pages.UnionWith(Enumerable.Range(1, document.PageCount));
            }
            else if (item.Address.StartsWith("style/", StringComparison.Ordinal))
            {
                styles.Add(item.Address);
            }
        }

        if (styles.Count > 0)
        {
            foreach (Node node in document.GetChildNodes(NodeType.Any, true).Where(node => UsesStyle(document, node, styles)))
            {
                AddPages(collector, node.GetAncestor(NodeType.HeaderFooter)?.ParentNode ?? node, pages);
            }
        }

        return pages.ToArray();
    }

    private static void AddPages(LayoutCollector collector, Node node, SortedSet<int> pages)
    {
        for (int page = collector.GetStartPageIndex(node); page > 0 && page <= collector.GetEndPageIndex(node); page++)
        {
            pages.Add(page);
        }
    }

    private static int IndexOf(IReadOnlyList<Node> nodes, Node node)
    {
        for (int index = 0; index < nodes.Count; index++)
        {
            if (ReferenceEquals(nodes[index], node))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>Whether a paragraph, run or table is in one of the styles, by their addresses, or in a style based on one.</summary>
    private static bool UsesStyle(Document document, Node node, IReadOnlySet<string> styles)
    {
        string? name = node switch
        {
            Paragraph paragraph => paragraph.ParagraphFormat.StyleName,
            Run run => run.Font.StyleName,
            Aspose.Words.Tables.Table table => table.StyleName,
            _ => null,
        };
        for (int depth = 0; !string.IsNullOrEmpty(name) && depth < 32; depth++)
        {
            if (styles.Contains(WordsReadBack.StyleAddress(name)))
            {
                return true;
            }

            name = document.Styles[name]?.BaseStyleName;
        }

        return false;
    }

    private static IEnumerable<Node> BodyBlocks(Document document) =>
        document.Sections.Cast<Section>().SelectMany(static section => DocumentBlockIndex.BodyBlocks(section.Body));

    /// <summary>
    /// WORDS-PAGE-FIELD-LAYOUT: fills the page fields insert_field added, which get no result
    /// until the layout is rebuilt, from one layout of the whole batch's result.
    /// </summary>
    private static void UpdatePageFields(Document document, IReadOnlyList<Field> pageFields)
    {
        if (pageFields.Count == 0)
        {
            return;
        }

        document.UpdatePageLayout();
        foreach (Field field in pageFields.Where(field => Attached(document, field.Start)))
        {
            field.Update();
        }
    }

    private static bool Attached(Document document, Node node) =>
        ReferenceEquals(node.GetAncestor(NodeType.Document), document);

    /// <summary>
    /// Reports save-and-reopen evidence for the staged candidate. A comparison that
    /// finds no body change is evidence, not a fault: whether an operation did
    /// anything is already answered, authoritatively, by its own outcome.
    /// </summary>
    private static WordsEditVerification Verify(
        WordsSession session,
        string candidatePath,
        string format,
        Secret? outputPassword,
        Document baseline,
        WordsSavedState expected,
        IReadOnlyList<string> checkedOps)
    {
        var issues = new List<VerificationIssue>();

        using LoadedDocument reopened = session.Loader.OpenPublishedCandidate(candidatePath, outputPassword);
        WordsSavedState found = WordsReadBack.Saved(reopened.Document);
        Compare(WordsDiagnostics.FieldCountChanged, "Field count", expected.FieldCount, found.FieldCount, "fields", WordsFormats.WordIds);
        Compare(WordsDiagnostics.RevisionCountChanged, "Revision count", expected.RevisionCount, found.RevisionCount, "tracked revisions", WordsFormats.RevisionIds);
        Compare(WordsDiagnostics.ProtectionChanged, "Protection", expected.Protection ?? "none", found.Protection ?? "none", "protection", WordsFormats.WordIds);

        Document comparisonBaseline = baseline.Clone();
        Document comparisonOutput = reopened.Document.Clone();
        comparisonBaseline.AcceptAllRevisions();
        comparisonOutput.AcceptAllRevisions();
        comparisonBaseline.Compare(comparisonOutput, "Aspose CLI", new DateTime(2000, 1, 1));
        bool semanticChanges = comparisonBaseline.Revisions.Count > 0;
        comparisonBaseline.Cleanup();
        comparisonOutput.Cleanup();

        return new WordsEditVerification
        {
            Ok = issues.Count == 0,
            Issues = issues,
            CheckedOps = checkedOps,
            SemanticChangesDetected = semanticChanges,
            FieldCount = found.FieldCount,
            RevisionCount = found.RevisionCount,
            Protection = found.Protection,
        };

        void Compare<T>(DiagnosticDescriptor descriptor, string what, T wanted, T kept, string state, IReadOnlyList<string> keeping)
        {
            if (!EqualityComparer<T>.Default.Equals(wanted, kept))
            {
                issues.Add(VerificationIssues.Of(
                    descriptor, $"{what} changed during save/reopen: expected {wanted}, found {kept}.", hint: KeepStateHint(state, format, keeping)));
            }
        }
    }

    /// <summary>
    /// The hint for state lost in <paramref name="format"/>. Advises another format only when
    /// <paramref name="format"/> is not one of <paramref name="keepingFormats"/>; otherwise a
    /// format that can keep the state lost it, and saving to another would not help.
    /// </summary>
    internal static string KeepStateHint(string state, string format, IReadOnlyList<string> keepingFormats) =>
        keepingFormats.Contains(format, StringComparer.Ordinal)
            ? $"The {state} did not survive save and reopen in {format}; check the output with 'aspose-cli words inspect' before relying on it."
            : $"{format} may not keep {state}; save to docx or another Word format and verify again.";

    private static IReadOnlyList<Warning>? MutationWarnings(
        Document document,
        string format,
        bool revisionsKept,
        bool inputWasSigned,
        ProtectionType inputProtection,
        int remoteResourcesBlocked)
    {
        var extra = new List<Warning>();
        if (revisionsKept)
        {
            extra.Add(TrackedChangesPresent);
        }

        if (inputWasSigned)
        {
            extra.Add(new Warning(WarningCodes.SignatureInvalidated, "Editing invalidates the document's digital signature.") { Hint = "Re-sign the produced document after review." });
        }

        if (inputProtection != ProtectionType.NoProtection)
        {
            // Editing restrictions guide Word's UI; they are not encryption and do not bind the SDK.
            extra.Add(new Warning(WarningCodes.ProtectionNotEnforced, $"The input has {WordsProtection.ToMode(inputProtection)} editing restrictions; the edit was applied through them.")
            {
                // LOSSY_CONVERSION discloses restrictions a non-Word output cannot keep.
                Hint = WordsFormats.IsWord(format)
                    ? "Confirm the change is authorized. The output keeps the restrictions unless the batch changed them with protect_document or unprotect_document."
                    : "Confirm the change is authorized.",
            });
        }

        extra.AddRange(ConversionWarnings(document, format));

        if (LocalDocumentResourceLoader.OmissionWarning(remoteResourcesBlocked) is { } omitted)
        {
            extra.Add(omitted);
        }

        return EnvelopeParts.CombineWarnings(extra);
    }

    /// <summary>
    /// The ids, in batch order, of the applied operations whose effect verification reads back:
    /// it compares the field count, the revision count and the protection of the reopened output
    /// with the edited document, so it checks the operations that add, update or remove fields,
    /// set protection or accept and reject revisions, and with tracked changes every operation,
    /// whose revisions it counts.
    /// </summary>
    private static IReadOnlyList<string> VerifiedOps(IReadOnlyList<BoundedOperationOutcome> outcomes, bool trackChanges) =>
    [
        .. outcomes
            .Where(outcome => outcome.Status == OpStatuses.Ok && (trackChanges || StateOps.Contains(outcome.Op)))
            .Select(static outcome => outcome.Id),
    ];
}
