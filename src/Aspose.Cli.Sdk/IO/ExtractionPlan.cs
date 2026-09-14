namespace Aspose.Cli.Sdk.IO;

/// <summary>Plans collision-free names and tracks newly created extraction directories.</summary>
internal sealed class ExtractionPlan(string root, bool workerStagingOnly)
{
    private readonly HashSet<string> _reserved = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FilePhysicalIdentity?> _created = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);
    public IEnumerable<string> Directories => _directories;
    public void EnsureRoot() => EnsureDirectory(root);

    public string ReserveFile(string relativePath, string suggestedPath, bool overwrite)
    {
        string candidate = Path.Combine(root, relativePath);
        string stem = Path.GetFileNameWithoutExtension(candidate);
        string extension = Path.GetExtension(candidate);
        int suffix = 2;
        while (_reserved.Contains(candidate) || (!overwrite && File.Exists(candidate)))
        {
            candidate = Path.Combine(Path.GetDirectoryName(candidate)!, $"{stem}-{suffix++}{extension}");
        }
        string full = Path.GetFullPath(candidate);
        EnsureBelowRoot(full, suggestedPath);
        EnsureDirectory(Path.GetDirectoryName(full)!);
        _reserved.Add(full);
        return full;
    }

    public string CreateDirectory(string relativePath, string suggestedPath)
    {
        string target = Path.GetFullPath(Path.Combine(root, relativePath));
        EnsureBelowRoot(target, suggestedPath);
        EnsureDirectory(target);
        _reserved.Add(target);
        return target;
    }

    private void EnsureBelowRoot(string full, string suggestedPath)
    {
        if (!full.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw ExtractionPathValidator.Refused($"unsafe extraction name '{suggestedPath}'");
        }
        ExtractionPathValidator.EnsureNoLinks(full);
    }

    private void EnsureDirectory(string path)
    {
        OutputPathValidator.EnsureSafeDirectory(path);
        _directories.Add(path);
        if (workerStagingOnly || Directory.Exists(path)) { return; }
        var missing = new Stack<string>();
        for (string? current = path; current is not null && !Directory.Exists(current); current = Path.GetDirectoryName(current))
        {
            missing.Push(current);
        }
        while (missing.TryPop(out string? current))
        {
            Directory.CreateDirectory(current);
            _created.Add(current, FilePublicationOwnedDelete.TryGetDirectoryIdentity(current));
        }
    }

    public void RemoveCreatedDirectories()
    {
        foreach ((string path, FilePhysicalIdentity? identity) in _created.OrderByDescending(item => item.Key.Length))
        {
            if (Directory.Exists(path)) { _ = FilePublicationOwnedDelete.TryDeleteDirectory(path, identity); }
        }
    }
}
