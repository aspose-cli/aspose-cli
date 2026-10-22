using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Xml.Linq;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using SkiaSharp;

namespace Aspose.Cli.Invariants.Tests.Isolation;

/// <summary>How a format carries references, which decides how a sample document is built.</summary>
internal enum Family
{
    /// <summary>An Open Packaging zip (Office Open XML): pictures linked by <c>TargetMode="External"</c> relationships and an SVG picture part.</summary>
    Package,

    /// <summary>An OpenDocument zip: <c>draw:image</c> links and an SVG picture.</summary>
    OpenDocument,

    /// <summary>A flat OpenDocument XML file.</summary>
    FlatOpenDocument,

    /// <summary>A Word package flattened into one XML file (Flat OPC).</summary>
    FlatOpc,

    /// <summary>Word 2003 XML: VML image data by <c>src</c>.</summary>
    WordMl,

    /// <summary>HTML: images, style sheets, CSS backgrounds, inline and data SVG, frames.</summary>
    Html,

    /// <summary>The HTML sample in a MIME archive.</summary>
    Mhtml,

    /// <summary>Markdown images, inline HTML images and a local SVG image that names the references.</summary>
    Markdown,

    /// <summary>An EPUB package whose chapter is the HTML sample.</summary>
    Epub,

    /// <summary>An SVG image file.</summary>
    Svg,
}

/// <summary>
/// Builds a sample document in any readable format that can reference external resources, with
/// each reference form the format has. Container formats start from a base document of the
/// product's own (several neutral grey pictures, built once per test process with the product's
/// <c>create</c>, <c>edit</c> and <c>convert</c>) whose pictures are then pointed at the references;
/// text formats are written directly.
/// </summary>
internal static class IsolationDocuments
{
    /// <summary>The sample document's file name stem.</summary>
    public const string Stem = "sample";

    private const string RelationshipsNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string ImageRelationship = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace AsSvg = "http://schemas.microsoft.com/office/drawing/2016/SVG/main";
    private static readonly XNamespace Xlink = "http://www.w3.org/1999/xlink";
    private static readonly XNamespace Draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
    private static readonly XNamespace Office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private static readonly XNamespace Manifest = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";
    private static readonly XNamespace Pkg = "http://schemas.microsoft.com/office/2006/xmlPackage";
    private static readonly XNamespace WordMl = "http://schemas.microsoft.com/office/word/2003/wordml";
    private const string SvgBlipExtension = "{96DAC541-7B7A-43D3-8B79-37D633B846F1}";

    /// <summary>
    /// The formats a sample is built for, by the family that decides how. Every readable format a
    /// product declares is here or in <see cref="NotApplicable"/>; <c>IsolationMatrixTests</c> fails
    /// for one in neither, so a new format is classified before it ships.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, Family> Families = new Dictionary<string, Family>(StringComparer.Ordinal)
    {
        ["xlsx"] = Family.Package,
        ["xltx"] = Family.Package,
        ["xlsm"] = Family.Package,
        ["xltm"] = Family.Package,
        ["xlsb"] = Family.Package,
        ["docx"] = Family.Package,
        ["docm"] = Family.Package,
        ["dotx"] = Family.Package,
        ["dotm"] = Family.Package,
        ["pptx"] = Family.Package,
        ["pptm"] = Family.Package,
        ["ppsx"] = Family.Package,
        ["ppsm"] = Family.Package,
        ["potx"] = Family.Package,
        ["potm"] = Family.Package,
        ["ods"] = Family.OpenDocument,
        ["odt"] = Family.OpenDocument,
        ["ott"] = Family.OpenDocument,
        ["odp"] = Family.OpenDocument,
        ["otp"] = Family.OpenDocument,
        ["fodp"] = Family.FlatOpenDocument,
        ["flatopc"] = Family.FlatOpc,
        ["wordml"] = Family.WordMl,
        ["html"] = Family.Html,
        ["mhtml"] = Family.Mhtml,
        ["md"] = Family.Markdown,
        ["epub"] = Family.Epub,
        ["svg"] = Family.Svg,
    };

    /// <summary>
    /// The readable formats without a sample, each with the reason. Plain text has no reference
    /// syntax at all; the binary formats can link a picture, but only through records this test
    /// has no writer for, so they stay listed until one is added.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> NotApplicable = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["csv"] = "Delimited text holds only values: no reference syntax.",
        ["tsv"] = "Delimited text holds only values: no reference syntax.",
        ["txt"] = "Plain text holds no reference syntax.",
        ["xls"] = "Binary BIFF8 workbook: linked pictures are binary records, and the matrix has no BIFF writer.",
        ["doc"] = "Binary Word 97 document: linked pictures are INCLUDEPICTURE fields in binary records, and the matrix has no writer for them.",
        ["dot"] = "Binary Word 97 template: linked pictures are INCLUDEPICTURE fields in binary records, and the matrix has no writer for them.",
        ["rtf"] = "RTF links pictures only through INCLUDEPICTURE fields and \\pict properties, none of the matrix's reference forms (SVG, HTML, package relationships).",
        ["ppt"] = "Binary PowerPoint 97 presentation: linked pictures are binary records, and the matrix has no writer for them.",
        ["pps"] = "Binary PowerPoint 97 show: linked pictures are binary records, and the matrix has no writer for them.",
        ["pot"] = "Binary PowerPoint 97 template: linked pictures are binary records, and the matrix has no writer for them.",
        ["pdf"] = "A PDF names other files only in actions and file specifications that no reader follows while opening; it has none of the matrix's reference forms.",
        ["mobi"] = "Mobipocket book: its HTML records are compressed binary records, and the matrix has no writer for them.",
        ["azw3"] = "Kindle KF8 book: its HTML records are compressed binary records, and the matrix has no writer for them.",
        ["chm"] = "Compiled HTML Help: an ITSS archive, and the matrix has no writer for it.",
    };

    /// <summary>Whether the format's sample can hold <see cref="ReferenceKind.Decoy"/>, which is HTML markup.</summary>
    public static bool HoldsDecoy(string format) => Families[format] is Family.Html or Family.Mhtml or Family.Markdown;

    /// <summary>
    /// The extension a file of a format is written with, where it is not the format id: Words
    /// reads Flat OPC and Word 2003 XML as <c>.xml</c>.
    /// </summary>
    public static string Extension(string format) => format switch
    {
        "flatopc" or "wordml" => "xml",
        _ => format,
    };

    /// <summary>
    /// How many folders a relative reference climbs before it leaves the document: an EPUB chapter
    /// resolves one from inside its package, so <c>../</c> names the document's own folder. The
    /// OpenDocument readers resolve a picture's relative link from the document's folder.
    /// </summary>
    public static int PackageDepth(string format) => Families[format] is Family.Epub ? 1 : 0;

    /// <summary>
    /// Formats the products do not write, made from one they do by changing what declares the
    /// document type: the main part's content type of a presentation package, the mimetype of an
    /// OpenDocument package.
    /// </summary>
    private static readonly Dictionary<string, (string Source, string Type)> Derived = new(StringComparer.Ordinal)
    {
        ["ppsx"] = ("pptx", "application/vnd.openxmlformats-officedocument.presentationml.slideshow.main+xml"),
        ["potx"] = ("pptx", "application/vnd.openxmlformats-officedocument.presentationml.template.main+xml"),
        ["ppsm"] = ("pptm", "application/vnd.ms-powerpoint.slideshow.macroEnabled.main+xml"),
        ["potm"] = ("pptm", "application/vnd.ms-powerpoint.template.macroEnabled.main+xml"),
        ["otp"] = ("odp", "application/vnd.oasis.opendocument.presentation-template"),
    };

    private static readonly Dictionary<string, string> PrimaryFormats = new(StringComparer.Ordinal)
    {
        ["cells"] = "xlsx",
        ["slides"] = "pptx",
        ["words"] = "docx",
    };

    private static readonly ConcurrentDictionary<string, Lazy<byte[]>> Bases = new(StringComparer.Ordinal);

    /// <summary>
    /// Writes the sample of <paramref name="format"/> for <paramref name="product"/> into the input
    /// folder and returns its path relative to the layout root.
    /// </summary>
    public static string Write(string product, string format, References references, IsolationLayout layout)
    {
        string name = $"{Stem}.{Extension(format)}";
        string path = Path.Combine(layout.Input, name);
        switch (Families[format])
        {
            case Family.Package:
                File.WriteAllBytes(path, Zip(PatchPackage(Unzip(Base(product, format)), references)));
                break;
            case Family.OpenDocument:
                File.WriteAllBytes(path, PatchOpenDocument(Base(product, format), references));
                break;
            case Family.FlatOpenDocument:
                File.WriteAllText(path, FlatOpenDocument(references));
                break;
            case Family.FlatOpc:
                File.WriteAllText(path, FlatOpc(PatchPackage(Unzip(Base(product, "docx")), references)));
                break;
            case Family.WordMl:
                File.WriteAllText(path, PatchWordMl(Base(product, format), references));
                break;
            case Family.Html:
                File.WriteAllText(path, Html(references, xhtml: false));
                break;
            case Family.Mhtml:
                File.WriteAllText(path, Mhtml(Html(references, xhtml: false)));
                break;
            case Family.Markdown:
                File.WriteAllBytes(Path.Combine(layout.Input, "picture.svg"), Svg(references.SvgImages));
                File.WriteAllText(path, Markdown(references));
                break;
            case Family.Epub:
                File.WriteAllBytes(path, Epub(references, layout.Input));
                break;
            case Family.Svg:
                File.WriteAllBytes(path, Svg([.. references.Images, .. references.SvgImages]));
                break;
        }
        return $"{IsolationLayout.InputFolder}/{name}";
    }

    /// <summary>An SVG picture that draws each reference as a 40-pixel image.</summary>
    public static byte[] Svg(IEnumerable<string> hrefs)
    {
        var images = new StringBuilder();
        foreach (string href in hrefs)
        {
            images.Append($"""<image xlink:href="{Attribute(href)}" x="0" y="0" width="40" height="40"/>""");
        }
        return Encoding.UTF8.GetBytes(
            $"""<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="40" height="40">{images}</svg>""");
    }

    private static string Attribute(string value) => WebUtility.HtmlEncode(value);

    // ----- Base documents -----

    /// <summary>
    /// The product's base document in a format: its own document with <see cref="References.MaxImages"/>
    /// + 1 neutral pictures, converted to the format or derived from one it converts to.
    /// </summary>
    private static byte[] Base(string product, string format) =>
        Bases.GetOrAdd($"{product}.{format}", static key => new Lazy<byte[]>(() =>
        {
            int dot = key.IndexOf('.', StringComparison.Ordinal);
            return BuildBase(key[..dot], key[(dot + 1)..]);
        })).Value;

    private static byte[] BuildBase(string product, string format)
    {
        if (!PrimaryFormats.TryGetValue(product, out string? primary))
        {
            throw new InvalidOperationException($"No base document for {product}, which reads {format}.");
        }
        if (Derived.TryGetValue(format, out (string Source, string Type) derived))
        {
            return Derive(Base(product, derived.Source), derived.Source, derived.Type);
        }
        using var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);
        string source = "base." + primary;
        if (format == primary)
        {
            CreateWithPictures(workspace, product, source);
            return File.ReadAllBytes(workspace.File(source));
        }
        File.WriteAllBytes(workspace.File(source), Base(product, primary));
        string target = "converted." + Extension(format);
        Require(workspace.Run(product, "convert", source, "--to", format, "--out", target), $"convert the {product} base document to {format}");
        return File.ReadAllBytes(workspace.File(target));
    }

    private static void CreateWithPictures(TempWorkspace workspace, string product, string file)
    {
        int count = References.MaxImages + 1;
        for (int index = 0; index < count; index++)
        {
            File.WriteAllBytes(workspace.File($"picture{index}.png"), OutputScan.Png(new SKColor(128, 128, (byte)(120 + index))));
        }
        if (product == "cells")
        {
            Require(workspace.Run("cells", "create", file), "create the cells base workbook");
            string ops = System.Text.Json.JsonSerializer.Serialize(new
            {
                ops = Enumerable.Range(0, count).Select(index => new { op = "insert_image", sheet = "Sheet1", at = $"B{2 + (index * 4)}", path = $"picture{index}.png" }).ToArray(),
            });
            File.WriteAllText(workspace.File("ops.json"), ops);
            Require(workspace.Run("cells", "edit", file, "--ops", "ops.json", "--in-place"), "insert the cells base pictures");
            return;
        }
        var markdown = new StringBuilder("# Isolation sample\n\nThe base document of the isolation matrix.\n\n");
        if (product == "slides")
        {
            // A Markdown outline places one picture per slide; the edit places them all on the first.
            File.WriteAllText(workspace.File("source.md"), markdown.ToString());
            Require(workspace.Run("slides", "create", file, "--from-markdown", "source.md"), "create the slides base presentation");
            string ops = System.Text.Json.JsonSerializer.Serialize(new
            {
                ops = Enumerable.Range(0, count).Select(index => new
                {
                    op = "insert_image",
                    slide = 1,
                    path = $"picture{index}.png",
                    rect = new { x = 20 + (index * 120), y = 300, width = 100, height = 100 },
                }).ToArray(),
            });
            File.WriteAllText(workspace.File("ops.json"), ops);
            Require(workspace.Run("slides", "edit", file, "--ops", "ops.json", "--in-place"), "insert the slides base pictures");
            return;
        }
        for (int index = 0; index < count; index++)
        {
            markdown.Append($"![picture {index}](picture{index}.png)\n\n");
        }
        File.WriteAllText(workspace.File("source.md"), markdown.ToString());
        Require(workspace.Run(product, "create", file, "--from-markdown", "source.md"), $"create the {product} base document");
    }

    private static byte[] Derive(byte[] source, string sourceFormat, string type)
    {
        Dictionary<string, byte[]> parts = Unzip(source);
        if (Families[sourceFormat] == Family.OpenDocument)
        {
            parts["mimetype"] = Encoding.ASCII.GetBytes(type);
            XDocument manifest = Xml(parts["META-INF/manifest.xml"]);
            manifest.Root!.Elements(Manifest + "file-entry").Single(entry => (string?)entry.Attribute(Manifest + "full-path") == "/")
                .SetAttributeValue(Manifest + "media-type", type);
            parts["META-INF/manifest.xml"] = Bytes(manifest);
            return Zip(parts);
        }
        XDocument types = Xml(parts["[Content_Types].xml"]);
        XNamespace ct = types.Root!.Name.Namespace;
        XElement main = types.Root.Elements(ct + "Override").Single(element => ((string)element.Attribute("ContentType")!).EndsWith(".main+xml", StringComparison.Ordinal)
            && ((string)element.Attribute("PartName")!).StartsWith("/ppt/", StringComparison.Ordinal));
        main.SetAttributeValue("ContentType", type);
        parts["[Content_Types].xml"] = Bytes(types);
        return Zip(parts);
    }

    private static void Require(CliResult result, string step)
    {
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Could not {step}: exit {result.ExitCode}: {result.StdErr}{result.StdOut}");
        }
    }

    // ----- Office Open XML packages -----

    /// <summary>
    /// Points the pictures of a package at the references: the first picture becomes an SVG part
    /// (with an <c>svgBlip</c> extension where it is a DrawingML picture) that draws
    /// <see cref="References.SvgImages"/>, and each next one links one of
    /// <see cref="References.Images"/> through a new <c>TargetMode="External"</c> relationship,
    /// as <c>r:link</c> of a DrawingML picture or <c>r:href</c> of VML image data.
    /// </summary>
    /// <exception cref="InvalidOperationException">The base has too few pictures for the references.</exception>
    private static Dictionary<string, byte[]> PatchPackage(Dictionary<string, byte[]> parts, References references)
    {
        bool svg = false;
        int linked = 0;
        foreach (string relationshipsName in parts.Keys.Where(static name => name.EndsWith(".rels", StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToArray())
        {
            string owner = OwnerOf(relationshipsName);
            if (!parts.TryGetValue(owner, out byte[]? ownerBytes) || TryXml(ownerBytes) is not { } document)
            {
                continue;
            }
            XDocument relationships = Xml(parts[relationshipsName]);
            XNamespace rel = RelationshipsNamespace;
            Dictionary<string, XElement> images = relationships.Root!.Elements(rel + "Relationship")
                .Where(static element => (string?)element.Attribute("Type") == ImageRelationship && element.Attribute("TargetMode") is null)
                .ToDictionary(static element => (string)element.Attribute("Id")!, StringComparer.Ordinal);
            bool changed = false;
            foreach (XElement element in document.Descendants().ToArray())
            {
                XAttribute? reference = element.Name.LocalName switch
                {
                    "blip" => element.Attribute(R + "embed"),
                    "imagedata" => element.Attribute(R + "id"),
                    _ => null,
                };
                if (reference is null || !images.TryGetValue(reference.Value, out XElement? image))
                {
                    continue;
                }
                if (!svg)
                {
                    string target = Resolve(owner, (string)image.Attribute("Target")!);
                    string svgPart = Path.ChangeExtension(target, null) + "-isolation.svg";
                    parts[svgPart] = Svg(references.SvgImages);
                    string id = "rIdIsolationSvg";
                    relationships.Root.Add(new XElement(rel + "Relationship",
                        new XAttribute("Id", id), new XAttribute("Type", ImageRelationship), new XAttribute("Target", RelativeTo(owner, svgPart))));
                    reference.Value = id;
                    element.Attribute(R + "link")?.Remove();
                    element.Attribute(R + "href")?.Remove();
                    if (element.Name.LocalName == "blip")
                    {
                        element.Elements(A + "extLst").Remove();
                        element.AddFirst(new XElement(A + "extLst", new XElement(A + "ext", new XAttribute("uri", SvgBlipExtension),
                            new XElement(AsSvg + "svgBlip", new XAttribute(XNamespace.Xmlns + "asvg", AsSvg), new XAttribute(R + "embed", id)))));
                    }
                    svg = true;
                    changed = true;
                    continue;
                }
                if (linked == references.Images.Count)
                {
                    continue;
                }
                string link = $"rIdIsolationLink{linked}";
                relationships.Root.Add(new XElement(rel + "Relationship",
                    new XAttribute("Id", link), new XAttribute("Type", ImageRelationship),
                    new XAttribute("Target", references.Images[linked]), new XAttribute("TargetMode", "External")));
                reference.Remove();
                element.Attribute(R + "link")?.Remove();
                element.Attribute(R + "href")?.Remove();
                element.SetAttributeValue(element.Name.LocalName == "blip" ? R + "link" : R + "href", link);
                linked++;
                changed = true;
            }
            if (changed)
            {
                parts[relationshipsName] = Bytes(relationships);
                parts[owner] = Bytes(document);
            }
        }
        if (!svg || linked < references.Images.Count)
        {
            throw new InvalidOperationException(
                $"The base package has {(svg ? 1 : 0) + linked} pictures; the references need {references.Images.Count + 1}.");
        }
        XDocument types = Xml(parts["[Content_Types].xml"]);
        XNamespace ct = types.Root!.Name.Namespace;
        if (!types.Root.Elements(ct + "Default").Any(static element => string.Equals((string?)element.Attribute("Extension"), "svg", StringComparison.OrdinalIgnoreCase)))
        {
            types.Root.AddFirst(new XElement(ct + "Default", new XAttribute("Extension", "svg"), new XAttribute("ContentType", "image/svg+xml")));
            parts["[Content_Types].xml"] = Bytes(types);
        }
        return parts;
    }

    /// <summary>The part whose relationships a <c>.rels</c> part lists: <c>word/_rels/document.xml.rels</c> lists <c>word/document.xml</c>.</summary>
    private static string OwnerOf(string relationships)
    {
        string folder = Path.GetDirectoryName(Path.GetDirectoryName(relationships))?.Replace('\\', '/') ?? string.Empty;
        string name = Path.GetFileName(relationships)[..^".rels".Length];
        return folder.Length == 0 ? name : $"{folder}/{name}";
    }

    private static string Resolve(string owner, string target)
    {
        if (target.StartsWith('/'))
        {
            return target[1..];
        }
        var segments = new List<string>((Path.GetDirectoryName(owner)?.Replace('\\', '/') ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries));
        foreach (string segment in target.Split('/'))
        {
            if (segment == "..")
            {
                segments.RemoveAt(segments.Count - 1);
            }
            else if (segment != ".")
            {
                segments.Add(segment);
            }
        }
        return string.Join('/', segments);
    }

    private static string RelativeTo(string owner, string part)
    {
        string folder = Path.GetDirectoryName(owner)?.Replace('\\', '/') ?? string.Empty;
        return Path.GetRelativePath(folder.Length == 0 ? "." : folder, part).Replace('\\', '/');
    }

    /// <summary>A package flattened into one Flat OPC document, every XML part inline and every other part in base64.</summary>
    private static string FlatOpc(Dictionary<string, byte[]> parts)
    {
        XDocument types = Xml(parts["[Content_Types].xml"]);
        XNamespace ct = types.Root!.Name.Namespace;
        var package = new XElement(Pkg + "package", new XAttribute(XNamespace.Xmlns + "pkg", Pkg));
        foreach ((string name, byte[] content) in parts.Where(static part => part.Key != "[Content_Types].xml"))
        {
            string type = (string?)types.Root.Elements(ct + "Override").FirstOrDefault(element => (string?)element.Attribute("PartName") == "/" + name)?.Attribute("ContentType")
                ?? (string?)types.Root.Elements(ct + "Default").FirstOrDefault(element => string.Equals((string?)element.Attribute("Extension"), Path.GetExtension(name).TrimStart('.'), StringComparison.OrdinalIgnoreCase))?.Attribute("ContentType")
                ?? "application/octet-stream";
            var part = new XElement(Pkg + "part", new XAttribute(Pkg + "name", "/" + name), new XAttribute(Pkg + "contentType", type));
            if (type.EndsWith("xml", StringComparison.Ordinal) && TryXml(content) is { } xml)
            {
                part.Add(new XElement(Pkg + "xmlData", xml.Root));
            }
            else
            {
                part.Add(new XAttribute(Pkg + "compression", "store"), new XElement(Pkg + "binaryData", Convert.ToBase64String(content)));
            }
            package.Add(part);
        }
        var document = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), new XProcessingInstruction("mso-application", "progid=\"Word.Document\""), package);
        return document.Declaration + Environment.NewLine + document.ToString(SaveOptions.DisableFormatting);
    }

    // ----- Word 2003 XML -----

    /// <summary>
    /// Points the VML pictures of a Word 2003 XML document at the references: the first one's
    /// embedded data becomes the SVG, and each next one names a reference as its <c>src</c> in
    /// place of its embedded data.
    /// </summary>
    private static string PatchWordMl(byte[] document, References references)
    {
        XDocument xml = Xml(document);
        Dictionary<string, XElement> data = xml.Descendants(WordMl + "binData")
            .ToDictionary(static element => (string)element.Attribute(WordMl + "name")!, StringComparer.Ordinal);
        bool svg = false;
        int linked = 0;
        foreach (XElement image in xml.Descendants().Where(static element => element.Name.LocalName == "imagedata").ToArray())
        {
            string? source = (string?)image.Attribute("src");
            if (source is null || !data.TryGetValue(source, out XElement? embedded))
            {
                continue;
            }
            if (!svg)
            {
                embedded.Value = Convert.ToBase64String(Svg(references.SvgImages));
                svg = true;
            }
            else if (linked < references.Images.Count)
            {
                image.SetAttributeValue("src", references.Images[linked++]);
                embedded.Remove();
            }
        }
        if (!svg || linked < references.Images.Count)
        {
            throw new InvalidOperationException($"The Word 2003 XML base has {(svg ? 1 : 0) + linked} pictures; the references need {references.Images.Count + 1}.");
        }
        return xml.Declaration + Environment.NewLine + xml.ToString(SaveOptions.DisableFormatting);
    }

    // ----- OpenDocument -----

    /// <summary>
    /// Points the <c>draw:image</c> pictures of an OpenDocument package at the references: the
    /// first becomes an SVG picture in the package, a copy of its frame holds the same SVG inline
    /// as <c>office:binary-data</c>, and each next one links a reference.
    /// </summary>
    private static byte[] PatchOpenDocument(byte[] package, References references)
    {
        Dictionary<string, byte[]> parts = Unzip(package);
        bool svg = false;
        int linked = 0;
        foreach (string name in new[] { "content.xml", "styles.xml" }.Where(parts.ContainsKey))
        {
            XDocument document = Xml(parts[name]);
            foreach (XElement image in document.Descendants(Draw + "image").ToArray())
            {
                string? href = (string?)image.Attribute(Xlink + "href");
                if (href is null || !href.StartsWith("Pictures/", StringComparison.Ordinal))
                {
                    continue;
                }
                if (!svg)
                {
                    string picture = "Pictures/isolation.svg";
                    parts[picture] = Svg(references.SvgImages);
                    image.SetAttributeValue(Xlink + "href", picture);
                    XDocument manifest = Xml(parts["META-INF/manifest.xml"]);
                    manifest.Root!.Add(new XElement(Manifest + "file-entry",
                        new XAttribute(Manifest + "full-path", picture), new XAttribute(Manifest + "media-type", "image/svg+xml")));
                    parts["META-INF/manifest.xml"] = Bytes(manifest);
                    XElement frame = image.Parent!;
                    var inline = new XElement(frame);
                    XElement inlineImage = inline.Element(Draw + "image")!;
                    inlineImage.Attribute(Xlink + "href")!.Remove();
                    inlineImage.Add(new XElement(Office + "binary-data", Convert.ToBase64String(Svg(references.SvgImages))));
                    frame.AddAfterSelf(inline);
                    svg = true;
                }
                else if (linked < references.Images.Count)
                {
                    image.SetAttributeValue(Xlink + "href", references.Images[linked++]);
                }
            }
            parts[name] = Bytes(document);
        }
        if (!svg || linked < references.Images.Count)
        {
            throw new InvalidOperationException($"The OpenDocument base has {(svg ? 1 : 0) + linked} pictures; the references need {references.Images.Count + 1}.");
        }
        return Zip(parts);
    }

    /// <summary>A flat OpenDocument presentation with one page: a frame per image reference and one SVG picture.</summary>
    private static string FlatOpenDocument(References references)
    {
        var frames = new StringBuilder();
        double x = 1;
        foreach (string href in references.Images)
        {
            frames.Append($"""<draw:frame draw:style-name="gr1" svg:x="{x}cm" svg:y="2cm" svg:width="3cm" svg:height="3cm"><draw:image xlink:href="{Attribute(href)}" xlink:type="simple" xlink:show="embed" xlink:actuate="onLoad"/></draw:frame>""");
            x += 4;
        }
        frames.Append($"""<draw:frame draw:style-name="gr1" svg:x="1cm" svg:y="8cm" svg:width="3cm" svg:height="3cm"><draw:image><office:binary-data>{Convert.ToBase64String(Svg(references.SvgImages))}</office:binary-data></draw:image></draw:frame>""");
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <office:document xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:style="urn:oasis:names:tc:opendocument:xmlns:style:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0" xmlns:draw="urn:oasis:names:tc:opendocument:xmlns:drawing:1.0" xmlns:presentation="urn:oasis:names:tc:opendocument:xmlns:presentation:1.0" xmlns:svg="urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0" xmlns:fo="urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0" xmlns:xlink="http://www.w3.org/1999/xlink" office:version="1.2" office:mimetype="application/vnd.oasis.opendocument.presentation">
            <office:styles><style:style style:name="standard" style:family="graphic"/></office:styles>
            <office:automatic-styles><style:page-layout style:name="PM1"><style:page-layout-properties fo:page-width="28cm" fo:page-height="15.75cm"/></style:page-layout>
            <style:style style:name="dp1" style:family="drawing-page"/><style:style style:name="gr1" style:family="graphic" style:parent-style-name="standard"/></office:automatic-styles>
            <office:master-styles><style:master-page style:name="Default" style:page-layout-name="PM1" draw:style-name="dp1"/></office:master-styles>
            <office:body><office:presentation><draw:page draw:name="page1" draw:style-name="dp1" draw:master-page-name="Default">
            <draw:frame draw:style-name="gr1" svg:x="1cm" svg:y="0.5cm" svg:width="20cm" svg:height="1.5cm"><draw:text-box><text:p>Isolation sample</text:p></draw:text-box></draw:frame>
            {frames}
            </draw:page></office:presentation></office:body></office:document>
            """;
    }

    // ----- HTML, MHTML, Markdown, EPUB -----

    /// <summary>
    /// The HTML sample: each image reference as an <c>img</c> and a CSS background, the SVG
    /// references in an inline SVG and in an SVG <c>data:</c> image, the style sheets as
    /// <c>link</c> and <c>@import</c>, the pages as <c>iframe</c> and <c>object</c>, and the decoy
    /// address after an attribute value that looks like a namespace declaration.
    /// </summary>
    private static string Html(References references, bool xhtml)
    {
        string end = xhtml ? " /" : string.Empty;
        var head = new StringBuilder();
        foreach (string sheet in references.Stylesheets)
        {
            head.Append($"""<link rel="stylesheet" type="text/css" href="{Attribute(sheet)}"{end}>""");
        }
        head.Append("<style>");
        foreach (string sheet in references.Stylesheets)
        {
            head.Append($"@import url('{sheet.Replace("\\", "/", StringComparison.Ordinal)}');");
        }
        head.Append("</style>");
        var body = new StringBuilder("<table><tr><td>Isolation sample</td><td>2</td></tr></table><p>Isolation sample</p>");
        foreach (string image in references.Images)
        {
            body.Append($"""<p><img src="{Attribute(image)}" width="40" height="40" alt="picture"{end}></p>""");
            body.Append($"""<div style="width:40px;height:40px;background-image:url('{Attribute(image.Replace("\\", "/", StringComparison.Ordinal))}')"></div>""");
        }
        string svg = Encoding.UTF8.GetString(Svg(references.SvgImages));
        body.Append("<p>").Append(svg).Append("</p>");
        body.Append($"""<p><img width="40" height="40" alt="svg" src="data:image/svg+xml;base64,{Convert.ToBase64String(Svg(references.SvgImages))}"{end}></p>""");
        foreach (string page in references.Pages)
        {
            body.Append($"""<iframe src="{Attribute(page)}" width="60" height="60"></iframe>""");
            body.Append($"""<object data="{Attribute(page)}" type="text/html" width="60" height="60"></object>""");
        }
        if (references.Decoy is { } decoy && !xhtml)
        {
            body.Append($"""<p><img alt='xmlns="' src={decoy} title='"'></p>""");
        }
        return xhtml
            ? $"""<?xml version="1.0" encoding="utf-8"?><html xmlns="http://www.w3.org/1999/xhtml"><head><title>Isolation sample</title>{head}</head><body>{body}</body></html>"""
            : $"""<!DOCTYPE html><html><head><meta charset="utf-8"><title>Isolation sample</title>{head}</head><body>{body}</body></html>""";
    }

    private static string Mhtml(string html) =>
        "MIME-Version: 1.0\r\nContent-Type: multipart/related; boundary=isolation-sample\r\n\r\n"
        + "--isolation-sample\r\nContent-Type: text/html; charset=utf-8\r\n"
        + "Content-Transfer-Encoding: 8bit\r\n\r\n" + html
        + "\r\n--isolation-sample--\r\n";

    /// <summary>
    /// Markdown images and inline HTML images of each reference, a local SVG image
    /// (<c>picture.svg</c>) that draws the SVG references, and the decoy as inline HTML.
    /// </summary>
    private static string Markdown(References references)
    {
        var markdown = new StringBuilder("# Isolation sample\n\nThe isolation matrix sample.\n\n");
        foreach (string image in references.Images)
        {
            markdown.Append($"![picture](<{image}>)\n\n");
            markdown.Append($"<img src=\"{Attribute(image)}\" width=\"40\" height=\"40\">\n\n");
        }
        markdown.Append("![svg picture](picture.svg)\n\n");
        if (references.Decoy is { } decoy)
        {
            markdown.Append($"<img alt='xmlns=\"' src={decoy} title='\"'>\n\n");
        }
        return markdown.ToString();
    }

    /// <summary>
    /// An EPUB whose chapter is the XHTML sample. A relative reference to a file of the input
    /// folder, which only a control makes, is packaged beside the chapter, so a control keeps
    /// only what its own package holds.
    /// </summary>
    private static byte[] Epub(References references, string inputFolder)
    {
        var local = references.Images.Concat(references.Stylesheets).Concat(references.Pages)
            .Where(static href => !href.Contains(':', StringComparison.Ordinal) && !href.StartsWith('\\'))
            .Select(static href => href.Replace("../", string.Empty, StringComparison.Ordinal))
            .Where(href => File.Exists(Path.Combine(inputFolder, href)))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var parts = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["mimetype"] = "application/epub+zip"u8.ToArray(),
            ["META-INF/container.xml"] = Encoding.UTF8.GetBytes("""<?xml version="1.0"?><container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container"><rootfiles><rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/></rootfiles></container>"""),
        };
        var manifest = new StringBuilder("""<item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/><item id="chapter" href="chapter.xhtml" media-type="application/xhtml+xml"/>""");
        int number = 0;
        foreach (string file in local)
        {
            parts["OEBPS/" + file] = File.ReadAllBytes(Path.Combine(inputFolder, file));
            string type = Path.GetExtension(file) switch { ".png" => "image/png", ".css" => "text/css", _ => "application/xhtml+xml" };
            manifest.Append($"""<item id="local{++number}" href="{file}" media-type="{type}"/>""");
        }
        parts["OEBPS/content.opf"] = Encoding.UTF8.GetBytes($"""<?xml version="1.0" encoding="utf-8"?><package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="id"><metadata xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:identifier id="id">isolation-sample</dc:identifier><dc:title>Isolation sample</dc:title><dc:language>en</dc:language><meta property="dcterms:modified">2026-01-01T00:00:00Z</meta></metadata><manifest>{manifest}</manifest><spine><itemref idref="chapter"/></spine></package>""");
        parts["OEBPS/nav.xhtml"] = Encoding.UTF8.GetBytes("""<?xml version="1.0" encoding="utf-8"?><html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops"><head><title>Contents</title></head><body><nav epub:type="toc"><ol><li><a href="chapter.xhtml">Isolation sample</a></li></ol></nav></body></html>""");
        References chapter = references with
        {
            Images = [.. references.Images.Select(Packaged)],
            SvgImages = [.. references.SvgImages.Select(Packaged)],
            Stylesheets = [.. references.Stylesheets.Select(Packaged)],
            Pages = [.. references.Pages.Select(Packaged)],
        };
        parts["OEBPS/chapter.xhtml"] = Encoding.UTF8.GetBytes(Html(chapter, xhtml: true));
        return Zip(parts);

        string Packaged(string href) => local.Contains(href.Replace("../", string.Empty, StringComparison.Ordinal)) ? href.Replace("../", string.Empty, StringComparison.Ordinal) : href;
    }

    // ----- Zip and XML -----

    private static Dictionary<string, byte[]> Unzip(byte[] package)
    {
        var parts = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        using var archive = new ZipArchive(new MemoryStream(package, writable: false), ZipArchiveMode.Read);
        foreach (ZipArchiveEntry entry in archive.Entries.Where(static entry => !entry.FullName.EndsWith('/')))
        {
            using var buffer = new MemoryStream();
            using (Stream stream = entry.Open())
            {
                stream.CopyTo(buffer);
            }
            parts[entry.FullName] = buffer.ToArray();
        }
        return parts;
    }

    /// <summary>A zip of the parts in their order; a <c>mimetype</c> part goes first and uncompressed, as OpenDocument and EPUB require.</summary>
    private static byte[] Zip(Dictionary<string, byte[]> parts)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, byte[] content) in parts.OrderBy(static part => part.Key == "mimetype" ? 0 : 1))
            {
                ZipArchiveEntry entry = archive.CreateEntry(name, name == "mimetype" ? CompressionLevel.NoCompression : CompressionLevel.Fastest);
                using Stream stream = entry.Open();
                stream.Write(content);
            }
        }
        return buffer.ToArray();
    }

    private static XDocument Xml(byte[] content) =>
        TryXml(content) ?? throw new InvalidOperationException("A part that must be XML is not.");

    private static XDocument? TryXml(byte[] content)
    {
        if (content.Length == 0 || (content[0] != '<' && !content.AsSpan().StartsWith(Encoding.UTF8.Preamble)))
        {
            return null;
        }
        try
        {
            using var stream = new MemoryStream(content, writable: false);
            return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    private static byte[] Bytes(XDocument document)
    {
        using var buffer = new MemoryStream();
        using (var writer = System.Xml.XmlWriter.Create(buffer, new System.Xml.XmlWriterSettings { Encoding = new UTF8Encoding(false) }))
        {
            document.Save(writer);
        }
        return buffer.ToArray();
    }
}
