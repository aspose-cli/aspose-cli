using System.Text;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Pdf;
using Aspose.Pdf.Text;
using static Aspose.Cli.Product.Pdf.Engine.PdfEngineSupport;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Creates one PDF from images, HTML, text or Markdown: <c>pdf create</c>.</summary>
internal static class PdfCreate
{
    internal static PdfWriteResult Run(PdfSession session, NewPdfRequest request)
    {
        int sources = request.ImagePaths is { Count: > 0 } ? 1 : 0;
        sources += request.HtmlPath is null ? 0 : 1;
        sources += request.TextPath is null ? 0 : 1;
        if (sources != 1)
        {
            throw CliErrors.Usage(["Choose exactly one image list, HTML file, text file or Markdown file."]);
        }

        if (request.AllowNetworkResources && request.HtmlPath is null)
        {
            throw CliErrors.OptionInvalid(
                "--allow-network-resources",
                "it applies only to --from-html",
                "The Markdown importer follows local file references inside fetched resources without any resource hook, so network resources stay refused for Markdown; convert the Markdown to HTML first.");
        }

        byte[]? html = EnsureCreationInputs(session.Budgets, request);
        // The Markdown importer resolves the Markdown's relative references against the
        // working directory and reads files itself; the check holds them until the save.
        bool fromMarkdown = request.TextPath is { } textPath
            && string.Equals(Path.GetExtension(textPath), ".md", StringComparison.OrdinalIgnoreCase);
        using MarkdownImportResources? markdown = fromMarkdown && request.TextPath is { } markdownPath
            ? new MarkdownImportResources(markdownPath, session.Budgets, Environment.CurrentDirectory) : null;
        LicenseState state = session.Outputs.License;
        using HtmlImportResources? resources = request.HtmlPath is { } htmlPath
            ? new HtmlImportResources(htmlPath, session.Budgets, request.AllowNetworkResources) : null;
        using Document document = request.ImagePaths is { Count: > 0 } images
            ? CreateFromImages(images, request, session.Budgets)
            : request.HtmlPath is not null
                ? CreateFromHtml(request.HtmlPath, request, resources!)
                : CreateFromText(request.TextPath!, fromMarkdown, request);
        resources?.ThrowIfFailed();
        if (request.HtmlPath is not null || fromMarkdown)
        {
            // Both importers set the title, author and subject to a placeholder
            // (PDF-IMPORT-INFO-PLACEHOLDER); keep only the title the HTML states.
            document.Info.Title = html is null ? string.Empty : HtmlDocumentTitle.Read(html) ?? string.Empty;
            document.Info.Author = string.Empty;
            document.Info.Subject = string.Empty;
        }

        long size = session.Outputs.Write(request.Output.Path, request.Output.Overwrite, document, path =>
        {
            try { document.Save(path); }
            finally { resources?.ThrowIfFailed(); }
        });
        var warnings = new List<Warning>();
        warnings.AddRange(resources?.Warnings ?? []);
        if (html is not null && FormLosses(document, html) is { } losses)
        {
            warnings.Add(losses);
        }

        return new PdfWriteResult
        {
            Action = "create",
            Output = BuildOutput(request.Output.Path, "pdf", size),
            Inputs = CreationInputs(request),
            License = EnvelopeParts.License(state),
            Warnings = warnings.Count == 0 ? null : warnings,
        };
    }

    /// <summary>
    /// One page per image. The page takes the named size in the image's orientation (landscape
    /// for an image wider than it is tall), and the image keeps its aspect ratio: it is scaled to
    /// fill the margin box in one dimension and centred in the other. The margins must leave a
    /// content area on each page in the orientation it takes.
    /// </summary>
    private static Document CreateFromImages(
        IReadOnlyList<string> paths, NewPdfRequest request, ResourceBudgetLedger resourceBudgets)
    {
        var document = new Document();
        try
        {
            (double portraitWidth, double portraitHeight) = PdfPageSizes.Dimensions(request.PageSize);
            PdfMargins margins = request.Margins;
            foreach (string path in paths)
            {
                if (!File.Exists(path))
                {
                    throw CliErrors.FileNotFound(path);
                }

                string fullPath = Path.GetFullPath(path);
                (double imageWidth, double imageHeight) = PdfImageSize.Read(resourceBudgets.Inputs, fullPath);
                bool landscape = imageWidth > imageHeight;
                (double width, double height) = landscape
                    ? (portraitHeight, portraitWidth)
                    : (portraitWidth, portraitHeight);
                ValidateMargins(margins, width, height, $"the {(landscape ? "landscape" : "portrait")} page of {Path.GetFileName(path)}");
                double boxWidth = width - margins.Left - margins.Right;
                double boxHeight = height - margins.Top - margins.Bottom;
                double scale = Math.Min(boxWidth / imageWidth, boxHeight / imageHeight);
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
                    File = fullPath,
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

    private static Document CreateFromHtml(
        string path, NewPdfRequest request, HtmlImportResources resources)
    {
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw CliErrors.FileNotFound(fullPath);
        }

        (double width, double height) = PdfPageSizes.Dimensions(request.PageSize);
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

    /// <summary>The input types the HTML importer drops without a field (PDF-HTML-FORM-INPUTS).</summary>
    private static readonly Regex DroppedInput = new(
        """<input\b[^>]*?\stype\s*=\s*["']?(email|tel|url|time|datetime-local|month|week|color|range|file)\b""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>An HTML comment, whose inputs the importer does not see.</summary>
    private static readonly Regex HtmlComment = new("<!--.*?-->", RegexOptions.Singleline, TimeSpan.FromSeconds(1));

    /// <summary>
    /// The HTML importer keeps the name of a single-line text input only; it names every other
    /// control itself and drops its HTML value (PDF-HTML-FORM-NAMES), and it drops some input
    /// types entirely (PDF-HTML-FORM-INPUTS), which the HTML shows. Both are disclosed, so the
    /// fields are matched to their labels by position before filling.
    /// </summary>
    private static Warning? FormLosses(Document document, byte[] html)
    {
        string[] names = document.Form.Fields
            .Where(static field => field is not Aspose.Pdf.Forms.TextBoxField { Multiline: false })
            .Select(static field => $"'{field.FullName}'")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] dropped = DroppedInput.Matches(HtmlComment.Replace(Encoding.UTF8.GetString(html), string.Empty))
            .Select(static match => match.Groups[1].Value.ToLowerInvariant())
            .ToArray();
        var losses = new List<string>(2);
        if (names.Length > 0)
        {
            losses.Add($"these form fields have generated names and lost their HTML values: {string.Join(", ", names)}");
        }

        if (dropped.Length > 0)
        {
            losses.Add($"it dropped {dropped.Length} input(s) of type {string.Join(", ", dropped.Distinct(StringComparer.Ordinal))}, which have no field");
        }

        return losses.Count == 0 ? null : new Warning
        {
            Code = WarningCodes.LossyConversion,
            Message = $"The HTML importer keeps the name and value of single-line text inputs only: {string.Join("; ", losses)}.",
            Hint = "Read the fields with 'pdf query forms' and match each field's page and rect to the label beside it before filling; give a dropped input type=\"text\" to keep it as a field.",
        };
    }

    private static Document CreateFromText(string path, bool markdown, NewPdfRequest request)
    {
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw CliErrors.FileNotFound(fullPath);
        }

        (double width, double height) = PdfPageSizes.Dimensions(request.PageSize);
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
                "--margins",
                page is null
                    ? "values must be non-negative and leave a positive content area"
                    : $"values must be non-negative and leave a positive content area on {page}",
                "Use smaller top,right,bottom,left values in points.");
        }
    }

    private static IReadOnlyList<SourceInfo> CreationInputs(NewPdfRequest request)
    {
        IEnumerable<string> paths = request.ImagePaths
            ?? (request.HtmlPath is not null ? [request.HtmlPath] : [request.TextPath!]);
        return paths.Select(path => new SourceInfo
        {
            Path = Path.GetFullPath(path),
            Format = Path.GetExtension(path).TrimStart('.').ToLowerInvariant(),
            SizeBytes = new FileInfo(path).Length,
        }).ToArray();
    }

    /// <summary>Checks every creation input and returns the HTML input's bytes, if any.</summary>
    private static byte[]? EnsureCreationInputs(
        ResourceBudgetLedger resourceBudgets,
        NewPdfRequest request)
    {
        IReadOnlyList<string> paths = request.ImagePaths
            ?? (request.HtmlPath is not null ? [request.HtmlPath] : [request.TextPath!]);
        if (paths.Count > 1000)
        {
            throw CliErrors.OptionInvalid(
                "--from-images",
                "more than 1000 inputs were requested",
                "Create smaller PDFs and merge them in bounded batches.");
        }

        foreach (string path in paths)
        {
            InputSizeGuard.Ensure(resourceBudgets, path);
        }

        foreach (string image in request.ImagePaths ?? [])
        {
            using Stream content = resourceBudgets.Inputs.OpenFile(image);
            NetworkReferenceGuard.EnsureNoneInImage(content, image);
        }

        if (request.HtmlPath is not { } html)
        {
            return null;
        }

        // The HTML importer reaches the network before any resource policy applies; refuse
        // first unless the caller allowed it. Markdown is checked with its local references.
        byte[] bytes = resourceBudgets.Inputs.ReadAllBytes(html);
        if (!request.AllowNetworkResources)
        {
            NetworkReferenceGuard.EnsureNone(bytes, "HTML input", html, optIn: "--allow-network-resources");
        }

        return bytes;
    }
}
