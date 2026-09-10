using System.Globalization;
using System.Text.Json;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Text;

namespace Aspose.Cli.Product.Slides.Operations;

/// <summary>Parses and validates presentation operations before a deck is opened.</summary>
internal static class SlidesOpsParser
{
    private const int MaximumOperations = 256;

    public static SlidesOpsBatch Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw Invalid("the ops document is empty");
        }

        SlidesOpsBatch batch;
        try
        {
            batch = ProductJsonContext.Definition.Deserialize<SlidesOpsBatch>(json);
        }
        catch (JsonException exception)
        {
            throw Invalid(exception.Message);
        }

        return Prepare(batch);
    }

    /// <summary>Validates a composed batch and assigns stable missing IDs.</summary>
    internal static SlidesOpsBatch Prepare(SlidesOpsBatch batch)
    {
        BoundedOperationValidation.ValidateEnvelope(
            batch,
            SlidesSchemaIds.Ops,
            Invalid);

        if (batch.Ops.Count is < 1 or > MaximumOperations)
        {
            throw Invalid($"the ops array must contain 1-{MaximumOperations} operations");
        }

        IReadOnlyList<SlidesOp> identified = BoundedOperationIds.Assign(
            batch.Ops,
            static operation => operation.Id,
            static (operation, id) => operation with { Id = id },
            Invalid);
        for (int index = 0; index < identified.Count; index++)
        {
            Validate(identified[index], index);
        }

        return batch with { SchemaVersion = 2, Ops = identified };
    }

    private static void Validate(SlidesOp op, int index)
    {
        if (op is AddSlideOp or DeleteSlidesOp or MoveSlideOp or DuplicateSlideOp
            or SetSlideHiddenOp or ApplyLayoutOp or SetBackgroundOp or AddSectionOp or AppendPresentationOp)
        {
            ValidateSlideOperation(op, index);
        }
        else if (op is SetTitleOp or SetBodyOp or SetTextOp or SlidesReplaceTextOp or SetNotesOp)
        {
            ValidateTextOperation(op, index);
        }
        else if (op is SlidesInsertImageOp or InsertShapeOp or SlidesInsertTableOp or SlidesSetTableCellOp
                 or InsertChartOp or UpdateChartDataOp or DeleteShapeOp or SetShapeStyleOp)
        {
            ValidateObjectOperation(op, index);
        }
        else
        {
            ValidatePresentationOperation(op, index);
        }
    }

    private static void ValidateSlideOperation(SlidesOp op, int index)
    {
        switch (op)
        {
            case AddSlideOp value:
                Require(value.At is null or > 0, index, op, "at is 1-based");
                break;
            case DeleteSlidesOp value: Slides(value.Slides); break;
            case MoveSlideOp value: Target(value, index); Require(value.To > 0, index, op, "to is 1-based"); break;
            case DuplicateSlideOp value: Target(value, index); Require(value.At is null or > 0, index, op, "at is 1-based"); break;
            case SetSlideHiddenOp value: Slides(value.Slides); break;
            case ApplyLayoutOp value: Slides(value.Slides); Text(value.Layout, "layout", index, op); break;
            case SetBackgroundOp value:
                OptionalSlides(value.Slides);
                Require((value.Color is null) != (value.ImagePath is null), index, op, "choose exactly one of color or imagePath");
                if (value.Color is not null)
                {
                    Color(value.Color, index, op);
                }
                break;
            case AddSectionOp value: Text(value.Name, "name", index, op); Require(value.AtSlide > 0, index, op, "atSlide is 1-based"); break;
            case AppendPresentationOp value:
                Text(value.Path, "path", index, op);
                Require(value.MasterPolicy is "keep-source" or "use-dest", index, op, "masterPolicy must be keep-source or use-dest");
                break;
        }
    }

    private static void ValidateTextOperation(SlidesOp op, int index)
    {
        switch (op)
        {
            case SetTitleOp value: Target(value, index); break;
            case SetBodyOp value:
                Target(value, index);
                Require(value.Paragraphs.Count > 0, index, op, "paragraphs must not be empty");
                Require(value.Paragraphs.All(static paragraph => paragraph.Level is >= 0 and <= 8), index, op, "paragraph levels must be 0-8");
                break;
            case SetTextOp value: ShapeTarget(value, index); break;
            case SlidesReplaceTextOp value:
                Text(value.Find, "find", index, op);
                Require(value.Scope is "shapes" or "notes" or "all", index, op, "scope must be shapes, notes or all");
                if (value.Regex)
                {
                    try
                    {
                        _ = SafeRegex.Create(value.Find, value.MatchCase);
                    }
                    catch (ArgumentException exception)
                    {
                        throw Invalid($"op {index} ({op.OpName}): invalid regex: {exception.Message}");
                    }
                }

                break;
            case SetNotesOp value: Target(value, index); break;
        }
    }

    private static void ValidateObjectOperation(SlidesOp op, int index)
    {
        switch (op)
        {
            case SlidesInsertImageOp value: Target(value, index); Text(value.Path, "path", index, op); OptionalRect(value.Rect, index, op); break;
            case InsertShapeOp value:
                Target(value, index);
                Require(value.Kind is "rectangle" or "rounded-rectangle" or "ellipse" or "line" or "chevron", index, op, "unsupported shape kind");
                Rect(value.Rect, index, op);
                Style(value.Style, index, op);
                break;
            case SlidesInsertTableOp value:
                Target(value, index);
                Rect(value.Rect, index, op);
                Require(value.Rows is >= 1 and <= 100 && value.Cols is >= 1 and <= 50, index, op, "rows must be 1-100 and cols 1-50");
                Require(
                    value.Data is null || value.Data.Count <= value.Rows && value.Data.All(row => row.Count <= value.Cols),
                    index,
                    op,
                    "data exceeds the declared table dimensions");
                break;
            case SlidesSetTableCellOp value:
                ShapeTarget(value, index);
                Require(value.Row > 0 && value.Col > 0, index, op, "row and col are 1-based");
                break;
            case InsertChartOp value:
                Target(value, index);
                Rect(value.Rect, index, op);
                Require(value.Kind is "bar" or "column" or "line" or "pie" or "scatter", index, op, "unsupported chart kind");
                ChartData(value.Kind, value.Categories, value.Series, index, op);
                break;
            case UpdateChartDataOp value:
                ShapeTarget(value, index);
                Require(value.Categories is not null || value.Series is not null, index, op, "categories or series is required");
                if (value.Series is not null)
                {
                    Require(value.Series.Count > 0, index, op, "series must not be empty");
                    Require(value.Series.All(static series => series.Values.Count > 0), index, op, "series values must not be empty");
                }

                break;
            case DeleteShapeOp value: ShapeTarget(value, index); break;
            case SetShapeStyleOp value:
                ShapeTarget(value, index);
                Style(value.Style, index, op);
                Require(HasStyle(value.Style), index, op, "style must set at least one property");
                break;
        }
    }

    private static void ValidatePresentationOperation(SlidesOp op, int index)
    {
        switch (op)
        {
            case SetFooterOp value:
                OptionalSlides(value.Slides);
                Require(
                    value.Text is not null || value.ShowNumber is not null || value.ShowDate is not null,
                    index,
                    op,
                    "text, showNumber or showDate is required");
                break;
            case SetTransitionOp value:
                Slides(value.Slides);
                Require(value.Kind is not null || value.DurationMs is not null, index, op, "kind or durationMs is required");
                Require(
                    value.Kind is null or "none" or "fade" or "push" or "wipe" or "split" or "cover",
                    index,
                    op,
                    "unsupported transition kind");
                Require(value.DurationMs is null or >= 0 and <= 60_000, index, op, "durationMs must be 0-60000");
                break;
            case SlidesSetPropertiesOp value:
                Require(
                    value.Title is not null || value.Author is not null || value.Subject is not null
                    || value.Keywords is not null || value.Company is not null,
                    index,
                    op,
                    "at least one property is required");
                break;
            case SetSlideSizeOp value:
                Require(
                    value.Size is "16x9" or "4x3" || TryCustomSize(value.Size),
                    index,
                    op,
                    "size must be 16x9, 4x3 or WxHpt");
                break;
        }
    }

    private static void Target(SlideTargetOp value, int index) =>
        Require((value.Slide is not null) != (value.SlideId is not null)
            && value.Slide is null or > 0
            && value.SlideId is null or > 0, index, value, "choose one valid slide or slideId");

    private static void ShapeTarget(ShapeTargetOp value, int index)
    {
        Target(value, index);
        int count = (value.Shape is not null ? 1 : 0)
            + (value.ShapeName is not null ? 1 : 0)
            + (value.Placeholder is not null ? 1 : 0);
        Require(count == 1, index, value, "choose exactly one of shape, shapeName or placeholder");
        Require(value.Shape is null or > 0, index, value, "shape id must be positive");
        if (value.ShapeName is not null)
        {
            Text(value.ShapeName, "shapeName", index, value);
        }
        if (value.Placeholder is not null)
        {
            Require(value.Placeholder is "title" or "body" or "subtitle" or "footer", index, value, "unknown placeholder role");
        }
    }

    private static void ChartData(
        string kind,
        IReadOnlyList<string> categories,
        IReadOnlyList<SlidesChartSeriesInput> series,
        int index,
        SlidesOp op)
    {
        Require(categories.Count > 0 && categories.Count <= 1000, index, op, "categories must contain 1-1000 items");
        Require(series.Count > 0 && series.Count <= 50, index, op, "series must contain 1-50 items");
        Require(series.All(item => item.Values.Count == categories.Count), index, op, "each series must match the category count");
        Require(
            kind != "scatter" || series.All(item => item.XValues?.Count == item.Values.Count),
            index,
            op,
            "scatter series require xValues matching values");
    }

    private static void Slides(string value) => _ = PageRange.Parse(value);
    private static void OptionalSlides(string? value)
    {
        if (value is not null)
        {
            Slides(value);
        }
    }

    private static void OptionalRect(SlidesRectInput? value, int index, SlidesOp op)
    {
        if (value is not null)
        {
            Rect(value, index, op);
        }
    }

    private static void Rect(SlidesRectInput value, int index, SlidesOp op) =>
        Require(value.X >= 0 && value.Y >= 0 && value.Width > 0 && value.Height > 0, index, op, "rectangle coordinates must be non-negative and its size positive");

    private static void Style(SlidesShapeStyleInput? value, int index, SlidesOp op)
    {
        if (value is null)
        {
            return;
        }

        if (value.Fill is not null)
        {
            Color(value.Fill, index, op);
        }

        if (value.Line is not null)
        {
            Color(value.Line, index, op);
        }

        if (value.Color is not null)
        {
            Color(value.Color, index, op);
        }
        Require(value.Size is null or > 0 and <= 400, index, op, "font size must be 0-400");
    }

    private static bool HasStyle(SlidesShapeStyleInput value) =>
        value.Fill is not null || value.Line is not null || value.Font is not null
        || value.Size is not null || value.Color is not null || value.Bold is not null;

    private static void Color(string value, int index, SlidesOp op) =>
        Require(
            value.Length == 7 && value[0] == '#'
            && int.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _),
            index,
            op,
            "color must use #RRGGBB");

    private static void Text(string value, string field, int index, SlidesOp op) =>
        Require(!string.IsNullOrWhiteSpace(value), index, op, $"{field} is required");

    private static bool TryCustomSize(string value)
    {
        if (!value.EndsWith("pt", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        string[] parts = value[..^2].Split('x', StringSplitOptions.TrimEntries);
        return parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double width)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double height)
            && width is >= 72 and <= 7200
            && height is >= 72 and <= 7200;
    }

    private static void Require(bool condition, int index, SlidesOp op, string reason)
    {
        if (!condition)
        {
            throw Invalid($"op {index} ({op.OpName}): {reason}");
        }
    }

    private static CliException Invalid(string reason) => new(
        ErrorCodes.OpsInvalid,
        $"Invalid Slides ops batch: {reason}.",
        hint: "Fix the named op using 'aspose-cli schema v2/slides/ops'.");
}
