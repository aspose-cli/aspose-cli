using System.Globalization;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Operations;
using Aspose.Cli.Sdk.Text;
using static Aspose.Cli.Sdk.Operations.OperationInvalidException;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Semantic rules of Slides operations that the contract types cannot express.</summary>
internal static partial class SlidesOpRules
{
    private const int MaximumCategories = 1000;
    private const int MaximumSeries = 50;

    internal static void AddSlide(AddSlideOp op) => Require(op.At is null or > 0, "at is 1-based");

    internal static void DeleteSlides(DeleteSlidesOp op) => Slides(op.Slides);

    internal static void MoveSlide(MoveSlideOp op)
    {
        Target(op);
        Require(op.To > 0, "to is 1-based");
    }

    internal static void DuplicateSlide(DuplicateSlideOp op)
    {
        Target(op);
        Require(op.At is null or > 0, "at is 1-based");
    }

    internal static void SetSlideHidden(SetSlideHiddenOp op) => Slides(op.Slides);

    internal static void ApplyLayout(ApplyLayoutOp op)
    {
        Slides(op.Slides);
        Text(op.Layout, "layout");
    }

    internal static void SetBackground(SetBackgroundOp op)
    {
        OptionalSlides(op.Slides);
        Require((op.Color is null) != (op.ImagePath is null), "choose exactly one of color or imagePath");
        if (op.Color is not null)
        {
            Color(op.Color);
        }
    }

    internal static void AddSection(AddSectionOp op)
    {
        Text(op.Name, "name");
        Require(op.AtSlide > 0, "atSlide is 1-based");
    }

    internal static void AppendPresentation(AppendPresentationOp op)
    {
        Text(op.Path, "path");
        Require(op.MasterPolicy is "keep-source" or "use-dest", "masterPolicy must be keep-source or use-dest");
    }

    internal static void SetTitle(SetTitleOp op) => Target(op);

    internal static void SetBody(SetBodyOp op)
    {
        Target(op);
        Require(op.Paragraphs.Count > 0, "paragraphs must not be empty");
        Require(op.Paragraphs.All(static paragraph => paragraph.Level is >= 0 and <= 8), "paragraph levels must be 0-8");
    }

    internal static void SetText(SetTextOp op) => ShapeTarget(op);

    internal static void ReplaceText(SlidesReplaceTextOp op)
    {
        Text(op.Find, "find");
        Require(op.Scope is "shapes" or "notes" or "all", "scope must be shapes, notes or all");
        if (op.Regex)
        {
            try
            {
                _ = SafeRegex.Create(op.Find, op.MatchCase);
            }
            catch (ArgumentException exception)
            {
                throw new OperationInvalidException($"invalid regex: {exception.Message}");
            }
        }
    }

    internal static void SetNotes(SetNotesOp op) => Target(op);

    internal static void InsertImage(SlidesInsertImageOp op)
    {
        Target(op);
        Text(op.Path, "path");
        if (op.Rect is not null)
        {
            Rect(op.Rect);
        }
    }

    internal static void InsertShape(InsertShapeOp op)
    {
        Target(op);
        Require(op.Kind is "rectangle" or "rounded-rectangle" or "ellipse" or "line" or "chevron", "unsupported shape kind");
        Rect(op.Rect);
        Style(op.Style);
    }

    internal static void InsertTable(SlidesInsertTableOp op)
    {
        Target(op);
        Rect(op.Rect);
        Require(op.Rows is >= 1 and <= 100 && op.Cols is >= 1 and <= 50, "rows must be 1-100 and cols 1-50");
        Require(op.Data is null || op.Data.Count <= op.Rows && op.Data.All(row => row.Count <= op.Cols),
            "data exceeds the declared table dimensions");
    }

    internal static void SetTableCell(SlidesSetTableCellOp op)
    {
        ShapeTarget(op);
        Require(op.Row > 0 && op.Col > 0, "row and col are 1-based");
    }

    internal static void InsertChart(InsertChartOp op)
    {
        Target(op);
        Rect(op.Rect);
        Require(op.Kind is "bar" or "column" or "line" or "pie" or "scatter", "unsupported chart kind");
        Categories(op.Categories);
        Series(op.Series);
        Require(op.Series.All(item => item.Values.Count == op.Categories.Count), "each series must match the category count");
        Require(op.Kind != "scatter" || op.Series.All(static item => item.XValues?.Count == item.Values.Count),
            "scatter series require xValues matching values");
    }

    internal static void UpdateChartData(UpdateChartDataOp op)
    {
        ShapeTarget(op);
        Require(op.Categories is not null || op.Series is not null, "categories or series is required");
        if (op.Categories is not null)
        {
            Categories(op.Categories);
        }
        if (op.Series is not null)
        {
            Series(op.Series);
            Require(op.Categories is null || op.Series.All(item => item.Values.Count == op.Categories.Count),
                "each series must match the category count");
        }
    }

    internal static void DeleteShape(DeleteShapeOp op) => ShapeTarget(op);

    internal static void SetShapeStyle(SetShapeStyleOp op)
    {
        ShapeTarget(op);
        Style(op.Style);
        Require(op.Style.Fill is not null || op.Style.Line is not null || op.Style.Font is not null
            || op.Style.Size is not null || op.Style.Color is not null || op.Style.Bold is not null,
            "style must set at least one property");
    }

    internal static void SetFooter(SetFooterOp op)
    {
        OptionalSlides(op.Slides);
        Require(op.Text is not null || op.ShowNumber is not null || op.ShowDate is not null,
            "text, showNumber or showDate is required");
    }

    internal static void SetTransition(SetTransitionOp op)
    {
        Slides(op.Slides);
        Require(op.Kind is not null || op.DurationMs is not null, "kind or durationMs is required");
        Require(op.Kind is null or "none" or "fade" or "push" or "wipe" or "split" or "cover", "unsupported transition kind");
        Require(op.DurationMs is null or >= 0 and <= 60_000, "durationMs must be 0-60000");
    }

    internal static void SetProperties(SlidesSetPropertiesOp op) =>
        Require(op.Title is not null || op.Author is not null || op.Subject is not null
            || op.Keywords is not null || op.Company is not null, "at least one property is required");

    internal static void SetSlideSize(SetSlideSizeOp op) =>
        Require(
            op.Size is "16x9" or "4x3" || TryParseCustomSize(op.Size, out _, out _),
            "size must be 16x9, 4x3 or WxHpt with each side from 72 through 7200 points");

    /// <summary>
    /// Parses a custom slide size such as <c>800x450pt</c> or <c>800.5x450pt</c>: the exact
    /// spelling the ops schema allows, with each side from 72 through 7200 points.
    /// </summary>
    internal static bool TryParseCustomSize(string value, out float width, out float height)
    {
        width = 0;
        height = 0;
        Match match = CustomSizePattern().Match(value);
        return match.Success
            && float.TryParse(match.Groups["width"].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out width)
            && float.TryParse(match.Groups["height"].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out height)
            && width is >= 72 and <= 7200
            && height is >= 72 and <= 7200;
    }

    [GeneratedRegex(@"^(?<width>[0-9]+(?:\.[0-9]+)?)x(?<height>[0-9]+(?:\.[0-9]+)?)pt$", RegexOptions.CultureInvariant)]
    private static partial Regex CustomSizePattern();

    private static void Target(SlideTargetOp op) =>
        Require((op.Slide is not null) != (op.SlideId is not null)
            && op.Slide is null or > 0
            && op.SlideId is null or > 0, "choose one valid slide or slideId");

    private static void ShapeTarget(ShapeTargetOp op)
    {
        Target(op);
        int count = (op.Shape is not null ? 1 : 0) + (op.ShapeName is not null ? 1 : 0) + (op.Placeholder is not null ? 1 : 0);
        Require(count == 1, "choose exactly one of shape, shapeName or placeholder");
        Require(op.Shape is null or > 0, "shape id must be positive");
        if (op.ShapeName is not null)
        {
            Text(op.ShapeName, "shapeName");
        }
        Require(op.Placeholder is null or "title" or "body" or "subtitle" or "footer", "unknown placeholder role");
    }

    private static void Categories(IReadOnlyList<string> categories) =>
        Require(categories.Count is > 0 and <= MaximumCategories, $"categories must contain 1-{MaximumCategories} items");

    private static void Series(IReadOnlyList<SlidesChartSeriesInput> series)
    {
        Require(series.Count is > 0 and <= MaximumSeries, $"series must contain 1-{MaximumSeries} items");
        Require(series.All(static item => item.Values.Count is > 0 and <= MaximumCategories),
            $"series values must contain 1-{MaximumCategories} items");
    }

    private static void Slides(string value) => _ = PageRange.Parse(value);

    private static void OptionalSlides(string? value)
    {
        if (value is not null)
        {
            Slides(value);
        }
    }

    private static void Rect(SlidesRectInput value) =>
        Require(value.X >= 0 && value.Y >= 0 && value.Width > 0 && value.Height > 0,
            "rectangle coordinates must be non-negative and its size positive");

    private static void Style(SlidesShapeStyleInput? value)
    {
        if (value is null)
        {
            return;
        }
        foreach (string? color in new[] { value.Fill, value.Line, value.Color })
        {
            if (color is not null)
            {
                Color(color);
            }
        }
        Require(value.Size is null or > 0 and <= 400, "font size must be 0-400");
    }

    private static void Color(string value) =>
        Require(
            value.Length == 7 && value[0] == '#'
            && int.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _),
            "color must use #RRGGBB");

    private static void Text(string value, string field) => Require(!string.IsNullOrWhiteSpace(value), $"{field} is required");

}
