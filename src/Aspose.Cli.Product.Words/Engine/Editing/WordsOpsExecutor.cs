using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Words;
using Aspose.Words.Layout;
using Aspose.Words.Rendering;
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
        ValidateRequest(request);
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
            ApplyOperations(loaded, resolved, request, loader, inputs);
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
                originalPages,
                baseline,
                outcomes,
                loader,
                precondition,
                loaded.Resources);
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
        };
    }

    private static void ValidateRequest(WordsEditRequest request)
    {
        if (request.TrackChanges && string.IsNullOrWhiteSpace(request.Author))
        {
            throw CliErrors.OptionInvalid(
                "--author",
                "--track-changes requires a non-empty author",
                "Pass --author with the person or agent responsible for the edit.");
        }
    }

    private static IReadOnlyList<BoundedOperationOutcome> ApplyOperations(
        LoadedDocument loaded,
        IReadOnlyList<ResolvedWordsOp> resolved,
        WordsEditRequest request,
        WordsDocumentLoader loader,
        InputSource inputs)
    {
        Document document = loaded.Document;
        var outcomes = new List<BoundedOperationOutcome>(resolved.Count);
        for (int index = 0; index < resolved.Count; index++)
        {
            ResolvedWordsOp item = resolved[index];
            try
            {
                string? secret = null;
                _ = request.OpSecrets?.TryGetValue(index, out secret);
                long affected = item.Op switch
                {
                    InsertMarkdownOp markdown =>
                        WordsContentOpHandlers.InsertMarkdown(document, item.Nodes[0], markdown,
                            loader.OpenMarkdown(markdown.Markdown, loaded)),
                    AppendDocumentOp append =>
                        WordsStructureOpHandlers.AppendDocument(document, append, loader),
                    MailMergeOp merge =>
                        WordsObjectOpHandlers.MailMerge(document, merge, inputs),
                    _ => WordsOpHandlers.Apply(document, item, secret),
                };
                outcomes.Add(new BoundedOperationOutcome
                {
                    Id = item.Op.Id!,
                    Index = index,
                    Op = item.Op.OpName,
                    Status = OpStatuses.Ok,
                    ItemsAffected = affected,
                    Targets = item.Targets,
                });
            }
            catch (Exception exception) when (
                exception is CliException or InvalidOperationException or ArgumentException
                && exception is not CliException { ExitCode: ExitCode.OperationTimeout }
                && (exception is not CliException failure || failure.Code != ErrorCodes.InputBudgetExceeded))
            {
                CliException translated = exception as CliException ?? InvalidAt(index, item.Op.OpName, exception.Message);
                if (!request.Options.BestEffort)
                {
                    throw translated;
                }

                outcomes.Add(new BoundedOperationOutcome
                {
                    Id = item.Op.Id!,
                    Index = index,
                    Op = item.Op.OpName,
                    Status = OpStatuses.Failed,
                    ItemsAffected = 0,
                    Targets = item.Targets,
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

    private static (
        OutputInfo? Output,
        BackupInfo? Backup,
        WordsVerification? Verification) Persist(
        Document document,
        WordsEditRequest request,
        SafeFileWriter writer,
        IReadOnlyList<int> originalPages,
        Document? baseline,
        IReadOnlyList<BoundedOperationOutcome> outcomes,
        WordsDocumentLoader loader,
        FileWritePrecondition precondition,
        LocalDocumentResourceLoader resources)
    {
        OutputInfo? output = null;
        BackupInfo? backup = null;
        WordsVerification? verification = null;
        if (!request.Options.DryRun)
        {
            string format = FormatId(request.OutputPath);
            SaveOptions options = WordsSavePipeline.Options(format, request.EncryptPassword);
            using var transaction = new AtomicOutputSetWriter(writer, Path.GetDirectoryName(request.OutputPath)!, "words-edit");
            StagedOutput write = transaction.Stage(
                request.OutputPath,
                request.Overwrite,
                request.BackupPath,
                precondition,
                temp =>
                {
                    try { document.Save(temp, options); }
                    finally { resources.ThrowIfFailed(); }
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
                verification = write.Read(candidate => Verify(
                    candidate,
                    request,
                    originalPages,
                    baseline!,
                    expected,
                    outcomes.Any(static outcome =>
                        outcome.Status == OpStatuses.Ok && outcome.ItemsAffected > 0),
                    loader,
                    transaction));
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

    private static WordsVerification Verify(
        string candidatePath,
        WordsEditRequest request,
        IReadOnlyList<int> originalPages,
        Document baseline,
        ExpectedDocumentState expected,
        bool expectedChange,
        WordsDocumentLoader loader,
        AtomicOutputSetWriter transaction)
    {
        var issues = new List<string>();
        var renders = new List<PageOutput>();
        using LoadedDocument reopened = loader.OpenPublishedCandidate(
            candidatePath,
            request.EncryptPassword);
        var index = new DocumentBlockIndex(reopened.Document);
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
        if (expectedChange && !semanticChanges)
        {
            issues.Add("The batch reported affected items, but semantic comparison found no persisted change.");
        }

        IReadOnlyList<int> pages = VerificationPages(reopened.Document.PageCount, originalPages);
        foreach (int page in pages)
        {
            string renderPath = $"{request.OutputPath}.verify.p{page}.png";
            PageInfo info = reopened.Document.GetPageInfo(page - 1);
            long width = (long)Math.Ceiling(info.WidthInPoints / 72d * 150);
            long height = (long)Math.Ceiling(info.HeightInPoints / 72d * 150);
            RenderPixelGuard.EnsureFits(width, height, 150);
            var options = (ImageSaveOptions)WordsSavePipeline.Options("png", pages: [page], dpi: 150);
            StagedOutput rendered = transaction.Stage(renderPath, request.OverwriteArtifacts,
                path => reopened.Document.Save(path, options));
            renders.Add(new PageOutput
            {
                Page = page,
                Output = new OutputInfo
                {
                    Path = renderPath,
                    Format = "png",
                    SizeBytes = rendered.SizeBytes,
                },
            });
        }

        return new WordsVerification
        {
            Ok = issues.Count == 0,
            VisualReviewRequired = reopened.Document.PageCount > 20,
            ReadBackBlocks = Enumerable.Range(1, Math.Min(index.Count, 20)).ToArray(),
            Renders = renders,
            Issues = issues,
            SemanticChangesDetected = semanticChanges,
            FieldCount = fieldCount,
            RevisionCount = revisionCount,
            Protection = protection,
        };
    }

    private static IReadOnlyList<int> VerificationPages(int pageCount, IReadOnlyList<int> touched)
    {
        if (pageCount <= 20)
        {
            return Enumerable.Range(1, pageCount).ToArray();
        }

        return touched.SelectMany(static page => new[] { page - 1, page, page + 1 })
            .Append(1)
            .Append(pageCount)
            .Where(page => page >= 1 && page <= pageCount)
            .Distinct()
            .Order()
            .Take(12)
            .ToArray();
    }

    private static string FormatId(string path)
    {
        string extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return extension switch { "xml" => "flatopc", "htm" => "html", _ => extension };
    }

    private static CliException InvalidAt(int index, string op, string reason) => new(
        ErrorCodes.OpsInvalid,
        $"Words op {index} ({op}) failed: {reason}.",
        hint: "Correct the operation and retry the complete atomic batch.");

    private sealed record ExpectedDocumentState(int FieldCount, int RevisionCount, string Protection);
}
