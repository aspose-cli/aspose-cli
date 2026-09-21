using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.IO;

/// <summary>Single-file convenience entry point over the shared output transaction.</summary>
public sealed class SafeFileWriter
{
    private readonly ResourceBudgetLedger _resourceBudgets;

    public SafeFileWriter(ResourceBudgetLedger resourceBudgets) =>
        _resourceBudgets = resourceBudgets ?? throw new ArgumentNullException(nameof(resourceBudgets));

    internal ResourceBudgetLedger ResourceBudgets => _resourceBudgets;

    public long Write(string targetPath, bool overwrite, Action<string> writeToTemp) =>
        Write(targetPath, overwrite, backupPath: null, writeToTemp).SizeBytes;

    public SafeWriteResult Write(string targetPath, bool overwrite, string? backupPath, Action<string> writeToTemp) =>
        Write(targetPath, overwrite, backupPath, inputPrecondition: null, writeToTemp);

    public SafeWriteResult Write(string targetPath, bool overwrite, string? backupPath,
        FileWritePrecondition? inputPrecondition, Action<string> writeToTemp)
    {
        string target = OutputPathValidator.NormalizeFile(targetPath);
        string? backup = backupPath is null ? null : OutputPathValidator.NormalizeFile(backupPath, phase: "backup");
        string directory = System.IO.Path.GetDirectoryName(target)!;
        using var transaction = new AtomicOutputSetWriter(this,
            backup is null ? [directory] : new[] { directory, System.IO.Path.GetDirectoryName(backup)! }, "write");
        StagedOutput staged;
        try
        {
            staged = transaction.Stage(target, overwrite, backup, inputPrecondition, writeToTemp);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw CliErrors.OutputUnwritable(target, "the staged output could not be written", exception, "write");
        }
        transaction.Commit();
        return new SafeWriteResult(staged.SizeBytes, staged.Backup)
        {
            PublishedSnapshot = staged.Snapshot,
        };
    }

    internal void ConsumeOutput(long bytes, string phase) =>
        _resourceBudgets?.Consume(ResourceBudgetKinds.OutputBytes, bytes, "bytes", phase);
}

/// <summary>Outcome of one atomic file write.</summary>
public sealed record SafeWriteResult(long SizeBytes, SafeBackupResult? Backup)
{
    internal FilePublicationSnapshot PublishedSnapshot { get; init; } = FilePublicationSnapshot.Missing;
    public FileFingerprint Fingerprint => new() { Sha256 = PublishedSnapshot.Sha256!.ToLowerInvariant() };
}

/// <summary>Outcome of the stable, never-overwritten safety backup.</summary>
public sealed record SafeBackupResult(string Path, bool Created, long SizeBytes);
