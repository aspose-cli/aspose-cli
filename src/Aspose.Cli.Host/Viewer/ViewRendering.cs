using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Serialization;
using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Host.Viewer;

/// <summary>
/// Renders one product view into a dedicated directory: the product writes
/// through a bounded sink, the files it wrote are proven to be exactly the
/// ones its manifest names, and every part is stamped with the digest of its
/// bytes so viewers can tell which parts changed between two renders. Static
/// review evidence and live revisions are produced the same way.
/// </summary>
internal static class ViewRendering
{
    public static ViewManifest Render(
        Func<IViewArtifactSink, ViewManifest> render,
        string directory,
        int maxPartCount,
        LocalServiceResourceLimits limits)
    {
        ArgumentNullException.ThrowIfNull(render);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(limits);

        var sink = new BoundedViewArtifactSink(directory, limits);
        ViewManifest manifest = render(sink);
        sink.EnsureComplete();
        ViewManifestValidator.Validate(manifest, maxPartCount);
        EnsureNamedFilesOnly(manifest, directory);
        return Fitted(
            manifest with
            {
                Parts = manifest.Parts.Select(part => part with
                {
                    Digest = Digest(PartPath(directory, part)),
                }).ToArray(),
            },
            directory,
            limits);
    }

    /// <summary>
    /// The text of <c>view.json</c>, the manifest a view's directory holds beside its parts,
    /// whose bytes count in the view's limits.
    /// </summary>
    public static string ManifestText(ViewManifest manifest) =>
        JsonSerializer.Serialize(manifest, SdkJsonContext.Default.ViewManifest) + Environment.NewLine;

    /// <summary>
    /// The manifest counts in the snapshot's bytes as the parts do. When it does not fit beside
    /// them, the view leaves out its last parts and their files, as it leaves out a part the sink
    /// had no room for; the parts left out still count in the total, as omitted.
    /// </summary>
    private static ViewManifest Fitted(ViewManifest manifest, string directory, LocalServiceResourceLimits limits)
    {
        var parts = manifest.Parts.ToList();
        long bytes = parts.Sum(part => new FileInfo(PartPath(directory, part)).Length);
        ViewManifest fitted = manifest;
        while (parts.Count > 0
            && bytes + Encoding.UTF8.GetByteCount(ManifestText(fitted)) > limits.MaximumSnapshotBytes)
        {
            string last = PartPath(directory, parts[^1]);
            bytes -= new FileInfo(last).Length;
            LocalFileCleanup.DeleteFile(last);
            parts.RemoveAt(parts.Count - 1);
            fitted = manifest with { Parts = [.. parts] };
        }

        return fitted;
    }

    private static string PartPath(string directory, ViewPart part) =>
        Path.Combine(directory, part.File.Replace('/', Path.DirectorySeparatorChar));

    private static void EnsureNamedFilesOnly(ViewManifest manifest, string directory)
    {
        string[] written = Directory
            .EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(directory, path).Replace('\\', '/'))
            .ToArray();
        var named = manifest.Parts
            .Select(static part => part.File)
            .ToHashSet(FilePaths.Comparer);
        string? stray = written.FirstOrDefault(path => !named.Contains(path));
        if (stray is not null || written.Length != named.Count)
        {
            throw new InvalidOperationException(
                $"The product view files do not match its manifest{(stray is null ? "" : $": '{stray}'")}.");
        }
    }

    private static string Digest(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return "sha256:" + Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
