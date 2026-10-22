using SkiaSharp;

namespace Aspose.Cli.Invariants.Tests.Isolation;

/// <summary>What one isolation case points its document at.</summary>
internal enum ReferenceKind
{
    /// <summary>A loopback HTTP server, by every reference form the format has.</summary>
    Network,

    /// <summary>Sentinel files in a sibling of the input folder, by absolute path, file URI and relative path.</summary>
    Files,

    /// <summary>
    /// Only a server address after an attribute value that looks like a namespace declaration, which a
    /// content scan can take for one: HTML and Markdown samples alone, as any other network reference
    /// in the same document lets a scan refuse it before the decoy is tried.
    /// </summary>
    Decoy,

    /// <summary>The sentinel files through the administrative share <c>\\localhost\X$</c>.</summary>
    Unc,

    /// <summary>Only resources inside the input folder, which the command must keep.</summary>
    Control,

    /// <summary>
    /// A named pipe of the case's own, <c>\\.\pipe\&lt;name&gt;</c>, in every reference place, by
    /// its device path, through <c>\\localhost\pipe</c> and as a file URI. A pipe is no file
    /// anyone could have copied the sentinel into: any engine that opens a reference by its path,
    /// whatever it does with what it reads, connects to it, which an output scan cannot see.
    /// </summary>
    Pipe,
}

/// <summary>
/// The folders of an isolation case: the input folder <see cref="InputFolder"/>, which holds the
/// document and, for a control, its own resources, and the sibling <see cref="OutsideFolder"/>,
/// which holds the sentinels a document must never bring into an output. Every image sentinel is
/// a solid <see cref="SentinelColour"/>, every style sheet paints it, and every page and text holds
/// <see cref="Marker"/>; the control resources use <see cref="ControlColour"/> instead.
/// </summary>
internal sealed class IsolationLayout
{
    public const string InputFolder = "in";
    public const string OutsideFolder = "outside";
    public const string Marker = "ISOLATION-SENTINEL-7d41c2";
    public const string ControlMarker = "ISOLATION-CONTROL-5be093";
    public static readonly SKColor SentinelColour = new(0, 200, 0);
    public static readonly SKColor ControlColour = new(200, 0, 200);

    public IsolationLayout(string root)
    {
        Root = root;
        Directory.CreateDirectory(Input);
        Directory.CreateDirectory(Outside);
        File.WriteAllBytes(Path.Combine(Outside, "sentinel.png"), OutputScan.Png(SentinelColour));
        File.WriteAllText(Path.Combine(Outside, "sentinel.css"), Css("#00c800", Marker));
        File.WriteAllText(Path.Combine(Outside, "sentinel.html"), Page("#00c800", Marker));
    }

    public string Root { get; }

    public string Input => Path.Combine(Root, InputFolder);

    public string Outside => Path.Combine(Root, OutsideFolder);

    /// <summary>Writes the resources a control document references into the input folder.</summary>
    public void WriteControlResources()
    {
        File.WriteAllBytes(Path.Combine(Input, "control.png"), OutputScan.Png(ControlColour));
        File.WriteAllText(Path.Combine(Input, "control.css"), Css("#c800c8", ControlMarker));
        File.WriteAllText(Path.Combine(Input, "control.html"), Page("#c800c8", ControlMarker));
    }

    /// <summary>
    /// The sentinel through <c>\\localhost\X$</c>, or why that share cannot be read here: a UNC case
    /// is skipped with this reason rather than passing without a reference that could be followed.
    /// </summary>
    public (string? Path, string? Unreachable) Unc(string file)
    {
        string local = System.IO.Path.Combine(Outside, file);
        if (!OperatingSystem.IsWindows())
        {
            return (null, "UNC paths exist only on Windows.");
        }
        string root = System.IO.Path.GetPathRoot(local)!;
        if (root.Length != 3 || root[1] != ':')
        {
            return (null, $"The temporary folder {root} is not on a drive letter, so it has no administrative share.");
        }
        string unc = $@"\\localhost\{char.ToUpperInvariant(root[0])}$\{local[3..]}";
        try
        {
            return File.ReadAllBytes(unc).AsSpan().SequenceEqual(File.ReadAllBytes(local))
                ? (unc, null)
                : (null, $"{unc} does not read the same file as {local}.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (null, $"The administrative share is not reachable ({unc}): {exception.Message}");
        }
    }

    private static string Css(string colour, string marker) =>
        $"/* {marker} */ html, body, table, td, p, div {{ background: {colour}; background-color: {colour}; }}";

    private static string Page(string colour, string marker) =>
        $"<!DOCTYPE html><html><body style=\"background:{colour}\"><p>{marker}</p></body></html>";
}

/// <summary>
/// The references a sample document makes, by role. A generator gives each of <see cref="Images"/>
/// its own picture, puts every one of <see cref="SvgImages"/> into one SVG picture, and, where the
/// format has them, links <see cref="Stylesheets"/> and embeds <see cref="Pages"/> as frames.
/// </summary>
/// <param name="Kind">What the references point at.</param>
/// <param name="Images">Image references, each its own picture.</param>
/// <param name="SvgImages">The <c>&lt;image href&gt;</c> values of one SVG picture.</param>
/// <param name="Stylesheets">Style sheet references.</param>
/// <param name="Pages">References to whole pages or text, such as an iframe's.</param>
/// <param name="Decoy">
/// An address hidden in markup that looks like a namespace declaration to a content scan, or null.
/// </param>
internal sealed record References(
    ReferenceKind Kind,
    IReadOnlyList<string> Images,
    IReadOnlyList<string> SvgImages,
    IReadOnlyList<string> Stylesheets,
    IReadOnlyList<string> Pages,
    string? Decoy)
{
    /// <summary>The most images any kind references, so a base document has a picture for each and one for the SVG.</summary>
    public const int MaxImages = 3;

    /// <summary>
    /// The references of a kind. <paramref name="packageDepth"/> is how many folders a relative
    /// reference must climb before it reaches the input folder: 0 for a plain file, 1 where the
    /// format resolves it from inside its package, as OpenDocument does.
    /// </summary>
    public static References For(ReferenceKind kind, IsolationLayout layout, string serverUrl, int packageDepth, string? uncImage = null, string? uncStylesheet = null, string? uncPage = null, string? pipe = null)
    {
        string up = string.Concat(Enumerable.Repeat("../", packageDepth));
        string png = Path.Combine(layout.Outside, "sentinel.png");
        switch (kind)
        {
            case ReferenceKind.Network:
                return new References(kind, [serverUrl + "/link.png"], [serverUrl + "/svg.png"], [serverUrl + "/style.css"], [serverUrl + "/page.html"], null);
            case ReferenceKind.Files:
                string[] files = [png, new Uri(png).AbsoluteUri, up + "../outside/sentinel.png"];
                return new References(kind, files, files,
                    [Path.Combine(layout.Outside, "sentinel.css"), up + "../outside/sentinel.css"],
                    [up + "../outside/sentinel.html", Path.Combine(layout.Outside, "sentinel.html")],
                    null);
            case ReferenceKind.Decoy:
                return new References(kind, [], [], [], [], serverUrl + "/decoy.png");
            case ReferenceKind.Pipe:
                string[] pipes = [IsolationPipe.DevicePath(pipe!), IsolationPipe.SharePath(pipe!), IsolationPipe.FileUri(pipe!)];
                return new References(kind, pipes, pipes, pipes, pipes, null);
            case ReferenceKind.Control:
                layout.WriteControlResources();
                // A relative path only: the one form every importer documents for a resource of its
                // own folder (the PDF Markdown importer refuses absolute paths and URLs outright).
                string[] inside = [up + "control.png"];
                return new References(kind, inside, inside, [up + "control.css"], [up + "control.html"], null);
            default:
                string[] unc = [uncImage!, new Uri(uncImage!).AbsoluteUri];
                return new References(kind, unc, unc, [uncStylesheet!], [uncPage!], null);
        }
    }
}
