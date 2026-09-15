using System.Security.Cryptography;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Sdk.Licensing;

/// <summary>Bounded private license snapshots and transactional per-user file storage.</summary>
public static class LicenseInstaller
{
    /// <summary>Maximum bytes admitted for one license file.</summary>
    public const int MaximumBytes = 1024 * 1024;

    /// <summary>
    /// Captures one private source snapshot, lets the caller validate that exact
    /// snapshot and select compatible destinations, then publishes its bytes as
    /// one recoverable output set. Existing targets must already be private.
    /// </summary>
    public static IReadOnlyList<string> InstallMany(
        ResourceBudgetLedger resourceBudgets,
        string sourcePath,
        Func<string, IEnumerable<string>> validateAndSelectDestinations) =>
        InstallMany(resourceBudgets, sourcePath, validateAndSelectDestinations,
            NoPublicationFaultInjector.Instance);

    /// <summary>
    /// Installs a caller-owned readable stream through the same private
    /// snapshot and validation boundary. The input stream remains open.
    /// </summary>
    public static IReadOnlyList<string> InstallMany(
        ResourceBudgetLedger resourceBudgets,
        Stream source,
        Func<string, IEnumerable<string>> validateAndSelectDestinations) =>
        InstallMany(resourceBudgets, source, validateAndSelectDestinations,
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
        byte[] contents;
        using (var source = new FileStream(Path.GetFullPath(sourcePath),
            FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            contents = ReadSource(resourceBudgets, source);
        }
        return InstallSnapshot(resourceBudgets, contents, validateAndSelectDestinations, faults);
    }

    internal static IReadOnlyList<string> InstallMany(
        ResourceBudgetLedger resourceBudgets,
        Stream source,
        Func<string, IEnumerable<string>> validateAndSelectDestinations,
        IPublicationFaultInjector faults)
    {
        ArgumentNullException.ThrowIfNull(resourceBudgets);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(validateAndSelectDestinations);
        ArgumentNullException.ThrowIfNull(faults);
        if (!source.CanRead)
        {
            throw new ArgumentException("The license stream must be readable.", nameof(source));
        }
        return InstallSnapshot(resourceBudgets, ReadSource(resourceBudgets, source),
            validateAndSelectDestinations, faults);
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
            snapshotDirectory = resourceBudgets.OutputSession is { } worker
                ? worker.CreatePrivateDirectory("license-snapshot")
                : PrivateUserStorage.CreateTemporaryDirectory("license-install");
            string snapshot = Path.Combine(snapshotDirectory, "source.lic");
            using (FileStream created = PrivateUserStorage.CreateFile(snapshot))
            {
                created.Write(contents);
                created.Flush(flushToDisk: true);
            }

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
            StringComparison comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (string.Equals(directory, snapshotDirectory, comparison)
                || directory.StartsWith(snapshotDirectory + Path.DirectorySeparatorChar, comparison))
            {
                throw new ArgumentException("A license destination cannot be inside its temporary snapshot.");
            }
            ValidateExistingTargets(destinations);
            EnsurePrivateDirectory(directory);
            foreach (string parent in destinations.Select(static path => Path.GetDirectoryName(path)!).Distinct())
            {
                EnsurePrivateDirectory(parent);
            }

            using var transaction = new AtomicOutputSetWriter(
                new SafeFileWriter(resourceBudgets), directory, "license-install", faults);
            ValidateExistingTargets(destinations);
            foreach (string destination in destinations)
            {
                resourceBudgets.Deadline.ThrowIfExpired("license-install-stage");
                transaction.Stage(destination, overwrite: true, staged =>
                {
                    File.WriteAllBytes(staged, contents);
                    PrivateUserStorage.ProtectFile(staged);
                });
            }
            transaction.Commit();
            return destinations;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(contents);
            if (snapshotDirectory is not null)
            {
                PrivateUserStorage.TryDeleteTree(snapshotDirectory);
            }
        }
    }

    /// <summary>
    /// Removes existing private licenses as one recoverable output set and
    /// returns their paths. Missing files are a no-op. A supervised worker only
    /// stages the deletions; the parent uses the same publication transaction.
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
        EnsurePrivateDirectory(directory);
        using var transaction = new AtomicOutputSetWriter(
            new SafeFileWriter(resourceBudgets), directory, "license-remove", faults);
        ValidateExistingTargets(destinations);
        var removed = new List<string>(destinations.Length);
        foreach (string destination in destinations)
        {
            resourceBudgets.Deadline.ThrowIfExpired("license-remove-stage");
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
        using var bounded = new BoundedReadStream(source, budgets,
            ResourceBudgetKinds.InputBytes, "license-snapshot-read", leaveOpen: true);
        using var contents = new BudgetedMemoryStream(budgets,
            ResourceBudgetKinds.MemoryBufferBytes, "license-snapshot-buffer");
        Span<byte> chunk = stackalloc byte[4096];
        try
        {
            while (true)
            {
                budgets.Deadline.ThrowIfExpired("license-snapshot-read");
                int requested = Math.Min(chunk.Length, MaximumBytes - checked((int)contents.Length) + 1);
                int read = bounded.Read(chunk[..requested]);
                if (read == 0)
                {
                    break;
                }
                if (contents.Length + read > MaximumBytes)
                {
                    throw CliErrors.FileTooLarge(contents.Length + read, MaximumBytes);
                }
                contents.Write(chunk[..read]);
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
        PrivateUserStorage.ValidateFile(path);
        using var snapshot = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        bool matches = snapshot.Length == contents.Length;
        Span<byte> buffer = stackalloc byte[4096];
        int offset = 0;
        while (matches && offset < contents.Length)
        {
            budgets.Deadline.ThrowIfExpired("license-snapshot-verify");
            int read = snapshot.Read(buffer[..Math.Min(buffer.Length, contents.Length - offset)]);
            matches = read > 0 && buffer[..read].SequenceEqual(contents.AsSpan(offset, read));
            offset += read;
        }
        if (!matches || snapshot.ReadByte() >= 0)
        {
            throw CliErrors.LicenseInvalid("file", "the private license snapshot changed during validation");
        }
    }

    private static string[] NormalizePaths(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return paths.Select(Path.GetFullPath)
            .Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .ToArray();
    }

    private static string CommonDirectory(IReadOnlyList<string> paths)
    {
        string directory = Path.GetDirectoryName(paths[0])!;
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        foreach (string path in paths.Skip(1))
        {
            string parent = Path.GetDirectoryName(path)!;
            while (!string.Equals(parent, directory, comparison)
                && !parent.StartsWith(Path.TrimEndingDirectorySeparator(directory)
                    + Path.DirectorySeparatorChar, comparison))
            {
                directory = Path.GetDirectoryName(directory)
                    ?? throw new ArgumentException("License destinations must share a filesystem root.", nameof(paths));
            }
        }
        if (string.Equals(directory, Path.GetPathRoot(directory), comparison))
        {
            throw new ArgumentException("License storage must be below the filesystem root.", nameof(paths));
        }
        return directory;
    }

    private static void EnsurePrivateDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            // A common ancestor can cover several configured directories. Do
            // not rewrite unrelated ancestor permissions to make it private.
            PrivateUserStorage.ValidateDirectory(directory);
        }
        else
        {
            PrivateUserStorage.EnsureDirectory(directory);
        }
    }
    private static void ValidateExistingTargets(IEnumerable<string> paths)
    {
        foreach (string path in paths)
        {
            OutputPathValidator.EnsureSafeFile(path);
            if (!File.Exists(path))
            {
                continue;
            }
            try
            {
                PrivateUserStorage.ValidateFile(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw CliErrors.OutputUnwritable(path,
                    "the existing license file is not private; it was preserved", exception);
            }
        }
    }
}
