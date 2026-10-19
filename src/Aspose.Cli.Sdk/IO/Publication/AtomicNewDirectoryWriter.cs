using System.Diagnostics;
using System.Text.Json;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Files;
using Aspose.Cli.Sdk.IO.Paths;

namespace Aspose.Cli.Sdk.IO.Publication;

/// <summary>
/// Publishes one immutable directory with complete-or-absent visibility: a new one, or, in
/// replacement mode, one that replaces the directory at the target. A replaced directory is
/// moved into the publication's transaction directory before the candidate takes its place and
/// is deleted only after that rename commits; until then the journal lets a failed or abandoned
/// publication put it back. The caller owns the candidate contents; mixed file/directory output
/// sets are unsupported.
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
    private readonly bool _replace;
    private readonly string? _transactionParent;
    private NewDirectoryOutput? _output;
    private bool _completed;
    private bool _disposed;

    /// <summary>Starts the publication of a directory at <paramref name="targetDirectory"/>.</summary>
    /// <param name="budgets">The invocation's budgets; in a worker, its output session retains the candidate for the parent.</param>
    /// <param name="targetDirectory">The directory to publish.</param>
    /// <param name="operation">The operation the journal names.</param>
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
        _parents = new OwnedOutputDirectories(budgets.OutputSession is not null);
        _journal = new PublicationJournal { Operation = operation, State = PublicationTransactionState.Staging };
        if (budgets.OutputSession is null)
        {
            AtomicPublicationRecovery.RecoverPendingHierarchy(parent, budgets.Deadline);
            if (_transactionParent is not null && !FilePaths.Same(_transactionParent, parent))
            { AtomicPublicationRecovery.RemoveAbandonedCandidatesIn(_transactionParent, budgets.Deadline); }
        }
        _parents.Ensure(parent);
        _transaction = budgets.OutputSession?.CreateDirectory("directory")
            ?? PublicationTransactionDirectory.Create(Path.Combine(_transactionParent ?? parent,
                $".aspose-publication-{Environment.ProcessId}-{ProcessIdentity.CurrentStartUtcTicks}-{Guid.NewGuid():N}"));
        _transactionIdentity = FilePublicationOwnedDelete.TryGetDirectoryIdentity(_transaction);
        StagingDirectory = Directory.CreateDirectory(Path.Combine(_transaction, "directory")).FullName;
        _candidateIdentity = FilePublicationOwnedDelete.TryGetDirectoryIdentity(StagingDirectory);
    }

    public string TargetDirectory { get; }
    public string StagingDirectory { get; }

    // An ancestor of the target's parent on the same volume, so the renames between it and the
    // target stay on one volume and recovery, which scans the target's ancestors, finds it.
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

    /// <summary>Seals the candidate and either retains it for the parent or publishes it once.</summary>
    public void Commit() => Commit(admitPrevious: null, admitted: null);

    /// <summary>
    /// Seals the candidate and either retains it for the parent or publishes it once, over a
    /// target that <paramref name="admitPrevious"/> admits.
    /// </summary>
    /// <param name="admitPrevious">
    /// Decides, given the target path, whether the publication may take the place of what the
    /// target holds, and throws to refuse it. It runs under the publication's lease after the
    /// directory to replace is captured, so the tree it admits is the one replaced. In a worker it
    /// runs there, and the parent that publishes refuses a target that no longer holds the tree
    /// it admitted.
    /// </param>
    public void Commit(Action<string> admitPrevious) =>
        Commit(admitPrevious ?? throw new ArgumentNullException(nameof(admitPrevious)), admitted: null);

    // The parent publishing a worker's candidate passes what the worker admitted.
    internal void Commit(Action<string>? admitPrevious, AdmittedDirectoryPrevious? admitted)
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
        _journal.DirectoryOutput = _output;
        _budgets.EnsureWithin(ResourceBudgetKinds.PublicationMetadataBytes,
            _journal.EnsureLifecycleCapacity(JournalPath), "bytes", "directory-seal");
        if (_budgets.OutputSession is { } worker)
        {
            // The worker admits the target as it is now; the parent publishes only over that tree.
            AdmittedDirectoryPrevious? admittedHere = admitPrevious is null ? null : new AdmittedDirectoryPrevious(Admit(admitPrevious));
            worker.RegisterDirectory(_output, _transactionParent, admittedHere, _budgets.Deadline);
            _completed = true;
            return;
        }

        Persist(PublicationTransactionState.Prepared);
        using PublicationDirectoryLease lease = PublicationDirectoryLease.Acquire(
            Path.GetDirectoryName(TargetDirectory)!, [Path.GetDirectoryName(TargetDirectory)!], _budgets.Deadline);
        NewDirectoryPublication.EnsureAnchor(_anchor, _anchorIdentity);
        lease.EnsureDirectoryUnchanged();
        DirectoryTreeSnapshot? previous = Admit(admitPrevious);
        if (admitted is not null) { EnsureAdmitted(admitted, previous); }
        NewDirectoryPublication.ValidateTree(StagingDirectory, tree, _budgets.Deadline);
        if (previous is not null)
        {
            // The journal names the replaced tree before it moves, so recovery can put it back.
            _output = _output with { Previous = previous };
            _journal.DirectoryOutput = _output;
            _budgets.EnsureWithin(ResourceBudgetKinds.PublicationMetadataBytes,
                _journal.EnsureLifecycleCapacity(JournalPath), "bytes", "directory-seal");
        }

        Persist(PublicationTransactionState.Publishing);
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
        catch when (previous is not null)
        {
            // The candidate did not take the target's place, so the replaced directory returns.
            TryRestorePrevious();
            throw;
        }

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

    // Captures what the target holds, refuses what this publication may not take the place of, and
    // returns the directory the candidate replaces, or null when the target does not exist.
    private DirectoryTreeSnapshot? Admit(Action<string>? admitPrevious)
    {
        DirectoryTreeSnapshot? previous = _replace ? CapturePrevious() : null;
        if (previous is null) { NewDirectoryPublication.EnsureAbsent(TargetDirectory); }
        admitPrevious?.Invoke(TargetDirectory);
        return previous;
    }

    // The target still holds what the worker admitted: the same tree, or still nothing.
    private void EnsureAdmitted(AdmittedDirectoryPrevious admitted, DirectoryTreeSnapshot? previous)
    {
        try
        {
            if ((admitted.Tree is null) != (previous is null)) { throw new IOException("The target directory appeared or disappeared."); }
            if (admitted.Tree is not null) { NewDirectoryPublication.ValidateTree(TargetDirectory, admitted.Tree, _budgets.Deadline); }
        }
        catch (IOException error)
        {
            throw new IOException("The target directory changed after the worker admitted it.", error);
        }
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
            Trace.TraceWarning("A replaced directory stays in its publication transaction for recovery ({0}).", error.GetType().Name);
            return false;
        }
    }

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
            // A replaced directory that cannot return keeps the candidate and the journal, so
            // recovery finds the publication as it stopped and puts the directory back.
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

/// <summary>
/// What a worker admitted at the target of a directory replacement: the tree it held
/// (<see cref="Tree"/>), or nothing when <see cref="Tree"/> is null.
/// </summary>
internal sealed record AdmittedDirectoryPrevious(DirectoryTreeSnapshot? Tree);

/// <summary>Bounded tree validation shared by directory production, worker transport and recovery.</summary>
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

    internal static void ValidateDescriptor(NewDirectoryOutput output)
    {
        if (output.Tree is null || output.Tree.Files is null || output.Tree.Directories is null
            || output.Tree.Files.Count > MaximumFiles || output.Tree.Directories.Count is < 1 or > PublicationLimits.MaximumDirectories)
        { throw new InvalidDataException("Invalid bounded directory output."); }
        foreach (string path in new[] { output.Target, output.Staged, output.ParentAnchor })
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || !FilePaths.Same(path, Path.GetFullPath(path)))
            { throw new InvalidDataException("Directory output paths must be canonical and absolute."); }
            OutputPathValidator.EnsureSafeDirectory(path);
        }
        string relative = Path.GetRelativePath(output.ParentAnchor, Path.GetDirectoryName(output.Target)!);
        if (Escapes(relative) || output.ParentIdentity is null || output.TransactionIdentity is null)
        { throw new InvalidDataException("Invalid directory output parent boundary."); }
        EnsureAnchor(Path.GetDirectoryName(output.Staged)!, output.TransactionIdentity);
        var paths = new HashSet<string>(FilePaths.Comparer);
        var dirs = new HashSet<string>(FilePaths.Comparer);
        foreach (DirectoryOutputDirectory directory in output.Tree.Directories)
        {
            if (directory is null || !SafeRelative(directory.Path, allowRoot: true) || !paths.Add(directory.Path)
                || directory.Identity is null)
            { throw new InvalidDataException("Invalid directory output tree directories."); }
            dirs.Add(directory.Path);
        }
        if (!dirs.Contains(".")) { throw new InvalidDataException("The directory output root is missing."); }
        if (output.Previous is { } previous && (!output.Replace || previous.Files is null || previous.Directories is null
            || previous.Files.Count > MaximumFiles || previous.Directories.Count is < 1 or > PublicationLimits.MaximumDirectories
            || previous.Directories.Any(static directory => directory is null || !SafeRelative(directory.Path, allowRoot: true))
            || previous.Files.Any(static file => file is null || !SafeRelative(file.Path, allowRoot: false)
                || file.Snapshot is null || !file.Snapshot.Exists || !file.Snapshot.IsStructurallyValid())))
        { throw new InvalidDataException("Invalid replaced directory tree."); }
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
        // The transaction is in the target's parent or, when its writer chose one, a farther
        // ancestor; recovery holds the parent's lease, as the writer did.
        string parent = Path.GetDirectoryName(output.Target)!;
        if (!Enum.IsDefined(journal.State) || journal.Entries.Count != 0 || Escapes(Path.GetRelativePath(root, parent))
            || !FilePaths.Same(output.Staged, Path.Combine(transaction, "directory")))
        { throw new InvalidDataException("The directory publication journal is outside its transaction."); }
        using PublicationDirectoryLease? owned = suppliedLease is null
            ? PublicationDirectoryLease.Acquire(parent, [parent], deadline) : null;
        if (!(suppliedLease ?? owned!).CoversDirectories([parent])) { throw new PublicationLeaseExpansionException(); }
        NewDirectoryOutput current = PublicationJournal.Read(Path.Combine(transaction, AtomicPublicationPlan.JournalName), deadline: deadline).DirectoryOutput
            ?? throw new InvalidDataException("The directory publication journal changed.");
        if (!string.Equals(JsonSerializer.Serialize(output), JsonSerializer.Serialize(current), StringComparison.Ordinal))
        { throw new InvalidDataException("The directory publication journal changed before recovery."); }
        EnsureAnchor(output.ParentAnchor, output.ParentIdentity);
        FilePhysicalIdentity? identity = output.TransactionIdentity;
        string previous = PreviousPath(transaction);
        if (Directory.Exists(output.Staged))
        {
            if (output.Previous is { } replaced && Directory.Exists(previous))
            {
                // The replaced directory moved out but the candidate never took its place: it returns.
                ValidateTree(previous, replaced, deadline);
                if (Directory.Exists(output.Target) || File.Exists(output.Target))
                { throw new IOException("The target of an interrupted replacement is occupied; the replaced directory was preserved."); }
                Directory.Move(previous, output.Target);
            }
            DeleteTree(output.Staged, output.Tree);
        }
        else if (journal.State is PublicationTransactionState.Publishing or PublicationTransactionState.Committed)
        {
            // A missing candidate with the exact original tree at the target proves rename committed.
            ValidateTree(output.Target, output.Tree, deadline);
            if (output.Previous is { } replaced && Directory.Exists(previous)) { DeleteTree(previous, replaced); }
        }
        DeleteTransaction(transaction, identity);
        return true;
    }
}