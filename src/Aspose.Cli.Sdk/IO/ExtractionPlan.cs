namespace Aspose.Cli.Sdk.IO;

/// <summary>
/// Plans names that are distinct within one extraction and the directories below the root that
/// it needs. The transaction creates the root and its missing parents. A file already on disk
/// keeps its name: publication replaces it only with <c>--overwrite</c> and refuses it otherwise.
/// </summary>
internal sealed class ExtractionPlan(string root)
{
    private readonly HashSet<string> _reserved = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);
    public IEnumerable<string> Directories => _directories;

    public string ReserveFile(string relativePath, string suggestedPath)
    {
        string candidate = Path.Combine(root, relativePath);
        string stem = Path.GetFileNameWithoutExtension(candidate);
        string extension = Path.GetExtension(candidate);
        int suffix = 2;
        while (_reserved.Contains(candidate))
        {
            candidate = Path.Combine(Path.GetDirectoryName(candidate)!, $"{stem}-{suffix++}{extension}");
        }
        string full = Path.GetFullPath(candidate);
        EnsureBelowRoot(full, suggestedPath);
        _directories.Add(Path.GetDirectoryName(full)!);
        _reserved.Add(full);
        return full;
    }

    public string CreateDirectory(string relativePath, string suggestedPath)
    {
        string target = Path.GetFullPath(Path.Combine(root, relativePath));
        EnsureBelowRoot(target, suggestedPath);
        _directories.Add(target);
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
}
