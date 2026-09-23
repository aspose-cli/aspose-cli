using System.Globalization;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.IO;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
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

        return new PdfInfoResult
        {
            Source = Source(path, includeFingerprint: true),
            Pdf = new PdfSummary
            {
                Pages = document.Pages.Count,
                DistinctPageSizes = DistinctPageSizes(document),
                Version = Version(document.Version),
                Encrypted = document.IsEncrypted,
                Linearized = document.IsLinearized,
                Tagged = IsTagged(document),
                PdfaCompliant = document.IsPdfaCompliant,
                FormType = form.Type,
                Attachments = document.EmbeddedFiles.Count,
                Signed = signatures.Any(static item => item.Signed),
                PasswordType = loaded.PasswordType.ToString().ToLowerInvariant(),
            },
            Pages = includePages ? Pages(document) : null,
            PageLabels = PageLabels(document),
            Outline = details.Contains("outline") ? Outline(document) : null,
            Forms = details.Contains("forms") ? form : null,
            Attachments = details.Contains("attachments") ? Attachments(document) : null,
            Fonts = details.Contains("fonts") ? Fonts(document) : null,
            Permissions = details.Contains("permissions") ? Permissions(document, loaded.PasswordType) : null,
            Signatures = details.Contains("signatures") ? signatures : null,
            Layers = details.Contains("layers") ? Layers(document) : null,
            Metadata = details.Contains("metadata") ? Metadata(document) : null,
        };
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
                Count = group.Count(),
            })
            .OrderByDescending(static size => size.Count)
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
                    NumberingStyle = PageLabelStyle(label.NumberingStyle),
                    Prefix = string.IsNullOrEmpty(label.Prefix) ? null : label.Prefix,
                    StartingValue = label.StartingValue,
                };
            })
            .ToArray();

    private static string PageLabelStyle(NumberingStyle style) => style switch
    {
        NumberingStyle.NumeralsArabic => "arabic",
        NumberingStyle.NumeralsRomanUppercase => "roman-upper",
        NumberingStyle.NumeralsRomanLowercase => "roman-lower",
        NumberingStyle.LettersUppercase => "letters-upper",
        NumberingStyle.LettersLowercase => "letters-lower",
        NumberingStyle.None => "none",
        _ => "unknown",
    };

    private static PdfPageInfo Page(Page page) => new()
    {
        Number = page.Number,
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
        AppendOutline(document.Outlines, level: 1, results);
        return results.ToArray();
    }

    private static void AppendOutline(
        IEnumerable<OutlineItemCollection> items,
        int level,
        List<PdfOutlineItem> results)
    {
        foreach (OutlineItemCollection item in items)
        {
            if (results.Count >= OutlineLimit)
            {
                return;
            }

            results.Add(new PdfOutlineItem
            {
                Title = item.Title ?? string.Empty,
                Level = level,
                Destination = OutlineDestination(item),
            });
            AppendOutline(item, level + 1, results);
        }
    }

    private static string? OutlineDestination(OutlineItemCollection item)
    {
        ExplicitDestination? destination = item.Destination as ExplicitDestination;
        if (destination is null && item.Action is GoToAction action)
        {
            destination = action.Destination as ExplicitDestination;
        }

        return destination?.PageNumber is > 0 and int pageNumber
            ? $"page:{pageNumber.ToString(CultureInfo.InvariantCulture)}"
            : null;
    }

    private static PdfFormSummary Form(Document document)
    {
        bool xfa = document.Form.HasXfa;
        string type = xfa ? "xfa" : document.Form.Count > 0 ? "acro" : "none";
        return new PdfFormSummary
        {
            Type = type,
            Fields = document.Form.Fields.Length,
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

    private static PdfPermissionInfo Permissions(Document document, PasswordType passwordType)
    {
        var permissions = (Aspose.Pdf.Permissions)document.Permissions;
        bool owner = passwordType is PasswordType.Owner or PasswordType.None;
        bool hasOpenPassword;
        bool hasOwnerPassword;
        var fileInfo = new PdfFileInfo(document);
        hasOpenPassword = fileInfo.HasOpenPassword;
        hasOwnerPassword = fileInfo.HasEditPassword;

        bool Allowed(Aspose.Pdf.Permissions value) =>
            !document.IsEncrypted || owner || permissions.HasFlag(value);

        return new PdfPermissionInfo
        {
            HasOpenPassword = hasOpenPassword,
            HasOwnerPassword = hasOwnerPassword,
            OwnerAccess = owner,
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
            ["creationDate"] = InfoDate(() => document.Info.CreationDate),
            ["creator"] = EmptyToNull(document.Info.Creator),
            ["keywords"] = EmptyToNull(document.Info.Keywords),
            ["modificationDate"] = InfoDate(() => document.Info.ModDate),
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
        value == default ? null : value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static string? InfoDate(Func<DateTime> read)
    {
        try
        {
            return Date(read());
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            || exception.GetType().Assembly.GetName().Name == "Aspose.PDF")
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
