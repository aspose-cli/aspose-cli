using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Tables;
using Aspose.Slides;
using Aspose.Slides.Charts;

namespace Aspose.Cli.Product.Slides.Engine.Mapping;

/// <summary>The value kinds the Slides table adds to the SDK's: chart values, their sources and cells, and pictures.</summary>
internal static class SlidesValues
{
    /// <summary>A value of a chart: a number or a string, as the engine gives it.</summary>
    internal static ValueKind<object?> ChartValue { get; } = ValueKinds.Create<object?>(
        "chart-value",
        "A number or a string, as the engine gives it; null where it gives none.",
        static () => new JsonObject { ["type"] = new JsonArray("number", "string", "null") },
        static value => value switch
        {
            null => null,
            string text => JsonValue.Create(text),
            double number => double.IsFinite(number) ? JsonValue.Create(number) : null,
            float number => float.IsFinite(number) ? JsonValue.Create(number) : null,
            IConvertible number when number.GetTypeCode() is >= TypeCode.SByte and <= TypeCode.Decimal => JsonValue.Create(number.ToDouble(CultureInfo.InvariantCulture)),
            _ => JsonValue.Create(Convert.ToString(value, CultureInfo.InvariantCulture)),
        },
        static value => value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.String => value.GetString(),
            _ => throw ValueKinds.Expected("a number or a string", value),
        });

    /// <summary>Where a chart value comes from: the engine's <c>DataSourceType</c>, and the workbook cell when it is one.</summary>
    internal sealed record Source(string Type, IChartDataCell? Cell);

    /// <summary>The source of a chart value, read only.</summary>
    internal static ValueKind<Source?> ChartSource { get; } = ValueKinds.Create<Source?>(
        "chart-source",
        "Where the value comes from: type is the engine's DataSourceType (Worksheet, StringLiterals, DoubleLiterals); worksheet, row and column, the engine's indexes from 0, name the cell of the chart's workbook when it is one.",
        static () => new JsonObject
        {
            ["type"] = new JsonArray("object", "null"),
            ["properties"] = new JsonObject
            {
                ["type"] = new JsonObject { ["type"] = "string" },
                ["worksheet"] = new JsonObject { ["type"] = "integer" },
                ["row"] = new JsonObject { ["type"] = "integer" },
                ["column"] = new JsonObject { ["type"] = "integer" },
            },
        },
        static source =>
        {
            if (source is null)
            {
                return null;
            }

            var written = new JsonObject { ["type"] = source.Type };
            if (source.Cell is { } cell)
            {
                written["worksheet"] = cell.ChartDataWorksheet.Index;
                written["row"] = cell.Row;
                written["column"] = cell.Column;
            }

            return written;
        },
        static value => throw ValueKinds.Expected("settable; a source is read only", value));

    /// <summary>The source of a chart value: its cell when it comes from the workbook, none otherwise.</summary>
    internal static Source SourceOf(IBaseChartValue value) =>
        new(Enum.GetName(value.DataSourceType) ?? value.DataSourceType.ToString(),
            value.DataSourceType == DataSourceType.Worksheet && value is ISingleCellChartValue single ? single.AsCell : null);

    /// <summary>A cell of a chart's workbook, by the engine's indexes from 0.</summary>
    internal sealed record CellReference(int Worksheet, int Row, int Column);

    /// <summary>A cell of a chart's workbook: <c>{"worksheet": 0, "row": 1, "column": 2}</c>.</summary>
    internal static ValueKind<CellReference> Cell { get; } = ValueKinds.Create<CellReference>(
        "chart-cell",
        "A cell of the chart's workbook: worksheet, row and column, the engine's indexes from 0.",
        static () => new JsonObject
        {
            ["type"] = "object",
            ["required"] = new JsonArray("worksheet", "row", "column"),
            ["additionalProperties"] = false,
            ["properties"] = new JsonObject
            {
                ["worksheet"] = new JsonObject { ["type"] = "integer" },
                ["row"] = new JsonObject { ["type"] = "integer" },
                ["column"] = new JsonObject { ["type"] = "integer" },
            },
        },
        static cell => new JsonObject { ["worksheet"] = cell.Worksheet, ["row"] = cell.Row, ["column"] = cell.Column },
        static value =>
            value.ValueKind == JsonValueKind.Object
            && value.EnumerateObject().Count() == 3
            && value.TryGetProperty("worksheet", out JsonElement worksheet) && worksheet.TryGetInt32(out int sheet)
            && value.TryGetProperty("row", out JsonElement row) && row.TryGetInt32(out int rowIndex)
            && value.TryGetProperty("column", out JsonElement column) && column.TryGetInt32(out int columnIndex)
                ? new CellReference(sheet, rowIndex, columnIndex)
                : throw ValueKinds.Expected("an object of worksheet, row and column, each a whole number", value));

    /// <summary>
    /// The workbook cell a reference names, through <c>GetCell</c>. A worksheet the workbook does
    /// not have is <c>ADDRESS_NOT_FOUND</c>: <c>GetCell</c> would add one.
    /// </summary>
    internal static IChartDataCell Resolve(IChartData data, CellReference cell)
    {
        IChartDataWorkbook workbook = data.ChartDataWorkbook;
        int worksheets = workbook.Worksheets.Count;
        if (cell.Worksheet < 0 || cell.Worksheet >= worksheets)
        {
            throw TableErrors.NotFound("worksheet", cell.Worksheet.ToString(CultureInfo.InvariantCulture),
                [.. Enumerable.Range(0, worksheets).Select(static index => index.ToString(CultureInfo.InvariantCulture))]);
        }

        return workbook.GetCell(cell.Worksheet, cell.Row, cell.Column);
    }

    /// <summary>A picture: set as an image file, read as its position among the presentation's images.</summary>
    internal static ValueKind<object?> Picture { get; } = ValueKinds.Create<object?>(
        "picture",
        "Read: the picture's position among the presentation's images (image/<index>), from 0, or null. Set: the path of an image file, which the presentation adds to its images.",
        static () => new JsonObject { ["type"] = new JsonArray("integer", "string", "null") },
        static value => value is int index ? JsonValue.Create(index) : null,
        static value => value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } path ? path : throw ValueKinds.Expected("an image file path", value),
        TableInput.Image);

    /// <summary>The position of a picture among the presentation's images, by reference; null when there is none.</summary>
    internal static object? PictureIndex(IPresentation presentation, IPPImage? image) =>
        image is null ? null : presentation.Images.Select(static (candidate, index) => (candidate, index)).FirstOrDefault(found => ReferenceEquals(found.candidate, image)) is { candidate: not null } found ? found.index : null;
}
