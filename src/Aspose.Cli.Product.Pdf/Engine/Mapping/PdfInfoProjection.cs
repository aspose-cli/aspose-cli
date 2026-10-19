using System.Globalization;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Results;
using Aspose.Pdf;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Text;

namespace Aspose.Cli.Product.Pdf.Engine.Mapping;

/// <summary>
/// The state of a PDF as <c>pdf inspect</c> reports it. Editing reads the same state from here:
/// the snapshots of an operation and the expectations its verification reads back are this
/// projection's values, and the objects are named by <see cref="Address"/>.
/// </summary>
internal static class PdfInfoProjection
{
    /// <summary>
    /// The address of a named object, such as <c>attachment/notes.txt</c>: its unit and its name as
    /// a read reports it, escaped by <see cref="TargetName.Escape"/>.
    /// </summary>
    internal static string Address(string unit, string name) => $"{unit}/{TargetName.Escape(name)}";

    /// <summary>The address of the document information entry <paramref name="key"/>, by the name a read reports it under.</summary>
    internal static string PropertyAddress(string key) => Address("property", PdfPropertyNames.Reported(key) ?? key);

    public static PdfInspectResult Project(LoadedPdf loaded, string path, PdfInspectRequest request)
    {
        Document document = loaded.Document;
        HashSet<string> details = new(request.Details ?? [], StringComparer.Ordinal);
        bool includePages = request.IncludePreview;
        var warnings = new List<Warning>();
        PdfLinkInfo[]? links = details.Contains(PdfInspectDetails.Links) ? [.. Links(document).Take(PdfInspectResult.LinkLimit + 1)] : null;
        if (links?.Length > PdfInspectResult.LinkLimit)
        {
            warnings.Add(EnvelopeParts.ListTruncated(
                "links",
                PdfInspectResult.LinkLimit,
                Links(document).Count(),
                "Inspect a split part of the document, such as 'aspose-cli pdf split --by pages --every 50', to see the rest."));
            links = links[..PdfInspectResult.LinkLimit];
        }

        bool fieldsTruncated = false;
        PdfFormInfo? form = details.Contains(PdfInspectDetails.Forms) ? PdfForms.Project(document, out fieldsTruncated) : null;
        PdfSignatureInfo[] signatures = Signatures(document);
        PdfOutlineItem[]? outline = details.Contains(PdfInspectDetails.Outline) ? Outline(document) : null;
        if (includePages && document.Pages.Count > PdfInspectResult.PagePreviewLimit)
        {
            warnings.Add(EnvelopeParts.ListTruncated(
                "pages",
                PdfInspectResult.PagePreviewLimit,
                document.Pages.Count,
                "'pdf.distinctPageSizes' counts the size of every page."));
        }

        if (outline?.Length == PdfInspectResult.OutlineLimit && PdfOutlineAddresses.CountOutline(document.Outlines) is int total and > PdfInspectResult.OutlineLimit)
        {
            warnings.Add(EnvelopeParts.ListTruncated(
                "outline",
                PdfInspectResult.OutlineLimit,
                total,
                "Split at top-level bookmarks with 'aspose-cli pdf split --by bookmarks' and inspect each part's outline."));
        }

        if (form is not null && fieldsTruncated)
        {
            warnings.Add(EnvelopeParts.ListTruncated(
                "forms.fields",
                PdfInspectResult.FieldLimit,
                form.FieldCount,
                "Fill or export the form as a whole with 'aspose-cli pdf extract --what forms --form-format json --out-dir <dir>'."));
        }

        var result = new PdfInspectResult
        {
            Input = Source(path, loaded),
            Pdf = new PdfSummary
            {
                PageCount = document.Pages.Count,
                DistinctPageSizes = DistinctPageSizes(document),
                Version = Version(document.Version),
                Encrypted = document.IsEncrypted,
                Linearized = document.IsLinearized,
                Tagged = false,
                PdfaProfile = DeclaredPdfa(document.PdfFormat),
                FormType = FormKind(document),
                AttachmentCount = document.EmbeddedFiles.Count,
                Signed = signatures.Any(static item => item.Signed),
                PasswordType = OpenedWith(loaded),
            },
            Pages = includePages ? Pages(document) : null,
            PageLabels = PageLabels(document),
            Outline = outline,
            Links = links,
            Forms = form,
            Attachments = details.Contains(PdfInspectDetails.Attachments) ? Attachments(document) : null,
            Fonts = details.Contains(PdfInspectDetails.Fonts) ? Fonts(document) : null,
            Permissions = details.Contains(PdfInspectDetails.Permissions) ? Permissions(loaded) : null,
            Signatures = details.Contains(PdfInspectDetails.Signatures) ? signatures : null,
            Layers = details.Contains(PdfInspectDetails.Layers) ? Layers(document) : null,
            Properties = details.Contains(PdfInspectDetails.Properties) ? Properties(document) : null,
            Warnings = warnings.Count == 0 ? null : warnings,
        };

        // Reading the structure tree rewrites the document's metadata and PDF/A identification
        // (PDF-TAGGED-CONTENT-WRITES), so it is read last; the inspected document is never saved.
        return result with { Pdf = result.Pdf with { Tagged = IsTagged(document) } };
    }

    public static InputInfo Source(string path, LoadedPdf loaded) => FileObjects.Input(path, "pdf", loaded.Encrypted);

    private static string Version(string version) =>
        version.Replace("v_", string.Empty, StringComparison.Ordinal).Replace('_', '.');

    /// <summary>The declared PDF/A part and conformance, such as <c>PDF_A_2B</c> as <c>pdfa-2b</c>.</summary>
    private static string? DeclaredPdfa(PdfFormat format)
    {
        string name = format.ToString();
        return name.StartsWith("PDF_A_", StringComparison.Ordinal)
            ? "pdfa-" + name["PDF_A_".Length..].Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant()
            : null;
    }

    private static bool IsTagged(Document document)
    {
        try
        {
            return document.TaggedContent.RootElement.ChildElements.Count > 0;
        }
        catch (Exception exception) when (exception.GetType().Assembly.GetName().Name == "Aspose.PDF")
        {
            return false;
        }
    }

    private static PdfPageInfo[] Pages(Document document) =>
        Enumerable.Range(1, Math.Min(document.Pages.Count, PdfInspectResult.PagePreviewLimit))
            .Select(pageNumber => Page(document.Pages[pageNumber]))
            .ToArray();

    private static PdfPageSizeSummary[] DistinctPageSizes(Document document) =>
        document.Pages
            .Select(static page => (Width: Round(page.GetPageRect(considerRotation: true).Width), Height: Round(page.GetPageRect(considerRotation: true).Height)))
            .GroupBy(static size => size)
            .Select(static group => new PdfPageSizeSummary
            {
                PageSize = PageSizeInfo.Of(group.Key.Width, group.Key.Height),
                PageCount = group.Count(),
            })
            .OrderByDescending(static size => size.PageCount)
            .ThenBy(static size => size.PageSize.Width)
            .ThenBy(static size => size.PageSize.Height)
            .ToArray();

    internal static PdfPageLabelInfo[] PageLabels(Document document) =>
        document.PageLabels.GetPages()
            .Order()
            .Select(pageIndex =>
            {
                PageLabel label = document.PageLabels.GetLabel(pageIndex);
                return new PdfPageLabelInfo
                {
                    StartPage = pageIndex + 1,
                    NumberStyle = PdfPageLabelStyles.ToStyle(label.NumberingStyle),
                    Prefix = string.IsNullOrEmpty(label.Prefix) ? null : label.Prefix,
                    StartingValue = label.StartingValue,
                };
            })
            .ToArray();

    internal static PdfPageInfo Page(Page page) => new()
    {
        Page = page.Number,
        PageSize = PageSizeInfo.Of(Round(page.GetPageRect(considerRotation: true).Width), Round(page.GetPageRect(considerRotation: true).Height)),
        Rotation = Degrees(page.Rotate),
        MediaBox = Box(page, page.MediaBox),
        CropBox = Box(page, page.CropBox),
    };

    /// <summary>The page's clockwise rotation in degrees; a full turn is no rotation.</summary>
    private static int Degrees(Rotation rotation) => rotation switch
    {
        Rotation.None or Rotation.on360 => 0,
        Rotation.on90 => 90,
        Rotation.on180 => 180,
        Rotation.on270 => 270,
        _ => throw new ArgumentOutOfRangeException(nameof(rotation), rotation, "Page rotation is missing from the projection."),
    };

    /// <summary>
    /// A page box as it is seen, with the page's rotation, in points from the top-left corner of
    /// the media box seen the same way.
    /// </summary>
    private static PdfRect Box(Page page, Rectangle box)
    {
        Rectangle media = page.RotationMatrix.Transform(page.MediaBox);
        Rectangle seen = page.RotationMatrix.Transform(box);
        return PdfGeometry.Rect(seen.LLX - media.LLX, media.URY - seen.URY, seen.Width, seen.Height);
    }

    private static double Round(double value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);

    private static PdfOutlineItem[] Outline(Document document) =>
        [.. Outline(document.Outlines, parent: null, levels: int.MaxValue, PdfInspectResult.OutlineLimit).Select(static entry => entry.Item)];

    /// <summary>
    /// The bookmarks of <paramref name="items"/>, which lie below the bookmark at
    /// <paramref name="parent"/> (the top level when null), in outline order down to
    /// <paramref name="levels"/> levels and at most <paramref name="limit"/> of them; the level of
    /// <paramref name="items"/> is 1. Each comes with the page its destination stores, which is
    /// not 1 or more when it opens no page of the document (<see cref="PdfOutlineItem.Page"/> is then null).
    /// </summary>
    internal static List<(PdfOutlineItem Item, int Destination)> Outline(IEnumerable<OutlineItemCollection> items, string? parent, int levels, int limit)
    {
        var results = new List<(PdfOutlineItem Item, int Destination)>();
        Append(items, parent, depth: 1);
        return results;

        void Append(IEnumerable<OutlineItemCollection> siblings, string? above, int depth)
        {
            int position = 0;
            foreach (OutlineItemCollection item in siblings)
            {
                if (results.Count >= limit)
                {
                    return;
                }

                string index = PdfOutlineAddresses.OutlineIndex(above, ++position);
                int destination = PdfNavigationCensus.DestinationPage(item);
                results.Add((new PdfOutlineItem
                {
                    Title = item.Title ?? string.Empty,
                    Level = depth,
                    Bookmark = index,
                    Page = destination > 0 ? destination : null,
                }, destination));
                if (depth < levels)
                {
                    Append(item, index, depth + 1);
                }
            }
        }
    }

    /// <summary>The kind of the document's form, or null when it has none.</summary>
    private static string? FormKind(Document document) =>
        document.Form.HasXfa ? PdfFormKinds.Xfa : document.Form.Count > 0 ? PdfFormKinds.Acro : null;

    /// <summary>The links of every page that open a web address, in page order.</summary>
    private static IEnumerable<PdfLinkInfo> Links(Document document) =>
        Enumerable.Range(1, document.Pages.Count).SelectMany(number =>
        {
            Page page = document.Pages[number];
            return page.Annotations.OfType<Aspose.Pdf.Annotations.LinkAnnotation>()
                .Where(static link => link.Action is Aspose.Pdf.Annotations.GoToURIAction { URI.Length: > 0 })
                .Select(link => new PdfLinkInfo
                {
                    Page = number,
                    Rect = PdfGeometry.ToContractRect(page, link.Rect),
                    Url = ((Aspose.Pdf.Annotations.GoToURIAction)link.Action).URI,
                });
        });

    private static PdfAttachmentInfo[] Attachments(Document document) =>
        [.. AttachmentEntries(document).Select(static entry => entry.Info).OrderBy(static item => item.Name, StringComparer.Ordinal)];

    /// <summary>The attachments in the order the engine lists them, each as a read reports it, with its file.</summary>
    internal static IEnumerable<(PdfAttachmentInfo Info, FileSpecification File)> AttachmentEntries(Document document) =>
        document.EmbeddedFiles
            .Select(static file => (Info: new PdfAttachmentInfo
            {
                Name = file.UnicodeName ?? file.Name ?? string.Empty,
                MimeType = string.IsNullOrWhiteSpace(file.MIMEType) ? null : file.MIMEType,
                SizeBytes = file.Params?.Size,
                Description = string.IsNullOrWhiteSpace(file.Description) ? null : file.Description,
            }, File: file));

    private static PdfFontInfo[] Fonts(Document document)
    {
        var fonts = new Dictionary<string, PdfFontInfo>(StringComparer.Ordinal);
        foreach (Font font in PdfFontResources.Enumerate(document))
        {
            string name = font.FontName ?? "unknown";
            fonts[name] = new PdfFontInfo
            {
                Name = name,
                Embedded = font.IsEmbedded,
                Subset = font.IsSubset,
            };
        }

        return fonts.Values.OrderBy(static item => item.Name, StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// The password the file was opened with, or null when it needed none: a file with only an
    /// owner password opened without one needed none.
    /// </summary>
    private static string? OpenedWith(LoadedPdf loaded) =>
        loaded.PasswordType == PasswordType.None || (loaded.PasswordType == PasswordType.User && !loaded.HasOpenPassword)
            ? null
            : loaded.PasswordType.ToString().ToLowerInvariant();

    /// <summary>
    /// The passwords of the file and the permissions it grants readers without the owner
    /// password, also when this run opened it with the owner password, which lifts them.
    /// </summary>
    private static PdfPermissionInfo Permissions(LoadedPdf loaded)
    {
        Document document = loaded.Document;
        var permissions = (Aspose.Pdf.Permissions)document.Permissions;

        bool Allowed(Aspose.Pdf.Permissions value) =>
            !document.IsEncrypted || permissions.HasFlag(value);

        return new PdfPermissionInfo
        {
            HasOpenPassword = loaded.HasOpenPassword,
            HasOwnerPassword = loaded.HasOwnerPassword,
            OwnerAccess = loaded.PasswordType is PasswordType.Owner or PasswordType.None,
            Print = Allowed(Aspose.Pdf.Permissions.PrintDocument),
            Copy = Allowed(Aspose.Pdf.Permissions.ExtractContent),
            Modify = Allowed(Aspose.Pdf.Permissions.ModifyContent),
            Annotate = Allowed(Aspose.Pdf.Permissions.ModifyTextAnnotations),
            FillForms = Allowed(Aspose.Pdf.Permissions.FillForm),
            ExtractAccessibility = Allowed(Aspose.Pdf.Permissions.ExtractContentWithDisabilities),
            Assemble = Allowed(Aspose.Pdf.Permissions.AssembleDocument),
            PrintHighResolution = Allowed(Aspose.Pdf.Permissions.PrintingQuality),
        };
    }

    private static PdfSignatureInfo[] Signatures(Document document) =>
        document.Form.Fields
            .OfType<SignatureField>()
            .Select(static field =>
            {
                Signature? signature = field.Signature;
                bool signed = signature is not null;
                return new PdfSignatureInfo
                {
                    Name = field.FullName ?? field.PartialName ?? string.Empty,
                    Signed = signed,
                    Valid = signed ? Verify(signature!) : null,
                };
            })
            .OrderBy(static item => item.Name, StringComparer.Ordinal)
            .ToArray();

    private static bool? Verify(Signature signature)
    {
        try
        {
            return signature.Verify();
        }
        catch (Exception exception) when (exception.GetType().Assembly.GetName().Name == "Aspose.PDF")
        {
            return null;
        }
    }

    private static string[] Layers(Document document) =>
        document.Pages
            .SelectMany(static page => page.Layers ?? [])
            .Select(static layer => layer.Name)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray()!;

    /// <summary>The document properties by the names a read reports them under.</summary>
    internal static IReadOnlyDictionary<string, string?> Properties(Document document)
    {
        var values = new SortedDictionary<string, string?>(StringComparer.Ordinal);
        foreach ((string name, string? value) in PropertyEntries(document))
        {
            values[name] = value;
        }

        return values;
    }

    /// <summary>
    /// The document information entries, every standard one and each custom one under its own
    /// key, as set_properties custom names it, in the order of their keys; then the XMP entries.
    /// </summary>
    internal static IEnumerable<(string Name, string? Value)> PropertyEntries(Document document)
    {
        DocumentInfo info = document.Info;
        return PdfPropertyNames.Standards
            .Select(entry => (entry.Key, entry.Name, Value: StandardValue(info, entry.Key)))
            .Concat(info.Keys.Where(static key => PdfPropertyNames.Standard(key) is null).Select(key => (Key: key, Name: key, Value: InfoText(info[key]))))
            .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
            .Select(static entry => (entry.Name, entry.Value))
            .Concat(document.Metadata.Select(static entry => ("xmp:" + entry.Key, XmpText(entry.Value))));
    }

    /// <summary>A document information text as a read reports it: blank text is no value.</summary>
    internal static string? InfoText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string? StandardValue(DocumentInfo info, string key) => key switch
    {
        "Title" => InfoText(info.Title),
        "Author" => InfoText(info.Author),
        "Subject" => InfoText(info.Subject),
        "Keywords" => InfoText(info.Keywords),
        "Creator" => InfoText(info.Creator),
        "Producer" => InfoText(info.Producer),
        _ => InfoDate(info, key),
    };

    private static string? Date(DateTime value) =>
        value == default ? null : Iso(value.ToUniversalTime());

    /// <summary>
    /// ISO 8601 without a fraction of a second that is zero, as XMP writes dates: UTC ends in
    /// Z, a date without an offset has none.
    /// </summary>
    private static string Iso(DateTime value) =>
        value.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK", CultureInfo.InvariantCulture);

    /// <summary>
    /// A document information date in UTC, read from its stored text: the engine's
    /// <see cref="DocumentInfo.CreationDate"/> keeps the clock time and drops the offset. A
    /// date without an offset is reported without one; text that is not a PDF date as stored.
    /// </summary>
    private static string? InfoDate(DocumentInfo info, string key) =>
        info.TryGetValue(key, out string? stored) && !string.IsNullOrWhiteSpace(stored)
            ? PdfDate(stored.Trim()) ?? stored.Trim()
            : null;

    /// <summary>Parses <c>D:YYYYMMDDHHmmSSOHH'mm'</c>, where every part after the year is optional.</summary>
    private static string? PdfDate(string text)
    {
        Match match = Regex.Match(
            text,
            @"^(?:D:)?(?<y>\d{4})(?<M>\d{2})?(?<d>\d{2})?(?<h>\d{2})?(?<m>\d{2})?(?<s>\d{2})?(?:(?<z>Z)(?:00'?(?:00'?)?)?|(?<sign>[+-])(?<oh>\d{2})(?:'?(?<om>\d{2})'?)?)?$",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return null;
        }

        int Part(string name, int fallback) =>
            match.Groups[name].Success ? int.Parse(match.Groups[name].Value, CultureInfo.InvariantCulture) : fallback;
        try
        {
            var clock = new DateTime(Part("y", 0), Part("M", 1), Part("d", 1), Part("h", 0), Part("m", 0), Part("s", 0), DateTimeKind.Unspecified);
            if (match.Groups["z"].Success)
            {
                return Iso(DateTime.SpecifyKind(clock, DateTimeKind.Utc));
            }

            if (!match.Groups["sign"].Success)
            {
                return Iso(clock);
            }

            var offset = new TimeSpan(Part("oh", 0), Part("om", 0), 0);
            var stated = new DateTimeOffset(clock, match.Groups["sign"].Value == "-" ? -offset : offset);
            return Iso(stated.UtcDateTime);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static string? XmpText(XmpValue value)
    {
        try
        {
            if (value.IsString)
            {
                return value.ToStringValue();
            }

            if (value.IsDateTime)
            {
                return Date(value.ToDateTime());
            }

            if (value.IsInteger)
            {
                return value.ToInteger().ToString(CultureInfo.InvariantCulture);
            }

            if (value.IsDouble)
            {
                return value.ToDouble().ToString(CultureInfo.InvariantCulture);
            }

            return value.IsRaw ? value.ToRaw().OuterXml : value.ToString();
        }
        catch (Exception exception) when (exception.GetType().Assembly.GetName().Name == "Aspose.PDF")
        {
            return null;
        }
    }

    /// <summary>
    /// What an edit compares that a read reports coarser or not at all: the text the document
    /// stores, which a read trims, normalizes or leaves out, and the parts of an object no read
    /// reports. The objects are those a read names, under the same addresses.
    /// </summary>
    internal static class Unreported
    {
        /// <summary>
        /// The document information entries a read reports, each by the name it reports and the
        /// text the document stores, in the order of their keys, then every XMP entry by its stored
        /// value; an empty entry is one the document does not hold.
        /// </summary>
        internal static IEnumerable<(string Name, string Address, string Stored)> Properties(Document document)
        {
            foreach (string key in document.Info.Keys.Order(StringComparer.Ordinal))
            {
                if (document.Info[key] is { Length: > 0 } stored && PdfPropertyNames.Reported(key) is { } name)
                {
                    yield return (name, Address("property", name), stored);
                }
            }

            foreach ((string key, XmpValue value) in document.Metadata)
            {
                yield return ("xmp:" + key, $"property/xmp:{TargetName.Escape(key)}", value.ToString() ?? string.Empty);
            }
        }

        /// <summary>The text the document information entry <paramref name="key"/> stores, or null when there is none.</summary>
        internal static string? Info(Document document, string key) => document.Info.TryGetValue(key, out string? stored) ? stored : null;

        /// <summary>An attachment's stored description, media type and length (-1 when its stream cannot tell it).</summary>
        internal static string Attachment(FileSpecification file) =>
            string.Create(CultureInfo.InvariantCulture, $"{file.Description}|{file.MIMEType}|{Length(file) ?? -1}");

        /// <summary>The attachment's length when its stream can tell it without being read.</summary>
        internal static long? Length(FileSpecification file) => file.Contents is { CanSeek: true } contents ? contents.Length : null;

        /// <summary>A page's stored rotation and boxes, which a read reports rounded and from the media box.</summary>
        internal static string Geometry(Page page) =>
            string.Create(CultureInfo.InvariantCulture, $"{page.Rotate}|{Box(page.MediaBox)}|{Box(page.CropBox)}");

        /// <summary>A rectangle as the document stores it.</summary>
        internal static string Box(Rectangle? box) =>
            box is null ? "-" : string.Create(CultureInfo.InvariantCulture, $"{box.LLX},{box.LLY},{box.URX},{box.URY}");
    }
}
