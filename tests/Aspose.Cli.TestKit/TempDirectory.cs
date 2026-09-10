namespace Aspose.Cli.TestKit;

/// <summary>A disposable temporary directory for real-file-system tests.</summary>
public sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "aspose-cli-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>Absolute path of the directory.</summary>
    public string Path { get; }

    /// <summary>Resolves a path inside the directory.</summary>
    public string File(string relativePath) => System.IO.Path.Combine(Path, relativePath);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort cleanup; leaked temp directories must not fail tests.
        }
    }
}
