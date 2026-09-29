using System.Diagnostics;
using System.Text.Json;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;

namespace Aspose.Cli.Sdk.IO;

/// <summary>
/// Publishes one new immutable directory with complete-or-absent visibility.
/// The caller owns the candidate contents; replacement and mixed file/directory output sets are unsupported.
/// </summary>
public sealed class AtomicNewDirectoryWriter : IDisposable
{
    private readonly ResourceBudgetLedger _budgets;
    private readonly IPublicationFaultInjector _faults;
    private readonly OwnedOutputDirectories _parents;
    private readonly string _transaction;
    private readonly FilePhysicalIdentity? _transactionIdentity;
    private readonly FilePhysicalIdentity? _candidateIdentity;
    private readonly string _anchor;
    private readonly FilePhysicalIdentity? _anchorIdentity;
    private readonly PublicationJournal _journal;
    private NewDirectoryOutput? _output;
    private bool _completed;
    private bool _disposed;

    public AtomicNewDirectoryWriter(ResourceBudgetLedger budgets, string targetDirectory, string operation)
        : this(budgets, targetDirectory, operation, NoPublicationFaultInjector.Instance) { }

    internal AtomicNewDirectoryWriter(ResourceBudgetLedger budgets, string targetDirectory, string operation,
        IPublicationFaultInjector faults)
    {
        _budgets = budgets ?? throw new ArgumentNullException(nameof(budgets));
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        if (operation.Length > 128) { throw new ArgumentOutOfRangeException(nameof(operation)); }
        _faults = faults ?? throw new ArgumentNullException(nameof(faults));
        budgets.ThrowIfFailed();
        budgets.Deadline.ThrowIfExpired("directory-start");
        TargetDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetDirectory));
        NewDirectoryPublication.EnsureAbsent(TargetDirectory);
        string parent = Path.GetDirectoryName(TargetDirectory)
            ?? throw CliErrors.OutputUnwritable(TargetDirectory, "the directory has no parent");
        _anchor = parent;
        while (!Directory.Exists(_anchor)) { _anchor = Path.GetDirectoryName(_anchor)!; }
        _anchorIdentity = FilePublicationOwnedDelete.TryGetDirectoryIdentity(_anchor);
        NewDirectoryPublication.EnsureAnchor(_anchor, _anchorIdentity);
        _parents = new OwnedOutputDirectories(budgets.OutputSession is not null);
        _journal = new PublicationJournal { Operation = operation, State = PublicationTransactionState.Staging };
        if (budgets.OutputSession is null) { AtomicPublicationRecovery.RecoverPendingHierarchy(parent, budgets.Deadline); }
        _parents.Ensure(parent);
        _transaction = budgets.OutputSession?.CreateDirectory("directory")
            ?? PublicationTransactionDirectory.Create(Path.Combine(parent,
                $".aspose-publication-{Environment.ProcessId}-{PublicationJournal.CurrentProcessStartUtcTicks}-{Guid.NewGuid():N}"));
        _transactionIdentity = FilePublicationOwnedDelete.TryGetDirectoryIdentity(_transaction);
        StagingDirectory = Directory.CreateDirectory(Path.Combine(_transaction, "directory")).FullName;
        _candidateIdentity = FilePublicationOwnedDelete.TryGetDirectoryIdentity(StagingDirectory);
    }

    public string TargetDirectory { get; }
    public string StagingDirectory { get; }

    /// <summary>Seals the candidate and either retains it for the parent or publishes it once.</summary>
    public void Commit()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_completed) { throw new InvalidOperationException("The directory output is already committed."); }
        _budgets.ThrowIfFailed();
        _budgets.Deadline.ThrowIfExpired("directory-seal");
        NewDirectoryPublication.EnsureAnchor(_transaction, _transactionIdentity);
        NewDirectoryPublication.EnsureAnchor(StagingDirectory, _candidateIdentity);
        DirectoryTreeSnapshot tree = NewDirectoryPublication.Capture(StagingDirectory, _budgets);
        _output = new NewDirectoryOutput(TargetDirectory, StagingDirectory, _anchor, _anchorIdentity, _transactionIdentity, tree);
        _budgets.Consume(ResourceBudgetKinds.OutputSetEntries, 1, "items", "directory-seal");
        _budgets.Consume(ResourceBudgetKinds.OutputSetDirectories, tree.Directories.Count + _parents.Declared.Count(), "items", "directory-seal");
        _budgets.Consume(ResourceBudgetKinds.OutputBytes, tree.Files.Sum(file => file.Snapshot.Length), "bytes", "directory-seal");
        _journal.DirectoryOutput = _output;
        _budgets.EnsureWithin(ResourceBudgetKinds.PublicationMetadataBytes,
            _journal.EnsureLifecycleCapacity(JournalPath), "bytes", "directory-seal");
        if (_budgets.OutputSession is { } worker)
        {
            worker.RegisterDirectory(_output, _budgets.Deadline);
            _completed = true;
            return;
        }

        Persist(PublicationTransactionState.Prepared);
        using PublicationDirectoryLease lease = PublicationDirectoryLease.Acquire(
            Path.GetDirectoryName(TargetDirectory)!, [Path.GetDirectoryName(TargetDirectory)!], _budgets.Deadline);
        NewDirectoryPublication.EnsureAnchor(_anchor, _anchorIdentity);
        lease.EnsureDirectoryUnchanged();
        NewDirectoryPublication.EnsureAbsent(TargetDirectory);
        NewDirectoryPublication.ValidateTree(StagingDirectory, tree, _budgets.Deadline);
        Persist(PublicationTransactionState.Publishing);
        _faults.Hit(new PublicationFaultPoint(PublicationFaultKind.Publish, 0, TargetDirectory));
        _budgets.Deadline.ThrowIfExpired("directory-rename");
        NewDirectoryPublication.EnsureAnchor(_anchor, _anchorIdentity);
        lease.EnsureDirectoryUnchanged();
        NewDirectoryPublication.EnsureAbsent(TargetDirectory);
        NewDirectoryPublication.ValidateTree(StagingDirectory, tree, _budgets.Deadline);
        _budgets.Deadline.ThrowIfExpired("directory-rename");
        Directory.Move(StagingDirectory, TargetDirectory);
        // Rename is the commit point: later cancellation or journal/cleanup failure cannot undo success.
        _completed = true;
        FilePublicationInheritance.TryReset(TargetDirectory);
        _budgets.MarkOutputsCommitted();
        try { Persist(PublicationTransactionState.Committed); }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or CliException)
        { Trace.TraceWarning("Directory publication committed; its journal was retained ({0}).", error.GetType().Name); }
        CleanUp();
    }

    private string JournalPath => Path.Combine(_transaction, AtomicPublicationPlan.JournalName);

    private void Persist(PublicationTransactionState state)
    {
        _journal.State = state;
        _faults.Hit(new PublicationFaultPoint(PublicationFaultKind.JournalWrite, -1, JournalPath));
        _journal.Write(JournalPath, deadline: state == PublicationTransactionState.Committed ? null : _budgets.Deadline);
    }

    public void Dispose()
    {
        if (_disposed) { return; }
        _disposed = true;
        if (!_completed || _budgets.OutputSession is null) { CleanUp(); }
        if (!_completed) { _parents.CleanUp(); }
    }

    private void CleanUp()
    {
        try
        {
            _faults.Hit(new PublicationFaultPoint(PublicationFaultKind.Cleanup, -1, _transaction));
            if (Directory.Exists(StagingDirectory))
            {
                NewDirectoryPublication.EnsureAnchor(_transaction, _transactionIdentity);
                NewDirectoryPublication.EnsureAnchor(StagingDirectory, _candidateIdentity);
                DirectoryTreeSnapshot tree = _output?.Tree ?? NewDirectoryPublication.Capture(StagingDirectory);
                NewDirectoryPublication.DeleteTree(StagingDirectory, tree);
            }
            NewDirectoryPublication.DeleteTransaction(_transaction, _transactionIdentity);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or CliException)
        { Trace.TraceWarning("Directory publication retained unverified staging ({0}).", error.GetType().Name); }
    }
}

internal sealed record DirectoryOutputFile(string Path, FilePublicationSnapshot Snapshot);
internal sealed record DirectoryOutputDirectory(string Path, FilePhysicalIdentity? Identity);
internal sealed record DirectoryTreeSnapshot(
    IReadOnlyList<DirectoryOutputFile> Files, IReadOnlyList<DirectoryOutputDirectory> Directories);
internal sealed record NewDirectoryOutput(string Target, string Staged, string ParentAnchor,
    FilePhysicalIdentity? ParentIdentity, FilePhysicalIdentity? TransactionIdentity, DirectoryTreeSnapshot Tree);

/// <summary>Bounded tree validation shared by directory production, worker transport and recovery.</summary>
internal static class NewDirectoryPublication
{
    internal const int MaximumFiles = PublicationLimits.MaximumDirectoryFiles;
    private static StringComparer Comparer => WorkerManifestStore.PathComparer;

    internal static DirectoryTreeSnapshot Capture(string root, ResourceBudgetLedger? budgets = null, OperationDeadline? deadline = null)
    {
        OutputPathValidator.EnsureSafeDirectory(root);
        var files = new List<DirectoryOutputFile>();
        var directories = new List<DirectoryOutputDirectory>();
        var pending = new Stack<string>();
        pending.Push(root);
        long bytes = 0;
        while (pending.TryPop(out string? directory))
        {
            (budgets?.Deadline ?? deadline)?.ThrowIfExpired("directory-snapshot");
            OutputPathValidator.EnsureSafeDirectory(directory);
            if (directories.Count >= PublicationLimits.MaximumDirectories)
            { throw new InvalidDataException("The directory output exceeds its directory limit."); }
            directories.Add(new DirectoryOutputDirectory(Path.GetRelativePath(root, directory),
                FilePublicationOwnedDelete.TryGetDirectoryIdentity(directory)));
            foreach (FileSystemInfo item in new DirectoryInfo(directory).EnumerateFileSystemInfos())
            {
                (budgets?.Deadline ?? deadline)?.ThrowIfExpired("directory-snapshot");
                if ((item.Attributes & FileAttributes.ReparsePoint) != 0 || item.LinkTarget is not null)
                { throw new InvalidDataException("Directory outputs cannot contain links."); }
                if (item is DirectoryInfo)
                {
                    if (directories.Count + pending.Count >= PublicationLimits.MaximumDirectories)
                    { throw new InvalidDataException("The directory output exceeds its directory limit."); }
                    pending.Push(item.FullName); continue;
                }
                if (item is not FileInfo file || files.Count >= MaximumFiles)
                { throw new InvalidDataException("The directory output exceeds its file limit."); }
                bytes = checked(bytes + file.Length);
                if (bytes > ResourceBudgetDefaults.MaximumOutputBytes)
                { throw new InvalidDataException("The directory output exceeds its byte limit."); }
                budgets?.EnsureWithin(ResourceBudgetKinds.OutputBytes, bytes, "bytes", "directory-snapshot");
                OutputPathValidator.EnsureSafeFile(file.FullName);
                FilePublicationSnapshot snapshot = FilePublicationSnapshot.Capture(file.FullName);
                if (!snapshot.Exists) { throw new IOException("A directory output file disappeared."); }
                if (OperatingSystem.IsWindows())
                {
                    using var handle = OpenedFileBoundary.OpenNoFollow(file.FullName, directory: false);
                    if (!OpenedFileBoundary.IsRegularSingleLinkFile(handle))
                    { throw new InvalidDataException("Directory output files must be regular single-link files."); }
                }
                files.Add(new DirectoryOutputFile(Path.GetRelativePath(root, file.FullName), snapshot));
            }
        }
        return new DirectoryTreeSnapshot(files.OrderBy(file => file.Path, Comparer).ToArray(),
            directories.OrderBy(directory => directory.Path, Comparer).ToArray());
    }

    internal static void ValidateDescriptor(NewDirectoryOutput output)
    {
        if (output.Tree is null || output.Tree.Files is null || output.Tree.Directories is null
            || output.Tree.Files.Count > MaximumFiles || output.Tree.Directories.Count is < 1 or > PublicationLimits.MaximumDirectories)
        { throw new InvalidDataException("Invalid bounded directory output."); }
        foreach (string path in new[] { output.Target, output.Staged, output.ParentAnchor })
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || !Comparer.Equals(path, Path.GetFullPath(path)))
            { throw new InvalidDataException("Directory output paths must be canonical and absolute."); }
            OutputPathValidator.EnsureSafeDirectory(path);
        }
        string relative = Path.GetRelativePath(output.ParentAnchor, Path.GetDirectoryName(output.Target)!);
        if (Escapes(relative) || OperatingSystem.IsWindows() && (output.ParentIdentity is null || output.TransactionIdentity is null))
        { throw new InvalidDataException("Invalid directory output parent boundary."); }
        EnsureAnchor(Path.GetDirectoryName(output.Staged)!, output.TransactionIdentity);
        var paths = new HashSet<string>(Comparer);
        var dirs = new HashSet<string>(Comparer);
        foreach (DirectoryOutputDirectory directory in output.Tree.Directories)
        {
            if (directory is null || !SafeRelative(directory.Path, allowRoot: true) || !paths.Add(directory.Path)
                || OperatingSystem.IsWindows() && directory.Identity is null)
            { throw new InvalidDataException("Invalid directory output tree directories."); }
            dirs.Add(directory.Path);
        }
        if (!dirs.Contains(".")) { throw new InvalidDataException("The directory output root is missing."); }
        foreach (DirectoryOutputFile file in output.Tree.Files)
        {
            if (file is null || !SafeRelative(file.Path, allowRoot: false) || !paths.Add(file.Path)
                || file.Snapshot is null || !file.Snapshot.Exists || !file.Snapshot.IsStructurallyValid())
            { throw new InvalidDataException("Invalid directory output tree files."); }
        }
        foreach (string path in paths.Where(path => path != "."))
        {
            string parent = Path.GetDirectoryName(path) is { Length: > 0 } value ? value : ".";
            if (!dirs.Contains(parent)) { throw new InvalidDataException("A directory output parent is undeclared."); }
        }
    }

    private static bool SafeRelative(string path, bool allowRoot) =>
        !string.IsNullOrWhiteSpace(path) && (allowRoot && path == "."
            || !Escapes(path) && !Path.IsPathRooted(path)
            && path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]).All(segment =>
                segment.Length > 0 && segment is not "." and not ".." && !ExtractionPathValidator.IsUnsafeSegment(segment)));

    private static bool Escapes(string relative) => Path.IsPathRooted(relative) || relative == ".."
        || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    internal static void EnsureAnchor(string anchor, FilePhysicalIdentity? identity)
    {
        OutputPathValidator.EnsureSafeDirectory(anchor);
        if (!Directory.Exists(anchor) || OperatingSystem.IsWindows()
            && (identity is null || identity != FilePublicationOwnedDelete.TryGetDirectoryIdentity(anchor)))
        { throw new IOException("The directory output parent changed during production."); }
    }

    internal static void EnsureAbsent(string target)
    {
        OutputPathValidator.EnsureSafeDirectory(target);
        if (File.Exists(target) || Directory.Exists(target))
        { throw CliErrors.OutputExists(target); }
    }

    internal static void ValidateTree(string root, DirectoryTreeSnapshot expected, OperationDeadline? deadline = null)
    {
        using OperationDeadline? owned = deadline is null ? OperationDeadline.Start(null) : null;
        DirectoryTreeSnapshot actual = Capture(root, deadline: deadline ?? owned!);
        if (actual.Files.Count != expected.Files.Count || actual.Directories.Count != expected.Directories.Count)
        { throw new IOException("The directory output tree changed after sealing."); }
        var files = expected.Files.ToDictionary(file => file.Path, Comparer);
        var dirs = expected.Directories.ToDictionary(directory => directory.Path, Comparer);
        if (actual.Files.Any(file => !files.TryGetValue(file.Path, out DirectoryOutputFile? was)
                || !was.Snapshot.VersionEquals(file.Snapshot) || !was.Snapshot.Metadata!.Matches(Path.Combine(root, file.Path)))
            || actual.Directories.Any(directory => !dirs.TryGetValue(directory.Path, out DirectoryOutputDirectory? was)
                || OperatingSystem.IsWindows() && was.Identity != directory.Identity))
        { throw new IOException("The directory output tree changed after sealing."); }
        deadline?.ThrowIfExpired("directory-verification");
    }

    internal static void Copy(NewDirectoryOutput output, string destination, ResourceBudgetLedger budgets)
    {
        ValidateDescriptor(output);
        ValidateTree(output.Staged, output.Tree, budgets.Deadline);
        foreach (DirectoryOutputDirectory directory in output.Tree.Directories.OrderBy(directory => directory.Path.Length))
        {
            budgets.Deadline.ThrowIfExpired("directory-copy");
            Directory.CreateDirectory(Path.Combine(destination, directory.Path));
        }
        byte[] buffer = new byte[81920];
        foreach (DirectoryOutputFile file in output.Tree.Files)
        {
            budgets.Deadline.ThrowIfExpired("directory-copy");
            string source = Path.Combine(output.Staged, file.Path);
            using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var target = new FileStream(Path.Combine(destination, file.Path), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            int count;
            long bytes = 0;
            while ((count = input.Read(buffer)) > 0)
            {
                budgets.Deadline.ThrowIfExpired("directory-copy");
                bytes = checked(bytes + count);
                if (bytes > file.Snapshot.Length) { throw new IOException("A retained directory file grew during copy."); }
                target.Write(buffer, 0, count);
            }
            target.Flush(flushToDisk: true);
            if (bytes != file.Snapshot.Length || !file.Snapshot.VersionEquals(FilePublicationSnapshot.Capture(source)))
            { throw new IOException("A retained directory file changed during copy."); }
        }
        ValidateTree(output.Staged, output.Tree, budgets.Deadline);
    }

    internal static void DeleteTree(string root, DirectoryTreeSnapshot tree)
    {
        ValidateTree(root, tree);
        foreach (DirectoryOutputFile file in tree.Files)
        {
            if (!FilePublicationOwnedDelete.TryDelete(Path.Combine(root, file.Path), file.Snapshot))
            { throw new IOException("A changed directory output file was preserved."); }
        }
        foreach (DirectoryOutputDirectory directory in tree.Directories.OrderByDescending(directory => directory.Path == "." ? -1 : directory.Path.Length))
        {
            if (!FilePublicationOwnedDelete.TryDeleteDirectory(Path.Combine(root, directory.Path), directory.Identity))
            { throw new IOException("A replaced directory output directory was preserved."); }
        }
    }

    internal static void DeleteTransaction(string transaction, FilePhysicalIdentity? identity)
    {
        if (!Directory.Exists(transaction)) { return; }
        EnsureAnchor(transaction, identity);
        foreach (string entry in Directory.EnumerateFileSystemEntries(transaction))
        {
            if (Path.GetFileName(entry) != AtomicPublicationPlan.JournalName && !PublicationJournal.IsTemporaryPath(entry))
            { throw new IOException("Unknown directory publication staging was preserved."); }
            if (Path.GetFileName(entry) == AtomicPublicationPlan.JournalName)
            { PublicationJournal.Delete(entry); continue; }
            if (!FilePublicationOwnedDelete.TryDelete(entry, FilePublicationSnapshot.Capture(entry)))
            { throw new IOException("A changed directory publication journal was preserved."); }
        }
        if (!FilePublicationOwnedDelete.TryDeleteDirectory(transaction, identity))
        { throw new IOException("A replaced directory publication transaction was preserved."); }
    }

    internal static bool Recover(string root, string transaction, PublicationJournal journal,
        PublicationDirectoryLease? suppliedLease, OperationDeadline? deadline)
    {
        NewDirectoryOutput output = journal.DirectoryOutput!;
        ValidateDescriptor(output);
        if (!Enum.IsDefined(journal.State) || journal.Entries.Count != 0 || !Comparer.Equals(Path.GetDirectoryName(output.Target), root)
            || !Comparer.Equals(output.Staged, Path.Combine(transaction, "directory")))
        { throw new InvalidDataException("The directory publication journal is outside its transaction."); }
        using PublicationDirectoryLease? owned = suppliedLease is null
            ? PublicationDirectoryLease.Acquire(root, [root], deadline) : null;
        if (!(suppliedLease ?? owned!).CoversDirectories([root])) { throw new PublicationLeaseExpansionException(); }
        NewDirectoryOutput current = PublicationJournal.Read(Path.Combine(transaction, AtomicPublicationPlan.JournalName), deadline: deadline).DirectoryOutput
            ?? throw new InvalidDataException("The directory publication journal changed.");
        if (!string.Equals(JsonSerializer.Serialize(output), JsonSerializer.Serialize(current), StringComparison.Ordinal))
        { throw new InvalidDataException("The directory publication journal changed before recovery."); }
        EnsureAnchor(output.ParentAnchor, output.ParentIdentity);
        FilePhysicalIdentity? identity = output.TransactionIdentity;
        if (Directory.Exists(output.Staged)) { DeleteTree(output.Staged, output.Tree); }
        else if (journal.State is PublicationTransactionState.Publishing or PublicationTransactionState.Committed)
        {
            // A missing candidate with the exact original tree at the target proves rename committed.
            ValidateTree(output.Target, output.Tree, deadline);
        }
        DeleteTransaction(transaction, identity);
        return true;
    }
}