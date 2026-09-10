using System.Drawing;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Slides;

namespace Aspose.Cli.Product.Slides.Engine.Mapping;

internal static partial class SlidesMarkdownBuilder
{
    private const int MaxMarkdownBytes = 8 * 1024 * 1024;

    public static void Build(
        ResourceBudgetLedger resourceBudgets,
        Presentation presentation,
        string markdownPath)
    {
        InputSizeGuard.Ensure(
            resourceBudgets,
            markdownPath,
            MaxMarkdownBytes);
        string markdown = resourceBudgets.Inputs.ReadTextFile(markdownPath);
        IReadOnlyList<MarkdownSlide> model = Parse(markdown, Path.GetFileNameWithoutExtension(markdownPath));

        presentation.Sections.Clear();
        while (presentation.Slides.Count > 0)
        {
            presentation.Slides.RemoveAt(0);
        }
        ILayoutSlide authoringLayout = presentation.LayoutSlides
            .FirstOrDefault(static layout => layout.Shapes.Count == 0)
            ?? presentation.LayoutSlides[0];
        foreach (MarkdownSlide item in model)
        {
            ISlide slide = presentation.Slides.AddEmptySlide(authoringLayout);
            foreach (IShape placeholder in slide.Shapes
                .Where(static shape => shape.Placeholder is not null)
                .ToArray())
            {
                slide.Shapes.Remove(placeholder);
            }
            ApplyBackground(slide);
            AddTitle(slide, presentation, item);
            AddBody(slide, presentation, item);
            if (item.Section is not null)
            {
                presentation.Sections.AddSection(item.Section, slide);
            }
        }

        string root = Path.GetDirectoryName(Path.GetFullPath(markdownPath))!;
        foreach ((int slideNumber, MarkdownSlide item) in model.Select((item, index) => (index + 1, item)))
        {
            if (item.ImagePath is null)
            {
                continue;
            }

            string imagePath = ResolveLocalImage(root, item.ImagePath);
            InputSizeGuard.Ensure(
                resourceBudgets,
                imagePath,
                InputSizeGuard.ResolveMaxBytes(
                    Environment.GetEnvironmentVariable));
            byte[] bytes = resourceBudgets.Inputs.ReadAllBytes(imagePath);
            IPPImage image = presentation.Images.AddImage(bytes);
            ISlide slide = presentation.Slides[slideNumber - 1];
            float slideWidth = presentation.SlideSize.Size.Width;
            float slideHeight = presentation.SlideSize.Size.Height;
            slide.Shapes.AddPictureFrame(
                ShapeType.Rectangle,
                slideWidth * 0.56f,
                slideHeight * 0.23f,
                slideWidth * 0.38f,
                slideHeight * 0.62f,
                image);
        }
    }

    internal static IReadOnlyList<MarkdownSlide> Parse(string markdown, string fallbackTitle)
    {
        string[] lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var slides = new List<MarkdownSlide>();
        MarkdownSlide? current = null;
        string? pendingSection = null;
        bool inCode = false;
        var code = new List<string>();

        void FlushCode()
        {
            if (current is not null && code.Count > 0)
            {
                current.CodeBlocks.Add(string.Join("\n", code));
            }

            code.Clear();
        }

        foreach (string raw in lines)
        {
            string line = raw.TrimEnd();
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                if (inCode)
                {
                    FlushCode();
                }

                inCode = !inCode;
                continue;
            }

            if (inCode)
            {
                code.Add(raw);
                continue;
            }

            if (line == "---")
            {
                pendingSection = $"Section {slides.Count + 1}";
                current = null;
                continue;
            }

            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                current = new MarkdownSlide(CleanInlineMarkdown(line[2..]), TitleSlide: true) { Section = pendingSection };
                pendingSection = null;
                slides.Add(current);
                continue;
            }

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                current = new MarkdownSlide(CleanInlineMarkdown(line[3..]), TitleSlide: false) { Section = pendingSection };
                pendingSection = null;
                slides.Add(current);
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            current ??= AddFallback(slides, fallbackTitle, pendingSection);
            pendingSection = null;
            Match image = ImagePattern().Match(line.Trim());
            if (image.Success)
            {
                current.ImagePath ??= image.Groups["path"].Value.Trim();
            }
            else if (line.TrimStart().StartsWith('>'))
            {
                current.Quotes.Add(CleanInlineMarkdown(line.TrimStart()[1..]));
            }
            else if (BulletPattern().Match(line) is { Success: true } bullet)
            {
                int spaces = bullet.Groups["indent"].Value.Replace("\t", "  ", StringComparison.Ordinal).Length;
                current.Bullets.Add(new MarkdownBullet(
                    CleanInlineMarkdown(bullet.Groups["text"].Value),
                    Math.Min(spaces / 2, 8)));
            }
            else
            {
                current.Paragraphs.Add(CleanInlineMarkdown(line));
            }
        }

        if (inCode)
        {
            FlushCode();
        }

        return slides.Count == 0
            ? [new MarkdownSlide(fallbackTitle, TitleSlide: true)]
            : slides;
    }

    private static MarkdownSlide AddFallback(List<MarkdownSlide> slides, string title, string? section)
    {
        var value = new MarkdownSlide(title, TitleSlide: false) { Section = section };
        slides.Add(value);
        return value;
    }

    private static void AddTitle(ISlide slide, Presentation presentation, MarkdownSlide item)
    {
        bool centered = item.TitleSlide || !item.HasContent;
        float width = presentation.SlideSize.Size.Width;
        float height = presentation.SlideSize.Size.Height;
        IAutoShape shape = slide.Shapes.AddAutoShape(
            ShapeType.Rectangle,
            width * 0.07f,
            centered ? height * 0.30f : height * 0.08f,
            width * 0.86f,
            centered ? height * 0.24f : height * 0.15f);
        shape.Name = "Title";
        shape.FillFormat.FillType = FillType.NoFill;
        shape.LineFormat.FillFormat.FillType = FillType.NoFill;
        shape.TextFrame.Text = item.Title;
        ApplyTextStyle(
            shape,
            centered ? 36 : 28,
            bold: true,
            Color.FromArgb(15, 42, 61),
            "Arial",
            centered ? TextAlignment.Center : TextAlignment.Left);
        shape.TextFrame.TextFrameFormat.AutofitType = TextAutofitType.None;
        shape.TextFrame.TextFrameFormat.AnchoringType = TextAnchorType.Center;

        IAutoShape accent = slide.Shapes.AddAutoShape(
            ShapeType.Rectangle,
            width * (centered ? 0.42f : 0.07f),
            centered ? height * 0.57f : height * 0.19f,
            centered ? width * 0.16f : width * 0.09f,
            height * 0.012f);
        accent.LineFormat.FillFormat.FillType = FillType.NoFill;
        accent.FillFormat.FillType = FillType.Solid;
        accent.FillFormat.SolidFillColor.Color = Color.FromArgb(15, 157, 138);
    }

    private static void AddBody(ISlide slide, Presentation presentation, MarkdownSlide item)
    {
        var content = new List<string>(item.Paragraphs);
        content.AddRange(item.Bullets.Select(static bullet => bullet.Text));
        if (content.Count == 0 && item.Quotes.Count == 0 && item.CodeBlocks.Count == 0)
        {
            return;
        }

        float width = presentation.SlideSize.Size.Width;
        float height = presentation.SlideSize.Size.Height;
        float bodyWidth = item.ImagePath is null ? width * 0.86f : width * 0.44f;
        if (content.Count > 0)
        {
            IAutoShape shape = slide.Shapes.AddAutoShape(
                ShapeType.Rectangle,
                width * 0.07f,
                item.TitleSlide ? height * 0.62f : height * 0.25f,
                bodyWidth,
                item.TitleSlide ? height * 0.20f : height * (item.Quotes.Count + item.CodeBlocks.Count == 0 ? 0.62f : 0.38f));
            shape.Name = "Body";
            shape.FillFormat.FillType = FillType.NoFill;
            shape.LineFormat.FillFormat.FillType = FillType.NoFill;
            shape.TextFrame.Paragraphs.Clear();
            foreach (string paragraphText in item.Paragraphs)
            {
                var paragraph = new Paragraph();
                paragraph.Portions.Add(new Portion(paragraphText));
                shape.TextFrame.Paragraphs.Add(paragraph);
            }

            foreach (MarkdownBullet bullet in item.Bullets)
            {
                var paragraph = new Paragraph();
                paragraph.Portions.Add(new Portion(bullet.Text));
                paragraph.ParagraphFormat.Bullet.Type = BulletType.Symbol;
                paragraph.ParagraphFormat.Depth = (short)bullet.Level;
                shape.TextFrame.Paragraphs.Add(paragraph);
            }

            ApplyTextStyle(
                shape,
                ContentFontSize(item, content),
                bold: false,
                Color.FromArgb(30, 41, 59),
                "Arial",
                TextAlignment.Left);
            shape.TextFrame.TextFrameFormat.AutofitType = TextAutofitType.Normal;
        }

        if (item.Quotes.Count > 0)
        {
            IAutoShape quote = slide.Shapes.AddAutoShape(
                ShapeType.RoundCornerRectangle,
                width * 0.07f,
                height * 0.70f,
                item.CodeBlocks.Count > 0 ? width * 0.44f : bodyWidth,
                height * 0.16f);
            quote.Name = "Callout";
            quote.FillFormat.FillType = FillType.Solid;
            quote.FillFormat.SolidFillColor.Color = Color.FromArgb(224, 242, 254);
            quote.LineFormat.FillFormat.FillType = FillType.NoFill;
            quote.TextFrame.Text = string.Join("\n", item.Quotes.Select(static value => $"\u201c{value}\u201d"));
            ApplyTextStyle(quote, 16, bold: false, Color.FromArgb(3, 105, 161), "Arial", TextAlignment.Center);
            quote.TextFrame.TextFrameFormat.AutofitType = TextAutofitType.Normal;
        }

        if (item.CodeBlocks.Count > 0)
        {
            IAutoShape code = slide.Shapes.AddAutoShape(
                ShapeType.RoundCornerRectangle,
                width * (item.ImagePath is null ? 0.55f : 0.07f),
                height * 0.70f,
                width * (item.ImagePath is null ? 0.38f : 0.44f),
                height * 0.16f);
            code.Name = "Code";
            code.FillFormat.FillType = FillType.Solid;
            code.FillFormat.SolidFillColor.Color = Color.FromArgb(30, 41, 59);
            code.LineFormat.FillFormat.FillType = FillType.NoFill;
            code.TextFrame.Text = string.Join("\n\n", item.CodeBlocks);
            ApplyTextStyle(code, 14, bold: false, Color.FromArgb(226, 232, 240), "Consolas", TextAlignment.Left);
            code.TextFrame.TextFrameFormat.AutofitType = TextAutofitType.Normal;
        }
    }

    private static float ContentFontSize(MarkdownSlide item, IReadOnlyCollection<string> content)
    {
        int characters = content.Sum(static value => value.Length);
        int lines = item.Paragraphs.Count + item.Bullets.Count;
        return (characters, lines) switch
        {
            (> 900, _) or (_, > 12) => 14,
            (> 600, _) or (_, > 9) => 16,
            _ => 18,
        };
    }

    private static void ApplyBackground(ISlide slide)
    {
        slide.Background.Type = BackgroundType.OwnBackground;
        slide.Background.FillFormat.FillType = FillType.Solid;
        slide.Background.FillFormat.SolidFillColor.Color = Color.FromArgb(248, 250, 252);
    }

    private static void ApplyTextStyle(
        IAutoShape shape,
        float size,
        bool bold,
        Color color,
        string font,
        TextAlignment alignment)
    {
        foreach (IParagraph paragraph in shape.TextFrame.Paragraphs)
        {
            paragraph.ParagraphFormat.Alignment = alignment;
            foreach (IPortion portion in paragraph.Portions)
            {
                portion.PortionFormat.FontHeight = size;
                portion.PortionFormat.FontBold = bold ? NullableBool.True : NullableBool.False;
                portion.PortionFormat.LatinFont = new FontData(font);
                portion.PortionFormat.FillFormat.FillType = FillType.Solid;
                portion.PortionFormat.FillFormat.SolidFillColor.Color = color;
            }
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

internal sealed record MarkdownBullet(string Text, int Level);

internal sealed record MarkdownSlide(string Title, bool TitleSlide)
{
    public string? Section { get; set; }
    public string? ImagePath { get; set; }
    public List<string> Paragraphs { get; } = [];
    public List<MarkdownBullet> Bullets { get; } = [];
    public List<string> Quotes { get; } = [];
    public List<string> CodeBlocks { get; } = [];

    public bool HasContent =>
        Paragraphs.Count > 0
        || Bullets.Count > 0
        || Quotes.Count > 0
        || CodeBlocks.Count > 0
        || ImagePath is not null;
}
