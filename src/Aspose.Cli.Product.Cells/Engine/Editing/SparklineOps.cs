using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cells;
using Aspose.Cells.Charts;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>
/// Sparklines — tiny in-cell charts. A data block fans out to one sparkline per
/// row (or column) in one group, or a list of data ranges to one sparkline each,
/// each landing in one cell of at.
/// Takes the workbook because the group's series colour is
/// a <c>CellsColor</c> only the workbook can create (as the CF ops do).
/// </summary>
internal static class SparklineOps
{
    public static void AddSparkline(Workbook workbook, Worksheet sheet, AddSparklineOp op)
    {
        // Which data each sparkline plots and the cell it lands in: one per row or column of a
        // data block, or one per range of a list (AddSparklineOp.Sparklines).
        IReadOnlyList<(RangeSpec Data, CellRef Cell)> sparklines = op.Sparklines();
        Worksheet[] dataSheets = [.. sparklines.Select(line => line.Data.SheetName is { } name ? Sheets.Resolve(workbook, name) : sheet)];
        SparklineType type = ToType(op.Kind);

        // A cell holds one sparkline: the group these cells already hold is no change, and the
        // groups drawn in any of them are cleared first, every one in one step.
        var cells = sparklines.Select(static line => (line.Cell.Row, line.Cell.Column)).ToHashSet();
        var overlapped = new List<(int Index, SparklineGroup Group, Sparkline[] Drawn)>();
        for (int index = 0; index < sheet.SparklineGroups.Count; index++)
        {
            SparklineGroup existing = sheet.SparklineGroups[index];
            Sparkline[] drawn = [.. existing.Sparklines.Cast<Sparkline>()];
            if (drawn.Any(line => cells.Contains((line.Row, line.Column))))
            {
                overlapped.Add((index, existing, drawn));
            }
        }

        if (overlapped is [var only] && Same(sheet, only.Group, only.Drawn, type, op.Color, sparklines, dataSheets))
        {
            return;
        }

        if (overlapped.Count > 0)
        {
            var numbers = new List<string>();
            var places = new List<string>();
            var clears = new List<string>();
            foreach ((int index, _, Sparkline[] drawn) in overlapped)
            {
                IReadOnlyList<RangeRef> blocks = Blocks([.. drawn.Select(static line => new CellRef(line.Row, line.Column))]);
                numbers.Add((index + 1).ToString(CultureInfo.InvariantCulture));
                places.Add(A1.FormatRanges(blocks));
                clears.AddRange(blocks.Select(block => new JsonObject
                {
                    ["op"] = "clear_range",
                    ["sheet"] = sheet.Name,
                    ["range"] = A1.FormatRange(block),
                    ["what"] = CellsClearTargets.Everything,
                }.ToJsonString(Readable)));
            }

            throw new OperationInvalidException(
                $"'at' overlaps sparkline {(overlapped.Count == 1 ? "group" : "groups")} {string.Join(" and ", numbers)} on sheet '{sheet.Name}', "
                    + $"drawn in {string.Join(" and ", places)}, and a cell holds one sparkline",
                hint: $"Clear {(overlapped.Count == 1 ? "that group" : "those groups")} first with {string.Join(",", clears)}, "
                    + "which clears the values and formats in those cells too, or draw these sparklines in other cells.");
        }

        // Known issue CELLS-SPARKLINE-APOSTROPHE (KNOWN-ISSUES.md): the engine's
        // one-call SparklineGroups.Add rejects data on a sheet whose name has an
        // apostrophe, so the group is built from its parts: an empty group with
        // the settings the one-call Add applies (an empty group has no colours,
        // and reading its PresetStyle throws), then one sparkline per data row
        // or column, each range written as start:end. Tests pin the result to
        // the one-call Add's.
        SparklineGroup group = NewGroup(sheet, type);
        for (int index = 0; index < sparklines.Count; index++)
        {
            (RangeSpec data, CellRef cell) = sparklines[index];
            group.Sparklines.Add(
                Sheets.QuotedName(dataSheets[index]) + "!" + A1.FormatCell(data.Range.Start) + ":" + A1.FormatCell(data.Range.End),
                cell.Row,
                cell.Column);
        }

        if (op.Color is { } color)
        {
            CellsColor seriesColor = workbook.CreateCellsColor();
            seriesColor.Color = StyleWriter.ParseHex(color);
            group.SeriesColor = seriesColor;
        }
    }

    // JSON a caller can copy as it reads: a sheet name keeps its own letters.
    private static readonly JsonSerializerOptions Readable = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// An empty group with the settings the engine's one-call SparklineGroups.Add applies (an
    /// empty group has no colors, and reading its PresetStyle throws).
    /// </summary>
    private static SparklineGroup NewGroup(Worksheet sheet, SparklineType type)
    {
        SparklineGroup group = sheet.SparklineGroups[sheet.SparklineGroups.Add(type)];
        group.PresetStyle = SparklinePresetStyleType.Style1;
        group.ShowNegativePoints = type == SparklineType.WinLoss;
        return group;
    }

    /// <summary>
    /// Whether a group's series color is the one a group drawn with no color given has: the
    /// engine stores it as a color of the workbook's theme, so it is compared as the theme color
    /// and tint the preset style gives, whatever the theme makes of them.
    /// </summary>
    private static bool DefaultColored(CellsColor? color, SparklineType type)
    {
        if (color is not { Type: ColorType.Theme, ThemeColor: { } theme })
        {
            return false;
        }

        using var scratch = new Workbook();
        Worksheet sheet = scratch.Worksheets[0];
        SparklineGroup group = NewGroup(sheet, type);
        group.Sparklines.Add(Sheets.QuotedName(sheet) + "!A1:B1", 0, 2);
        return group.SeriesColor is { Type: ColorType.Theme, ThemeColor: { } preset }
            && preset.ColorType == theme.ColorType && Math.Abs(preset.Tint - theme.Tint) < 1e-9;
    }

    /// <summary>
    /// The cells as the fewest ranges that cover exactly them: runs down each column, or along
    /// each row when that takes fewer.
    /// </summary>
    private static IReadOnlyList<RangeRef> Blocks(IReadOnlyList<CellRef> cells)
    {
        static List<RangeRef> Runs(IEnumerable<CellRef> ordered, bool down)
        {
            var runs = new List<RangeRef>();
            foreach (CellRef cell in ordered)
            {
                if (runs.Count > 0 && runs[^1] is var last
                    && (down
                        ? last.End.Column == cell.Column && last.End.Row + 1 == cell.Row
                        : last.End.Row == cell.Row && last.End.Column + 1 == cell.Column))
                {
                    runs[^1] = new RangeRef(last.Start, cell);
                }
                else
                {
                    runs.Add(new RangeRef(cell, cell));
                }
            }

            return runs;
        }

        CellRef[] distinct = [.. cells.Distinct()];
        List<RangeRef> columns = Runs(distinct.OrderBy(static cell => cell.Column).ThenBy(static cell => cell.Row), down: true);
        List<RangeRef> rows = Runs(distinct.OrderBy(static cell => cell.Row).ThenBy(static cell => cell.Column), down: false);
        return rows.Count < columns.Count ? rows : columns;
    }

    /// <summary>Whether a group draws exactly these sparklines, of this kind and in this color, the default one when none is given.</summary>
    private static bool Same(
        Worksheet host,
        SparklineGroup group,
        Sparkline[] drawn,
        SparklineType type,
        string? color,
        IReadOnlyList<(RangeSpec Data, CellRef Cell)> sparklines,
        Worksheet[] dataSheets)
    {
        if (group.Type != type || drawn.Length != sparklines.Count
            || (color is null
                ? !DefaultColored(group.SeriesColor, type)
                : !string.Equals(group.SeriesColor is { } series ? StylePool.ToHex(series.Color) : null, color, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        for (int index = 0; index < sparklines.Count; index++)
        {
            (RangeSpec data, CellRef cell) = sparklines[index];
            Sparkline? line = drawn.FirstOrDefault(item => item.Row == cell.Row && item.Column == cell.Column);
            RangeSpec? plotted = line is null ? null : Plotted(line);
            if (plotted is null || plotted.Range != data.Range
                || !string.Equals(plotted.SheetName ?? host.Name, dataSheets[index].Name, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static RangeSpec? Plotted(Sparkline line)
    {
        try
        {
            return A1.ParseRange((line.DataRange ?? string.Empty).TrimStart('='));
        }
        catch (CliException)
        {
            return null;
        }
    }

    /// <summary>Removes the sparklines drawn in an area, and a group left with none.</summary>
    public static void Clear(Worksheet sheet, CellArea area)
    {
        sheet.SparklineGroups.ClearSparklines(area);
        for (int index = sheet.SparklineGroups.Count - 1; index >= 0; index--)
        {
            if (sheet.SparklineGroups[index].Sparklines.Count == 0)
            {
                sheet.SparklineGroups.RemoveAt(index);
            }
        }
    }

    /// <summary>
    /// Wire name to the engine's <see cref="SparklineType"/>. Excel calls the
    /// third type "win/loss"; the engine exposes it as <c>WinLoss</c>.
    /// </summary>
    /// <summary>The wire name of an engine sparkline type, or null for one outside the vocabulary.</summary>
    internal static string? FromType(SparklineType type) =>
        Types.FirstOrDefault(name => ToType(name) == type);

    private static readonly string[] Types = [CellsSparklineTypes.Line, CellsSparklineTypes.Column, CellsSparklineTypes.WinLoss];

    private static SparklineType ToType(string type) => type switch
    {
        CellsSparklineTypes.Line => SparklineType.Line,
        CellsSparklineTypes.Column => SparklineType.Column,
        CellsSparklineTypes.WinLoss => SparklineType.WinLoss,
        _ => throw new ArgumentOutOfRangeException(
            nameof(type), type, "Sparkline type is missing from the engine mapper."),
    };
}
