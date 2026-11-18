using System.Diagnostics;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Files;
using Aspose.Cli.Sdk.IO.Paths;

namespace Aspose.Cli.Sdk.IO.Publication;

/// <summary>
/// Publishes one immutable directory with complete-or-absent visibility: a new one, or, in
/// replacement mode, one that replaces the directory at the target. A replaced directory is
/// moved into the publication's transaction directory before the candidate takes its place and
/// is deleted only after that rename commits; a publication that fails before then puts it back.
/// The caller owns the candidate contents; mixed file/directory output sets are unsupported.
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
    private readonly bool _replace;
    private readonly string? _transactionParent;
    private NewDirectoryOutput? _output;
    private bool _completed;
    private bool _disposed;

    /// <summary>Starts the publication of a directory at <paramref name="targetDirectory"/>.</summary>
    /// <param name="budgets">The invocation's budgets.</param>
    /// <param name="targetDirectory">The directory to publish.</param>
    /// <param name="operation">The operation, a name of at most 128 characters.</param>
    /// <param name="replace">
    /// Whether the publication replaces a directory that exists at the target (<c>--overwrite</c>);
    /// otherwise an existing target is refused with <c>OUTPUT_EXISTS</c>. A file at the target is
    /// refused either way.
    /// </param>
    /// <param name="transactionParent">
    /// The directory that holds the publication's transaction: by default the target's parent, or
    /// a farther ancestor of the target on the same volume, for a parent whose readers treat every
    /// directory in it as content.
    /// </param>
    public AtomicNewDirectoryWriter(ResourceBudgetLedger budgets, string targetDirectory, string operation, bool replace = false,
        string? transactionParent = null)
        : this(budgets, targetDirectory, operation, NoPublicationFaultInjector.Instance, replace, transactionParent) { }

    internal AtomicNewDirectoryWriter(ResourceBudgetLedger budgets, string targetDirectory, string operation,
        IPublicationFaultInjector faults, bool replace = false, string? transactionParent = null)
    {
        _budgets = budgets ?? throw new ArgumentNullException(nameof(budgets));
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        if (operation.Length > 128) { throw new ArgumentOutOfRangeException(nameof(operation)); }
        _faults = faults ?? throw new ArgumentNullException(nameof(faults));
        budgets.ThrowIfFailed();
        budgets.Deadline.ThrowIfExpired("directory-start");
        TargetDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetDirectory));
        _replace = replace;
        if (replace) { OutputPathValidator.EnsureSafeDirectory(TargetDirectory); }
        else { NewDirectoryPublication.EnsureAbsent(TargetDirectory); }
        string parent = Path.GetDirectoryName(TargetDirectory)
            ?? throw CliErrors.OutputUnwritable(TargetDirectory, "the directory has no parent");
        _transactionParent = transactionParent is null ? null : TransactionParent(parent, transactionParent);
        _anchor = parent;
        while (!Directory.Exists(_anchor)) { _anchor = Path.GetDirectoryName(_anchor)!; }
        _anchorIdentity = FilePublicationOwnedDelete.TryGetDirectoryIdentity(_anchor);
        NewDirectoryPublication.EnsureAnchor(_anchor, _anchorIdentity);
        _parents = new OwnedOutputDirectories();
        _parents.Ensure(parent);
        _transaction = PublicationTransactionDirectory.Create(Path.Combine(_transactionParent ?? parent,
            $".aspose-publication-{Guid.NewGuid():N}"));
        _transactionIdentity = FilePublicationOwnedDelete.TryGetDirectoryIdentity(_transaction);
        StagingDirectory = Directory.CreateDirectory(Path.Combine(_transaction, "directory")).FullName;
        _candidateIdentity = FilePublicationOwnedDelete.TryGetDirectoryIdentity(StagingDirectory);
    }

    public string TargetDirectory { get; }
    public string StagingDirectory { get; }

    // An ancestor of the target's parent on the same volume, so the renames between it and the
    // target stay on one volume.
    private static string TransactionParent(string parent, string transactionParent)
    {
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(transactionParent));
        if (NewDirectoryPublication.Escapes(Path.GetRelativePath(full, parent))
            || !FilePaths.Same(Path.GetPathRoot(full), Path.GetPathRoot(parent)))
        {
            throw new ArgumentException("The transaction parent must be an ancestor of the target on its volume.", nameof(transactionParent));
        }
        OutputPathValidator.EnsureSafeDirectory(full);
        return full;
    }

    // Where a replaced directory waits, inside the transaction, until the replacement commits.
    private string PreviousDirectory => NewDirectoryPublication.PreviousPath(_transaction);

    /// <summary>Seals the candidate and publishes it once.</summary>
    public void Commit() => CommitAdmitting(admitPrevious: null);

    /// <summary>
    /// Seals the candidate and publishes it once, over a target that
    /// <paramref name="admitPrevious"/> admits.
    /// </summary>
    /// <param name="admitPrevious">
    /// Decides, given the target path, whether the publication may take the place of what the
    /// target holds, and throws to refuse it. It runs under the publication's lease after the
    /// directory to replace is captured, so the tree it admits is the one replaced.
    /// </param>
    public void Commit(Action<string> admitPrevious) =>
        CommitAdmitting(admitPrevious ?? throw new ArgumentNullException(nameof(admitPrevious)));

    private void CommitAdmitting(Action<string>? admitPrevious)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_completed) { throw new InvalidOperationException("The directory output is already committed."); }
        _budgets.ThrowIfFailed();
        _budgets.Deadline.ThrowIfExpired("directory-seal");
        NewDirectoryPublication.EnsureAnchor(_transaction, _transactionIdentity);
        NewDirectoryPublication.EnsureAnchor(StagingDirectory, _candidateIdentity);
        DirectoryTreeSnapshot tree = NewDirectoryPublication.Capture(StagingDirectory, _budgets);
        _output = new NewDirectoryOutput(TargetDirectory, StagingDirectory, _anchor, _anchorIdentity, _transactionIdentity, tree, _replace);
        _budgets.Consume(ResourceBudgetKinds.OutputSetEntries, 1, "items", "directory-seal");
        _budgets.Consume(ResourceBudgetKinds.OutputSetDirectories, tree.Directories.Count + _parents.Declared.Count(), "items", "directory-seal");
        _budgets.Consume(ResourceBudgetKinds.OutputBytes, tree.Files.Sum(file => file.Snapshot.Length), "bytes", "directory-seal");
        using PublicationDirectoryLease lease = PublicationDirectoryLease.Acquire(
            Path.GetDirectoryName(TargetDirectory)!, [Path.GetDirectoryName(TargetDirectory)!], _budgets.Deadline);
        NewDirectoryPublication.EnsureAnchor(_anchor, _anchorIdentity);
        lease.EnsureDirectoryUnchanged();
        DirectoryTreeSnapshot? previous = Admit(admitPrevious);
        NewDirectoryPublication.ValidateTree(StagingDirectory, tree, _budgets.Deadline);
        if (previous is not null)
        {
            // The cleanup deletes the replaced tree by this snapshot once the candidate takes its place.
            _output = _output with { Previous = previous };
        }

        _faults.Hit(new PublicationFaultPoint(PublicationFaultKind.Publish, 0, TargetDirectory));
        _budgets.Deadline.ThrowIfExpired("directory-rename");
        NewDirectoryPublication.EnsureAnchor(_anchor, _anchorIdentity);
        lease.EnsureDirectoryUnchanged();
        if (previous is null) { NewDirectoryPublication.EnsureAbsent(TargetDirectory); }
        else { NewDirectoryPublication.ValidateTree(TargetDirectory, previous, _budgets.Deadline); }
        NewDirectoryPublication.ValidateTree(StagingDirectory, tree, _budgets.Deadline);
        _budgets.Deadline.ThrowIfExpired("directory-rename");
        try
        {
            if (previous is not null)
            {
                Directory.Move(TargetDirectory, PreviousDirectory);
                _faults.Hit(new PublicationFaultPoint(PublicationFaultKind.Publish, 1, TargetDirectory));
            }

            Directory.Move(StagingDirectory, TargetDirectory);
        }
        catch (Exception error) when (previous is not null)
        {
            // The candidate did not take the target's place, so the replaced directory returns;
            // one that cannot stays in the transaction, and the error names where it is.
            if (TryRestorePrevious()) { throw; }
            CliException stranded = CliErrors.OutputUnwritable(TargetDirectory,
                "the directory it replaces could not be moved back", error, "directory-rename");
            stranded.Details!["originalPath"] = PreviousDirectory;
            throw stranded;
        }

        // Rename is the commit point: later cancellation or cleanup failure cannot undo success.
        _completed = true;
        FilePublicationInheritance.TryReset(TargetDirectory);
        _budgets.MarkOutputsCommitted();
        CleanUp();
    }

    // Captures what the target holds, refuses what this publication may not take the place of, and
    // returns the directory the candidate replaces, or null when the target does not exist.
    private DirectoryTreeSnapshot? Admit(Action<string>? admitPrevious)
    {
        DirectoryTreeSnapshot? previous = _replace ? CapturePrevious() : null;
        if (previous is null) { NewDirectoryPublication.EnsureAbsent(TargetDirectory); }
        foreach (DirectoryOutputFile file in previous?.Files ?? [])
        {
            _budgets.EnsureNotInput(Path.Combine(TargetDirectory, file.Path), file.Snapshot.PhysicalIdentity);
        }
        admitPrevious?.Invoke(TargetDirectory);
        return previous;
    }

    // The directory the candidate replaces, or null when the target does not exist.
    private DirectoryTreeSnapshot? CapturePrevious()
    {
        if (!Directory.Exists(TargetDirectory)) { return null; }
        try { return NewDirectoryPublication.Capture(TargetDirectory, deadline: _budgets.Deadline); }
        catch (InvalidDataException error)
        {
            throw CliErrors.OutputUnwritable(TargetDirectory, $"the existing directory cannot be replaced: {error.Message}", error);
        }
    }

    // Moves a replaced directory back to the target the candidate did not take; false when it stays displaced.
    private bool TryRestorePrevious()
    {
        if (!Directory.Exists(PreviousDirectory)) { return true; }
        try
        {
            if (Directory.Exists(TargetDirectory) || File.Exists(TargetDirectory)) { return false; }
            Directory.Move(PreviousDirectory, TargetDirectory);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning("A replaced directory stays in its publication transaction ({0}).", error.GetType().Name);
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) { return; }
        _disposed = true;
        CleanUp();
        if (!_completed) { _parents.CleanUp(); }
    }

    private void CleanUp()
    {
        try
        {
            _faults.Hit(new PublicationFaultPoint(PublicationFaultKind.Cleanup, -1, _transaction));
            // A replaced directory that cannot return stays in the transaction with the candidate.
            if (!_completed && !TryRestorePrevious()) { return; }
            if (Directory.Exists(StagingDirectory))
            {
                NewDirectoryPublication.EnsureAnchor(_transaction, _transactionIdentity);
                NewDirectoryPublication.EnsureAnchor(StagingDirectory, _candidateIdentity);
                DirectoryTreeSnapshot tree = _output?.Tree ?? NewDirectoryPublication.Capture(StagingDirectory);
                NewDirectoryPublication.DeleteTree(StagingDirectory, tree);
            }
            if (_completed && _output?.Previous is { } previous && Directory.Exists(PreviousDirectory))
            {
                NewDirectoryPublication.DeleteTree(PreviousDirectory, previous);
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
/// <summary>
/// One directory publication: the candidate tree staged for the target and, when it replaces a
/// directory (<see cref="Replace"/>), the tree it replaces (<see cref="Previous"/>), recorded
/// before that directory moves into the transaction.
/// </summary>
internal sealed record NewDirectoryOutput(string Target, string Staged, string ParentAnchor,
    FilePhysicalIdentity? ParentIdentity, FilePhysicalIdentity? TransactionIdentity, DirectoryTreeSnapshot Tree,
    bool Replace = false, DirectoryTreeSnapshot? Previous = null);

/// <summary>Bounded tree validation of directory production.</summary>
internal static class NewDirectoryPublication
{
    internal const int MaximumFiles = PublicationLimits.MaximumDirectoryFiles;
    /// <summary>Where a replaced directory waits inside its publication transaction.</summary>
    internal static string PreviousPath(string transaction) => Path.Combine(transaction, "previous");

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
                using (var handle = OpenedFileBoundary.OpenNoFollow(file.FullName, directory: false))
                {
                    if (!OpenedFileBoundary.IsRegularSingleLinkFile(handle))
                    { throw new InvalidDataException("Directory output files must be regular single-link files."); }
                }
                files.Add(new DirectoryOutputFile(Path.GetRelativePath(root, file.FullName), snapshot));
            }
        }
        return new DirectoryTreeSnapshot(files.OrderBy(file => file.Path, FilePaths.Comparer).ToArray(),
            directories.OrderBy(directory => directory.Path, FilePaths.Comparer).ToArray());
    }

    internal static bool Escapes(string relative) => Path.IsPathRooted(relative) || relative == ".."
        || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    internal static void EnsureAnchor(string anchor, FilePhysicalIdentity? identity)
    {
        OutputPathValidator.EnsureSafeDirectory(anchor);
        if (!Directory.Exists(anchor)
            || identity is null || identity != FilePublicationOwnedDelete.TryGetDirectoryIdentity(anchor))
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
        var files = expected.Files.ToDictionary(file => file.Path, FilePaths.Comparer);
        var dirs = expected.Directories.ToDictionary(directory => directory.Path, FilePaths.Comparer);
        if (actual.Files.Any(file => !files.TryGetValue(file.Path, out DirectoryOutputFile? was)
                || !was.Snapshot.VersionEquals(file.Snapshot) || !was.Snapshot.Metadata!.Matches(Path.Combine(root, file.Path)))
            || actual.Directories.Any(directory => !dirs.TryGetValue(directory.Path, out DirectoryOutputDirectory? was)
                || was.Identity != directory.Identity))
        { throw new IOException("The directory output tree changed after sealing."); }
        deadline?.ThrowIfExpired("directory-verification");
    }

    // Deletes a candidate or replaced tree that the publication's transaction owns; a read-only
    // file in it, admitted by the snapshot, goes too.
    internal static void DeleteTree(string root, DirectoryTreeSnapshot tree)
    {
        ValidateTree(root, tree);
        foreach (DirectoryOutputFile file in tree.Files)
        {
            if (!FilePublicationOwnedDelete.TryDelete(Path.Combine(root, file.Path), file.Snapshot, clearReadOnly: true))
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
        if (Directory.EnumerateFileSystemEntries(transaction).Any())
        { throw new IOException("Unknown directory publication staging was preserved."); }
        if (!FilePublicationOwnedDelete.TryDeleteDirectory(transaction, identity))
        { throw new IOException("A replaced directory publication transaction was preserved."); }
    }
}