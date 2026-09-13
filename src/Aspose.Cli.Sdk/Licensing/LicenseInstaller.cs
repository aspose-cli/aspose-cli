using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Sdk.Licensing;

/// <summary>
/// The filesystem side of <c>aspose-cli license install</c> / <c>remove</c>:
/// copying a license into place and deleting it. Validation and state
/// reporting stay with the caller; this type only touches the file, and takes
/// the destination explicitly so it is testable and never hard-codes a location.
/// </summary>
public static class LicenseInstaller
{
    /// <summary>
    /// Copies <paramref name="sourcePath"/> to <paramref name="destinationPath"/>
    /// atomically, creating the destination directory and overwriting any existing
    /// file, and returns the destination path. On Unix the installed copy is made
    /// readable and writable by its owner only.
    /// </summary>
    public static string Install(
        ResourceBudgetLedger resourceBudgets,
        string sourcePath,
        string destinationPath)
    {
        ArgumentNullException.ThrowIfNull(resourceBudgets);
        ArgumentException.ThrowIfNullOrEmpty(sourcePath);
        ArgumentException.ThrowIfNullOrEmpty(destinationPath);

        string fullSource = Path.GetFullPath(sourcePath);
        string fullDestination = Path.GetFullPath(destinationPath);
        PrivateUserStorage.EnsureDirectory(
            Path.GetDirectoryName(fullDestination)!);

        var writer = new SafeFileWriter(resourceBudgets);
        writer.Write(
            fullDestination,
            overwrite: true,
            temporary =>
            {
                File.Copy(fullSource, temporary, overwrite: true);
                HardenStagedPermissions(temporary);
            });
        HardenPermissions(fullDestination);
        return fullDestination;
    }

    /// <summary>
    /// Installs one validated source for several products as a transactional
    /// batch. If any destination fails, every destination is restored to its
    /// previous state.
    /// </summary>
    public static IReadOnlyList<string> InstallMany(
        ResourceBudgetLedger resourceBudgets,
        string sourcePath,
        IEnumerable<string> destinationPaths)
    {
        ArgumentNullException.ThrowIfNull(resourceBudgets);
        ArgumentException.ThrowIfNullOrEmpty(sourcePath);
        ArgumentNullException.ThrowIfNull(destinationPaths);

        string fullSource = Path.GetFullPath(sourcePath);
        string[] destinations = destinationPaths
            .Select(Path.GetFullPath)
            .Distinct(OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal)
            .ToArray();
        if (destinations.Length == 0)
        {
            throw new ArgumentException(
                "At least one destination path is required.",
                nameof(destinationPaths));
        }

        string transactionRoot = Path.GetDirectoryName(destinations[0])!;
        foreach (string directory in destinations
                     .Select(static destination =>
                         Path.GetDirectoryName(destination)!)
                     .Distinct(OperatingSystem.IsWindows()
                         ? StringComparer.OrdinalIgnoreCase
                         : StringComparer.Ordinal))
        {
            PrivateUserStorage.EnsureDirectory(directory);
        }

        using var transaction = new AtomicOutputSetWriter(
            new SafeFileWriter(resourceBudgets),
            transactionRoot,
            "license-install");
        foreach (string destination in destinations)
        {
            transaction.Stage(
                destination,
                overwrite: true,
                staged =>
                {
                    File.Copy(fullSource, staged, overwrite: true);
                    HardenStagedPermissions(staged);
                });
        }

        transaction.Commit();
        foreach (string destination in destinations)
        {
            HardenPermissions(destination);
        }

        return destinations;
    }

    /// <summary>
    /// Deletes the license at <paramref name="destinationPath"/> if it exists;
    /// returns <c>true</c> when a file was removed, <c>false</c> when there was
    /// nothing to remove.
    /// </summary>
    public static bool Remove(string destinationPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(destinationPath);

        if (WorkerOutputSession.IsActive)
        {
            return WorkerOutputSession.Delete(destinationPath);
        }

        if (!File.Exists(destinationPath))
        {
            return false;
        }

        File.Delete(destinationPath);
        return true;
    }

    private static void HardenPermissions(string path)
    {
        PrivateUserStorage.ProtectFile(path);
    }

    private static void HardenStagedPermissions(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

}
