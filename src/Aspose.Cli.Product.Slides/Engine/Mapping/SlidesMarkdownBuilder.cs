using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Slides;

namespace Aspose.Cli.Product.Slides.Engine.Mapping;

/// <summary>
/// Maps a Markdown outline onto the presentation's own layouts. The builder
/// only fills title, subtitle and content placeholders; fonts, colors,
/// backgrounds and geometry always come from the master, layouts and theme,
/// so a template fully owns the look of the authored deck.
/// </summary>
internal static partial class SlidesMarkdownBuilder
{

    // A code block is the one semantic that needs a typeface the theme does not name.
    private const string CodeFont = "Consolas";

    public static void Build(
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

        foreach (MarkdownSlide item in model)
        {
            ISlide slide = presentation.Slides.AddEmptySlide(LayoutFor(presentation, item));
            IAutoShape title = SlidesPlaceholders.Title(slide) ?? AddFallbackTitle(slide, presentation);
            title.Name = "Title";
            title.TextFrame.Text = item.Title;

            IAutoShape[] content = SlidesPlaceholders.Content(slide, includeSubtitle: item.TitleSlide);
            if (item.Blocks.Count > 0)
            {
                IAutoShape body = content.FirstOrDefault() ?? AddFallbackBody(slide, presentation);
                body.Name = "Body";
                WriteBlocks(body.TextFrame, item.Blocks);
                body.TextFrame.TextFrameFormat.AutofitType = TextAutofitType.Normal;
            }

            if (item.ImagePath is not null)
            {
                AddImage(resourceBudgets, presentation, slide, root, item, content);
            }

            RemoveEmptyPlaceholders(slide);
            if (item.Section is not null)
            {
                presentation.Sections.AddSection(item.Section, slide);
            }
        }
    }

    internal static IReadOnlyList<MarkdownSlide> Parse(string markdown, string fallbackTitle)
    {
        string[] lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var slides = new List<MarkdownSlide>();
        MarkdownSlide? current = null;
        string? pendingSection = null;
        bool inCode = false;

        foreach (string raw in lines)
        {
            string line = raw.TrimEnd();
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                inCode = !inCode;
                continue;
            }

            if (inCode)
            {
                current ??= AddFallback(slides, fallbackTitle, ref pendingSection);
                current.Blocks.Add(new MarkdownBlock(MarkdownBlockKind.Code, raw, 0));
                continue;
            }

            if (line == "---")
            {
                pendingSection = $"Section {slides.Count + 1}";
                current = null;
                continue;
            }

            if (line.StartsWith("# ", StringComparison.Ordinal)
                || line.StartsWith("## ", StringComparison.Ordinal))
            {
                bool titleSlide = line[1] == ' ';
                current = new MarkdownSlide(
                    CleanInlineMarkdown(line[(titleSlide ? 2 : 3)..]),
                    titleSlide)
                {
                    Section = pendingSection,
                };
                pendingSection = null;
                slides.Add(current);
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            current ??= AddFallback(slides, fallbackTitle, ref pendingSection);
            string trimmed = line.Trim();
            if (ImagePattern().Match(trimmed) is { Success: true } image)
            {
                current.ImagePath ??= image.Groups["path"].Value.Trim();
            }
            else if (trimmed.StartsWith('>'))
            {
                current.Blocks.Add(new MarkdownBlock(
                    MarkdownBlockKind.Quote,
                    $"“{CleanInlineMarkdown(trimmed[1..])}”",
                    0));
            }
            else if (BulletPattern().Match(line) is { Success: true } bullet)
            {
                int spaces = bullet.Groups["indent"].Value.Replace("\t", "  ", StringComparison.Ordinal).Length;
                current.Blocks.Add(new MarkdownBlock(
                    MarkdownBlockKind.Bullet,
                    CleanInlineMarkdown(bullet.Groups["text"].Value),
                    Math.Min(spaces / 2, 8)));
            }
            else
            {
                current.Blocks.Add(new MarkdownBlock(MarkdownBlockKind.Paragraph, CleanInlineMarkdown(line), 0));
            }
        }

        return slides.Count == 0
            ? [new MarkdownSlide(fallbackTitle, TitleSlide: true)]
            : slides;
    }

    private static MarkdownSlide AddFallback(List<MarkdownSlide> slides, string title, ref string? section)
    {
        var value = new MarkdownSlide(title, TitleSlide: false) { Section = section };
        section = null;
        slides.Add(value);
        return value;
    }

    private static ILayoutSlide LayoutFor(Presentation presentation, MarkdownSlide item)
    {
        SlideLayoutType type = item switch
        {
            { TitleSlide: true } => SlideLayoutType.Title,
            { ImagePath: not null, Blocks.Count: > 0 } => SlideLayoutType.TwoObjects,
            { ImagePath: null, Blocks.Count: 0 } => SlideLayoutType.TitleOnly,
            _ => SlideLayoutType.TitleAndObject,
        };
        return presentation.LayoutSlides.GetByType(type)
            ?? presentation.LayoutSlides.GetByType(SlideLayoutType.TitleAndObject)
            ?? presentation.LayoutSlides.FirstOrDefault(static layout =>
                layout.Shapes.Any(static shape => SlidesPlaceholders.IsTitle(shape.Placeholder)))
            ?? presentation.LayoutSlides[0];
    }

    private static void WriteBlocks(ITextFrame frame, IReadOnlyList<MarkdownBlock> blocks)
    {
        frame.Paragraphs.Clear();
        foreach (MarkdownBlock block in blocks)
        {
            var paragraph = new Paragraph();
            paragraph.Portions.Add(new Portion(block.Text));
            paragraph.ParagraphFormat.Depth = (short)block.Level;
            if (block.Kind != MarkdownBlockKind.Bullet)
            {
                paragraph.ParagraphFormat.Bullet.Type = BulletType.None;
            }

            if (block.Kind == MarkdownBlockKind.Code)
            {
                paragraph.Portions[0].PortionFormat.LatinFont = new FontData(CodeFont);
            }

            frame.Paragraphs.Add(paragraph);
        }
    }

    private static void AddImage(
        ResourceBudgetLedger resourceBudgets,
        Presentation presentation,
        ISlide slide,
        string root,
        MarkdownSlide item,
        IAutoShape[] content)
    {
        string imagePath = ResolveLocalImage(root, item.ImagePath!);
        InputSizeGuard.Ensure(resourceBudgets, imagePath);
        IPPImage image = presentation.Images.AddImage(resourceBudgets.Inputs.ReadAllBytes(imagePath));

        // The image takes the frame of the first content placeholder that holds no text.
        IAutoShape? frame = content.FirstOrDefault(static shape => string.IsNullOrEmpty(shape.TextFrame?.Text));
        float width = presentation.SlideSize.Size.Width;
        float height = presentation.SlideSize.Size.Height;
        (float x, float y, float w, float h) = Fit(
            image,
            frame is null
                ? (width * 0.55f, height * 0.25f, width * 0.38f, height * 0.62f)
                : (frame.X, frame.Y, frame.Width, frame.Height));
        IPictureFrame picture = slide.Shapes.AddPictureFrame(ShapeType.Rectangle, x, y, w, h, image);
        picture.Name = "Image";
        picture.PictureFrameLock.AspectRatioLocked = true;
        if (frame is not null)
        {
            slide.Shapes.Remove(frame);
        }
    }

    /// <summary>Largest rectangle with the image's aspect ratio, centered in the box.</summary>
    private static (float X, float Y, float Width, float Height) Fit(
        IPPImage image,
        (float X, float Y, float Width, float Height) box)
    {
        if (image.Width <= 0 || image.Height <= 0)
        {
            return box;
        }

        float scale = Math.Min(box.Width / image.Width, box.Height / image.Height);
        float width = image.Width * scale;
        float height = image.Height * scale;
        return (box.X + ((box.Width - width) / 2), box.Y + ((box.Height - height) / 2), width, height);
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

    // Templates without a title or content placeholder still get readable, unstyled text boxes.
    private static IAutoShape AddFallbackTitle(ISlide slide, Presentation presentation)
    {
        float width = presentation.SlideSize.Size.Width;
        float height = presentation.SlideSize.Size.Height;
        IAutoShape shape = slide.Shapes.AddAutoShape(ShapeType.Rectangle, width * 0.07f, height * 0.06f, width * 0.86f, height * 0.16f);
        shape.FillFormat.FillType = FillType.NoFill;
        shape.LineFormat.FillFormat.FillType = FillType.NoFill;
        return shape;
    }

    private static IAutoShape AddFallbackBody(ISlide slide, Presentation presentation)
    {
        float width = presentation.SlideSize.Size.Width;
        float height = presentation.SlideSize.Size.Height;
        IAutoShape shape = slide.Shapes.AddAutoShape(ShapeType.Rectangle, width * 0.07f, height * 0.25f, width * 0.86f, height * 0.65f);
        shape.FillFormat.FillType = FillType.NoFill;
        shape.LineFormat.FillFormat.FillType = FillType.NoFill;
        shape.TextFrame.TextFrameFormat.AnchoringType = TextAnchorType.Top;
        return shape;
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

    private static string CleanInlineMarkdown(string value)
    {
        string cleaned = MarkdownLinkPattern().Replace(value.Trim(), "${text}");
        cleaned = StrongEmphasisPattern().Replace(cleaned, "${text}");
        cleaned = InlineCodePattern().Replace(cleaned, "${text}");
        return cleaned;
    }

    [GeneratedRegex(@"^\s*!\[[^\]]*\]\((?<path>[^)]+)\)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex ImagePattern();

    [GeneratedRegex(@"^(?<indent>\s*)[-*]\s+(?<text>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex BulletPattern();

    [GeneratedRegex(@"\[(?<text>[^\]]+)\]\([^)]+\)", RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownLinkPattern();

    [GeneratedRegex(@"(?:\*\*|__)(?<text>.+?)(?:\*\*|__)", RegexOptions.CultureInvariant)]
    private static partial Regex StrongEmphasisPattern();

    [GeneratedRegex(@"`(?<text>[^`]+)`", RegexOptions.CultureInvariant)]
    private static partial Regex InlineCodePattern();
}

internal enum MarkdownBlockKind
{
    Paragraph,
    Bullet,
    Quote,
    Code,
}

/// <summary>One body paragraph in source order.</summary>
internal sealed record MarkdownBlock(MarkdownBlockKind Kind, string Text, int Level);

internal sealed record MarkdownSlide(string Title, bool TitleSlide)
{
    public string? Section { get; set; }
    public string? ImagePath { get; set; }
    public List<MarkdownBlock> Blocks { get; } = [];
}
