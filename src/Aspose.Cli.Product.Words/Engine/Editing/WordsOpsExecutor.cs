using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Operations;
using Aspose.Words;
using Aspose.Words.Layout;
using Aspose.Words.Saving;

namespace Aspose.Cli.Product.Words.Engine.Editing;

/// <summary>Runs one pre-resolved Words batch and commits it through the atomic writer.</summary>
internal static class WordsOpsExecutor
{
    public static WordsEditResult Apply(
        LoadedDocument loaded,
        string inputPath,
        FileWritePrecondition precondition,
        WordsOpsBatch batch,
        WordsEditRequest request,
        SafeFileWriter writer,
        WordsDocumentLoader loader,
        InputSource inputs)
    {
        using InputResourceScope operationInputs = inputs.CreateScope();
        ValidateRequest(request, batch);
        string format = FormatId(request.OutputPath);
        string? outputPassword = request.EncryptPassword
            ?? (loaded.Format.IsEncrypted && WordsFormats.EncryptIds.Contains(format, StringComparer.Ordinal)
                ? request.Password : null);
        SaveOptions saveOptions = WordsSavePipeline.Options(format, outputPassword);
        if (request.Verify && !WordsFormats.IsLoad(format))
        {
            throw CliErrors.OptionInvalid("--verify", $"format '{format}' cannot be reopened as a document",
                "Use a reloadable document output when requesting semantic verification.");
        }
        SourceInfo input = InfoProjection.Source(inputPath, loaded);
        FileFingerprints.EnsureUnchanged(inputPath, precondition.Fingerprint, input.Fingerprint!);
        FileFingerprints.EnsureMatch(inputPath, request.Options.IfMatch, input.Fingerprint!);
        IReadOnlyList<ResolvedWordsOp> resolved = WordsAnchorResolver.Resolve(loaded.Document, batch);
        IReadOnlyList<int> originalPages = ResolveOriginalPages(loaded.Document, resolved);
        Document? baseline = request.Verify ? loaded.Document.Clone() : null;
        if (request.TrackChanges)
        {
            loaded.Document.StartTrackRevisions(request.Author!, new DateTime(2000, 1, 1));
        }

        IReadOnlyList<BoundedOperationOutcome> outcomes =
            ApplyOperations(loaded, resolved, request, loader, inputs, operationInputs);
        if (request.TrackChanges)
        {
            loaded.Document.StopTrackRevisions();
        }

        loader.EnsureWithinBudgets(loaded.Document, loaded.Resources);

        (OutputInfo? output, BackupInfo? backup, WordsVerification? verification) =
            Persist(
                loaded.Document,
                request,
                writer,
                baseline,
                loader,
                precondition,
                loaded.Resources,
                format,
                saveOptions,
                outputPassword);
        baseline?.Cleanup();
        return new WordsEditResult
        {
            Input = input,
            Output = output,
            DryRun = request.Options.DryRun,
            Applied = outcomes,
            Backup = backup,
            PagesTouched = originalPages.Count == 0 ? null : originalPages,
            Verification = verification,
            Warnings = loaded.Format.IsEncrypted && outputPassword is null && !request.Options.DryRun
                ? [new Warning
                {
                    Code = WordsDiagnostics.EncryptionRemoved,
                    Message = $"The '{format}' output cannot retain the source document encryption.",
                    Hint = "Use an encryption-capable document output to keep password protection.",
                }] : null,
        };
    }

    private static void ValidateRequest(WordsEditRequest request, WordsOpsBatch batch)
    {
        if (!request.TrackChanges)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(request.Author))
        {
            throw CliErrors.OptionInvalid(
                "--author",
                "--track-changes requires a non-empty author",
                "Pass --author with the person or agent responsible for the edit.");
        }

        string[] untracked = batch.Ops
            .Where(static op => !WordsOpRules.IsTrackable(op))
            .Select(static op => WordsOps.Catalog.NameOf(op))
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

    private static IReadOnlyList<BoundedOperationOutcome> ApplyOperations(
        LoadedDocument loaded,
        IReadOnlyList<ResolvedWordsOp> resolved,
        WordsEditRequest request,
        WordsDocumentLoader loader,
        InputSource inputs, InputResourceScope operationInputs)
    {
        Document document = loaded.Document;
        return BoundedOperationRunner.Run(
            WordsOps.Catalog,
            resolved.Select(static item => item.Op).ToArray(),
            request.Options.BestEffort,
            deadline: null,
            (op, index) =>
            {
                ResolvedWordsOp item = resolved[index];
                try
                {
                    WordsAnchorResolver.EnsureAttached(document, item);
                    string? secret = null;
                    _ = request.OpSecrets?.TryGetValue(index, out secret);
                    long affected = op switch
                    {
                        InsertImageOp image => WordsObjectOpHandlers.InsertImage(document, item.Nodes[0], image, operationInputs),
                        InsertMarkdownOp markdown =>
                            WordsContentOpHandlers.InsertMarkdown(document, item.Nodes[0], markdown,
                                loader.OpenMarkdown(markdown.Markdown, loaded)),
                        SetHeaderOp header => WordsStructureOpHandlers.SetHeaderFooter(
                            document, item.Sections, header.Kind, header.Paragraphs,
                            header.Markdown is null ? null : loader.OpenMarkdown(header.Markdown, loaded), isHeader: true),
                        SetFooterOp footer => WordsStructureOpHandlers.SetHeaderFooter(
                            document, item.Sections, footer.Kind, footer.Paragraphs,
                            footer.Markdown is null ? null : loader.OpenMarkdown(footer.Markdown, loaded), isHeader: false),
                        AppendDocumentOp append => WordsStructureOpHandlers.AppendDocument(document, append, loader),
                        MailMergeOp merge => WordsObjectOpHandlers.MailMerge(document, merge, inputs, loader),
                        InsertTableOp table => WordsTableOpHandlers.InsertTable(document, item.Nodes[0], table, loader),
                        AddWatermarkOp watermark => WordsObjectOpHandlers.AddWatermark(document, watermark, inputs, loader.ResourceBudgets),
                        _ => WordsOpHandlers.Apply(document, item, secret),
                    };
                    return new AppliedOperation(affected, item.Targets);
                }
                finally { operationInputs.ThrowIfFailed(); }
            },
            (_, index) => resolved[index].Targets);
    }

    private static (
        OutputInfo? Output,
        BackupInfo? Backup,
        WordsVerification? Verification) Persist(
        Document document,
        WordsEditRequest request,
        SafeFileWriter writer,
        Document? baseline,
        WordsDocumentLoader loader,
        FileWritePrecondition precondition,
        LocalDocumentResourceLoader resources,
        string format,
        SaveOptions saveOptions,
        string? outputPassword)
    {
        OutputInfo? output = null;
        BackupInfo? backup = null;
        WordsVerification? verification = null;
        if (!request.Options.DryRun)
        {
            using var transaction = new AtomicOutputSetWriter(writer, Path.GetDirectoryName(request.OutputPath)!, "words-edit");
            StagedOutput write = transaction.Stage(
                request.OutputPath,
                request.Overwrite,
                request.BackupPath,
                precondition,
                temp =>
                {
                    try { document.Save(temp, saveOptions); }
                    finally { resources.ThrowIfFailed(); }
                    if (!request.Verify && WordsFormats.IsLoad(format))
                    {
                        using LoadedDocument reopened = loader.OpenPublishedCandidate(temp, outputPassword);
                    }
                });
            output = new OutputInfo { Path = request.OutputPath, Format = format, SizeBytes = write.SizeBytes };
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
                var expected = new ExpectedDocumentState(
                    document.Range.Fields.Count,
                    document.Revisions.Count,
                    document.ProtectionType.ToString());
                verification = write.Read(
                    candidate => Verify(candidate, outputPassword, baseline!, expected, loader));
            }
            transaction.Commit();
        }
        return (output, backup, verification);
    }

    private static IReadOnlyList<int> ResolveOriginalPages(Document document, IReadOnlyList<ResolvedWordsOp> operations)
    {
        document.UpdatePageLayout();
        var collector = new LayoutCollector(document);
        return operations.SelectMany(static item => item.Nodes)
            .Where(static node => node.ParentNode is not null)
            .Select(collector.GetStartPageIndex)
            .Where(static page => page > 0)
            .Distinct()
            .Order()
            .ToArray();
    }

    /// <summary>
    /// Reports save-and-reopen evidence for the staged candidate. A comparison that
    /// finds no body change is evidence, not a fault: whether an operation did
    /// anything is already answered, authoritatively, by its own outcome.
    /// </summary>
    private static WordsVerification Verify(
        string candidatePath,
        string? outputPassword,
        Document baseline,
        ExpectedDocumentState expected,
        WordsDocumentLoader loader)
    {
        var issues = new List<string>();
        using LoadedDocument reopened = loader.OpenPublishedCandidate(
            candidatePath,
            outputPassword);
        int fieldCount = reopened.Document.Range.Fields.Count;
        int revisionCount = reopened.Document.Revisions.Count;
        string protection = reopened.Document.ProtectionType.ToString();
        if (fieldCount != expected.FieldCount)
        {
            issues.Add($"Field count changed during save/reopen: expected {expected.FieldCount}, found {fieldCount}.");
        }

        if (revisionCount != expected.RevisionCount)
        {
            issues.Add($"Revision count changed during save/reopen: expected {expected.RevisionCount}, found {revisionCount}.");
        }

        if (!string.Equals(protection, expected.Protection, StringComparison.Ordinal))
        {
            issues.Add($"Protection changed during save/reopen: expected {expected.Protection}, found {protection}.");
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

    private static string FormatId(string path)
    {
        string extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return extension switch { "xml" => "flatopc", "htm" => "html", _ => extension };
    }


    private sealed record ExpectedDocumentState(int FieldCount, int RevisionCount, string Protection);
}
