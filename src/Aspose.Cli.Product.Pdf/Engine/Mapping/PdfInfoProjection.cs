using System.Globalization;
using System.Text.RegularExpressions;
using Aspose.Cli.Product.Pdf.Engine.Editing;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Results;
using Aspose.Pdf;
using Aspose.Pdf.Facades;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Text;

namespace Aspose.Cli.Product.Pdf.Engine.Mapping;

internal static class PdfInfoProjection
{
    private const int PagePreviewLimit = 20;
    private const int OutlineLimit = 200;

    public static PdfInfoResult Project(LoadedPdf loaded, string path, PdfInfoRequest request)
    {
        Document document = loaded.Document;
        HashSet<string> details = new(request.Details ?? [], StringComparer.Ordinal);
        bool includePages = request.IncludePreview;
        PdfFormSummary form = Form(document);
        PdfSignatureInfo[] signatures = Signatures(document);
        PdfOutlineItem[]? outline = details.Contains("outline") ? Outline(document) : null;
        var warnings = new List<Warning>();
        if (includePages && document.Pages.Count > PagePreviewLimit)
        {
            warnings.Add(EnvelopeParts.ListTruncated(
                "pages",
                PagePreviewLimit,
                document.Pages.Count,
                "'pdf.distinctPageSizes' counts the size of every page."));
        }

        if (outline?.Length == OutlineLimit && PdfMutationSupport.CountOutline(document.Outlines) is int total and > OutlineLimit)
        {
            warnings.Add(EnvelopeParts.ListTruncated(
                "outline",
                OutlineLimit,
                total,
                "Split at top-level bookmarks with 'aspose-cli pdf split --by-bookmarks' and inspect each part's outline."));
        }

        var result = new PdfInfoResult
        {
            Source = Source(path, includeFingerprint: true),
            Pdf = new PdfSummary
            {
                PageCount = document.Pages.Count,
                DistinctPageSizes = DistinctPageSizes(document),
                Version = Version(document.Version),
                Encrypted = document.IsEncrypted,
                Linearized = document.IsLinearized,
                Tagged = false,
                PdfaProfile = DeclaredPdfa(document.PdfFormat),
                FormType = form.Type,
                AttachmentCount = document.EmbeddedFiles.Count,
                Signed = signatures.Any(static item => item.Signed),
                PasswordType = OpenedWith(loaded),
            },
            Pages = includePages ? Pages(document) : null,
            PageLabels = PageLabels(document),
            Outline = outline,
            Forms = details.Contains("forms") ? form : null,
            Attachments = details.Contains("attachments") ? Attachments(document) : null,
            Fonts = details.Contains("fonts") ? Fonts(document) : null,
            Permissions = details.Contains("permissions") ? Permissions(loaded) : null,
            Signatures = details.Contains("signatures") ? signatures : null,
            Layers = details.Contains("layers") ? Layers(document) : null,
            Metadata = details.Contains("metadata") ? Metadata(document) : null,
            Warnings = warnings.Count == 0 ? null : warnings,
        };

        // Reading the structure tree rewrites the document's metadata and PDF/A identification
        // (PDF-TAGGED-CONTENT-WRITES), so it is read last; the inspected document is never saved.
        return result with { Pdf = result.Pdf with { Tagged = IsTagged(document) } };
    }

    public static SourceInfo Source(string path, bool includeFingerprint = false) => new()
    {
        Path = Path.GetFullPath(path),
        Format = "pdf",
        SizeBytes = new FileInfo(path).Length,
        Fingerprint = includeFingerprint ? FileFingerprints.Capture(path) : null,
    };

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
        Enumerable.Range(1, Math.Min(document.Pages.Count, PagePreviewLimit))
            .Select(pageNumber => Page(document.Pages[pageNumber]))
            .ToArray();

    private static PdfPageSizeSummary[] DistinctPageSizes(Document document) =>
        document.Pages
            .Select(static page => (Width: Round(page.GetPageRect(considerRotation: true).Width), Height: Round(page.GetPageRect(considerRotation: true).Height)))
            .GroupBy(static size => size)
            .Select(static group => new PdfPageSizeSummary
            {
                WidthPoints = group.Key.Width,
                HeightPoints = group.Key.Height,
                PageCount = group.Count(),
            })
            .OrderByDescending(static size => size.PageCount)
            .ThenBy(static size => size.WidthPoints)
            .ThenBy(static size => size.HeightPoints)
            .ToArray();

    private static PdfPageLabelInfo[] PageLabels(Document document) =>
        document.PageLabels.GetPages()
            .Order()
            .Select(pageIndex =>
            {
                PageLabel label = document.PageLabels.GetLabel(pageIndex);
                return new PdfPageLabelInfo
                {
                    StartPage = pageIndex + 1,
                    Style = PdfPageLabelStyles.ToStyle(label.NumberingStyle),
                    Prefix = string.IsNullOrEmpty(label.Prefix) ? null : label.Prefix,
                    StartingValue = label.StartingValue,
                };
            })
            .ToArray();

    private static PdfPageInfo Page(Page page) => new()
    {
        Page = page.Number,
        WidthPoints = Round(page.GetPageRect(considerRotation: true).Width),
        HeightPoints = Round(page.GetPageRect(considerRotation: true).Height),
        Rotation = Degrees(page.Rotate),
        MediaBox = Box(page.MediaBox),
        CropBox = Box(page.CropBox),
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

    private static PdfBox Box(Rectangle rectangle) => new()
    {
        Left = Round(rectangle.LLX),
        Bottom = Round(rectangle.LLY),
        Right = Round(rectangle.URX),
        Top = Round(rectangle.URY),
    };

    private static double Round(double value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);

    private static PdfOutlineItem[] Outline(Document document)
    {
        var results = new List<PdfOutlineItem>();
        AppendOutline(document.Outlines, level: 1, parentIndex: null, results);
        return results.ToArray();
    }

    private static void AppendOutline(
        IEnumerable<OutlineItemCollection> items,
        int level,
        string? parentIndex,
        List<PdfOutlineItem> results)
    {
        int position = 0;
        foreach (OutlineItemCollection item in items)
        {
            if (results.Count >= OutlineLimit)
            {
                return;
            }

            position++;
            string index = PdfMutationSupport.OutlineIndex(parentIndex, position);
            results.Add(new PdfOutlineItem
            {
                Title = item.Title ?? string.Empty,
                Level = level,
                Index = index,
                Page = PdfNavigationCensus.DestinationPage(item) is > 0 and int page ? page : null,
            });
            AppendOutline(item, level + 1, index, results);
        }
    }

    private static PdfFormSummary Form(Document document)
    {
        bool xfa = document.Form.HasXfa;
        string type = xfa ? "xfa" : document.Form.Count > 0 ? "acro" : "none";
        return new PdfFormSummary
        {
            Type = type,
            // A radio group's buttons share its name and are one field.
            FieldCount = document.Form.Fields.Select(static field => field.FullName).Distinct(StringComparer.Ordinal).Count(),
            ReadOnly = xfa,
        };
    }

    private static PdfAttachmentInfo[] Attachments(Document document) =>
        document.EmbeddedFiles
            .Select(static file => new PdfAttachmentInfo
            {
                Name = file.UnicodeName ?? file.Name ?? string.Empty,
                MimeType = string.IsNullOrWhiteSpace(file.MIMEType) ? null : file.MIMEType,
                SizeBytes = file.Params?.Size,
            })
            .OrderBy(static item => item.Name, StringComparer.Ordinal)
            .ToArray();

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
    /// The password the file was opened with; a file with only an owner password opened without
    /// one reports none.
    /// </summary>
    private static string OpenedWith(LoadedPdf loaded) =>
        loaded.PasswordType == PasswordType.User && !loaded.HasOpenPassword
            ? "none"
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
            HasOwnerPassword = new PdfFileInfo(document).HasEditPassword,
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

    private static IReadOnlyDictionary<string, string?> Metadata(Document document)
    {
        var values = new SortedDictionary<string, string?>(StringComparer.Ordinal)
        {
            ["author"] = EmptyToNull(document.Info.Author),
            ["creationDate"] = InfoDate(document.Info, "CreationDate"),
            ["creator"] = EmptyToNull(document.Info.Creator),
            ["keywords"] = EmptyToNull(document.Info.Keywords),
            ["modificationDate"] = InfoDate(document.Info, "ModDate"),
            ["producer"] = EmptyToNull(document.Info.Producer),
            ["subject"] = EmptyToNull(document.Info.Subject),
            ["title"] = EmptyToNull(document.Info.Title),
        };

        foreach ((string key, XmpValue value) in document.Metadata)
        {
            values["xmp:" + key] = XmpText(value);
        }

        return values;
    }

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

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
