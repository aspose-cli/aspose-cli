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
internal sealed class WordsMutationService
{
    private readonly ILicenseGate _licenseGate;
    private readonly SafeFileWriter _writer;
    private readonly WordsDocumentLoader _loader;
    private readonly InputSource _inputs;

    internal WordsMutationService(
        ILicenseGate licenseGate,
        SafeFileWriter writer,
        WordsDocumentLoader loader,
        InputSource inputs)
    {
        _licenseGate = licenseGate ?? throw new ArgumentNullException(nameof(licenseGate));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _inputs = inputs ?? throw new ArgumentNullException(nameof(inputs));
    }

    /// <summary>Applies a validated operation batch and commits it atomically.</summary>
    internal WordsEditResult ApplyOps(string filePath, WordsOpsBatch batch, WordsEditRequest request)
    {
        batch = WordsOp.Catalog.Prepare(batch);
        LicenseState state = _licenseGate.EnsureApplied();
        FileWritePrecondition precondition = FileWritePrecondition.Capture(filePath);
        using LoadedDocument loaded = _loader.Open(filePath, request.Password);
        bool inputHadRevisions = loaded.Document.Revisions.Count > 0;
        bool inputWasSigned = loaded.Format.HasDigitalSignature;
        ProtectionType inputProtection = loaded.Document.ProtectionType;
        using InputResourceScope operationInputs = _inputs.CreateScope();
        ValidateRequest(request, batch);
        FormatDescriptor written = request.Output.Keeping(loaded.FormatId);
        string format = written.Id;
        Secret? outputPassword = request.EncryptPassword
            ?? (loaded.Format.IsEncrypted && written.Protectable ? request.Password : null);
        SaveOptions saveOptions = WordsSavePipeline.Options(format, outputPassword);
        if (request.Verify && !WordsFormats.IsLoad(format))
        {
            throw CliErrors.OptionInvalid("--verify", $"format '{format}' cannot be reopened as a document",
                "Use a reloadable document output when requesting semantic verification.");
        }
        SourceInfo input = InfoProjection.Source(filePath, loaded);
        FileFingerprints.EnsureUnchanged(filePath, precondition.Fingerprint, input.Fingerprint!);
        FileFingerprints.EnsureMatch(filePath, request.Options.IfMatch, input.Fingerprint!);
        Node[] blocksBefore = BodyBlocks(loaded.Document).ToArray();
        var blocks = new DocumentBlockIndex(loaded.Document, loaded.Evaluation);
        IReadOnlyList<ResolvedWordsOp> resolved = WordsAnchorResolver.Resolve(loaded.Document, blocks, batch);
        Document? baseline = request.Verify ? loaded.Document.Clone() : null;
        WordsRevisionTracking? tracking = request.TrackChanges
            ? new WordsRevisionTracking(loaded.Document, request.Author!)
            : null;
        tracking?.Start();
        var operationWarnings = new List<Warning>();
        var changed = new List<Node>();
        var pageFields = new List<Field>();
        IReadOnlyList<BoundedOperationOutcome> outcomes =
            ApplyOperations(loaded, resolved, blocks, request, operationInputs, tracking, operationWarnings, changed, pageFields);
        tracking?.Stop();
        UpdatePageFields(loaded.Document, pageFields);
        IReadOnlyList<int> pagesTouched = TouchedPages(loaded.Document, blocksBefore, resolved, changed);

        _loader.EnsureWithinBudgets(loaded.Document, loaded.Resources);
        WordsSavePipeline.RemoveMacrosUnlessKept(loaded.Document, format);

        (OutputInfo? output, BackupInfo? backup, WordsVerification? verification, string? truncation) =
            Persist(
                loaded.Document,
                request,
                baseline,
                precondition,
                loaded.Resources,
                format,
                saveOptions,
                outputPassword,
                loaded.Evaluation && !loaded.EvaluationInputTruncated && !loaded.ImportedInputTruncated);
        baseline?.Cleanup();
        var outputWarnings = new List<Warning>();
        if (loaded.Format.IsEncrypted && outputPassword is null && !request.Options.DryRun)
        {
            outputWarnings.Add(new Warning
            {
                Code = WordsDiagnostics.EncryptionRemoved,
                Message = $"The '{format}' output cannot retain the source document encryption.",
                Hint = "Use an encryption-capable document output to keep password protection.",
            });
        }

        outputWarnings.AddRange(SaveWarnings(loaded, format));
        if (truncation is not null)
        {
            outputWarnings.Add(new Warning
            {
                Code = WarningCodes.EvalInputTruncated,
                Message = $"Aspose.Words evaluation mode cut the edited document short: {truncation}",
                Hint = "Do not deliver this output as complete; apply a license and retry.",
            });
        }

        return new WordsEditResult
        {
            Input = input,
            Output = output,
            DryRun = request.Options.DryRun,
            Applied = outcomes,
            Backup = backup,
            PagesTouched = pagesTouched.Count == 0 ? null : pagesTouched,
            Verification = verification,
            License = EnvelopeParts.License(state),
            Warnings = EnvelopeParts.CombineWarnings(outputWarnings, EnvelopeParts.BackupWarnings(backup), operationWarnings, PdfInputWarnings(loaded), MutationWarnings(
                state,
                loaded.Document,
                format,
                // The input's revisions are disclosed while the output still contains revisions;
                // LOSSY_CONVERSION covers the formats that cannot store them.
                inputHadRevisions && KeepsRevisions(loaded.Document, format),
                inputWasSigned,
                inputProtection,
                loaded.RemoteResourcesBlocked,
                loaded.EvaluationInputTruncated || loaded.ImportedInputTruncated)),
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
        or AddCommentOp or RemoveCommentsOp or AppendDocumentOp;

    private IReadOnlyList<BoundedOperationOutcome> ApplyOperations(
        LoadedDocument loaded,
        IReadOnlyList<ResolvedWordsOp> resolved,
        DocumentBlockIndex blocks,
        WordsEditRequest request,
        InputResourceScope operationInputs,
        WordsRevisionTracking? tracking,
        List<Warning> warnings,
        List<Node> changed,
        List<Field> pageFields)
    {
        return BoundedOperationRunner.Run(
            WordsOp.Catalog,
            resolved.Select(static item => item.Op).ToArray(),
            request.Options.BestEffort,
            deadline: null,
            (_, index) =>
            {
                // An operation without a block address, such as replace_text, names the
                // original blocks that hold the nodes it changed.
                var nodes = new List<Node>();
                long count = new WordsMutationHandlers(loaded, resolved[index], _loader, _inputs, operationInputs, request.OpSecrets, tracking, warnings, nodes, pageFields).Run();
                changed.AddRange(nodes);
                return new AppliedOperation(
                    count,
                    nodes.Count == 0 ? resolved[index].Targets : WordsAnchorResolver.Targets(blocks, nodes, target: null));
            },
            (_, index) => resolved[index].Targets);
    }

    /// <summary>
    /// Saves, verifies and publishes the edited document. With <paramref name="detectTruncation"/>,
    /// it also returns what evaluation mode cut from the output, or null: Aspose.Words keeps
    /// only the start of a long document it lays out or saves in evaluation mode and ends it
    /// with its truncation notice, in the saved file and in the document itself alike.
    /// </summary>
    private (
        OutputInfo? Output,
        BackupInfo? Backup,
        WordsVerification? Verification,
        string? Truncation) Persist(
        Document document,
        WordsEditRequest request,
        Document? baseline,
        FileWritePrecondition precondition,
        LocalDocumentResourceLoader resources,
        string format,
        SaveOptions saveOptions,
        Secret? outputPassword,
        bool detectTruncation)
    {
        OutputInfo? output = null;
        BackupInfo? backup = null;
        WordsVerification? verification = null;
        string? truncation = null;
        if (!request.Options.DryRun)
        {
            using var transaction = new AtomicOutputSetWriter(_writer, request.Output.Directory, "words-edit");
            StagedOutput write = transaction.Stage(
                request.Output.Path,
                request.Output.Overwrite,
                request.Output.BackupPath,
                precondition,
                temp =>
                {
                    try { document.Save(temp, saveOptions); }
                    finally { resources.ThrowIfFailed(); }
                    if (detectTruncation && WordsEvaluation.IsTruncated(document))
                    {
                        truncation = $"the output keeps only its first {document.Sections.Count} section(s), the last of them "
                            + "incomplete and ending with the engine's truncation notice; everything after that point is missing.";
                    }
                    if (!request.Verify && WordsFormats.IsLoad(format))
                    {
                        using LoadedDocument reopened = _loader.OpenPublishedCandidate(temp, outputPassword);
                    }
                });
            output = new OutputInfo { Path = request.Output.Path, Format = format, SizeBytes = write.SizeBytes };
            backup = write.Backup;

            if (request.Verify)
            {
                var expected = new ExpectedDocumentState(
                    document.Range.Fields.Count,
                    document.Revisions.Count,
                    WordsProtection.ToMode(document.ProtectionType));
                verification = write.Read(
                    candidate => Verify(candidate, format, outputPassword, baseline!, expected, truncation));
            }
            transaction.Commit();
        }
        return (output, backup, verification, truncation);
    }

    /// <summary>
    /// The pages of the edited document that the batch changed: every page of the blocks and
    /// sections the operations addressed, of the nodes they recorded as changed and of the blocks
    /// they inserted, and for each block they removed, the page of the next original block that
    /// remains. A change in a header or footer touches every page of its section.
    /// </summary>
    private static IReadOnlyList<int> TouchedPages(
        Document document,
        IReadOnlyList<Node> blocksBefore,
        IReadOnlyList<ResolvedWordsOp> operations,
        IEnumerable<Node> changed)
    {
        document.UpdatePageLayout();
        var collector = new LayoutCollector(document);
        var before = new HashSet<Node>(blocksBefore);
        var pages = new SortedSet<int>();
        IEnumerable<Node> nodes = operations.SelectMany(static item => item.Nodes.Concat<Node>(item.Sections))
            .Concat(changed)
            .Concat(BodyBlocks(document).Where(block => !before.Contains(block)))
            .Where(node => Attached(document, node))
            .Select(static node => node.GetAncestor(NodeType.HeaderFooter)?.ParentNode ?? node)
            .Distinct();
        foreach (Node node in nodes)
        {
            for (int page = collector.GetStartPageIndex(node); page > 0 && page <= collector.GetEndPageIndex(node); page++)
            {
                pages.Add(page);
            }
        }

        Node? next = null;
        for (int index = blocksBefore.Count - 1; index >= 0; index--)
        {
            if (Attached(document, blocksBefore[index]))
            {
                next = blocksBefore[index];
            }
            else if ((next is null ? document.PageCount : collector.GetStartPageIndex(next)) is > 0 and int page)
            {
                pages.Add(page);
            }
        }

        return pages.ToArray();
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
    private WordsVerification Verify(
        string candidatePath,
        string format,
        Secret? outputPassword,
        Document baseline,
        ExpectedDocumentState expected,
        string? truncation)
    {
        var issues = new List<VerificationIssue>();
        if (truncation is not null)
        {
            issues.Add(VerificationIssues.Of(
                WordsDiagnostics.OutputTruncated,
                $"Evaluation mode cut the edited document short: {truncation}",
                hint: "Apply a license and run the edit again; the output does not hold the whole result."));
        }

        using LoadedDocument reopened = _loader.OpenPublishedCandidate(
            candidatePath,
            outputPassword);
        int fieldCount = reopened.Document.Range.Fields.Count;
        int revisionCount = reopened.Document.Revisions.Count;
        string protection = WordsProtection.ToMode(reopened.Document.ProtectionType);
        if (fieldCount != expected.FieldCount)
        {
            issues.Add(VerificationIssues.Of(
                WordsDiagnostics.FieldCountChanged,
                $"Field count changed during save/reopen: expected {expected.FieldCount}, found {fieldCount}.",
                hint: KeepStateHint("fields", format, WordsFormats.WordIds)));
        }

        if (revisionCount != expected.RevisionCount)
        {
            issues.Add(VerificationIssues.Of(
                WordsDiagnostics.RevisionCountChanged,
                $"Revision count changed during save/reopen: expected {expected.RevisionCount}, found {revisionCount}.",
                hint: KeepStateHint("tracked revisions", format, WordsFormats.RevisionIds)));
        }

        if (!string.Equals(protection, expected.Protection, StringComparison.Ordinal))
        {
            issues.Add(VerificationIssues.Of(
                WordsDiagnostics.ProtectionChanged,
                $"Protection changed during save/reopen: expected {expected.Protection}, found {protection}.",
                hint: KeepStateHint("protection", format, WordsFormats.WordIds)));
        }

        Document comparisonBaseline = baseline.Clone();
        Document comparisonOutput = reopened.Document.Clone();
        comparisonBaseline.AcceptAllRevisions();
        comparisonOutput.AcceptAllRevisions();
        comparisonBaseline.Compare(
            comparisonOutput,
            "Aspose CLI",
            new DateTime(2000, 1, 1));
        bool semanticChanges = comparisonBaseline.Revisions.Count > 0;
        comparisonBaseline.Cleanup();
        comparisonOutput.Cleanup();

        return new WordsVerification
        {
            Ok = issues.Count == 0,
            Issues = issues,
            SemanticChangesDetected = semanticChanges,
            FieldCount = fieldCount,
            RevisionCount = revisionCount,
            Protection = protection,
        };
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
        LicenseState state,
        Document document,
        string format,
        bool revisionsKept,
        bool inputWasSigned,
        ProtectionType inputProtection,
        int remoteResourcesBlocked,
        bool evaluationInputTruncated)
    {
        var extra = new List<Warning>();
        if (revisionsKept)
        {
            extra.Add(TrackedChangesPresent);
        }

        if (inputWasSigned)
        {
            extra.Add(new Warning { Code = WarningCodes.SignatureInvalidated, Message = "Editing invalidates the document's digital signature.", Hint = "Re-sign the produced document after review." });
        }

        if (inputProtection != ProtectionType.NoProtection)
        {
            // Editing restrictions guide Word's UI; they are not encryption and do not bind the SDK.
            extra.Add(new Warning
            {
                Code = WarningCodes.ProtectionNotEnforced,
                Message = $"The input has {WordsProtection.ToMode(inputProtection)} editing restrictions; the edit was applied through them.",
                // LOSSY_CONVERSION discloses restrictions a non-Word output cannot keep.
                Hint = WordsFormats.IsWord(format)
                    ? "Confirm the change is authorized. The output keeps the restrictions unless the batch changed them with protect or unprotect."
                    : "Confirm the change is authorized.",
            });
        }

        extra.AddRange(ConversionWarnings(document, format));

        if (LocalDocumentResourceLoader.OmissionWarning(remoteResourcesBlocked) is { } omitted)
        {
            extra.Add(omitted);
        }

        if (evaluationInputTruncated)
        {
            extra.Add(EvaluationTruncated);
        }

        return EnvelopeParts.CombineWarnings(EnvelopeParts.OutputWarnings(state), extra);
    }

    private sealed record ExpectedDocumentState(int FieldCount, int RevisionCount, string Protection);
}
