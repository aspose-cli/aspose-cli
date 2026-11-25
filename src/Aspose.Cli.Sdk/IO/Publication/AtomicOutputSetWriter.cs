using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Files;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.IO.Paths;

namespace Aspose.Cli.Sdk.IO.Publication;

/// <summary>
/// Stages a bounded output set, publishes each target with atomic visibility,
/// and puts the targets back, verified, if publication does not finish.
/// Multi-file visibility is not atomic: the transaction guarantee is an explicit
/// rollback with a machine-readable partial-state report.
/// </summary>
public sealed class AtomicOutputSetWriter : IDisposable
{
    private readonly AtomicPublicationPlan _plan;
    private readonly AtomicPublicationStaging _staging;
    private readonly AtomicPublicationCommit _commit;
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
    }

    /// <summary>Admits a directory and owns only directories this transaction actually creates.</summary>
    public void EnsureDirectory(string path)
    {
        ThrowIfDisposed();
        _plan.EnsureOutputDirectory(path);
    }

    /// <summary>Stages one unique target without making it user-visible.</summary>
    public StagedOutput Stage(string targetPath, bool overwrite, Action<string> write) =>
        Stage(targetPath, overwrite, inputPrecondition: null, write);

    /// <summary>Stages an output, binding its input and check.</summary>
    public StagedOutput Stage(string targetPath, bool overwrite,
        FileWritePrecondition? inputPrecondition, Action<string> write,
        Action<string>? verify = null)
    {
        ThrowIfDisposed();
        string target = OutputPathValidator.NormalizeFile(targetPath);
        FilePublicationSnapshot expected = inputPrecondition?.Targets(target) is true
            ? inputPrecondition.Snapshot : FilePublicationSnapshot.Capture(target);
        return _staging.StagePrepared(target, overwrite, expected, write,
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
            .Where(entry => !FilePaths.Same(entry, staged))
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

    internal StagedOutput StagePrepared(string targetPath, bool overwrite,
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
        return _staging.StagePrepared(target, overwrite, expectedTarget, write, inputPrecondition);
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
    /// A failed commit is rolled back and reported as a stable publication error;
    /// an incomplete rollback is OUTPUT_PUBLICATION_PARTIAL.
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
            _plan.CleanUpOutputDirectories();
        }
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
            _plan.CleanUpOutputDirectories();
        }
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);

    private (PublicationRecoveryReport Report, Exception? Failure)
        RollBackSafely()
    {
        try
        {
            return (_commit.RollBack(), null);
        }
        catch (Exception exception)
        {
            _plan.State = PublicationTransactionState.Partial;
            PublicationRecoveryItem[] items = _plan.Entries
                .OrderBy(static entry => entry.Index)
                .Select(entry => AtomicPublicationCommit.Located(new PublicationRecoveryItem(
                    entry.Target,
                    entry.Original.Exists,
                    Published: entry.State is PublicationEntryState.Publishing
                        or PublicationEntryState.Published,
                    Status: "unknown",
                    ContentVerified: false,
                    MetadataVerified: false,
                    Failure: exception.GetType().Name), entry))
                .ToArray();
            return (new PublicationRecoveryReport(false, items), exception);
        }
    }
}
