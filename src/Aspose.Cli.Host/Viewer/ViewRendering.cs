using System.Security.Cryptography;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.Viewer;
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
        int maxParts,
        LocalServiceResourceLimits limits)
    {
        ArgumentNullException.ThrowIfNull(render);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(limits);

        var sink = new BoundedViewArtifactSink(directory, limits);
        ViewManifest manifest = render(sink);
        sink.EnsureComplete();
        ViewManifestValidator.Validate(manifest, maxParts);
        EnsureNamedFilesOnly(manifest, directory);
        return manifest with
        {
            Parts = manifest.Parts.Select(part => part with
            {
                Digest = Digest(Path.Combine(
                    directory,
                    part.File.Replace('/', Path.DirectorySeparatorChar))),
            }).ToArray(),
        };
    }

    private static void EnsureNamedFilesOnly(ViewManifest manifest, string directory)
    {
        string[] written = Directory
            .EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(directory, path).Replace('\\', '/'))
            .ToArray();
        var named = manifest.Parts
            .Select(static part => part.File)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
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
