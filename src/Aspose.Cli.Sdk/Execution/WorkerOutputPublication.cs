using System.Diagnostics;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Sdk.Execution;

internal static class WorkerOutputPublisher
{
    internal static IReadOnlyList<long> Publish(
        WorkerOutputManifest manifest,
        string manifestPath)
    {
        if (manifest.Entries.Count == 0 && manifest.Directories.Count == 0)
        {
            return [];
        }
        if (manifest.Entries.Count > 0
            && manifest.Entries.All(static entry =>
                entry.State == WorkerPublicationState.Published))
        {
            Verify(manifest);
            return manifest.Entries
                .Select(static entry => entry.StagedSnapshot.Length)
                .ToArray();
        }

        try
        {
            CreateDirectories(manifest, manifestPath);
            PublishTransaction(manifest, manifestPath);
            Verify(manifest);
            return manifest.Entries.Select(static entry => entry.StagedSnapshot.Length).ToArray();
        }
        catch (Exception failure)
        {
            PublicationRecoveryReport recovery =
                WorkerOutputRecovery.Restore(manifest);
            TryWriteManifest(manifestPath, manifest);
            CleanDirectories(manifest.Directories);
            throw CliErrors.OutputPublicationFailure(failure, recovery);
        }
    }

    private static void CreateDirectories(
        WorkerOutputManifest manifest,
        string manifestPath)
    {
        foreach (WorkerDirectoryEntry directory in manifest.Directories.OrderBy(static entry => entry.Target.Length))
        {
            bool exists = Directory.Exists(directory.Target);
            if (exists != directory.Existed || File.Exists(directory.Target))
            {
                throw new IOException(
                    $"Worker output directory changed before publication: '{directory.Target}'.");
            }
            if (exists)
            {
                ExtractionPathValidator.EnsureNoLinks(directory.Target);
                continue;
            }

            Directory.CreateDirectory(directory.Target);
            ExtractionPathValidator.EnsureNoLinks(directory.Target);
            directory.CreatedIdentity =
                FilePublicationOwnedDelete.TryGetDirectoryIdentity(
                    directory.Target);
            if (OperatingSystem.IsWindows()
                && directory.CreatedIdentity is null)
            {
                throw new IOException(
                    $"Worker output directory identity could not be verified: '{directory.Target}'.");
            }
            WorkerOutputSession.WriteManifest(manifestPath, manifest);
        }
    }

    private static void EnsureOriginalUnchanged(WorkerOutputEntry entry, FilePublicationSnapshot current)
    {
        if (!entry.Original.VersionEquals(current))
        {
            throw CliErrors.OutputConflict(entry.Target, entry.Original, current);
        }
    }

    private static void EnsureBackupUnchanged(WorkerOutputEntry entry)
    {
        if (entry.BackupPath is null)
        {
            return;
        }

        FilePublicationSnapshot current =
            FilePublicationSnapshot.Capture(entry.BackupPath);
        FilePublicationSnapshot expected =
            entry.BackupOriginal ?? FilePublicationSnapshot.Missing;
        if (entry.BackupOriginal is null
            || !expected.VersionEquals(current)
            || Directory.Exists(entry.BackupPath))
        {
            throw CliErrors.OutputConflict(
                entry.BackupPath,
                expected,
                current);
        }
    }

    private static void PublishTransaction(
        WorkerOutputManifest manifest,
        string manifestPath)
    {
        IReadOnlyList<WorkerOutputEntry> entries = manifest.Entries;
        if (entries.Count == 0)
        {
            return;
        }
        foreach (WorkerOutputEntry entry in entries)
        {
            EnsureBackupUnchanged(entry);
            EnsureOriginalUnchanged(
                entry,
                FilePublicationSnapshot.Capture(entry.Target));
        }

        string root = CommonPublicationRoot(entries);
        using var transaction = new AtomicOutputSetWriter(
            SafeFileWriter.Recovery,
            root,
            "worker-publish");
        foreach (WorkerOutputEntry entry in entries)
        {
            entry.State = WorkerPublicationState.Publishing;
            if (entry.DeleteTarget)
            {
                transaction.StageDeletionPrepared(
                    entry.Target,
                    entry.Original);
            }
            else
            {
                transaction.StagePrepared(
                    entry.Target,
                    entry.Overwrite,
                    entry.BackupPath,
                    entry.Original,
                    staged => File.Copy(entry.Staged, staged, overwrite: true));
            }
        }
        WorkerOutputSession.WriteManifest(manifestPath, manifest);
        transaction.Commit(() =>
        {
            foreach (WorkerOutputEntry entry in entries)
            {
                entry.PublishedSnapshot =
                    transaction.PublishedSnapshot(entry.Target);
                entry.State = WorkerPublicationState.Published;
            }
            WorkerOutputSession.WriteManifest(manifestPath, manifest);
        });
    }

    private static string CommonPublicationRoot(
        IReadOnlyList<WorkerOutputEntry> entries)
    {
        string root = Path.GetDirectoryName(entries[0].Target)!;
        foreach (string directory in entries
                     .Select(static entry => Path.GetDirectoryName(entry.Target)!)
                     .Concat(entries
                         .Where(static entry => entry.BackupPath is not null)
                         .Select(static entry => Path.GetDirectoryName(entry.BackupPath!)!)))
        {
            while (!IsWithin(root, directory))
            {
                root = Path.GetDirectoryName(root)
                    ?? throw new IOException(
                        "Worker outputs do not share a bounded publication root.");
            }
        }
        if (string.Equals(
                Path.GetPathRoot(root),
                root,
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal))
        {
            throw new IOException(
                "Worker outputs span paths without a bounded publication root.");
        }
        return root;
    }

    private static bool IsWithin(string root, string path)
    {
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        string prefix = root.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return string.Equals(root, path, comparison)
            || path.StartsWith(prefix, comparison);
    }

    private static void Verify(WorkerOutputManifest manifest)
    {
        foreach (WorkerOutputEntry entry in manifest.Entries)
        {
            FilePublicationSnapshot current =
                FilePublicationSnapshot.Capture(entry.Target);
            bool valid = entry.DeleteTarget
                ? !current.Exists && !Directory.Exists(entry.Target)
                : entry.State == WorkerPublicationState.Published
                    && entry.PublishedSnapshot is not null
                    && entry.PublishedSnapshot.VersionEquals(current);
            if (!valid)
            {
                throw new IOException($"Worker output verification failed for '{entry.Target}'.");
            }
        }
    }

    private static void TryWriteManifest(
        string manifestPath,
        WorkerOutputManifest manifest)
    {
        try
        {
            WorkerOutputSession.WriteManifest(manifestPath, manifest);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning(
                "Worker recovery manifest could not be persisted ({0}).",
                exception.GetType().Name);
        }
    }

    private static void CleanDirectories(
        IEnumerable<WorkerDirectoryEntry> directories)
    {
        foreach (WorkerDirectoryEntry entry in directories
                     .Where(static entry =>
                         !entry.Existed
                         && (!OperatingSystem.IsWindows()
                             || entry.CreatedIdentity is not null))
                     .OrderByDescending(static entry => entry.Target.Length))
        {
            try
            {
                if (!FilePublicationOwnedDelete.TryDeleteDirectory(
                        entry.Target,
                        entry.CreatedIdentity))
                {
                    Trace.TraceWarning(
                        "Worker publication preserved changed directory '{0}'.",
                        entry.Target);
                }
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
            {
                Trace.TraceWarning(
                    "Worker publication could not remove directory '{0}' after failure ({1}).",
                    entry.Target,
                    exception.GetType().Name);
            }
        }
    }
}

internal static class WorkerOutputRecovery
{
    internal static PublicationRecoveryReport Restore(WorkerOutputManifest manifest)
    {
        var items = manifest.Entries.AsEnumerable().Reverse().Select(RestoreEntry).ToList();
        items.Reverse();
        bool complete = items.All(static item =>
            item.Status is "restored" or "unchanged"
            && item.ContentVerified
            && item.MetadataVerified);
        return new PublicationRecoveryReport(complete, items);
    }

    private static PublicationRecoveryItem RestoreEntry(WorkerOutputEntry entry)
    {
        try
        {
            bool publicationAttempted =
                entry.State is WorkerPublicationState.Publishing
                    or WorkerPublicationState.Published;
            if (entry.State == WorkerPublicationState.Restored)
            {
                PublicationRecoveryItem verified = Verify(entry);
                return verified.Status == "restored"
                    ? verified
                    : Unknown(
                        entry.Target,
                        entry.Original.Exists,
                        "verification-failed");
            }
            if (!publicationAttempted)
            {
                FilePublicationSnapshot current =
                    FilePublicationSnapshot.Capture(entry.Target);
                return entry.Original.VersionEquals(current)
                    ? Unchanged(entry.Target, entry.Original.Exists)
                    : Unknown(
                        entry.Target,
                        entry.Original.Exists,
                        "original-changed-before-publication");
            }
            if (!RestoreTarget(entry))
            {
                return Unknown(entry.Target, entry.Original.Exists, "content-mismatch");
            }
            PublicationRecoveryItem result = Verify(entry);
            if (result.Status == "restored")
            {
                entry.State = WorkerPublicationState.Restored;
                entry.PublishedSnapshot = null;
            }
            return result;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or CliException)
        {
            return Unknown(
                entry.Target,
                entry.Original.Exists,
                exception is CliException cli ? cli.Code.Name : exception.GetType().Name);
        }
    }

    private static bool RestoreTarget(WorkerOutputEntry entry)
    {
        FilePublicationSnapshot current = FilePublicationSnapshot.Capture(entry.Target);
        if (entry.Original.VersionEquals(current))
        {
            return true;
        }
        if (entry.State is not (
                WorkerPublicationState.Publishing
                or WorkerPublicationState.Published)
            || entry.PublishedSnapshot is null
            || !entry.PublishedSnapshot.VersionEquals(current))
        {
            return false;
        }
        if (!entry.Original.Exists)
        {
            return FilePublicationOwnedDelete.TryDelete(
                entry.Target,
                current);
        }
        if (entry.OriginalBackup is null
            || entry.OriginalBackupSnapshot is null
            || !entry.OriginalBackupSnapshot.VersionEquals(
                FilePublicationSnapshot.Capture(entry.OriginalBackup))
            || !entry.Original.ContentMatches(entry.OriginalBackup))
        {
            throw new IOException("The worker original backup is unavailable.");
        }

        FilePublicationAtomicSwap.RestoreFromBackup(
            entry.OriginalBackup,
            entry.Target,
            current,
            entry.Original);
        return true;
    }

    private static PublicationRecoveryItem Verify(WorkerOutputEntry entry)
    {
        bool content = entry.Original.ContentMatches(entry.Target);
        bool metadata = !entry.Original.Exists
            || entry.Original.Metadata is null
            || entry.Original.Metadata.Matches(entry.Target);
        return new PublicationRecoveryItem(
            entry.Target,
            entry.Original.Exists,
            Published: true,
            content && metadata ? "restored" : "unknown",
            content,
            metadata,
            content && metadata ? null : "verification-failed");
    }


    private static PublicationRecoveryItem Unchanged(
        string target,
        bool originalExisted) =>
        new(target, originalExisted, Published: false, "unchanged", true, true, null);

    private static PublicationRecoveryItem Unknown(string target, bool originalExisted, string reason) =>
        new(target, originalExisted, Published: true, "unknown", false, false, reason);
}
