using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.IO;

/// <summary>
/// Stages a bounded output set, publishes each target with atomic visibility,
/// and performs verified compensating recovery if publication does not finish.
/// Multi-file visibility is not atomic: the transaction guarantee is explicit
/// recovery with a machine-readable partial-state report.
/// </summary>
public sealed class AtomicOutputSetWriter : IDisposable
{
    private readonly AtomicPublicationPlan _plan;
    private readonly AtomicPublicationStaging _staging;
    private readonly AtomicPublicationCommit _commit;
    private readonly AtomicPublicationRecovery _recovery;
    private bool _disposed;

    /// <summary>Creates a staging area beneath the target directory.</summary>
    public AtomicOutputSetWriter(
        SafeFileWriter writer,
        string targetDirectory,
        string operation)
        : this(
            writer,
            targetDirectory,
            operation,
            NoPublicationFaultInjector.Instance)
    {
    }

    /// <summary>Creates one transaction spanning directories on the same filesystem.</summary>
    public AtomicOutputSetWriter(SafeFileWriter writer, IEnumerable<string> targetDirectories, string operation)
        : this(writer, OutputSetPaths.CommonDirectory(targetDirectories), operation) { }

    internal AtomicOutputSetWriter(
        SafeFileWriter writer,
        string targetDirectory,
        string operation,
        IPublicationFaultInjector faults)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(faults);
        _plan = AtomicPublicationPlan.Create(
            targetDirectory,
            operation,
            faults, writer.ResourceBudgets);
        _staging = new AtomicPublicationStaging(_plan, writer);
        _commit = new AtomicPublicationCommit(_plan);
        _recovery = new AtomicPublicationRecovery(_plan);
    }

    /// <summary>Admits a directory and owns only directories this transaction actually creates.</summary>
    public void EnsureDirectory(string path)
    {
        ThrowIfDisposed();
        _plan.EnsureOutputDirectory(path);
    }

    /// <summary>Stages one unique target without making it user-visible.</summary>
    public StagedOutput Stage(string targetPath, bool overwrite, Action<string> write) =>
        Stage(targetPath, overwrite, backupPath: null, inputPrecondition: null, write);

    /// <summary>Stages an output, binding its input, optional stable backup and check.</summary>
    public StagedOutput Stage(string targetPath, bool overwrite, string? backupPath,
        FileWritePrecondition? inputPrecondition, Action<string> write,
        Action<string>? verify = null)
    {
        ThrowIfDisposed();
        string target = OutputPathValidator.NormalizeFile(targetPath);
        FilePublicationSnapshot expected = inputPrecondition?.Targets(target) is true
            ? inputPrecondition.Snapshot : FilePublicationSnapshot.Capture(target);
        return _staging.StagePrepared(target, overwrite, backupPath, expected, write,
            inputPrecondition, verify);
    }

    /// <summary>
    /// Stages a file whose engine also writes companion files beside it, such as the scripts
    /// and style sheets of an HTML5 presentation, and stages each companion as a target beside
    /// the file, so the set is published, refused or rolled back as a whole. The file comes first.
    /// </summary>
    public IReadOnlyList<StagedOutput> StageFileSet(string targetPath, bool overwrite, Action<string> write)
    {
        ArgumentNullException.ThrowIfNull(write);
        string? staged = null;
        StagedOutput main = Stage(targetPath, overwrite, path =>
        {
            staged = path;
            write(path);
        });
        string targetDirectory = Path.GetDirectoryName(main.TargetPath)!;
        string[] companions = [.. Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(staged!)!)
            .Where(entry => !string.Equals(entry, staged, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)];
        var outputs = new List<StagedOutput> { main };
        try
        {
            foreach (string entry in companions)
            {
                if (Directory.Exists(entry))
                {
                    throw CliErrors.CompanionDirectoryUnpublished(main.TargetPath, Path.GetFileName(entry));
                }

                outputs.Add(Stage(Path.Combine(targetDirectory, Path.GetFileName(entry)), overwrite, path =>
                {
                    using (FileStream source = File.OpenRead(entry))
                    using (FileStream target = new(path, FileMode.Truncate, FileAccess.Write))
                    {
                        source.CopyTo(target);
                    }

                    File.Delete(entry);
                }));
            }
        }
        catch
        {
            // The staging area holds only what the transaction owns, so it can be removed.
            foreach (string entry in companions)
            {
                if (Directory.Exists(entry))
                {
                    Directory.Delete(entry, recursive: true);
                }
                else
                {
                    File.Delete(entry);
                }
            }

            throw;
        }

        return outputs;
    }

    internal StagedOutput StagePrepared(string targetPath, bool overwrite, string? requestedBackup,
        FilePublicationSnapshot expectedTarget, Action<string> write,
        FileWritePrecondition? inputPrecondition = null)
    {
        ThrowIfDisposed();
        string target = Path.GetFullPath(targetPath);
        FilePublicationSnapshot current = FilePublicationSnapshot.Capture(target);
        if (!expectedTarget.VersionEquals(current))
        {
            throw PublicationErrors.OutputConflict(target, expectedTarget, current);
        }
        return _staging.StagePrepared(target, overwrite, requestedBackup, expectedTarget, write, inputPrecondition);
    }

    internal void StageDeletionPrepared(
        string targetPath,
        FilePublicationSnapshot expectedTarget)
    {
        string target = Path.GetFullPath(targetPath);
        FilePublicationSnapshot current = FilePublicationSnapshot.Capture(target);
        if (!expectedTarget.Exists
            || !expectedTarget.VersionEquals(current))
        {
            throw PublicationErrors.OutputConflict(target, expectedTarget, current);
        }

        _staging.StageDeletionPrepared(target, expectedTarget);
    }

    /// <summary>
    /// Publishes all staged outputs and returns their sizes in stage order.
    /// A failed commit is recovered and reported as a stable publication error;
    /// incomplete recovery maps to exit code 8.
    /// </summary>
    public IReadOnlyList<long> Commit() => Commit(beforeCommit: null);

    internal IReadOnlyList<long> Commit(Action? beforeCommit)
    {
        ThrowIfDisposed();
        try
        {
            _plan.Seal();
            _plan.BeginCommit();
            return _commit.Execute(beforeCommit);
        }
        catch (Exception commitFailure)
        {
            (PublicationRecoveryReport recovery, Exception? recoveryFailure) =
                RollBackSafely();
            if (recovery.RecoveryComplete && commitFailure is OperationCanceledException) { throw; }
            throw PublicationErrors.OutputPublicationFailure(
                recoveryFailure is null
                    ? commitFailure
                    : new AggregateException(commitFailure, recoveryFailure),
                recovery);
        }
        finally
        {
            if (_plan.CanCleanUp)
            {
                _plan.CleanUp();
            }
            _disposed = true;
            try { _plan.CleanUpOutputDirectories(); }
            finally { _plan.ReleaseLease(); }
        }
    }

    /// <summary>
    /// Recovers abandoned transaction journals below an output directory.
    /// Journals owned by a still-running process are not touched.
    /// </summary>
    public static int RecoverPending(string targetDirectory) =>
        AtomicPublicationRecovery.RecoverPending(targetDirectory);

    internal FilePublicationSnapshot PublishedSnapshot(string targetPath)
    {
        string target = Path.GetFullPath(targetPath);
        StringComparer comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        PublicationJournalEntry entry = _plan.Journal.Entries.Single(
            candidate => comparer.Equals(candidate.Target, target));
        return entry.PublishedSnapshot
            ?? throw new InvalidOperationException(
                $"Publication snapshot for '{target}' is unavailable.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            if (!_plan.IsTerminal)
            {
                (PublicationRecoveryReport recovery, Exception? failure) =
                    RollBackSafely();
                if (!recovery.RecoveryComplete)
                {
                    throw PublicationErrors.OutputPublicationFailure(
                        failure is null
                            ? new IOException(
                                "The output transaction was disposed before commit.")
                            : new AggregateException(
                                "The output transaction recovery failed during disposal.",
                                failure),
                        recovery);
                }
            }

            if (_plan.CanCleanUp)
            {
                _plan.CleanUp();
            }
        }
        finally
        {
            try { _plan.CleanUpOutputDirectories(); }
            finally { _plan.ReleaseLease(); }
        }
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);

    private (PublicationRecoveryReport Report, Exception? Failure)
        RollBackSafely()
    {
        try
        {
            return (_recovery.RollBack(), null);
        }
        catch (Exception exception)
        {
            _plan.Journal.State = PublicationTransactionState.Partial;
            _plan.TryPersist();
            PublicationRecoveryItem[] items = _plan.Journal.Entries
                .OrderBy(static entry => entry.Index)
                .Select(entry => new PublicationRecoveryItem(
                    entry.Target,
                    entry.Original.Exists,
                    Published: entry.State is PublicationEntryState.Publishing
                        or PublicationEntryState.Published,
                    Status: "unknown",
                    ContentVerified: false,
                    MetadataVerified: false,
                    Failure: exception.GetType().Name))
                .ToArray();
            return (new PublicationRecoveryReport(false, items), exception);
        }
    }
}
