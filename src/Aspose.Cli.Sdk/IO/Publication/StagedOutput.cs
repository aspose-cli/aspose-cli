using Aspose.Cli.Sdk.IO.Files;
using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Sdk.IO.Publication;

/// <summary>The exact staged file produced for one final output, before publication.</summary>
public sealed class StagedOutput
{
    private readonly PublicationEntry _entry;

    internal StagedOutput(PublicationEntry entry) => _entry = entry;

    public string Path => _entry.Staged;
    public string TargetPath => _entry.Target;
    public long SizeBytes => _entry.Size;

    /// <summary>The facts of the file this output publishes.</summary>
    public PublishedFile Published => new(TargetPath, SizeBytes);

    internal FilePublicationSnapshot Snapshot => _entry.StagedSnapshot;

    /// <summary>Inspects the bound candidate while preventing Windows writers from replacing it.</summary>
    public T Read<T>(Func<string, T> inspect)
    {
        ArgumentNullException.ThrowIfNull(inspect);
        using var lease = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read);
        EnsureUnchanged();
        T result = inspect(Path);
        EnsureUnchanged();
        return result;
    }

    internal void EnsureUnchanged()
    {
        if (!_entry.StagedSnapshot.VersionEquals(FilePublicationSnapshot.Capture(Path)))
        {
            throw new IOException($"Staged output '{TargetPath}' changed after production.");
        }
    }
}
