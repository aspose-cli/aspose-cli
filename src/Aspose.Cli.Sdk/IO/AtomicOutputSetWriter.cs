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

    /// <summary>Creates a private staging area beneath the target directory.</summary>
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
            faults);
        _staging = new AtomicPublicationStaging(_plan, writer);
        _commit = new AtomicPublicationCommit(_plan);
        _recovery = new AtomicPublicationRecovery(_plan);
    }

    /// <summary>Stages one unique target without making it user-visible.</summary>
    public void Stage(string targetPath, bool overwrite, Action<string> write)
    {
        ThrowIfDisposed();
        _staging.Stage(targetPath, overwrite, write);
    }

    internal void StagePrepared(
        string targetPath,
        bool overwrite,
        string? requestedBackup,
        FilePublicationSnapshot expectedTarget,
        Action<string> write)
    {
        string target = Path.GetFullPath(targetPath);
        FilePublicationSnapshot current = FilePublicationSnapshot.Capture(target);
        if (!expectedTarget.VersionEquals(current))
        {
            throw CliErrors.OutputConflict(target, expectedTarget, current);
        }

        _staging.StagePrepared(
            target,
            overwrite,
            requestedBackup,
            expectedTarget,
            write);
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
            throw CliErrors.OutputConflict(target, expectedTarget, current);
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
            return _commit.Execute(beforeCommit);
        }
        catch (Exception commitFailure)
        {
            (PublicationRecoveryReport recovery, Exception? recoveryFailure) =
                RollBackSafely();
            throw CliErrors.OutputPublicationFailure(
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
            _plan.ReleaseLease();
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
                    throw CliErrors.OutputPublicationFailure(
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
            _plan.ReleaseLease();
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
