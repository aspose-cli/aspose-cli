using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Host.Preview;

/// <summary>
/// One immutable, fully materialized preview version. The server reads
/// whatever snapshot is current at request time, so a finished render can be
/// swapped in atomically (a single reference write) while requests are in
/// flight. Snapshots come in two shapes: an <em>inline</em> snapshot carries
/// the whole document as one HTML string (<see cref="InlineHtml"/> is
/// non-null; its version directory has already been deleted), while a
/// <em>directory</em> snapshot points at a version directory on disk
/// (<see cref="DirectoryPath"/> is non-null) whose satellite files are served
/// under the <c>/asset/</c> route.
/// </summary>
/// <param name="Revision">Monotonically increasing render number; the first render is 1.</param>
/// <param name="DirectoryPath">Absolute path of the version directory; null for inline snapshots.</param>
/// <param name="EntryFileName">File name of the document entry point (relative to the version directory).</param>
/// <param name="InlineHtml">The whole document for inline snapshots; null for directory snapshots.</param>
internal sealed record PreviewSnapshot(
    int Revision,
    string? DirectoryPath,
    string EntryFileName,
    string? InlineHtml)
{
    internal PreviewArtifactManifest? ArtifactManifest { get; init; }
}
