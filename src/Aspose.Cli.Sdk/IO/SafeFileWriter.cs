using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;

namespace Aspose.Cli.Sdk.IO;

/// <summary>
/// Publishes one output with atomic visibility on file systems that provide
/// same-directory atomic rename. This type does not by itself provide a
/// multi-file transaction or guarantee parent-directory durability.
/// </summary>
public sealed class SafeFileWriter
{
    private readonly ResourceBudgetLedger? _resourceBudgets;

    public SafeFileWriter(ResourceBudgetLedger resourceBudgets)
    {
        _resourceBudgets = resourceBudgets
            ?? throw new ArgumentNullException(nameof(resourceBudgets));
    }

    private SafeFileWriter()
    {
    }

    internal static SafeFileWriter Recovery { get; } = new();

    /// <summary>Produces a file without a safety backup.</summary>
    public long Write(string targetPath, bool overwrite, Action<string> writeToTemp) =>
        Write(targetPath, overwrite, backupPath: null, writeToTemp).SizeBytes;

    /// <summary>
    /// Produces <paramref name="targetPath"/> atomically, optionally preserving
    /// the pre-write bytes in a stable backup that is never overwritten.
    /// </summary>
    /// <param name="targetPath">Absolute path of the file to produce.</param>
    /// <param name="overwrite">Whether an existing file may be replaced.</param>
    /// <param name="backupPath">
    /// Optional absolute backup path. When the target exists, the backup is
    /// created immediately before replacement if missing and reused otherwise.
    /// </param>
    /// <param name="writeToTemp">
    /// Callback that writes the content to the temporary path it receives.
    /// Exceptions from the callback propagate unchanged (engine adapters
    /// throw already-translated <see cref="CliException"/>s).
    /// </param>
    /// <returns>Produced-file size and optional backup outcome.</returns>
    /// <exception cref="CliException">
    /// <c>OUTPUT_EXISTS</c> when the target exists and overwrite is false;
    /// <c>OUTPUT_UNWRITABLE</c> when the file system rejects the write.
    /// </exception>
    public SafeWriteResult Write(
        string targetPath,
        bool overwrite,
        string? backupPath,
        Action<string> writeToTemp) =>
        Write(targetPath, overwrite, backupPath, inputPrecondition: null, writeToTemp);

    /// <summary>
    /// Produces a file atomically and rejects publication if the admitted input
    /// changed while the product engine was applying its bounded mutation.
    /// </summary>
    public SafeWriteResult Write(
        string targetPath,
        bool overwrite,
        string? backupPath,
        FileWritePrecondition? inputPrecondition,
        Action<string> writeToTemp) =>
        WritePrepared(
            targetPath,
            overwrite,
            backupPath,
            inputPrecondition,
            writeToTemp,
            inspectProducedFile: null);

    /// <summary>
    /// Produces a file atomically, then inspects or normalizes the exact staged
    /// file under a verified physical-identity lock before publication.
    /// </summary>
    public SafeWriteResult WriteBound(
        string targetPath,
        bool overwrite,
        string? backupPath,
        FileWritePrecondition? inputPrecondition,
        Action<string> writeToTemp,
        Action<string, Stream> inspectProducedFile,
        Action<string> verifyProducedFile) =>
        WritePrepared(
            targetPath,
            overwrite,
            backupPath,
            inputPrecondition,
            writeToTemp,
            inspectProducedFile ?? throw new ArgumentNullException(nameof(inspectProducedFile)),
            verifyProducedFile ?? throw new ArgumentNullException(nameof(verifyProducedFile)));

    private SafeWriteResult WritePrepared(
        string targetPath,
        bool overwrite,
        string? backupPath,
        FileWritePrecondition? inputPrecondition,
        Action<string> writeToTemp,
        Action<string, Stream>? inspectProducedFile,
        Action<string>? verifyProducedFile = null)
    {
        if (WorkerOutputSession.IsActive)
        {
            return WorkerOutputSession.StageSingle(
                this,
                targetPath,
                overwrite,
                backupPath,
                inputPrecondition,
                writeToTemp,
                inspectProducedFile,
                verifyProducedFile);
        }

        string target = OutputPathValidator.NormalizeFile(targetPath);
        string directory = Path.GetDirectoryName(target)
            ?? throw CliErrors.OutputUnwritable(
                target,
                "the path has no parent directory");
        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            throw CliErrors.OutputUnwritable(
                target,
                "the output directory could not be created",
                exception);
        }

        using PublicationDirectoryLease lease =
            PublicationDirectoryLease.Acquire(directory);
        AtomicPublicationRecovery.RecoverPendingHierarchyUnderLease(
            directory,
            lease);
        FilePublicationSnapshot expectedTarget = inputPrecondition is not null
            && inputPrecondition.Targets(target)
                ? inputPrecondition.Snapshot
                : FilePublicationSnapshot.Capture(target);
        return Write(
            target,
            overwrite,
            backupPath,
            writeToTemp,
            expectedTarget,
            FilePublicationDurability.File,
            inputPrecondition: inputPrecondition,
            publicationLease: lease,
            inspectProducedFile: inspectProducedFile,
            verifyProducedFile: verifyProducedFile);
    }

    internal SafeWriteResult Write(
        string targetPath,
        bool overwrite,
        string? backupPath,
        Action<string> writeToTemp,
        FilePublicationSnapshot expectedTarget,
        FilePublicationDurability durability,
        bool chargeOutput = true,
        FileWritePrecondition? inputPrecondition = null,
        PublicationDirectoryLease? publicationLease = null,
        Action<string, Stream>? inspectProducedFile = null,
        Action<string>? verifyProducedFile = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetPath);
        ArgumentNullException.ThrowIfNull(writeToTemp);
        targetPath = OutputPathValidator.NormalizeFile(targetPath);
        backupPath = backupPath is null
            ? null
            : OutputPathValidator.NormalizeFile(
                backupPath,
                phase: "backup");

        if (!overwrite && expectedTarget.Exists)
        {
            throw CliErrors.OutputExists(targetPath);
        }

        string directory = Path.GetDirectoryName(targetPath)
            ?? throw CliErrors.OutputUnwritable(targetPath, "the path has no parent directory");
        PublicationDirectoryLease? ownedLease = null;
        OwnedTemporaryFile? temporary = null;
        string tempPath = Path.Combine(
            directory,
            $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            Directory.CreateDirectory(directory);
            OutputPathValidator.EnsureSafeDirectory(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw CliErrors.OutputUnwritable(
                targetPath,
                "the output directory could not be prepared",
                ex);
        }

        try
        {
            PublicationDirectoryLease lease = publicationLease
                ?? (ownedLease = PublicationDirectoryLease.Acquire(directory));
            lease.EnsureCovers(targetPath);
            FilePhysicalIdentity? parentIdentity =
                OutputPathValidator.CaptureParentIdentity(targetPath);
            if (ownedLease is not null)
            {
                AtomicPublicationRecovery.RecoverPendingHierarchyUnderLease(
                    directory,
                    lease);
            }
            try
            {
                temporary = OwnedTemporaryFile.Create(tempPath);
                writeToTemp(tempPath);
                // Some document engines save by atomically replacing the path.
                // Bind the exact produced file only after the engine returns.
                temporary.BindInspectAndVerify(
                    inspectProducedFile,
                    verifyProducedFile);
                if (chargeOutput)
                {
                    ConsumeOutput(new FileInfo(tempPath).Length, "output-stage");
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The producer writes through OUR temp file, so its IO failures
                // are output failures: without this they escaped as INTERNAL
                // ("this is a bug in the CLI, please report it") for something as
                // ordinary as a read-only output directory, while the same denial
                // on the final move reported OUTPUT_UNWRITABLE correctly.
                throw CliErrors.OutputUnwritable(
                    targetPath,
                    "the staged output could not be written",
                    ex,
                    "write");
            }

            SafeBackupResult? backup = backupPath is null
                ? null
                : EnsureBackup(targetPath, backupPath, expectedTarget);
            FilePublicationSnapshot published =
                FilePublicationSnapshot.Missing;

            try
            {
                inputPrecondition?.EnsureUnchanged();
                if (inputPrecondition is null || !inputPrecondition.Targets(targetPath))
                {
                    EnsureUnchanged(targetPath, expectedTarget);
                }
                OutputPathValidator.EnsureParentUnchanged(
                    targetPath,
                    parentIdentity);
                if (durability is FilePublicationDurability.File
                    or FilePublicationDurability.FileAndDirectory)
                {
                    temporary.FlushBound();
                }
                FilePublicationSnapshot verifiedStage =
                    temporary.CaptureBoundSnapshot();

                published = FilePublicationAtomicSwap.Publish(
                    tempPath,
                    targetPath,
                    overwrite,
                    expectedTarget,
                    expectedTarget,
                    expectedStage: verifiedStage);
                temporary.MarkPublished();
                if (durability == FilePublicationDurability.FileAndDirectory)
                {
                    FilePublicationDurabilityAdapter.FlushDirectory(directory);
                }

                // Verification or a later operation in the same invocation may
                // legitimately reopen an in-place output. Preserve the
                // admission invariant by recording the bytes this invocation
                // just published, without admitting unrelated new outputs.
                _resourceBudgets?.RefreshAdmissionAfterPublication(targetPath);
            }
            catch (IOException) when (!overwrite && File.Exists(targetPath))
            {
                throw CliErrors.OutputExists(targetPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw CliErrors.OutputUnwritable(
                    targetPath,
                    "the staged output could not be published",
                    ex,
                    "replace");
            }

            return new SafeWriteResult(new FileInfo(targetPath).Length, backup)
            {
                PublishedSnapshot = published,
            };
        }
        finally
        {
            temporary?.Dispose();
            ownedLease?.Dispose();
        }
    }

    private static SafeBackupResult EnsureBackup(
        string targetPath,
        string backupPath,
        FilePublicationSnapshot expectedTarget)
    {
        backupPath = OutputPathValidator.NormalizeFile(
            backupPath,
            phase: "backup");
        FilePhysicalIdentity? parentIdentity =
            OutputPathValidator.CaptureParentIdentity(backupPath);
        EnsureUnchanged(targetPath, expectedTarget);
        if (!expectedTarget.Exists)
        {
            throw CliErrors.OutputUnwritable(
                backupPath,
                "the source file no longer exists",
                phase: "backup");
        }

        if (File.Exists(backupPath))
        {
            return new SafeBackupResult(backupPath, Created: false, new FileInfo(backupPath).Length);
        }

        try
        {
            File.Copy(targetPath, backupPath, overwrite: false);
            FilePublicationDurabilityAdapter.FlushFile(backupPath);
            expectedTarget.Metadata?.ApplyContentAttributes(backupPath);
            OutputPathValidator.EnsureParentUnchanged(
                backupPath,
                parentIdentity);
            return new SafeBackupResult(backupPath, Created: true, new FileInfo(backupPath).Length);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw CliErrors.OutputUnwritable(
                backupPath,
                "the safety backup could not be created",
                ex,
                "backup");
        }
    }

    private static void EnsureUnchanged(string targetPath, FilePublicationSnapshot expected)
    {
        FilePublicationSnapshot actual;
        try
        {
            actual = FilePublicationSnapshot.Capture(targetPath);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            throw CliErrors.OutputConflict(
                targetPath,
                expected,
                actual: null,
                exception);
        }

        if (!expected.VersionEquals(actual))
        {
            throw CliErrors.OutputConflict(targetPath, expected, actual);
        }
    }

    internal void ConsumeOutput(long bytes, string phase) =>
        _resourceBudgets?.Consume(
            ResourceBudgetKinds.OutputBytes,
            bytes,
            "bytes",
            phase);
}

/// <summary>Outcome of one atomic file write.</summary>
public sealed record SafeWriteResult(long SizeBytes, SafeBackupResult? Backup)
{
    internal FilePublicationSnapshot PublishedSnapshot { get; init; } =
        FilePublicationSnapshot.Missing;
}

/// <summary>Outcome of the stable, never-overwritten safety backup.</summary>
public sealed record SafeBackupResult(string Path, bool Created, long SizeBytes);
