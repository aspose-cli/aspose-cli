using System.Collections.Frozen;

namespace Aspose.Cli.Host.Viewer;

/// <summary>
/// The media type of each file a view or a review publishes, by its extension, and how the
/// viewer may serve it over HTTP. A rendered part is document content: the viewer never serves
/// a script as one, and it does not serve a PDF or plain text as what it is either, which keeps
/// the local service's surface no larger than what it presents. A file it does not serve as
/// itself, and any extension this table does not list, is served as <see cref="Unknown"/>.
/// </summary>
internal static class MediaTypes
{
    /// <summary>The media type of a file this table does not describe, or the viewer does not serve as itself.</summary>
    public const string Unknown = "application/octet-stream";

    private static readonly FrozenDictionary<string, Entry> ByExtension = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = new("text/html", Text: true, ServedOverHttp: true, Visual: false),
        [".htm"] = new("text/html", Text: true, ServedOverHttp: true, Visual: false),
        [".css"] = new("text/css", Text: true, ServedOverHttp: true, Visual: false),
        [".js"] = new("text/javascript", Text: true, ServedOverHttp: false, Visual: false),
        [".json"] = new("application/json", Text: true, ServedOverHttp: true, Visual: false),
        [".svg"] = new("image/svg+xml", Text: false, ServedOverHttp: true, Visual: true),
        [".png"] = new("image/png", Text: false, ServedOverHttp: true, Visual: true),
        [".jpg"] = new("image/jpeg", Text: false, ServedOverHttp: true, Visual: true),
        [".jpeg"] = new("image/jpeg", Text: false, ServedOverHttp: true, Visual: true),
        [".gif"] = new("image/gif", Text: false, ServedOverHttp: true, Visual: true),
        [".webp"] = new("image/webp", Text: false, ServedOverHttp: true, Visual: true),
        [".pdf"] = new("application/pdf", Text: false, ServedOverHttp: false, Visual: true),
        [".txt"] = new("text/plain", Text: true, ServedOverHttp: false, Visual: false),
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>The media type of <paramref name="path"/> by its extension, such as <c>image/png</c>.</summary>
    public static string Of(string path) =>
        ByExtension.TryGetValue(Path.GetExtension(path), out Entry? entry) ? entry.MediaType : Unknown;

    /// <summary>
    /// The <c>Content-Type</c> the viewer serves a file of <paramref name="extension"/> with: its
    /// media type, with the UTF-8 charset for text, when the viewer serves it as itself; otherwise
    /// <see cref="Unknown"/>.
    /// </summary>
    public static string OverHttp(string extension) =>
        ByExtension.TryGetValue(extension, out Entry? entry) && entry.ServedOverHttp
            ? entry.Text ? entry.MediaType + "; charset=utf-8" : entry.MediaType
            : Unknown;

    /// <summary>Whether <paramref name="path"/> is visual evidence a finding about its part points at: an image or a PDF.</summary>
    public static bool IsVisual(string path) =>
        ByExtension.TryGetValue(Path.GetExtension(path), out Entry? entry) && entry.Visual;

    /// <param name="MediaType">The media type, without parameters.</param>
    /// <param name="Text">Whether it is UTF-8 text, served with its charset.</param>
    /// <param name="ServedOverHttp">Whether the viewer serves a revision file of this type as itself.</param>
    /// <param name="Visual">Whether a review points a finding about the part at it.</param>
    private sealed record Entry(string MediaType, bool Text, bool ServedOverHttp, bool Visual);
}
