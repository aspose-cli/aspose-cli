using System.Net;
using System.Text.RegularExpressions;
using Aspose.Cells;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Input;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Adapts verified resources to Cells and owns every stream supplied to the SDK. An HTML input
/// imports no images, so the images its <c>img</c> elements name are never read and are named in
/// the warning that says so; <paramref name="html"/> is that input.
/// </summary>
internal sealed class WorkbookResources(string path, ResourceBudgetLedger budgets, bool embeddedContainer, string? html = null)
    : IStreamProvider, IDisposable
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(10);

    private static readonly Regex ImageTag = new(
        """<img\b(?<attributes>(?:[^<>"']|"[^"]*"|'[^']*')*)>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, MatchTimeout);

    private static readonly Regex Attribute = new(
        """(?<name>[^\s"'<>/=]+)\s*=\s*(?:"(?<value>[^"]*)"|'(?<value>[^']*)'|(?<value>[^\s"'<>]+))""",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, MatchTimeout);

    private readonly LocalDocumentResourceLoader _loader = new(path, budgets);
    private readonly IReadOnlyList<string> _htmlImages = html is null ? [] : ImageSources(budgets.Inputs.ReadTextFile(html));
    private readonly HashSet<Stream> _streams = [];
    private readonly object _gate = new();
    private bool _disposed;
    private int _svgReferences;
    private int _svgPictures;
    private int _imagesLeftOut;

    public void InitStream(StreamProviderOptions options)
    {
        // Default permits SDK fetching, so refusal must be explicit before any work.
        options.ResourceLoadingType = ResourceLoadingType.Skip;
        options.Stream = null;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!IsHtmlImage(options.DefaultPath) && _loader.TryRead(options.DefaultPath, out byte[] data))
            {
                var stream = new MemoryStream(data, writable: false);
                _streams.Add(stream);
                options.Stream = stream;
                options.ResourceLoadingType = ResourceLoadingType.UserProvided;
            }
        }
    }

    public void CloseStream(StreamProviderOptions options)
    {
        lock (_gate)
        {
            if (options.Stream is { } stream && _streams.Remove(stream))
            {
                stream.Dispose();
            }
            options.Stream = null;
        }
    }

    internal void MaterializeLinkedPictures(Workbook workbook)
    {
        foreach (Worksheet sheet in workbook.Worksheets)
        {
            for (int index = sheet.Pictures.Count - 1; index >= 0; index--)
            {
                budgets.Deadline.ThrowIfExpired("document-resource");
                Aspose.Cells.Drawing.Picture picture = sheet.Pictures[index];
                if (!picture.IsLink)
                {
                    continue;
                }
                string reference = picture.SourceFullName;
                // Changing the link clears Data. Retain stored document content before
                // preventing deferred external acquisition.
                byte[]? cached = picture.Data;
                picture.SourceFullName = string.Empty;
                picture.IsLink = false;
                byte[]? content = cached is { Length: > 0 } ? cached
                    : _loader.TryRead(reference, out byte[] data) ? data
                    : null;
                // The engine rasterizes an SVG it is given (CELLS-SVG-EGRESS): give it one that
                // names nothing outside itself.
                if (content is not null && SvgPictures.IsSvg(content))
                {
                    if (SvgReferences.KeepInternal(content) is { } cleaned)
                    {
                        content = cleaned.Content;
                        _svgReferences += cleaned.Removed.Count;
                    }
                    else
                    {
                        content = null;
                        _svgPictures++;
                    }
                }
                if (content is not null)
                {
                    picture.Data = content;
                }
                else
                {
                    sheet.Pictures.RemoveAt(index);
                }
            }
        }
    }

    /// <summary>
    /// Removes every reference outside an SVG picture from it, and every SVG picture that does
    /// not parse (CELLS-SVG-EGRESS), before anything renders or saves the workbook.
    /// </summary>
    internal void CleanSvgPictures(Workbook workbook)
    {
        (int references, int pictures) = SvgPictures.CleanLoaded(workbook, budgets.Deadline.ThrowIfExpired);
        _svgReferences += references;
        _svgPictures += pictures;
    }

    /// <summary>
    /// Removes every picture of a workbook loaded from HTML or MHTML. The engine rasterizes an
    /// SVG image of such an input while it loads, reading what the SVG names past any resource
    /// hook (CELLS-SVG-EGRESS), and offers no option to skip images, so none reaches an output.
    /// </summary>
    internal void LeaveOutImages(Workbook workbook)
    {
        foreach (Worksheet sheet in workbook.Worksheets)
        {
            _imagesLeftOut += sheet.Pictures.Count;
            sheet.Pictures.Clear();
        }
    }

    internal void ThrowIfFailed() => _loader.ThrowIfFailed();

    /// <summary>The image references an HTML input's img elements write, once each, without data: URIs.</summary>
    private static IReadOnlyList<string> ImageSources(string html)
    {
        var sources = new List<string>();
        foreach (Match tag in ImageTag.Matches(html))
        {
            foreach (Match attribute in Attribute.Matches(tag.Groups["attributes"].Value))
            {
                if (!attribute.Groups["name"].Value.Equals("src", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string source = WebUtility.HtmlDecode(attribute.Groups["value"].Value).Trim();
                if (source.Length > 0 && !source.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                    && !sources.Contains(source, StringComparer.Ordinal))
                {
                    sources.Add(source);
                }
            }
        }

        return sources;
    }

    /// <summary>
    /// Whether the engine asks for an image an img element names: the engine passes the
    /// reference as written or resolved against the input's folder.
    /// </summary>
    private bool IsHtmlImage(string? requested)
    {
        if (_htmlImages.Count == 0 || string.IsNullOrWhiteSpace(requested))
        {
            return false;
        }

        string? full = LocalPath(requested);
        return _htmlImages.Any(source => string.Equals(source, requested, StringComparison.Ordinal)
            || (full is not null && LocalPath(source) is { } named && FilePaths.Same(full, named)));
    }

    private string? LocalPath(string reference)
    {
        try
        {
            return Uri.TryCreate(new Uri(_loader.BaseUri), reference, out Uri? resolved) && resolved.IsFile
                ? Path.GetFullPath(resolved.LocalPath)
                : null;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>
    /// What the load left out: resources the input names but the CLI did not load, images of an
    /// HTML or MHTML input, and SVG pictures that do not parse.
    /// </summary>
    internal Warning[] Warnings => new[] { CoverageWarning, ImagesNotImportedWarning, SvgPicturesWarning }.OfType<Warning>().ToArray();

    private Warning? CoverageWarning =>
        OmissionWarning ?? (embeddedContainer ? new Warning(CellsDiagnostics.MhtmlResourceCoverageUnverified, "The SDK resolves embedded MHTML resources internally without reporting unresolved references.")
        {
            Hint = "Inspect every required image and style; external resource completeness cannot be confirmed.",
            AffectsCompleteness = true,
        } : null);

    private Warning? OmissionWarning
    {
        get
        {
            if (_svgReferences == 0)
            {
                return _loader.Warning;
            }
            _loader.ThrowIfFailed();
            return SvgPictures.OmissionWarning(_loader.Omitted, _svgReferences);
        }
    }

    private Warning? ImagesNotImportedWarning => _htmlImages.Count > 0 || _imagesLeftOut > 0
        ? new Warning(CellsDiagnostics.ImagesSkipped, _htmlImages.Count > 0
            ? $"{_htmlImages.Count} image(s) of the HTML input were left out: {string.Join(", ", _htmlImages.Take(LocalDocumentResourceLoader.ListedResources).Select(_loader.ReferenceName))}{(_htmlImages.Count > LocalDocumentResourceLoader.ListedResources ? $", and {_htmlImages.Count - LocalDocumentResourceLoader.ListedResources} more" : "")}. HTML input imports no images."
            : $"{_imagesLeftOut} image(s) of the MHTML input were left out: MHTML input imports no images.")
        {
            Hint = "HTML and MHTML images are not imported, wherever they are: add each image the workbook needs with the insert_image operation.",
            AffectsCompleteness = true,
        }
        : null;

    private Warning? SvgPicturesWarning => _svgPictures > 0
        ? new Warning(CellsDiagnostics.ImagesSkipped, $"{_svgPictures} SVG picture(s) were left out: their SVG does not parse as XML, or they sit in a group.")
        {
            Hint = "Replace each such picture with a PNG, or with an SVG that parses and is not grouped, using insert_image.",
            AffectsCompleteness = true,
        }
        : null;

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            foreach (Stream stream in _streams)
            {
                stream.Dispose();
            }
            _streams.Clear();
            _loader.Dispose();
        }
    }
}
