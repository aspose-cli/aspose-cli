using System.Security.Cryptography;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Files;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.IO.Paths;
using Aspose.Cli.Sdk.IO.Publication;

namespace Aspose.Cli.Sdk.Licensing;

/// <summary>Bounded license snapshots and transactional per-user file storage.</summary>
public static class LicenseInstaller
{
    /// <summary>Maximum bytes admitted for one license file.</summary>
    public const int MaximumBytes = 1024 * 1024;

    /// <summary>
    /// Captures one source snapshot, lets the caller validate that exact
    /// snapshot and select compatible destinations, then publishes its bytes as
    /// one recoverable output set that replaces existing targets.
    /// </summary>
    public static IReadOnlyList<string> InstallMany(
        ResourceBudgetLedger resourceBudgets,
        string sourcePath,
        Func<string, IEnumerable<string>> validateAndSelectDestinations) =>
        InstallMany(resourceBudgets, sourcePath, validateAndSelectDestinations,
            NoPublicationFaultInjector.Instance);

    internal static IReadOnlyList<string> InstallMany(
        ResourceBudgetLedger resourceBudgets,
        string sourcePath,
        Func<string, IEnumerable<string>> validateAndSelectDestinations,
        IPublicationFaultInjector faults)
    {
        ArgumentNullException.ThrowIfNull(resourceBudgets);
        ArgumentException.ThrowIfNullOrEmpty(sourcePath);
        ArgumentNullException.ThrowIfNull(validateAndSelectDestinations);
        ArgumentNullException.ThrowIfNull(faults);
        resourceBudgets.AdmitFile(sourcePath);
        string sourceFile = Path.GetFullPath(sourcePath);
        // The source is an input: one that cannot be opened or read is the error any input reports.
        byte[] contents = InputFailures.Read(sourceFile, () =>
        {
            using Stream source = resourceBudgets.Inputs.OpenFile(sourceFile);
            return ReadSource(resourceBudgets, source);
        });
        return InstallSnapshot(resourceBudgets, contents, validateAndSelectDestinations, faults);
    }

    private static IReadOnlyList<string> InstallSnapshot(
        ResourceBudgetLedger resourceBudgets,
        byte[] contents,
        Func<string, IEnumerable<string>> validateAndSelectDestinations,
        IPublicationFaultInjector faults)
    {
        string? snapshotDirectory = null;
        try
        {
            snapshotDirectory = UserStorage.CreateTemporaryDirectory("license-install");
            string snapshot = Path.Combine(snapshotDirectory, "source.lic");
            // A license is a secret: only the current user may read any copy of it.
            CurrentUserFileAccess.Write(snapshot, contents);

            // Native validators may open the path, but managed writers cannot
            // replace or rewrite the file while its validation lease is held.
            using var lease = new FileStream(snapshot, FileMode.Open, FileAccess.Read, FileShare.Read);
            string[] destinations = NormalizePaths(validateAndSelectDestinations(snapshot));
            if (destinations.Length == 0)
            {
                throw new ArgumentException("At least one license destination is required.",
                    nameof(validateAndSelectDestinations));
            }
            VerifySnapshot(resourceBudgets, snapshot, contents);
            string directory = CommonDirectory(destinations);
            if (IsInsideSnapshot(snapshotDirectory, directory))
            {
                throw new ArgumentException("A license destination cannot be inside its temporary snapshot.");
            }
            ValidateExistingTargets(destinations);
            EnsureDirectory(directory);
            foreach (string parent in destinations.Select(static path => Path.GetDirectoryName(path)!).Distinct())
            {
                EnsureDirectory(parent);
            }

            using var transaction = new AtomicOutputSetWriter(
                new SafeFileWriter(resourceBudgets), directory, faults);
            ValidateExistingTargets(destinations);
            foreach (string destination in destinations)
            {
                resourceBudgets.Cancellation.ThrowIfCancellationRequested();
                transaction.Stage(destination, overwrite: true,
                    staged => CurrentUserFileAccess.Write(staged, contents));
            }
            transaction.Commit();
            return destinations;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(contents);
            if (snapshotDirectory is not null)
            {
                UserStorage.TryDeleteTree(snapshotDirectory);
            }
        }
    }

    /// <summary>
    /// Removes existing licenses as one recoverable output set and
    /// returns their paths. Missing files are a no-op.
    /// </summary>
    public static IReadOnlyList<string> RemoveMany(
        ResourceBudgetLedger resourceBudgets,
        IEnumerable<string> destinationPaths) =>
        RemoveMany(resourceBudgets, destinationPaths, NoPublicationFaultInjector.Instance);

    internal static IReadOnlyList<string> RemoveMany(
        ResourceBudgetLedger resourceBudgets,
        IEnumerable<string> destinationPaths,
        IPublicationFaultInjector faults)
    {
        ArgumentNullException.ThrowIfNull(resourceBudgets);
        ArgumentNullException.ThrowIfNull(faults);
        string[] destinations = NormalizePaths(destinationPaths);
        if (destinations.Length == 0)
        {
            return [];
        }
        ValidateExistingTargets(destinations);
        if (!destinations.Any(File.Exists))
        {
            return [];
        }
        string directory = CommonDirectory(destinations);
        EnsureDirectory(directory);
        using var transaction = new AtomicOutputSetWriter(
            new SafeFileWriter(resourceBudgets), directory, faults);
        ValidateExistingTargets(destinations);
        var removed = new List<string>(destinations.Length);
        foreach (string destination in destinations)
        {
            resourceBudgets.Cancellation.ThrowIfCancellationRequested();
            FilePublicationSnapshot original = FilePublicationSnapshot.Capture(destination);
            if (!original.Exists)
            {
                continue;
            }
            transaction.StageDeletionPrepared(destination, original);
            removed.Add(destination);
        }
        transaction.Commit();
        return removed;
    }

    private static byte[] ReadSource(ResourceBudgetLedger budgets, Stream source)
    {
        if (source.CanSeek && source.Length - source.Position > MaximumBytes)
        {
            throw CliErrors.FileTooLarge(source.Length - source.Position, MaximumBytes);
        }
        using var contents = new BudgetedMemoryStream(budgets,
            ResourceBudgetKinds.MemoryBufferBytes, "license-snapshot-buffer");
        // A license is a secret, so the read leaves no copy of it behind. The input stream serves
        // a read smaller than its own buffer through that buffer, which nothing clears; every
        // read here asks for a whole chunk of that size, goes straight into the chunk, and the
        // chunk is cleared below.
        byte[] chunk = new byte[InputFiles.BufferSize];
        try
        {
            while (true)
            {
                budgets.Cancellation.ThrowIfCancellationRequested();
                int read = source.Read(chunk);
                if (read == 0)
                {
                    break;
                }
                if (contents.Length + read > MaximumBytes)
                {
                    throw CliErrors.FileTooLarge(contents.Length + read, MaximumBytes);
                }
                contents.Write(chunk, 0, read);
            }
            budgets.Consume(ResourceBudgetKinds.MemoryBufferBytes,
                contents.Length, "bytes", "license-snapshot-copy");
            return contents.ToArray();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(contents.GetBuffer());
            CryptographicOperations.ZeroMemory(chunk);
        }
    }
    private static void VerifySnapshot(ResourceBudgetLedger budgets, string path, byte[] contents)
    {
        using var snapshot = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        bool matches = snapshot.Length == contents.Length;
        Span<byte> buffer = stackalloc byte[4096];
        int offset = 0;
        while (matches && offset < contents.Length)
        {
            budgets.Cancellation.ThrowIfCancellationRequested();
            int read = snapshot.Read(buffer[..Math.Min(buffer.Length, contents.Length - offset)]);
            matches = read > 0 && buffer[..read].SequenceEqual(contents.AsSpan(offset, read));
            offset += read;
        }
        if (!matches || snapshot.ReadByte() >= 0)
        {
            throw CliErrors.LicenseInvalid("file", "the license snapshot changed during validation");
        }
    }

    /// <summary>Whether the license destinations' directory is the temporary snapshot's or inside it.</summary>
    internal static bool IsInsideSnapshot(string snapshotDirectory, string directory) =>
        FilePaths.IsWithin(snapshotDirectory, directory, allowEqual: true);

    private static string[] NormalizePaths(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return paths.Select(Path.GetFullPath)
            .Distinct(FilePaths.Comparer)
            .ToArray();
    }

    private static string CommonDirectory(IReadOnlyList<string> paths)
    {
        string directory = Path.GetDirectoryName(paths[0])!;
        foreach (string path in paths.Skip(1))
        {
            string parent = Path.GetDirectoryName(path)!;
            while (!FilePaths.IsWithin(directory, parent, allowEqual: true))
            {
                directory = Path.GetDirectoryName(directory)
                    ?? throw new ArgumentException("License destinations must share a filesystem root.", nameof(paths));
            }
        }
        if (FilePaths.Same(directory, Path.GetPathRoot(directory)))
        {
            throw new ArgumentException("License storage must be below the filesystem root.", nameof(paths));
        }
        return directory;
    }

    private static void EnsureDirectory(string directory) => Directory.CreateDirectory(directory);

    private static void ValidateExistingTargets(IEnumerable<string> paths)
    {
        foreach (string path in paths)
        {
            OutputPathValidator.EnsureSafeFile(path);
        }
    }
}
