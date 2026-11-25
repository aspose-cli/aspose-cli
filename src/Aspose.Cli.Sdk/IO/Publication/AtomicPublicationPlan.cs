using System.Diagnostics;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Files;
using Aspose.Cli.Sdk.IO.Paths;

namespace Aspose.Cli.Sdk.IO.Publication;

/// <summary>Owns one immutable publication location and the entries staged there.</summary>
internal sealed class AtomicPublicationPlan
{
    private readonly IPublicationFaultInjector _faults;
    private bool _sealed;
    internal HashSet<string> CreatedStagingDirectories { get; } = [];
    private readonly OwnedOutputDirectories _outputDirectories;
    internal IEnumerable<string> OutputDirectories => _outputDirectories.Declared;

    private AtomicPublicationPlan(
        string stagingDirectory,
        IPublicationFaultInjector faults,
        string targetDirectory,
        OwnedOutputDirectories outputDirectories,
        ResourceBudgetLedger resourceBudgets)
    {
        StagingDirectory = stagingDirectory;
        _faults = faults;
        _outputDirectories = outputDirectories;
        ResourceBudgets = resourceBudgets;
        TargetDirectory = targetDirectory;
        _initialDirectoryIdentity = FilePublicationOwnedDelete.TryGetDirectoryIdentity(targetDirectory);
    }

    private readonly FilePhysicalIdentity? _initialDirectoryIdentity;
    public string TargetDirectory { get; }
    public string StagingDirectory { get; }

    public List<PublicationEntry> Entries { get; } = [];

    public PublicationTransactionState State { get; set; } = PublicationTransactionState.Staging;

    public IPublicationFaultInjector Faults => _faults;

    internal ResourceBudgetLedger ResourceBudgets { get; }

    public string StagedPath(string target, int index) => StagedPath(StagingDirectory, target, index);

    public string DisplacedPath(int index) =>
        Path.Combine(StagingDirectory, "backups", $"{index + 1:000000}.displaced");

    public bool IsTerminal =>
        State is PublicationTransactionState.Committed
            or PublicationTransactionState.RolledBack
            or PublicationTransactionState.Partial;

    public bool CanCleanUp =>
        State is PublicationTransactionState.Committed or PublicationTransactionState.RolledBack;

    public static AtomicPublicationPlan Create(
        string targetDirectory,
        IPublicationFaultInjector faults, ResourceBudgetLedger resourceBudgets)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetDirectory);
        string root = Path.GetFullPath(targetDirectory);
        var directories = new OwnedOutputDirectories();
        try
        {
            directories.Ensure(root);
            string stagingDirectory = CreateLocalStagingDirectory(root);
            return new AtomicPublicationPlan(
                stagingDirectory,
                faults,
                root,
                directories,
                resourceBudgets);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            directories.CleanUp();
            throw Aspose.Cli.Sdk.Errors.CliErrors.OutputUnwritable(root,
                "the transaction directory could not be created", error, "output-set-admission");
        }
    }

    internal static string StagedPath(
        string transactionDirectory,
        string target,
        int index)
    {
        return Path.Combine(transactionDirectory, $"output-{index + 1:000000}", Path.GetFileName(target));
    }

    internal void EnsureTarget(string target)
    {
        string parent = Path.GetDirectoryName(Path.GetFullPath(target))!;
        if (!FilePaths.IsWithin(TargetDirectory, parent, allowEqual: true))
        {
            throw new IOException($"Publication target '{target}' is outside '{TargetDirectory}'.");
        }
        OutputPathValidator.EnsureSafeFile(target);
    }

    internal void EnsureOutputDirectory(string path)
    {
        if (_sealed) { throw new InvalidOperationException("The output set is sealed."); }
        string full = Path.GetFullPath(path);
        string relative = Path.GetRelativePath(TargetDirectory, full);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        { throw new IOException("The output directory is outside the transaction root."); }
        ResourceBudgets.Cancellation.ThrowIfCancellationRequested();
        _outputDirectories.Ensure(full);
    }

    internal void CleanUpOutputDirectories()
    {
        if (State != PublicationTransactionState.Committed && CanCleanUp)
        { _outputDirectories.CleanUp(); }
    }

    internal void Seal()
    {
        if (_sealed) { throw new InvalidOperationException("The output set is already sealed."); }
        EnsureNoUnknownArtifacts();
        ResourceBudgets.EnsureWithin(ResourceBudgetKinds.OutputSetDirectories, OutputDirectories.Count(), "items", "output-directory-admission");
        _sealed = true;
    }

    internal void EnsureCapacityForEntry()
    {
        if (_sealed || State != PublicationTransactionState.Staging)
        { throw new InvalidOperationException("The output set is sealed."); }
        ResourceBudgets.EnsureWithin(ResourceBudgetKinds.OutputSetEntries, Entries.Count + 1L, "items", "output-set-admission");
        if (Entries.Count >= PublicationLimits.MaximumEntries)
        { throw Aspose.Cli.Sdk.Errors.CliErrors.OutputBudgetExceeded(ResourceBudgetKinds.OutputSetEntries, Entries.Count + 1L, PublicationLimits.MaximumEntries, "items", "output-set-admission"); }
    }

    internal void BeginCommit()
    {
        ResourceBudgets.Cancellation.ThrowIfCancellationRequested();
        if (_initialDirectoryIdentity != FilePublicationOwnedDelete.TryGetDirectoryIdentity(TargetDirectory))
        {
            throw new IOException("The publication root changed during production.");
        }
    }

    public void CleanUp()
    {
        try
        {
            _faults.Hit(new PublicationFaultPoint(
                PublicationFaultKind.Cleanup,
                -1,
                StagingDirectory));
            EnsureNoUnknownArtifacts();
            foreach (PublicationEntry entry in Entries)
            {
                DeleteKnownFile(entry.Staged, entry.StagedSnapshot);
                if (entry.Displaced is not null)
                {
                    DeleteKnownFile(
                        entry.Displaced,
                        entry.DisplacedSnapshot ?? entry.Original);
                }
            }

            foreach (string directory in Entries.Select(entry => Path.GetDirectoryName(entry.Staged)!).Concat(CreatedStagingDirectories).Distinct())
            {
                DeleteEmptyDirectory(directory);
            }
            string backups = Path.Combine(StagingDirectory, "backups");
            DeleteEmptyDirectory(backups);
            DeleteEmptyDirectory(StagingDirectory);
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning(
                "Publication cleanup failed for staging directory '{0}' ({1}).",
                StagingDirectory,
                exception.GetType().Name);
        }
    }

    internal void EnsureNoUnknownArtifacts()
    {
        var allowedFiles = new HashSet<string>(FilePaths.Comparer);
        var allowedDirectories = new HashSet<string>(FilePaths.Comparer) { Path.Combine(StagingDirectory, "backups") };
        foreach (PublicationEntry entry in Entries)
        {
            allowedDirectories.Add(Path.GetDirectoryName(entry.Staged)!);
            foreach (string? file in new[] { entry.Staged, entry.Displaced })
            {
                if (file is not null) { allowedFiles.Add(Path.GetFullPath(file)); }
            }
        }
        allowedDirectories.UnionWith(CreatedStagingDirectories);
        var pending = new Stack<string>();
        pending.Push(StagingDirectory);
        while (pending.TryPop(out string? directory))
        {
            foreach (FileSystemInfo item in new DirectoryInfo(directory).EnumerateFileSystemInfos())
            {
                if ((item.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException($"Publication preserved linked artifact '{item.FullName}'.");
                }
                if (item is DirectoryInfo)
                {
                    if (!allowedDirectories.Contains(item.FullName))
                    {
                        throw new IOException($"Publication preserved unknown directory '{item.FullName}'.");
                    }
                    pending.Push(item.FullName);
                }
                else if (!allowedFiles.Contains(item.FullName))
                {
                    throw new IOException($"Publication preserved unknown file '{item.FullName}'.");
                }
            }
        }
    }

    private static void DeleteKnownFile(
        string path,
        FilePublicationSnapshot? expected)
    {
        if (!File.Exists(path))
        {
            return;
        }
        // The swap gives a staged file the attributes of the target it replaces, and a displaced
        // file is that target, so either may be read-only.
        if (expected is null
            || !FilePublicationOwnedDelete.TryDelete(path, expected, clearReadOnly: true))
        {
            throw new IOException(
                $"Publication cleanup preserved changed or unknown file '{path}'.");
        }
    }

    private static void DeleteEmptyDirectory(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: false);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    private static string CreateLocalStagingDirectory(string root)
    {
        string stagingDirectory = Path.Combine(
            root,
            $".aspose-publication-{Guid.NewGuid():N}");
        return PublicationTransactionDirectory.Create(stagingDirectory);
    }
}
