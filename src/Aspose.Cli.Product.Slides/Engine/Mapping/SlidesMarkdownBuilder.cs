using System.Drawing;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Slides;

namespace Aspose.Cli.Product.Slides.Engine.Mapping;

/// <summary>
/// Maps a Markdown outline onto the presentation's own layouts. The builder only
/// fills title, subtitle and content placeholders; colors, backgrounds, geometry and
/// fonts come from the master, layouts and theme, so a template fully owns the look.
/// Emphasis becomes bold or italic runs, and code uses a monospace font. A pipe table
/// becomes a slide table in the template's default table style.
/// </summary>
internal static partial class SlidesMarkdownBuilder
{
    /// <summary>Replaces the presentation's slides with the outline; returns the fit warnings.</summary>
    public static IReadOnlyList<Warning> Build(
        ResourceBudgetLedger resourceBudgets,
        Presentation presentation,
        string markdownPath)
    {
        InputSizeGuard.Ensure(resourceBudgets, markdownPath);
        string markdown = resourceBudgets.Inputs.ReadTextFile(markdownPath);
        IReadOnlyList<MarkdownSlide> model = Parse(markdown, Path.GetFileNameWithoutExtension(markdownPath));
        string root = Path.GetDirectoryName(Path.GetFullPath(markdownPath))!;

        presentation.Sections.Clear();
        while (presentation.Slides.Count > 0)
        {
            presentation.Slides.RemoveAt(0);
        }

        var warnings = new List<Warning>();
        foreach (MarkdownSlide item in model)
        {
            ISlide slide = presentation.Slides.AddEmptySlide(LayoutFor(presentation, item));
            IAutoShape title = SlidesAuthoring.Title(slide);
            title.Name = SlidesAuthoring.TitleName;
            title.TextFrame.Text = item.Title;

            IAutoShape[] content = SlidesPlaceholders.Content(slide, includeSubtitle: item.TitleSlide);
            if (item.Blocks.Count > 0)
            {
                IAutoShape body = SlidesAuthoring.Body(slide, includeSubtitle: item.TitleSlide);
                body.Name = SlidesAuthoring.BodyName;
                SlidesAuthoring.WriteParagraphs(body.TextFrame, item.Blocks);
                body.TextFrame.TextFrameFormat.AutofitType = TextAutofitType.Normal;
            }

            if (item.Image is not null)
            {
                AddImage(resourceBudgets, presentation, slide, root, item.Image, content);
            }

            if (item.Table is not null
                && AddTable(slide, presentation.Slides.Count, item.Table, content, besideText: item.Blocks.Count > 0) is { } overflow)
            {
                warnings.Add(overflow);
            }

            RemoveEmptyPlaceholders(slide);
            if (item.Section is not null)
            {
                presentation.Sections.AddSection(item.Section, slide);
            }
        }

        return warnings;
    }

    internal static IReadOnlyList<MarkdownSlide> Parse(string markdown, string fallbackTitle)
    {
        string[] lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var slides = new List<MarkdownSlide>();
        MarkdownSlide? current = null;
        string? pendingSection = null;
        bool inCode = false;

        for (int index = 0; index < lines.Length; index++)
        {
            string raw = lines[index];
            string line = raw.TrimEnd();
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                inCode = !inCode;
                continue;
            }

            if (inCode)
            {
                current ??= AddFallback(slides, fallbackTitle, ref pendingSection);
                current.Blocks.Add(new AuthoredParagraph([new AuthoredRun(raw, Code: true)], 0, ParagraphList.None));
                continue;
            }

            if (line == "---")
            {
                pendingSection = $"Section {slides.Count + 1}";
                current = null;
                continue;
            }

            if (HeadingPattern().Match(line) is { Success: true } heading)
            {
                int depth = heading.Groups["marks"].Length;
                string text = heading.Groups["text"].Value;
                if (depth <= 2)
                {
                    current = new MarkdownSlide(PlainText(text), TitleSlide: depth == 1) { Section = pendingSection };
                    pendingSection = null;
                    slides.Add(current);
                }
                else
                {
                    // Deeper headings are sub-heads inside the current slide's body.
                    current ??= AddFallback(slides, fallbackTitle, ref pendingSection);
                    current.Blocks.Add(new AuthoredParagraph(
                        Inline(text).Select(static run => run with { Bold = true }).ToArray(),
                        0,
                        ParagraphList.None));
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            current ??= AddFallback(slides, fallbackTitle, ref pendingSection);
            string trimmed = line.Trim();
            if (ReadTable(lines, ref index, current.Title) is { } table)
            {
                // A slide holds one picture or table; another table continues on a new slide.
                if (current.HasObject)
                {
                    current = Continue(slides, current);
                }

                current.Table = table;
            }
            else if (ImagePattern().Match(trimmed) is { Success: true } image)
            {
                if (current.Table is not null)
                {
                    current = Continue(slides, current);
                }

                current.Image ??= new MarkdownImage(
                    image.Groups["path"].Value,
                    EmptyToNull(image.Groups["alt"].Value),
                    EmptyToNull(image.Groups["title"].Value));
            }
            else if (trimmed.StartsWith('>'))
            {
                current.Blocks.Add(new AuthoredParagraph(
                    [new AuthoredRun("“"), .. Inline(trimmed[1..].Trim()), new AuthoredRun("”")],
                    0,
                    ParagraphList.None));
            }
            else if (ListPattern().Match(line) is { Success: true } item)
            {
                int spaces = item.Groups["indent"].Value.Replace("\t", "  ", StringComparison.Ordinal).Length;
                current.Blocks.Add(new AuthoredParagraph(
                    Inline(item.Groups["text"].Value),
                    Math.Min(spaces / 2, 8),
                    item.Groups["number"].Success ? ParagraphList.Numbered : ParagraphList.Inherit));
            }
            else
            {
                current.Blocks.Add(new AuthoredParagraph(Inline(trimmed), 0, ParagraphList.None));
            }
        }

        return slides.Count == 0
            ? [new MarkdownSlide(fallbackTitle, TitleSlide: true)]
            : slides;
    }

    /// <summary>Inline Markdown as runs: strong, emphasis and code spans; links keep their text.</summary>
    internal static IReadOnlyList<AuthoredRun> Inline(string text)
    {
        var runs = new List<AuthoredRun>();
        Append(runs, text.Trim(), bold: false, italic: false);
        return runs.Count == 0 ? [new AuthoredRun(string.Empty)] : runs;
    }

    private static void Append(List<AuthoredRun> runs, string text, bool bold, bool italic)
    {
        int offset = 0;
        foreach (Match match in InlinePattern().Matches(text))
        {
            AppendPlain(runs, text[offset..match.Index], bold, italic);
            if (match.Groups["code"].Success)
            {
                runs.Add(new AuthoredRun(match.Groups["code"].Value, bold, italic, Code: true));
            }
            else if (match.Groups["strong"].Success)
            {
                Append(runs, match.Groups["strong"].Value, bold: true, italic);
            }
            else if (match.Groups["em"].Success)
            {
                Append(runs, match.Groups["em"].Value, bold, italic: true);
            }
            else
            {
                Append(runs, match.Groups["link"].Value, bold, italic);
            }

            offset = match.Index + match.Length;
        }

        AppendPlain(runs, text[offset..], bold, italic);
    }

    private static void AppendPlain(List<AuthoredRun> runs, string text, bool bold, bool italic)
    {
        if (text.Length > 0)
        {
            runs.Add(new AuthoredRun(text, bold, italic));
        }
    }

    private static string PlainText(string text) => string.Concat(Inline(text).Select(static run => run.Text));

    private static MarkdownSlide AddFallback(List<MarkdownSlide> slides, string title, ref string? section)
    {
        var value = new MarkdownSlide(title, TitleSlide: false) { Section = section };
        section = null;
        slides.Add(value);
        return value;
    }

    private static MarkdownSlide Continue(List<MarkdownSlide> slides, MarkdownSlide slide)
    {
        var value = new MarkdownSlide(slide.Title, TitleSlide: false);
        slides.Add(value);
        return value;
    }

    private static ILayoutSlide LayoutFor(Presentation presentation, MarkdownSlide item)
    {
        SlideLayoutType type = item switch
        {
            { TitleSlide: true } => SlideLayoutType.Title,
            { HasObject: true, Blocks.Count: > 0 } => SlideLayoutType.TwoObjects,
            { HasObject: false, Blocks.Count: 0 } => SlideLayoutType.TitleOnly,
            _ => SlideLayoutType.TitleAndObject,
        };
        return SlidesPlaceholders.Layout(presentation, type);
    }

    private static void AddImage(
        ResourceBudgetLedger resourceBudgets,
        Presentation presentation,
        ISlide slide,
        string root,
        MarkdownImage source,
        IAutoShape[] content)
    {
        string imagePath = ResolveLocalImage(root, source.Path);
        InputSizeGuard.Ensure(resourceBudgets, imagePath);
        IPPImage image = presentation.Images.AddImage(resourceBudgets.Inputs.ReadAllBytes(imagePath));

        // The image takes the frame of the first content placeholder that holds no text.
        IAutoShape? frame = content.FirstOrDefault(static shape => string.IsNullOrEmpty(shape.TextFrame?.Text));
        RectangleF box = SlidesAuthoring.Fit(
            image,
            frame is null
                ? SlidesAuthoring.Canvas(slide, new RectangleF(0.55f, 0.25f, 0.38f, 0.62f))
                : new RectangleF(frame.X, frame.Y, frame.Width, frame.Height));
        IPictureFrame picture = slide.Shapes.AddPictureFrame(ShapeType.Rectangle, box.X, box.Y, box.Width, box.Height, image);
        picture.Name = "Image";
        picture.PictureFrameLock.AspectRatioLocked = true;
        if (source.Alt is not null)
        {
            picture.AlternativeText = source.Alt;
        }

        if (source.Title is not null)
        {
            picture.AlternativeTextTitle = source.Title;
        }

        if (frame is not null)
        {
            slide.Shapes.Remove(frame);
        }
    }

    private static void RemoveEmptyPlaceholders(ISlide slide)
    {
        foreach (IShape shape in slide.Shapes
            .Where(static shape => shape.Placeholder is not null
                && shape is not IAutoShape { TextFrame.Text.Length: > 0 }
                && shape is not IPictureFrame)
            .ToArray())
        {
            slide.Shapes.Remove(shape);
        }
    }

    private static string ResolveLocalImage(string root, string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            && !uri.IsFile)
        {
            throw new CliException(
                ErrorCodes.FeatureUnsupported,
                $"Remote Markdown image is blocked: {value}.",
                hint: "Download the image beside the Markdown file and reference it with a relative path.");
        }

        string path = Path.GetFullPath(Path.Combine(root, value));
        if (!File.Exists(path))
        {
            throw CliErrors.FileNotFound(path);
        }

        return path;
    }

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex(@"^(?<marks>#{1,6})\s+(?<text>.+?)\s*#*$", RegexOptions.CultureInvariant)]
    private static partial Regex HeadingPattern();

    // ![alt](path), ![alt](<path with spaces>) and ![alt](path "title").
    [GeneratedRegex(@"^!\[(?<alt>[^\]]*)\]\(\s*(?:<(?<path>[^>]+)>|(?<path>[^\s)]+))(?:\s+""(?<title>[^""]*)"")?\s*\)$", RegexOptions.CultureInvariant)]
    private static partial Regex ImagePattern();

    // Bullets (-, *, +) and ordered items (1. or 1)).
    [GeneratedRegex(@"^(?<indent>\s*)(?:[-*+]|(?<number>\d{1,9})[.)])\s+(?<text>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex ListPattern();

    // Code spans first, then strong, emphasis and links. Underscore emphasis needs
    // word boundaries so snake_case identifiers stay intact.
    [GeneratedRegex(
        @"`(?<code>[^`]+)`"
        + @"|\*\*(?<strong>.+?)\*\*|__(?<strong>.+?)__"
        + @"|\*(?<em>[^*\s](?:[^*]*[^*\s])?)\*|(?<![\p{L}\p{N}_])_(?<em>[^_\s](?:[^_]*[^_\s])?)_(?![\p{L}\p{N}_])"
        + @"|\[(?<link>[^\]]+)\]\([^)]*\)",
        RegexOptions.CultureInvariant)]
    private static partial Regex InlinePattern();
}

/// <summary>A local picture referenced by the outline.</summary>
internal sealed record MarkdownImage(string Path, string? Alt, string? Title);

internal sealed record MarkdownSlide(string Title, bool TitleSlide)
{
    public string? Section { get; set; }
    public MarkdownImage? Image { get; set; }
    public MarkdownTable? Table { get; set; }
    public bool HasObject => Image is not null || Table is not null;
    public List<AuthoredParagraph> Blocks { get; } = [];
}
