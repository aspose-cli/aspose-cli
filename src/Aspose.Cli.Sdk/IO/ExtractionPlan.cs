namespace Aspose.Cli.Sdk.IO;

/// <summary>Plans collision-free targets below one validated extraction root.</summary>
internal sealed class ExtractionPlan
{
    private readonly string _root;
    private readonly string _workingRoot;
    private readonly bool _workerStagingOnly;
    private readonly HashSet<string> _reserved =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _createdDirectories = [];

    public ExtractionPlan(
        string root,
        string workingRoot,
        bool workerStagingOnly)
    {
        _root = root;
        _workingRoot = workingRoot;
        _workerStagingOnly = workerStagingOnly;
        EnsureDirectory(_workingRoot);
        ExtractionPathValidator.EnsureNoLinks(_workingRoot);
    }

    public IReadOnlyList<string> CreatedDirectories => _createdDirectories;

    public string ReserveFile(
        string relativePath,
        string suggestedPath,
        bool overwrite)
    {
        string candidate = Path.Combine(_root, relativePath);
        string stem = Path.GetFileNameWithoutExtension(candidate);
        string extension = Path.GetExtension(candidate);
        int suffix = 2;
        while (_reserved.Contains(candidate)
               || (!overwrite && File.Exists(candidate)))
        {
            candidate = Path.Combine(
                Path.GetDirectoryName(candidate) ?? _root,
                $"{stem}-{suffix++}{extension}");
        }

        string full = Path.GetFullPath(candidate);
        EnsureBelowRoot(full, suggestedPath);
        string directory = Path.GetDirectoryName(full) ?? _root;
        EnsureDirectory(_workerStagingOnly ? ToWorkingPath(directory) : directory);
        ExtractionPathValidator.EnsureNoLinks(
            _workerStagingOnly ? ToWorkingPath(directory) : directory);
        _reserved.Add(full);
        return full;
    }

    public string CreateDirectory(
        string relativePath,
        string suggestedPath)
    {
        string target = Path.GetFullPath(Path.Combine(_root, relativePath));
        EnsureBelowRoot(target, suggestedPath);
        if (File.Exists(target))
        {
            throw ExtractionPathValidator.Refused(
                $"directory entry '{suggestedPath}' collides with an existing file");
        }

        if (_workerStagingOnly)
        {
            EnsureDirectory(ToWorkingPath(target));
            Directory.CreateDirectory(target);
        }
        else
        {
            EnsureDirectory(target);
        }
        _reserved.Add(target);
        return target;
    }

    public string ToWorkingPath(string targetPath)
    {
        string relative = Path.GetRelativePath(_root, targetPath);
        return Path.GetFullPath(Path.Combine(_workingRoot, relative));
    }

    public void EnsureWorkingDirectory(string path) => EnsureDirectory(path);

    private void EnsureBelowRoot(string full, string suggestedPath)
    {
        if (!full.StartsWith(
                _root + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            throw ExtractionPathValidator.Refused(
                $"unsafe extraction name '{suggestedPath}'");
        }
    }

    private void EnsureDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            ExtractionPathValidator.EnsureNoLinks(path);
            return;
        }

        var missing = new List<string>();
        string? current = path;
        while (current is not null
               && !Directory.Exists(current)
               && (string.Equals(
                       current,
                       _workingRoot,
                       StringComparison.OrdinalIgnoreCase)
                   || current.StartsWith(
                       _workingRoot + Path.DirectorySeparatorChar,
                       StringComparison.OrdinalIgnoreCase)))
        {
            missing.Add(current);
            current = Path.GetDirectoryName(current);
        }

        Directory.CreateDirectory(path);
        ExtractionPathValidator.EnsureNoLinks(path);
        _createdDirectories.AddRange(missing.AsEnumerable().Reverse());
    }
}
