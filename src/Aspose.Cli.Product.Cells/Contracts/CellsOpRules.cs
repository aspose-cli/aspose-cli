using System.Globalization;
using System.Text;
using Aspose.Cli.Product.Cells.Addressing;
using Aspose.Cli.Sdk.Operations;
using Advanced = Aspose.Cli.Product.Cells.Contracts.CellsAdvancedOpValidator;
using static Aspose.Cli.Product.Cells.Contracts.CellsValidationSupport;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>Semantic rules of Cells operations that the contract types cannot express.</summary>
internal static class CellsCoreOpValidator
{
    internal static SetFormulaOp ValidateSetFormula(SetFormulaOp op) =>
        ValidateRangeOf(op, op.Range,
            () => Require(op.Formula.StartsWith('='), "the formula must start with '='"));

    internal static ClearRangeOp ValidateClear(ClearRangeOp op) =>
        ValidateRangeOf(op, op.Range,
            () => Require(
                op.What is null || ClearTargets.All.Contains(op.What),
                $"'what' must be one of: {string.Join(", ", ClearTargets.All)}"));

    internal static T ValidateRange<T>(T op, string range) where T : Op =>
        ValidateRangeOf(op, range);

    internal static T ValidateRow<T>(T op, int at, int? count) where T : Op =>
        ValidateRowOp(op, at, count);

    internal static T ValidateColumn<T>(T op, string at, int? count) where T : Op =>
        ValidateColumnOp(op, at, count);

    internal static T ValidateNamedSheet<T>(T op) where T : Op =>
        ValidateSheetIsNamed(op);

    internal static T ValidateName<T>(T op, string name) where T : Op =>
        ValidateNamed(op, name);

    internal static SetValuesOp ValidateSetValues(SetValuesOp op)
    {
        RangeRef range = ParseUnqualifiedRange(op.Range);
        IReadOnlyList<IReadOnlyList<object?>> values = JsonValueMatrix.Normalize(
            op.Values, reason => new OperationInvalidException(reason));

        bool isAnchor = range.CellCount == 1;
        if (!isAnchor && (range.RowCount != values.Count || range.ColumnCount != values[0].Count))
        {
            throw new OperationInvalidException($"the range spans {range.RowCount}x{range.ColumnCount} but the matrix is {values.Count}x{values[0].Count}",
                "Use a single anchor cell (e.g. \"B2\") to write a matrix of any size, or make the range match the matrix.");
        }

        return op with { Values = values };
    }

    internal static CopyRangeOp ValidateCopy(CopyRangeOp op)
    {
        // From/To may be sheet-qualified for cross-sheet copies.
        _ = A1.ParseRange(op.From);
        RangeSpec to = A1.ParseRange(op.To);
        if (to.Range.CellCount != 1)
        {
            throw new OperationInvalidException("'to' must be a single anchor cell, e.g. \"Summary!A1\"");
        }

        return op;
    }

    internal static FormatRangeOp ValidateFormat(FormatRangeOp op)
    {
        _ = ParseUnqualifiedRange(op.Range);
        ValidateStyle(op.Style, conditional: false);
        return op;
    }

    internal static ResizeRowsOp ValidateResizeRows(ResizeRowsOp op)
    {
        Require(op.From >= 1, "'from' is a 1-based row number");
        Require(op.To is null || op.To >= op.From, "'to' must not be smaller than 'from'");
        Require(op.Height is null or >= 0, "'height' must not be negative");
        return op;
    }

    internal static ResizeColumnsOp ValidateResizeColumns(ResizeColumnsOp op)
    {
        int from = ParseColumn(op.From);
        if (op.To is { } toLetters)
        {
            Require(ParseColumn(toLetters) >= from,
                "'to' must not be left of 'from'");
        }

        Require(op.Width is null or >= 0, "'width' must not be negative");
        return op;
    }

    internal static FreezePanesOp ValidateFreeze(FreezePanesOp op)
    {
        _ = A1.ParseCell(op.Cell);
        return op;
    }

    internal static CreateChartOp ValidateChart(CreateChartOp op)
    {
        Require(ChartTypes.All.Contains(op.Type),
            $"'type' must be one of: {string.Join(", ", ChartTypes.All)}");
        // dataRange may be sheet-qualified so a chart on one sheet can plot
        // another's data (as create_pivot's sourceRange already allows); the
        // engine's SetChartDataRange resolves the qualified reference. Placement
        // ('at') stays on the op's sheet.
        _ = A1.ParseRange(op.DataRange);

        RangeRef placement = ParseUnqualifiedRange(op.At);
        Require(placement.CellCount > 1,
            "'at' must be a range the chart is placed over, e.g. \"E2:L18\"");

        Advanced.ValidateChartCosmetics(op, op.Legend, op.AxisTitles, op.SeriesColors, op.DataLabels);
        // The type is known here, so the pie/axis mismatch fails before any
        // file is opened; update_chart only learns the real type in the
        // engine, which enforces the same rule there.
        Require(op.Type != ChartTypes.Pie || op.AxisTitles is null,
            "a 'pie' chart has no axes; omit 'axisTitles'");
        return op;
    }

    internal static CreatePivotOp ValidatePivot(CreatePivotOp op)
    {
        _ = A1.ParseRange(op.SourceRange); // may be sheet-qualified
        RangeSpec at = A1.ParseRange(op.At);
        Require(at.SheetName is null && at.Range.CellCount == 1,
            "'at' must be a single unqualified anchor cell on the op's sheet, e.g. \"F3\"");

        Require(op.Values.Count > 0, "'values' must name at least one field to aggregate");
        foreach (PivotValueField value in op.Values)
        {
            Require(!string.IsNullOrWhiteSpace(value.Field), "a value field name is empty");
            Require(value.Function is null || PivotFunctions.All.Contains(value.Function),
                $"'function' must be one of: {string.Join(", ", PivotFunctions.All)}");
            // The engine accepts arbitrary format codes, so presence is the
            // only thing to check.
            Require(value.NumberFormat is null || !string.IsNullOrWhiteSpace(value.NumberFormat),
                "'numberFormat' must not be empty; omit it instead");
        }

        Require(op.Rows is not { Count: 0 }, "'rows' must not be an empty array; omit it instead");
        Require(op.Columns is not { Count: 0 }, "'columns' must not be an empty array; omit it instead");
        return op;
    }

    internal static SetPageSetupOp ValidatePageSetup(SetPageSetupOp op)
    {
        bool any = op.Orientation is not null || op.PaperSize is not null || op.FitToWidth is not null
            || op.FitToHeight is not null || op.Scale is not null || op.Margins is not null
            || op.Header is not null || op.Footer is not null;
        Require(any, "set_page_setup needs at least one field to change");

        Require(op.Orientation is null || PageOrientations.All.Contains(op.Orientation),
            $"'orientation' must be one of: {string.Join(", ", PageOrientations.All)}");
        Require(op.PaperSize is null || PaperSizes.All.Contains(op.PaperSize),
            $"'paperSize' must be one of: {string.Join(", ", PaperSizes.All)}");
        Require(op.FitToWidth is null or >= 0, "'fitToWidth' must not be negative (0 = automatic)");
        Require(op.FitToHeight is null or >= 0, "'fitToHeight' must not be negative (0 = automatic)");
        Require(op.Scale is null or (>= 10 and <= 400), "'scale' must be between 10 and 400");

        if (op.Margins is { } margins)
        {
            bool anyMargin = margins.Top is not null || margins.Bottom is not null || margins.Left is not null
                || margins.Right is not null || margins.Header is not null || margins.Footer is not null;
            Require(anyMargin, "'margins' has no sides set; omit it instead");
            Require(
                (margins.Top is null or >= 0) && (margins.Bottom is null or >= 0) && (margins.Left is null or >= 0)
                    && (margins.Right is null or >= 0) && (margins.Header is null or >= 0) && (margins.Footer is null or >= 0),
                "margins must not be negative");
        }

        return op;
    }

    internal static SetPrintAreaOp ValidatePrintArea(SetPrintAreaOp op)
    {
        if (op.Range is { } range)
        {
            _ = ParseUnqualifiedRange(range);
        }

        // Titles are normalized to Excel's absolute band form ("$1:$2", "$A:$B") here,
        // so the engine receives exactly what it stores.
        return op with
        {
            TitleRows = op.TitleRows is { } rows ? A1.ParseRowBand(rows) : null,
            TitleColumns = op.TitleColumns is { } columns ? A1.ParseColumnBand(columns) : null,
        };
    }

    internal static InsertImageOp ValidateInsertImage(InsertImageOp op)
    {
        Require(!string.IsNullOrWhiteSpace(op.Path), "the image 'path' must not be empty");
        RangeRef at = ParseUnqualifiedRange(op.At);
        Require(at.CellCount == 1, "'at' must be a single anchor cell, e.g. \"E2\"");
        Require(op.Width is null or >= 1, "'width' must be a positive pixel count");
        Require(op.Height is null or >= 1, "'height' must be a positive pixel count");
        return op;
    }

    internal static SetAutoFilterOp ValidateAutoFilter(SetAutoFilterOp op)
    {
        if (op.Off is true)
        {
            return op;
        }

        Require(op.Range is not null, "'range' is required unless 'off' is true");
        _ = ParseUnqualifiedRange(op.Range!);
        return op;
    }

    internal static SortRangeOp ValidateSort(SortRangeOp op)
    {
        RangeRef range = ParseUnqualifiedRange(op.Range);
        Require(op.By.Count > 0, "'by' must name at least one column to sort by");
        foreach (SortKey key in op.By)
        {
            // The engine sorts by an absolute column; one outside the sorted
            // range is silently a no-op (the caller's sort intent vanishes with
            // no error). Reject it up front, as remove_duplicates already does.
            int absolute = ParseColumn(key.Column);
            Require(absolute >= range.Start.Column && absolute <= range.End.Column,
                $"sort column '{key.Column}' is outside the range '{op.Range}'");
            Require(key.Order is null || SortOrders.All.Contains(key.Order),
                $"'order' must be one of: {string.Join(", ", SortOrders.All)}");
        }

        return op;
    }

    internal static CreateTableOp ValidateCreateTable(CreateTableOp op) =>
        ValidateRangeOf(op, op.Range, () =>
        {
            if (op.Name is { } name)
            {
                RequireTableName(name);
            }
        });

    /// <summary>
    /// Excel's table-name rules: 1-255 characters; a letter, '_' or '\' first, then letters,
    /// digits, '.', '_', '\' or '?'; and no text that reads as a cell reference in A1
    /// (<c>T1</c>) or R1C1 (<c>R</c>, <c>C2</c>, <c>R1C1</c>) notation.
    /// </summary>
    private static void RequireTableName(string name)
    {
        const string Hint = "Use a descriptive name such as SalesTable or tbl_Sales.";
        Require(name.Length is >= 1 and <= 255, "the table 'name' must have 1-255 characters", Hint);
        Rune[] runes = name.EnumerateRunes().ToArray();
        Require(
            (Rune.IsLetter(runes[0]) || runes[0].Value is '_' or '\\')
                && runes.Skip(1).All(static rune => Rune.IsLetterOrDigit(rune) || IsCombiningMark(rune) || rune.Value is '.' or '_' or '\\' or '?'),
            $"table name '{name}' must start with a letter, '_' or '\\' and continue with letters, digits, '.', '_', '\\' or '?'",
            Hint);
        Require(!ReadsAsCellReference(name), $"table name '{name}' reads as a cell reference", Hint);

        static bool IsCombiningMark(Rune rune) =>
            Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark;
    }

    private static bool ReadsAsCellReference(string name)
    {
        // R1C1: R or C alone, R<n>, C<n>, R<n>C<n> and their row- or column-only forms.
        int index = 0;
        bool row = TryTake(name, ref index, 'R');
        bool column = TryTake(name, ref index, 'C');
        if ((row || column) && index == name.Length)
        {
            return true;
        }

        // A1: one to three column letters within XFD, then a row number within the sheet.
        int letters = name.TakeWhile(char.IsAsciiLetter).Count();
        string digits = name[letters..];
        return letters is >= 1 and <= 3
            && digits.Length is >= 1 and <= 7
            && digits.All(char.IsAsciiDigit)
            && name[..letters].Aggregate(0, static (column, letter) => column * 26 + char.ToUpperInvariant(letter) - 'A' + 1) <= A1.MaxColumns
            && int.Parse(digits, CultureInfo.InvariantCulture) is >= 1 and <= A1.MaxRows;

        static bool TryTake(string text, ref int position, char marker)
        {
            if (position >= text.Length || char.ToUpperInvariant(text[position]) != marker)
            {
                return false;
            }

            position++;
            while (position < text.Length && char.IsAsciiDigit(text[position]))
            {
                position++;
            }

            return true;
        }
    }

    internal static DefineNameOp ValidateDefineName(DefineNameOp op)
    {
        Require(!string.IsNullOrWhiteSpace(op.Name), "the defined name must not be empty");
        Require(!string.IsNullOrWhiteSpace(op.RefersTo), "'refersTo' must not be empty");
        return op;
    }

    internal static T ValidateComment<T>(T op, string cell, string text)
        where T : Op
    {
        RangeRef at = ParseUnqualifiedRange(cell);
        Require(at.CellCount == 1, "'cell' must be a single cell, e.g. \"B2\"");
        Require(!string.IsNullOrEmpty(text), "'text' must not be empty");
        return op;
    }

    internal static T ValidateCommentCell<T>(T op, string cell)
        where T : Op
    {
        RangeRef at = ParseUnqualifiedRange(cell);
        Require(at.CellCount == 1, "'cell' must be a single cell, e.g. \"B2\"");
        return op;
    }

    internal static ProtectSheetOp ValidateProtect(ProtectSheetOp op)
    {
        foreach (string action in op.Allow ?? [])
        {
            Require(ProtectActions.All.Contains(action),
                $"'allow' items must be one of: {string.Join(", ", ProtectActions.All)}");
        }

        return op;
    }

    private static T ValidateNamed<T>(T op, string name)
        where T : Op
    {
        Require(!string.IsNullOrWhiteSpace(name), "the defined name must not be empty");
        return op;
    }

    internal static SetValidationOp ValidateValidation(SetValidationOp op)
    {
        _ = ParseUnqualifiedRange(op.Range);
        Require(ValidationTypes.All.Contains(op.Type),
            $"'type' must be one of: {string.Join(", ", ValidationTypes.All)}");

        if (op.Type == ValidationTypes.List)
        {
            bool hasItems = op.ListItems is { Count: > 0 };
            bool hasSource = !string.IsNullOrWhiteSpace(op.ListSource);
            Require(hasItems ^ hasSource,
                "a 'list' validation needs exactly one of 'listItems' or 'listSource'");
            if (op.ListItems is { } items)
            {
                // Excel stores the items as one quoted, comma-separated literal of at
                // most 255 characters, with no escape for a comma or a quote.
                const string LongListHint = "Put the items in cells and give 'listSource' instead, e.g. \"Lists!A1:A40\".";
                foreach (string item in items)
                {
                    Require(item.Length > 0 && !item.Contains(',', StringComparison.Ordinal) && !item.Contains('"', StringComparison.Ordinal),
                        $"list item '{item}' must be non-empty and contain no comma or double quote", LongListHint);
                }

                int length = items.Sum(static item => item.Length) + items.Count - 1;
                Require(length <= 255, $"'listItems' joined with commas is {length} characters; Excel allows 255", LongListHint);
            }
        }
        else if (op.Type == ValidationTypes.Custom)
        {
            Require(!string.IsNullOrWhiteSpace(op.Value1),
                "a 'custom' validation needs 'value1' set to a formula");
        }
        else
        {
            Require(op.Operator is not null && ValidationOperators.All.Contains(op.Operator),
                $"'operator' is required and must be one of: {string.Join(", ", ValidationOperators.All)}");
            Require(!string.IsNullOrWhiteSpace(op.Value1), "'value1' is required");
            bool between = op.Operator is ValidationOperators.Between or ValidationOperators.NotBetween;
            Require(!between || !string.IsNullOrWhiteSpace(op.Value2),
                "'between'/'notBetween' need 'value2' as the upper bound");
        }

        return op;
    }

    internal static AddSheetOp ValidateAddSheet(AddSheetOp op)
    {
        Require(!string.IsNullOrWhiteSpace(op.Name), "the sheet name must not be empty");
        Require(op.Position is null or >= 0, "'position' is zero-based and must not be negative");
        return op;
    }

    internal static RenameSheetOp ValidateRenameSheet(RenameSheetOp op)
    {
        ValidateSheetIsNamed(op);
        Require(!string.IsNullOrWhiteSpace(op.To), "the new sheet name must not be empty");
        return op;
    }

    private static T ValidateSheetIsNamed<T>(T op)
        where T : Op
    {
        Require(op.Sheet is not null, "'sheet' is required: name the target sheet explicitly");
        return op;
    }

    internal static MoveSheetOp ValidateMoveSheet(MoveSheetOp op)
    {
        Require(op.Sheet is not null, "'sheet' is required: name the sheet to move explicitly");
        Require(op.Position >= 0, "'position' is a zero-based destination index and must not be negative");
        return op;
    }

    private static T ValidateRangeOf<T>(T op, string range, Action? extra = null)
        where T : Op
    {
        _ = ParseUnqualifiedRange(range);
        extra?.Invoke();
        return op;
    }

    private static T ValidateRowOp<T>(T op, int at, int? count)
        where T : Op
    {
        Require(at >= 1, "'at' is a 1-based row number");
        Require(count is null or >= 1, "'count' must be at least 1");
        return op;
    }

    private static T ValidateColumnOp<T>(T op, string at, int? count)
        where T : Op
    {
        _ = ParseColumn(at);
        Require(count is null or >= 1, "'count' must be at least 1");
        return op;
    }

    internal static T ValidateRowSpan<T>(T op, int from, int? to)
        where T : Op
    {
        Require(from >= 1, "'from' is a 1-based row number");
        Require(to is null || to >= from, "'to' must not be smaller than 'from'");
        return op;
    }

    internal static T ValidateColumnSpan<T>(T op, string from, string? to)
        where T : Op
    {
        int first = ParseColumn(from);
        if (to is { } toLetters)
        {
            Require(ParseColumn(toLetters) >= first, "'to' must not be left of 'from'");
        }

        return op;
    }

    internal static RemoveDuplicatesOp ValidateRemoveDuplicates(RemoveDuplicatesOp op)
    {
        RangeRef range = ParseUnqualifiedRange(op.Range);
        if (op.Columns is { } columns)
        {
            Require(columns.Count > 0, "'columns' must not be an empty array; omit it instead");
            foreach (string column in columns)
            {
                int absolute = ParseColumn(column);
                Require(absolute >= range.Start.Column && absolute <= range.End.Column,
                    $"column '{column}' is outside the range '{op.Range}'");
            }
        }

        return op;
    }

    internal static SetHyperlinkOp ValidateSetHyperlink(SetHyperlinkOp op)
    {
        RangeRef at = ParseUnqualifiedRange(op.Cell);
        Require(at.CellCount == 1, "'cell' must be a single cell, e.g. \"B2\"");

        bool hasUrl = !string.IsNullOrWhiteSpace(op.Url);
        bool hasTarget = !string.IsNullOrWhiteSpace(op.Target);
        Require(hasUrl ^ hasTarget, "give exactly one of 'url' or 'target'");
        if (hasTarget)
        {
            // An internal target is a cell or range, qualified when it lies on another sheet.
            _ = A1.ParseRange(op.Target!);
        }

        return op;
    }

}

internal static class CellsAdvancedOpValidator
{
    internal static UpdateChartOp ValidateUpdateChart(UpdateChartOp op)
    {
        bool hasIndex = op.Index is not null;
        bool hasName = !string.IsNullOrWhiteSpace(op.Name);
        Require(hasIndex ^ hasName, "identify the chart by exactly one of 'index' or 'name'");
        Require(op.Index is null or >= 0, "'index' is zero-based and must not be negative");

        bool anyChange = op.Title is not null || op.DataRange is not null
            || op.Type is not null
            || op.Legend is not null || op.AxisTitles is not null
            || op.SeriesColors is not null || op.DataLabels is not null;
        Require(anyChange,
            "update_chart needs at least one field to change (title, dataRange, type, "
            + "legend, axisTitles, seriesColors or dataLabels)");
        // The orientation is read when a data range is plotted; on its own it would change nothing.
        Require(op.SeriesInRows is null || op.DataRange is not null,
            "'seriesInRows' applies only together with 'dataRange'",
            "Give the chart's data range again with the orientation, e.g. \"dataRange\": \"A1:D5\", \"seriesInRows\": true.");

        Require(op.Type is null || ChartTypes.All.Contains(op.Type),
            $"'type' must be one of: {string.Join(", ", ChartTypes.All)}");
        if (op.DataRange is { } dataRange)
        {
            // As with create_chart, a replacement data range may be sheet-qualified.
            _ = A1.ParseRange(dataRange);
        }

        ValidateChartCosmetics(op, op.Legend, op.AxisTitles, op.SeriesColors, op.DataLabels);
        // Pie + axisTitles is only statically known when the op also sets the
        // type; identifying an existing pie is the engine mapper's job.
        Require(op.Type != ChartTypes.Pie || op.AxisTitles is null,
            "a 'pie' chart has no axes; omit 'axisTitles'");
        return op;
    }

    internal static DeleteChartOp ValidateDeleteChart(DeleteChartOp op)
    {
        bool hasIndex = op.Index is not null;
        bool hasName = !string.IsNullOrWhiteSpace(op.Name);
        Require(hasIndex ^ hasName, "identify the chart by exactly one of 'index' or 'name'");
        Require(op.Index is null or >= 0, "'index' is zero-based and must not be negative");
        return op;
    }

    /// <summary>
    /// The cosmetic fields shared by <c>create_chart</c> and
    /// <c>update_chart</c>. An empty object or empty array is rejected the way
    /// <c>margins</c> is — the caller meant something and got nothing.
    /// </summary>
    internal static void ValidateChartCosmetics(
        Op op,
        ChartLegendData? legend,
        ChartAxisTitlesData? axisTitles,
        IReadOnlyList<string>? seriesColors,
        ChartDataLabelsData? dataLabels)
    {
        if (legend is { } chartLegend)
        {
            Require(chartLegend.Visible is not null || chartLegend.Position is not null,
                "'legend' has no fields set; omit it instead");
            Require(chartLegend.Position is null || LegendPositions.All.Contains(chartLegend.Position),
                $"'legend.position' must be one of: {string.Join(", ", LegendPositions.All)}");
        }

        if (axisTitles is { } titles)
        {
            Require(titles.Category is not null || titles.Value is not null,
                "'axisTitles' has no fields set; omit it instead");
        }

        if (seriesColors is { } colors)
        {
            Require(colors.Count > 0,
                "'seriesColors' must not be an empty array; omit it instead");
            foreach (string color in colors)
            {
                Require(color is not null, "'seriesColors' items must not be null");
                RequireColor(color, "seriesColors");
            }
        }

        if (dataLabels is { } labels)
        {
            Require(labels.Visible is not null || labels.Format is not null,
                "'dataLabels' has no fields set; omit it instead");
        }
    }

    internal static AddConditionalFormatOp ValidateAddConditionalFormat(AddConditionalFormatOp op)
    {
        _ = ParseUnqualifiedRange(op.Range);
        ConditionalRule rule = op.Rule;
        Require(ConditionalRuleKinds.All.Contains(rule.Kind),
            $"'rule.kind' must be one of: {string.Join(", ", ConditionalRuleKinds.All)}");

        switch (rule.Kind)
        {
            case ConditionalRuleKinds.CellValue:
                Require(rule.Operator is not null && ValidationOperators.All.Contains(rule.Operator),
                    $"a 'cellValue' rule needs 'operator', one of: {string.Join(", ", ValidationOperators.All)}");
                Require(!string.IsNullOrWhiteSpace(rule.Value1), "a 'cellValue' rule needs 'value1'");
                bool between = rule.Operator is ValidationOperators.Between or ValidationOperators.NotBetween;
                Require(!between || !string.IsNullOrWhiteSpace(rule.Value2),
                    "'between'/'notBetween' need 'value2'");
                Require(op.Style is not null, "a 'cellValue' rule needs a 'style' to apply");
                break;
            case ConditionalRuleKinds.ColorScale:
                RequireColor(rule.MinColor, "rule.minColor");
                RequireColor(rule.MidColor, "rule.midColor");
                RequireColor(rule.MaxColor, "rule.maxColor");
                Require(rule.MinColor is not null && rule.MaxColor is not null,
                    "a 'colorScale' rule needs 'minColor' and 'maxColor' (add 'midColor' for a 3-point scale)");
                break;
            case ConditionalRuleKinds.DataBar:
                RequireColor(rule.BarColor, "rule.barColor");
                Require(rule.BarColor is not null, "a 'dataBar' rule needs 'barColor'");
                break;
            case ConditionalRuleKinds.Duplicates:
                Require(op.Style is not null, "a 'duplicates' rule needs a 'style' to apply");
                break;
            case ConditionalRuleKinds.Formula:
                Require(!string.IsNullOrWhiteSpace(rule.Value1),
                    "a 'formula' rule needs 'value1' set to a formula",
                    "The formula anchors at the range's top-left cell and shifts per cell; anchor the tested "
                    + "column with '$' to highlight whole rows, e.g. =$F2=\"OVERDUE\" over A2:F100.");
                Require(op.Style is not null, "a 'formula' rule needs a 'style' to apply");
                // The engine wraps a formula without the leading '=' into a
                // string literal that never matches — silent zero matches
                // (probe-verified). Normalize instead of rejecting, exactly as
                // set_validation's listSource and define_name's refersTo do.
                if (!rule.Value1!.StartsWith('='))
                {
                    op = op with { Rule = rule with { Value1 = "=" + rule.Value1 } };
                }

                break;
            case ConditionalRuleKinds.TopBottom:
                Require(
                    rule.Rank is >= 1 and <= 1000 && (rule.Percent is not true || rule.Rank <= 100),
                    "a 'topBottom' rule needs 'rank' (1-1000; 1-100 with 'percent')");
                Require(op.Style is not null, "a 'topBottom' rule needs a 'style' to apply");
                break;
            case ConditionalRuleKinds.IconSet:
                Require(rule.IconSet is not null && IconSetNames.All.Contains(rule.IconSet),
                    $"an 'iconSet' rule needs 'iconSet', one of: {string.Join(", ", IconSetNames.All)}");
                // Icons come from the set; a dxf style would be dead weight the
                // caller believes took effect — reject rather than ignore.
                Require(op.Style is null, "an 'iconSet' rule draws icons; omit 'style'");
                break;
        }

        if (op.Style is { } style)
        {
            ValidateStyle(style, conditional: true);
        }

        return op;
    }

    internal static AddSparklineOp ValidateAddSparkline(AddSparklineOp op)
    {
        // The data range may be sheet-qualified (as create_chart's dataRange):
        // KPI sparklines on a dashboard sheet plot a data sheet. The location
        // stays on the op's sheet.
        _ = A1.ParseRange(op.DataRange);

        RangeRef location = ParseUnqualifiedRange(op.Location);
        Require(location.RowCount == 1 || location.ColumnCount == 1,
            "'location' must be a single cell or a one-row/one-column range, e.g. \"F2\" or \"F2:F10\"");

        Require(op.Type is null || SparklineTypes.All.Contains(op.Type),
            $"'type' must be one of: {string.Join(", ", SparklineTypes.All)}");
        RequireColor(op.Color, "color");
        return op;
    }

    internal static SetBordersOp ValidateSetBorders(SetBordersOp op)
    {
        RangeRef range = ParseUnqualifiedRange(op.Range);
        Require(op.Edges.Count > 0,
            "'edges' must not be empty; use [\"all\"] for a full grid");
        foreach (string edge in op.Edges)
        {
            Require(edge is not null && BorderEdges.All.Contains(edge),
                $"'edges' items must be one of: {string.Join(", ", BorderEdges.All)}");
        }

        Require(op.Style is null || BorderLineStyles.All.Contains(op.Style),
            $"'style' must be one of: {string.Join(", ", BorderLineStyles.All)}");
        RequireColor(op.Color, "color");

        // A single cell has no inner boundaries, so a purely-inside edge set
        // (horizontal/vertical, or inside which expands to them) would apply
        // nothing — a silent no-op the caller reads as success. Reject it with
        // the fix spelled out.
        if (range.CellCount == 1)
        {
            bool hasOuterEdge = op.Edges.Any(static edge => edge
                is not (BorderEdges.Inside or BorderEdges.Horizontal or BorderEdges.Vertical));
            Require(hasOuterEdge,
                $"a single-cell range has no inner boundaries, so edges [{string.Join(", ", op.Edges)}] would draw nothing",
                "Use outline (or top/bottom/left/right) on a single cell, or a multi-cell range for inside borders.");
        }

        return op;
    }

    internal static SetDefaultFontOp ValidateSetDefaultFont(SetDefaultFontOp op)
    {
        string name = op.Name.Trim();
        Require(name.Length > 0, "the font name must not be empty");
        Require(op.Size is null or (>= 1 and <= 409), "'size' must be between 1 and 409 points");
        return op with { Name = name };
    }

    internal static SetTabColorOp ValidateSetTabColor(SetTabColorOp op)
    {
        // A null color is valid: it removes the tab color.
        RequireColor(op.Color, "color");
        return op;
    }

    internal static SetSheetViewOp ValidateSetSheetView(SetSheetViewOp op)
    {
        bool any = op.Gridlines is not null || op.Zoom is not null || op.Headings is not null;
        Require(any,
            "set_sheet_view needs at least one field to change (gridlines, zoom or headings)");

        // The engine's zoom setter silently ignores an out-of-range value and
        // keeps the old zoom (probe-verified), so this check is the only guard
        // between the caller and a silent no-op.
        Require(op.Zoom is null or (>= 10 and <= 400), "'zoom' must be between 10 and 400");
        return op;
    }

}

internal static class CellsValidationSupport
{
    internal static RangeRef ParseUnqualifiedRange(string text)
    {

        RangeSpec spec = A1.ParseRange(text);
        if (spec.SheetName is not null)
        {
            throw new OperationInvalidException($"the range '{text}' must not be sheet-qualified; use the op's 'sheet' field instead");
        }

        return spec.Range;
    }

    internal static int ParseColumn(string letters) => A1.ParseColumn(letters);

    /// <summary>
    /// The style rules shared by <c>format_range</c> and <c>add_conditional_format</c>.
    /// A conditional style is differential and Excel applies only its font emphasis,
    /// colors and number format, so the other fields are rejected there instead of
    /// being stored and silently ignored.
    /// </summary>
    internal static void ValidateStyle(StyleData style, bool conditional)
    {
        Require(style != new StyleData(), "'style' has no fields set; set at least one field");
        if (conditional)
        {
            string[] unsupported = new (string Name, bool Set)[]
                {
                    ("font", style.Font is not null), ("size", style.Size is not null),
                    ("hAlign", style.HAlign is not null), ("vAlign", style.VAlign is not null),
                    ("wrap", style.Wrap is not null), ("indent", style.Indent is not null),
                }
                .Where(static field => field.Set)
                .Select(static field => "'style." + field.Name + "'")
                .ToArray();
            Require(unsupported.Length == 0,
                $"a conditional format cannot set {string.Join(", ", unsupported)}",
                "A conditional style may set bold, italic, underline, strikethrough, color, bg and "
                + "numberFormat; use format_range for fonts, sizes, alignment, wrapping and indents.");
        }

        Require(style.Font is null || !string.IsNullOrWhiteSpace(style.Font), "'style.font' must not be empty");
        Require(style.Size is null or (>= 1 and <= 409), "'style.size' must be between 1 and 409 points");
        RequireColor(style.Color, "style.color");
        RequireColor(style.Bg, "style.bg");
        Require(style.NumberFormat is null || style.NumberFormat.Length > 0, "'style.numberFormat' must not be empty");
        Require(style.HAlign is null || HorizontalAlignments.All.Contains(style.HAlign),
            $"'style.hAlign' must be one of: {string.Join(", ", HorizontalAlignments.All)}");
        Require(style.VAlign is null || VerticalAlignments.All.Contains(style.VAlign),
            $"'style.vAlign' must be one of: {string.Join(", ", VerticalAlignments.All)}");
        Require(style.Indent is null or (>= 0 and <= 250), "'style.indent' must be between 0 and 250");
    }

    internal static void Require(bool condition, string reason, string? hint = null)
    {
        if (!condition)
        {
            throw new OperationInvalidException(reason, hint);
        }
    }


    internal static void RequireColor(string? value, string field)
    {
        if (value is null)
        {
            return;
        }

        bool valid = value.Length == 7 && value[0] == '#'
            && value.Skip(1).All(static c => char.IsAsciiHexDigit(c));
        Require(valid, $"'{field}' must be a #RRGGBB color, e.g. \"#1F4E79\"");
    }
}
