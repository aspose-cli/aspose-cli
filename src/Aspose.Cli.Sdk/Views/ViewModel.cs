using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Views;

/// <summary>Stable media kinds of one rendered view part.</summary>
public static class ViewPartKinds
{
    /// <summary>A raster image the viewer shows as a page, slide or sheet.</summary>
    public const string Image = "image";

    /// <summary>A product-rendered HTML document shown by the product presenter.</summary>
    public const string Html = "html";
}

/// <summary>Why a view is rendered; each product maps it to its own resolution.</summary>
public enum ViewPurpose
{
    /// <summary>Static evidence for agents: stable, moderate resolution.</summary>
    Evidence,

    /// <summary>Live display for people: sharp at high pixel density and zoom.</summary>
    Display,
}

/// <summary>One product view: an id, a label for people and the kind of parts it renders.</summary>
public sealed record ProductView(string Id, string Label, string PartKind);

/// <summary>
/// Product-owned browser assets that present a product's views inside the
/// shared viewer: the presenter script and its optional stylesheet.
/// </summary>
public sealed record ViewPresentation(string Script, string? Stylesheet);

/// <summary>Immutable input for one product view render.</summary>
public sealed record ViewRenderRequest
{
    /// <summary>View id declared by the owning product.</summary>
    public required string View { get; init; }

    /// <summary>Upper bound of parts rendered; parts beyond it are reported as omitted.</summary>
    public required int MaxPartCount { get; init; }

    /// <summary>Resolution intent.</summary>
    public required ViewPurpose Purpose { get; init; }

    /// <summary>Password of the source document, when encrypted.</summary>
    public string? Password { get; init; }
}

/// <summary>
/// Receives rendered view artifacts without exposing the host-owned directory.
/// Implementations enforce file-count, per-file and aggregate byte limits while
/// each artifact is written. Paths are forward-slash, sink-relative and must
/// not escape the sink root.
/// </summary>
public interface IViewArtifactSink
{
    /// <summary>
    /// Writes one artifact through a bounded synchronous stream. The callback
    /// must finish every write before it returns.
    /// </summary>
    void Write(
        string relativePath,
        Action<Stream> contentWriter);

    /// <summary>Writes one UTF-8 text artifact through the same bounded path.</summary>
    void WriteText(
        string relativePath,
        string content);
}

/// <summary>
/// What one view render produced: the ordered parts of the document and the
/// semantic layout of each part. Serialized as <c>view.json</c> beside the parts.
/// </summary>
public sealed record ViewManifest
{
    /// <summary>Canonical schema of the manifest document.</summary>
    [JsonPropertyOrder(-100)]
    public string Schema { get; } = CommonSchemaIds.View;

    /// <summary>Schema version of the manifest document.</summary>
    [JsonPropertyOrder(-99)]
    public int SchemaVersion { get; } = 2;

    /// <summary>View id that produced the parts.</summary>
    public required string View { get; init; }

    /// <summary>Detected format id of the source document.</summary>
    public required string SourceFormat { get; init; }

    /// <summary>Size of the source document in bytes.</summary>
    public required long SourceSizeBytes { get; init; }

    /// <summary>Parts the document contains, including parts beyond the render bound.</summary>
    public required int TotalPartCount { get; init; }

    /// <summary>Rendered parts in document order.</summary>
    public required IReadOnlyList<ViewPart> Parts { get; init; }

    /// <summary>Product warnings raised while rendering.</summary>
    public IReadOnlyList<Warning>? Warnings { get; init; }
}

/// <summary>One page, slide or sheet of a rendered view.</summary>
public sealed record ViewPart
{
    /// <summary>
    /// Identity of the part that survives unrelated edits where the document
    /// model allows it, for example a slide id or a sheet name.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>Label people see, for example <c>Page 3</c> or a slide title.</summary>
    public required string Label { get; init; }

    /// <summary>Sink-relative path of the rendered file.</summary>
    public required string File { get; init; }

    /// <summary>Kind of the rendered file; see <see cref="ViewPartKinds"/>.</summary>
    public required string Kind { get; init; }

    /// <summary>Layout width in CSS pixels; required for image parts.</summary>
    public int? Width { get; init; }

    /// <summary>Layout height in CSS pixels; required for image parts.</summary>
    public int? Height { get; init; }

    /// <summary>Whether the document hides the part, for example a hidden slide.</summary>
    public bool Hidden { get; init; }

    /// <summary>
    /// SHA-256 of the rendered file, assigned by the host after rendering so
    /// viewers can tell exactly which parts changed between two renders.
    /// </summary>
    public string? Digest { get; init; }

    /// <summary>Product-owned presentation facts, such as slide notes.</summary>
    public IReadOnlyDictionary<string, string>? Properties { get; init; }

    /// <summary>Semantic elements laid out on the part, in reading order.</summary>
    public IReadOnlyList<ViewElement>? Elements { get; init; }
}

/// <summary>One semantic element placed on a rendered part.</summary>
public sealed record ViewElement
{
    /// <summary>Identity that survives edits where the document model provides one.</summary>
    public string? Id { get; init; }

    /// <summary>Element kind, for example <c>paragraph</c>, <c>heading</c> or <c>shape</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>Bounds in the part's CSS pixel space.</summary>
    public required ViewBox Box { get; init; }

    /// <summary>Digest of the element's content and formatting, used to detect changes.</summary>
    public required string Digest { get; init; }

    /// <summary>Short text people can recognize the element by.</summary>
    public string? Label { get; init; }

    /// <summary>Outline level of a heading.</summary>
    public int? Level { get; init; }
}

/// <summary>A rectangle in a part's CSS pixel space.</summary>
public sealed record ViewBox(double X, double Y, double Width, double Height);
