using System.Diagnostics;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;

namespace Aspose.Cli.Sdk.IO;

/// <summary>Commits extraction outputs or restores every enrolled target in reverse order.</summary>
internal sealed class ExtractionTransaction
{
    private readonly bool _workerStagingOnly;
    private readonly string _workingRoot;
    private readonly IPublicationFaultInjector _faults;
    private readonly IReadOnlyList<string> _createdDirectories;
    private readonly List<CreatedFile> _created = [];
    private readonly List<ReplacedFile> _replaced = [];
    private readonly List<WorkerExtractedFile> _workerOutputs = [];
    private readonly List<string> _workerDirectories = [];
    private bool _committed;
    private bool _rolledBack;

    public ExtractionTransaction(
        bool workerStagingOnly,
        string workingRoot,
        IPublicationFaultInjector faults,
        IReadOnlyList<string> createdDirectories)
    {
        _workerStagingOnly = workerStagingOnly;
        _workingRoot = workingRoot;
        _faults = faults;
        _createdDirectories = createdDirectories;
    }

    public bool HasPublishedOutputs =>
        _workerStagingOnly
            ? _workerOutputs.Count > 0
            : _created.Count > 0 || _replaced.Count > 0;

    public void EnrollCreated(string target, FilePublicationSnapshot staged) =>
        _created.Add(new CreatedFile(target, staged));

    public void EnrollReplaced(
        string target,
        string backup,
        FilePublicationSnapshot backupSnapshot,
        FilePublicationSnapshot original,
        FilePublicationSnapshot staged) =>
        _replaced.Add(new ReplacedFile(
            target,
            backup,
            backupSnapshot,
            original,
            staged));

    public void EnrollWorkerFile(
        string target,
        string workingPath,
        bool overwrite,
        FilePublicationSnapshot original,
        FilePublicationSnapshot staged) =>
        _workerOutputs.Add(
            new WorkerExtractedFile(
                target,
                workingPath,
                overwrite,
                original,
                staged));

    public void EnrollWorkerDirectory(string target)
    {
        _workerDirectories.Add(target);
        WorkerOutputSession.RegisterDirectory(target);
    }

    public void Commit()
    {
        _committed = true;
        if (_workerStagingOnly)
        {
            foreach (string directory in _workerDirectories)
            {
                WorkerOutputSession.RegisterDirectory(directory);
            }
            return;
        }
        foreach (ReplacedFile replaced in _replaced)
        {
            DeleteForCleanup(replaced.Backup, replaced.BackupSnapshot);
        }
    }

    public PublicationRecoveryReport RollBack()
    {
        if (_workerStagingOnly)
        {
            return RollBackWorker();
        }

        var items = new List<PublicationRecoveryItem>();
        RollBackCreated(items);
        RollBackReplaced(items);
        RollBackDirectories(items);
        items.Reverse();
        bool complete = items.All(static item =>
            item.Status is "restored" or "unchanged"
            && item.ContentVerified
            && item.MetadataVerified);
        _rolledBack = true;
        return new PublicationRecoveryReport(complete, items);
    }

    public void DisposeIncomplete()
    {
        if (_committed || _rolledBack)
        {
            return;
        }
        PublicationRecoveryReport recovery = RollBack();
        if (!recovery.RecoveryComplete)
        {
            throw CliErrors.OutputPublicationFailure(
                new IOException(
                    _workerStagingOnly
                        ? "Worker extraction ended before commit."
                        : "Extraction ended before commit."),
                recovery);
        }
    }

    internal static void DeleteForCleanup(
        string path,
        FilePublicationSnapshot expected)
    {
        try
        {
            if (!FilePublicationOwnedDelete.TryDelete(path, expected))
            {
                Trace.TraceWarning(
                    "Extraction cleanup preserved changed file '{0}'.",
                    path);
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning(
                "Extraction cleanup failed for '{0}' ({1}).",
                path,
                exception.GetType().Name);
        }
    }

    private void RollBackCreated(ICollection<PublicationRecoveryItem> items)
    {
        foreach (CreatedFile created in _created.AsEnumerable().Reverse())
        {
            try
            {
                FilePublicationSnapshot current =
                    FilePublicationSnapshot.Capture(created.Target);
                if (!current.Exists)
                {
                    items.Add(Recovered(created.Target, originalExisted: false));
                }
                else if (created.Staged.VersionEquals(current))
                {
                    _faults.Hit(new PublicationFaultPoint(
                        PublicationFaultKind.Rollback,
                        items.Count,
                        created.Target));
                    if (FilePublicationOwnedDelete.TryDelete(
                            created.Target,
                            current))
                    {
                        items.Add(Recovered(
                            created.Target,
                            originalExisted: false));
                    }
                    else
                    {
                        items.Add(Unknown(
                            created.Target,
                            originalExisted: false,
                            "ownership-changed"));
                    }
                }
                else
                {
                    items.Add(Unknown(
                        created.Target,
                        originalExisted: false,
                        "content-mismatch"));
                }
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                items.Add(Unknown(
                    created.Target,
                    originalExisted: false,
                    exception.GetType().Name));
            }
        }
    }

    private void RollBackReplaced(ICollection<PublicationRecoveryItem> items)
    {
        foreach (ReplacedFile replaced in _replaced.AsEnumerable().Reverse())
        {
            try
            {
                FilePublicationSnapshot current =
                    FilePublicationSnapshot.Capture(replaced.Target);
                if (!replaced.Original.VersionEquals(current))
                {
                    if (!replaced.Staged.VersionEquals(current))
                    {
                        items.Add(Unknown(
                            replaced.Target,
                            originalExisted: true,
                            "content-mismatch"));
                        continue;
                    }

                    RestoreReplaced(replaced, current, items.Count);
                }

                bool contentVerified =
                    replaced.Original.ContentMatches(replaced.Target);
                bool metadataVerified = replaced.Original.Metadata is null
                    || replaced.Original.Metadata.Matches(replaced.Target);
                if (contentVerified && metadataVerified)
                {
                    DeleteForCleanup(
                        replaced.Backup,
                        replaced.BackupSnapshot);
                }
                items.Add(new PublicationRecoveryItem(
                    replaced.Target,
                    OriginalExisted: true,
                    Published: true,
                    contentVerified && metadataVerified ? "restored" : "unknown",
                    contentVerified,
                    metadataVerified,
                    contentVerified && metadataVerified
                        ? null
                        : "verification-failed"));
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                items.Add(Unknown(
                    replaced.Target,
                    originalExisted: true,
                    exception.GetType().Name));
            }
        }
    }

    private void RestoreReplaced(
        ReplacedFile replaced,
        FilePublicationSnapshot publishedTarget,
        int index)
    {
        _faults.Hit(new PublicationFaultPoint(
            PublicationFaultKind.Rollback,
            index,
            replaced.Target));
        if (!replaced.Original.ContentMatches(replaced.Backup))
        {
            throw new IOException("Backup verification failed.");
        }

        _faults.Hit(new PublicationFaultPoint(
            PublicationFaultKind.Metadata,
            index,
            replaced.Target));
        FilePublicationAtomicSwap.RestoreFromBackup(
            replaced.Backup,
            replaced.Target,
            publishedTarget,
            replaced.Original);
    }

    private void RollBackDirectories(ICollection<PublicationRecoveryItem> items)
    {
        foreach (string directory in _createdDirectories
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderByDescending(static path => path.Length))
        {
            try
            {
                Directory.Delete(directory, recursive: false);
            }
            catch (Exception exception) when (
                exception is IOException
                    or UnauthorizedAccessException
                    or DirectoryNotFoundException)
            {
                items.Add(Unknown(
                    directory,
                    originalExisted: false,
                    exception.GetType().Name));
            }
        }
    }

    private PublicationRecoveryReport RollBackWorker()
    {
        var items = new List<PublicationRecoveryItem>();
        foreach (WorkerExtractedFile output
            in _workerOutputs.AsEnumerable().Reverse())
        {
            try
            {
                if (FilePublicationOwnedDelete.TryDelete(
                        output.WorkingPath,
                        output.Staged))
                {
                    items.Add(Recovered(output.Target, output.Original.Exists));
                }
                else
                {
                    items.Add(Unknown(
                        output.Target,
                        output.Original.Exists,
                        "ownership-changed"));
                }
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                items.Add(Unknown(
                    output.Target,
                    output.Original.Exists,
                    exception.GetType().Name));
            }
        }

        DeleteWorkerDirectories(items);

        items.Reverse();
        bool complete = items.All(static item =>
            item.Status == "restored"
            && item.ContentVerified
            && item.MetadataVerified);
        _rolledBack = true;
        return new PublicationRecoveryReport(complete, items);
    }

    private void DeleteWorkerDirectories(ICollection<PublicationRecoveryItem> items)
    {
        IEnumerable<string> directories = _createdDirectories
            .Append(_workingRoot)
            .Distinct(OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal)
            .OrderByDescending(static path => path.Length);
        foreach (string directory in directories)
        {
            try
            {
                if (File.GetAttributes(directory).HasFlag(FileAttributes.ReparsePoint))
                {
                    throw new IOException("Worker cleanup refuses a reparse point.");
                }
                Directory.Delete(directory, recursive: false);
            }
            catch (DirectoryNotFoundException)
            {
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                items.Add(Unknown(
                    directory,
                    originalExisted: false,
                    exception.GetType().Name));
            }
        }
    }

    private static PublicationRecoveryItem Recovered(
        string target,
        bool originalExisted) =>
        new(
            target,
            originalExisted,
            Published: true,
            Status: "restored",
            ContentVerified: true,
            MetadataVerified: true,
            Failure: null);

    private static PublicationRecoveryItem Unknown(
        string target,
        bool originalExisted,
        string failure) =>
        new(
            target,
            originalExisted,
            Published: true,
            Status: "unknown",
            ContentVerified: false,
            MetadataVerified: false,
            Failure: failure);

    private sealed record CreatedFile(
        string Target,
        FilePublicationSnapshot Staged);

    private sealed record ReplacedFile(
        string Target,
        string Backup,
        FilePublicationSnapshot BackupSnapshot,
        FilePublicationSnapshot Original,
        FilePublicationSnapshot Staged);

    private sealed record WorkerExtractedFile(
        string Target,
        string WorkingPath,
        bool Overwrite,
        FilePublicationSnapshot Original,
        FilePublicationSnapshot Staged);
}
