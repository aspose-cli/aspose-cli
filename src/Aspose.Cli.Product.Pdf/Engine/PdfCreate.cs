using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Files;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Pdf;
using Aspose.Pdf.Text;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Creates one PDF from images, HTML, text or Markdown: <c>pdf create</c>.</summary>
internal static class PdfCreate
{
    internal static PdfCreateResult Run(PdfSession session, PdfCreateRequest request)
    {
        PdfCreateSource source = request.Source;
        (byte[]? html, IReadOnlyList<byte[]> imageContents) = EnsureCreationInputs(session.Budgets, source);
        // The Markdown importer resolves the Markdown's relative references against the
        // working directory and reads files itself; the check holds them until the save.
        using MarkdownImportResources? markdown = source is PdfMarkdownSource { File.Path: var markdownPath }
            ? new MarkdownImportResources(markdownPath, session.Budgets, Environment.CurrentDirectory) : null;
        LicenseState state = session.Outputs.License;
        using HtmlImportResources? resources = source is PdfHtmlSource { File.Path: var htmlPath, AllowNetworkResources: var allowNetwork }
            ? new HtmlImportResources(htmlPath, session.Budgets, allowNetwork) : null;
        var warnings = new List<Warning>();
        using Document document = source switch
        {
            PdfImageSource images => CreateFromImages([.. images.Files.Select(static file => file.Path)], imageContents, request, warnings),
            PdfHtmlSource page => CreateFromHtml(page.File.Path, request, resources!),
            PdfMarkdownSource notes => CreateFromText(notes.File.Path, markdown: true, request),
            PdfTextSource text => CreateFromText(text.File.Path, markdown: false, request),
            _ => throw new UnreachableException(),
        };
        resources?.ThrowIfFailed();
        if (source is PdfHtmlSource or PdfMarkdownSource)
        {
            // Both importers set the title, author and subject to a placeholder
            // (PDF-IMPORT-INFO-PLACEHOLDER); keep only the title the HTML states.
            document.Info.Title = html is null ? string.Empty : HtmlDocumentTitle.Read(html) ?? string.Empty;
            document.Info.Author = string.Empty;
            document.Info.Subject = string.Empty;
        }

        PdfOutputPasswords? passwords = PdfOutputEncryption.Requested(request.EncryptPassword);
        bool encrypted = PdfOutputEncryption.Encrypted(document, passwords);
        PublishedFile written = session.Outputs.Write(request.Output.Path, request.Output.Format, request.Output.Overwrite, document, path =>
        {
            try { PdfOutputEncryption.Save(document, path, passwords); }
            finally { resources?.ThrowIfFailed(); }
        }, encrypted: encrypted);
        warnings.AddRange(resources?.Warnings ?? []);
        if (html is not null && FormLosses(document, html) is { } losses)
        {
            warnings.Add(losses);
        }

        return new PdfCreateResult
        {
            Output = FileObjects.Output(written, request.Output.Format, encrypted),
            Inputs = CreationInputs(source, imageContents),
            License = EnvelopeParts.License(state),
            Warnings = warnings.Count == 0 ? null : warnings,
        };
    }

    /// <summary>
    /// One page per image. The page takes the named size in the image's orientation (landscape
    /// for an image wider than it is tall), and the image keeps its aspect ratio: it is scaled to
    /// fill the margin box in one dimension and centred in the other. The margins must leave a
    /// content area on each page in the orientation it takes. The engine embeds the bytes the
    /// image check read, so each file is read once and cannot change between the check and the
    /// save.
    /// </summary>
    private static Document CreateFromImages(
        IReadOnlyList<string> paths, IReadOnlyList<byte[]> contents, PdfCreateRequest request, List<Warning> warnings)
    {
        var document = new Document();
        try
        {
            (double portraitWidth, double portraitHeight) = PageSizePresets.Dimensions(request.PageSize);
            PdfMargins margins = request.Margins;
            for (int index = 0; index < paths.Count; index++)
            {
                string path = paths[index];
                byte[] content = contents[index];
                (double imageWidth, double imageHeight) = PdfImageSize.Read(content);
                bool landscape = imageWidth > imageHeight;
                (double width, double height) = landscape
                    ? (portraitHeight, portraitWidth)
                    : (portraitWidth, portraitHeight);
                ValidateMargins(margins, width, height, $"the {(landscape ? "landscape" : "portrait")} page of {Path.GetFileName(path)}");
                double boxWidth = width - margins.Left - margins.Right;
                double boxHeight = height - margins.Top - margins.Bottom;
                double scale = Math.Min(boxWidth / imageWidth, boxHeight / imageHeight);
                if (Blurred(path, content, Math.Max(imageWidth, imageHeight) * scale, index + 1) is { } blurred)
                {
                    warnings.Add(blurred);
                }

                double left = margins.Left + Math.Max(0, boxWidth - imageWidth * scale) / 2;
                double top = margins.Top + Math.Max(0, boxHeight - imageHeight * scale) / 2;

                // The layout places the image at the top-left margins and moves an image taller
                // than the room below them to the next page, again on every page: an image that
                // fills the margin box but comes out a rounding error taller than the room the
                // engine computes would never stop adding pages. So the right and bottom margins
                // are left to the image's own size, and the image is never larger than the room
                // from its corner to the page edges.
                Page page = document.Pages.Add();
                page.SetPageSize(width, height);
                page.PageInfo.Margin = new MarginInfo { Top = top, Left = left, Right = 0, Bottom = 0 };
                page.Paragraphs.Add(new Aspose.Pdf.Image
                {
                    ImageStream = new MemoryStream(content, writable: false),
                    FileType = FileTypeOf(content),
                    FixWidth = Math.Min(Math.Max(1, imageWidth * scale), width - left),
                    FixHeight = Math.Min(Math.Max(1, imageHeight * scale), height - top),
                });
            }

            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The pixels per inch below which an image drawn on a page shows blurred: 96, the pixels a
    /// screen shows an inch of a page at 100% zoom, so the image is enlarged beyond its own size.
    /// </summary>
    private const double SharpPixelsPerInch = 96;

    /// <summary>
    /// A warning for a raster image whose longer side, drawn <paramref name="longerSidePoints"/>
    /// long, has fewer than <see cref="SharpPixelsPerInch"/> pixels per inch; null for a sharp
    /// or vector image.
    /// </summary>
    private static Warning? Blurred(string path, byte[] content, double longerSidePoints, int page)
    {
        if (ImageInput.EngineFormatOf(content) == EngineImageFormat.Svg)
        {
            return null;
        }

        using SkiaSharp.SKCodec? codec = SkiaSharp.SKCodec.Create(new MemoryStream(content, writable: false));
        if (codec is null)
        {
            return null;
        }

        int longer = Math.Max(codec.Info.Width, codec.Info.Height);
        double perInch = longer / (longerSidePoints / 72);
        if (perInch >= SharpPixelsPerInch)
        {
            return null;
        }

        int needed = (int)Math.Ceiling(SharpPixelsPerInch * longerSidePoints / 72);
        return new Warning(PdfDiagnostics.ImageResolutionLow, string.Create(CultureInfo.InvariantCulture,
            $"Image '{Path.GetFileName(path)}' ({codec.Info.Width} x {codec.Info.Height} pixels) is drawn on page {page} at {perInch:0} pixels per inch, enlarged beyond its own size, so it shows blurred."))
        {
            Location = string.Create(CultureInfo.InvariantCulture, $"page/{page}"),
            Hint = string.Create(CultureInfo.InvariantCulture,
                $"Use an image with at least {needed} pixels on its longer side, or a smaller --page-size or wider --margins."),
        };
    }

    /// <summary>
    /// The type the engine reads a streamed image as, from the format the image check recognized:
    /// SVG and DICOM have their own types, and the engine recognizes any other image itself.
    /// </summary>
    internal static ImageFileType FileTypeOf(byte[] content) => ImageInput.EngineFormatOf(content) switch
    {
        EngineImageFormat.Svg => ImageFileType.Svg,
        EngineImageFormat.Dicom => ImageFileType.Dicom,
        _ => ImageFileType.Unknown,
    };

    private static Document CreateFromHtml(
        string path, PdfCreateRequest request, HtmlImportResources resources)
    {
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw CliErrors.FileNotFound(fullPath);
        }

        (double width, double height) = PageSizePresets.Dimensions(request.PageSize);
        ValidateMargins(request.Margins, width, height);
        var options = new HtmlLoadOptions(resources.BaseDirectory)
        {
            PageInfo = new PageInfo
            {
                Width = width,
                Height = height,
                Margin = Margin(request.Margins),
            },
            CustomLoaderOfExternalResources = resources.Load,
        };
        Document? document = null;
        try
        {
            // The importer reads the whole HTML while it constructs the document.
            using (FileStream stream = InputFiles.OpenRead(fullPath))
            {
                document = new Document(stream, options);
            }

            resources.ThrowIfFailed();
            // The importer gives a check box neither a border nor a border colour, so its
            // appearance draws no box and an unchecked box shows nothing (PDF-HTML-CHECKBOX-BOX).
            // A black one-point border, as the importer gives radio buttons, makes the engine draw it.
            foreach (Aspose.Pdf.Forms.CheckboxField box in document.Form.Fields.OfType<Aspose.Pdf.Forms.CheckboxField>())
            {
                if (box.Characteristics.Border.IsEmpty)
                {
                    box.Characteristics.Border = System.Drawing.Color.Black;
                    box.Border ??= new Aspose.Pdf.Annotations.Border(box) { Width = 1 };
                }
            }

            return document;
        }
        catch
        {
            document?.Dispose();
            resources.ThrowIfFailed();
            throw;
        }
    }

    /// <summary>
    /// The input types the HTML importer drops without a field (PDF-HTML-FORM-INPUTS); a hidden
    /// input shows nothing, but its value does not reach the PDF either.
    /// </summary>
    private static readonly Regex DroppedInput = new(
        """<input\b[^>]*?\stype\s*=\s*["']?(email|tel|url|time|datetime-local|month|week|color|range|file|image|hidden)\b""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>An element's id attribute, which the HTML importer names some fields after (PDF-HTML-FORM-NAMES).</summary>
    private static readonly Regex IdAttribute = new(
        """\sid\s*=\s*(?:"([^"]*)"|'([^']*)'|([^\s>]+))""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>An HTML comment, whose inputs the importer does not see.</summary>
    private static readonly Regex HtmlComment = new("<!--.*?-->", RegexOptions.Singleline, TimeSpan.FromSeconds(1));

    /// <summary>
    /// The HTML importer keeps the name of a single-line text input only; it names every other
    /// control after its id attribute, or itself when it has none or is a radio button, and
    /// drops its HTML value (PDF-HTML-FORM-NAMES), and it drops some input
    /// types entirely (PDF-HTML-FORM-INPUTS), which the HTML shows. Both are disclosed, so the
    /// fields are matched to their labels by position before filling.
    /// </summary>
    private static Warning? FormLosses(Document document, byte[] html)
    {
        string markup = HtmlComment.Replace(Encoding.UTF8.GetString(html), string.Empty);
        var ids = new HashSet<string>(
            IdAttribute.Matches(markup).Select(static match => match.Groups.Cast<System.Text.RegularExpressions.Group>().Skip(1).First(static group => group.Success).Value),
            StringComparer.Ordinal);
        ILookup<bool, string> names = document.Form.Fields
            .Where(static field => field is not Aspose.Pdf.Forms.TextBoxField { Multiline: false })
            .Select(static field => field.FullName)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToLookup(name => ids.Contains(name));
        string[] dropped = DroppedInput.Matches(markup)
            .Select(static match => match.Groups[1].Value.ToLowerInvariant())
            .ToArray();
        var losses = new List<string>(3);
        if (names[true].Any())
        {
            losses.Add($"these form fields are named after their HTML id attribute and lost their HTML values: {string.Join(", ", names[true].Select(static name => $"'{name}'"))}");
        }

        if (names[false].Any())
        {
            losses.Add($"these form fields have generated names and lost their HTML values: {string.Join(", ", names[false].Select(static name => $"'{name}'"))}");
        }

        if (dropped.Length > 0)
        {
            losses.Add($"it dropped {dropped.Length} input(s) of type {string.Join(", ", dropped.Distinct(StringComparer.Ordinal))}, which have no field");
        }

        return losses.Count == 0 ? null : new Warning(WarningCodes.LossyConversion, $"The HTML importer keeps the name and value of single-line text inputs only: {string.Join("; ", losses)}.")
        {
            Hint = "Read the fields with 'pdf inspect --detail forms' and match each field's page and rect to the label beside it before filling; give a dropped input type=\"text\" to keep it as a field, and place the picture of an image input with add_stamp_image.",
        };
    }

    private static Document CreateFromText(string path, bool markdown, PdfCreateRequest request)
    {
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw CliErrors.FileNotFound(fullPath);
        }

        (double width, double height) = PageSizePresets.Dimensions(request.PageSize);
        ValidateMargins(request.Margins, width, height);
        var pageInfo = new PageInfo { Width = width, Height = height, Margin = Margin(request.Margins) };
        return markdown
            ? CreateFromMarkdown(fullPath, pageInfo)
            : CreateFromPlainText(fullPath, pageInfo);
    }

    /// <summary>The importer reads the whole Markdown while it constructs the document.</summary>
    private static Document CreateFromMarkdown(string path, PageInfo pageInfo)
    {
        using FileStream stream = InputFiles.OpenRead(path);
        return new Document(stream, new MdLoadOptions { PageInfo = pageInfo });
    }

    /// <summary>
    /// Lays plain text out line by line on pages of the requested size and margins. The
    /// SDK's text loader has no page settings and fixes its own page and origin, so
    /// resizing its pages afterwards leaves the text where the loader put it.
    /// </summary>
    private static Document CreateFromPlainText(string path, PageInfo pageInfo)
    {
        var document = new Document();
        try
        {
            Page page = document.Pages.Add();
            page.SetPageSize(pageInfo.Width, pageInfo.Height);
            page.PageInfo.Margin = pageInfo.Margin;
            using var reader = new StreamReader(InputFiles.OpenRead(path));
            while (reader.ReadLine() is { } line)
            {
                page.Paragraphs.Add(new TextFragment(ExpandTabs(line)));
            }

            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    /// <summary>Replaces tabs with spaces up to the next eight-column stop, as a terminal shows them.</summary>
    private static string ExpandTabs(string line)
    {
        if (!line.Contains('\t', StringComparison.Ordinal))
        {
            return line;
        }

        var expanded = new StringBuilder(line.Length + 8);
        foreach (char character in line)
        {
            if (character == '\t')
            {
                expanded.Append(' ', 8 - (expanded.Length % 8));
            }
            else
            {
                expanded.Append(character);
            }
        }

        return expanded.ToString();
    }

    private static MarginInfo Margin(PdfMargins margins) => new()
    {
        Top = margins.Top,
        Right = margins.Right,
        Bottom = margins.Bottom,
        Left = margins.Left,
    };

    /// <summary>Refuses margins that leave no content area on a page, named by <paramref name="page"/> when given.</summary>
    private static void ValidateMargins(PdfMargins margins, double width, double height, string? page = null)
    {
        if (margins.Top < 0 || margins.Right < 0 || margins.Bottom < 0 || margins.Left < 0
            || margins.Left + margins.Right >= width
            || margins.Top + margins.Bottom >= height)
        {
            throw CliErrors.OptionInvalid(
                PdfOptionNames.Margins,
                page is null
                    ? "values must be non-negative and leave a positive content area"
                    : $"values must be non-negative and leave a positive content area on {page}",
                "Use smaller top,right,bottom,left values in points.");
        }
    }

    /// <summary>
    /// The source files, each with the option that named it and the id of the format it was
    /// read as: an image by its content (<see cref="ImageInput.FormatOf"/>), whatever its name,
    /// the others by the option that named them.
    /// </summary>
    private static IReadOnlyList<SourceInput> CreationInputs(PdfCreateSource source, IReadOnlyList<byte[]> imageContents) => source switch
    {
        PdfImageSource images => FileObjects.Sources([.. images.Files.Zip(imageContents, static (file, content) =>
            ((SourceFile?)file, (ImageInput.FormatOf(content) ?? throw new UnreachableException("A checked image has a format.")).Id, false))]),
        PdfHtmlSource html => FileObjects.Sources((html.File, "html", false)),
        PdfMarkdownSource markdown => FileObjects.Sources((markdown.File, "md", false)),
        PdfTextSource text => FileObjects.Sources((text.File, "txt", false)),
        _ => throw new UnreachableException(),
    };

    /// <summary>The files of a source.</summary>
    private static IReadOnlyList<string> Paths(PdfCreateSource source) => source switch
    {
        PdfImageSource images => [.. images.Files.Select(static file => file.Path)],
        PdfHtmlSource html => [html.File.Path],
        PdfMarkdownSource markdown => [markdown.File.Path],
        PdfTextSource text => [text.File.Path],
        _ => throw new UnreachableException(),
    };

    /// <summary>Checks every creation input and returns the HTML input's bytes, if any, and the images' bytes.</summary>
    private static (byte[]? Html, IReadOnlyList<byte[]> Images) EnsureCreationInputs(
        ResourceBudgetLedger resourceBudgets,
        PdfCreateSource source)
    {
        IReadOnlyList<string> paths = Paths(source);
        if (paths.Count > 1000)
        {
            throw CliErrors.OptionInvalid(
                PdfOptionNames.FromImages,
                "more than 1000 inputs were requested",
                "Create smaller PDFs and merge them in bounded batches.");
        }

        foreach (string path in paths)
        {
            InputSizeGuard.Ensure(resourceBudgets, path);
        }

        // The engine decodes each image only when the document is saved, and a damaged one fails
        // there or is embedded as it is; refuse it first.
        var images = new List<byte[]>();
        foreach (string image in source is PdfImageSource imageSource ? imageSource.Files.Select(static file => file.Path) : [])
        {
            using InputResourceScope scope = resourceBudgets.Inputs.CreateScope();
            images.Add(ImageInput.Read(scope, image, refuseNetworkReferences: true));
        }

        if (source is not PdfHtmlSource { File.Path: var html, AllowNetworkResources: var allowNetwork })
        {
            return (null, images);
        }

        // The HTML importer reaches the network before any resource policy applies; refuse
        // first unless the caller allowed it. Markdown is checked with its local references.
        byte[] bytes = resourceBudgets.Inputs.ReadAllBytes(html);
        if (!allowNetwork)
        {
            NetworkReferenceGuard.EnsureNone(bytes, "HTML input", html, optIn: PdfOptionNames.AllowNetworkResources);
        }

        return (bytes, images);
    }
}
