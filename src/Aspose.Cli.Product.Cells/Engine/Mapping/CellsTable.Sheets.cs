using System.Drawing;
using Aspose.Cells;
using Aspose.Cells.Charts;
using Aspose.Cells.Drawing;
using Aspose.Cells.Tables;
using Aspose.Cli.Sdk.Tables;
using Row = Aspose.Cli.Sdk.Tables.Row;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

internal static partial class CellsTable
{
    /// <summary>A defined name, addressed by its <c>Name.FullText</c>: <c>name/Total</c>, <c>name/Sales!Local</c>.</summary>
    internal static readonly ObjectKind<Name> NameKind = new("name", "A defined name: a workbook-level name, or a worksheet-level one, whose full text starts with its sheet's name.")
    {
        Row.Text<Name>("fullText", "Name.FullText", static n => n.FullText),
        Row.Text<Name>("text", "Name.Text", static n => n.Text),
        Row.Text<Name>("refersTo", "Name.RefersTo, such as =Sales!$B$2", static n => n.RefersTo, static (n, v) => n.RefersTo = v),
    };

    /// <summary>A worksheet's page setup: <c>sheet/Sales/pageSetup</c>.</summary>
    internal static readonly ObjectKind<PageSetup> PageSetupKind = new("pageSetup", "A worksheet's page setup.")
    {
        Row.Enum<PageSetup, PageOrientationType>("orientation", "PageSetup.Orientation", static p => p.Orientation, static (p, v) => p.Orientation = v),
        Row.Enum<PageSetup, PaperSizeType>("paperSize", "PageSetup.PaperSize", static p => p.PaperSize, static (p, v) => p.PaperSize = v),
        Row.Integer<PageSetup>("fitToPagesWide", "PageSetup.FitToPagesWide", static p => p.FitToPagesWide, static (p, v) => p.FitToPagesWide = v),
        Row.Integer<PageSetup>("fitToPagesTall", "PageSetup.FitToPagesTall", static p => p.FitToPagesTall, static (p, v) => p.FitToPagesTall = v),
        Row.Integer<PageSetup>("zoom", "PageSetup.Zoom, in percent", static p => p.Zoom, static (p, v) => p.Zoom = v),
        Row.Boolean<PageSetup>("isPercentScale", "PageSetup.IsPercentScale", static p => p.IsPercentScale),
        Row.Number<PageSetup>("topMarginInch", "PageSetup.TopMarginInch, in inches", static p => p.TopMarginInch, static (p, v) => p.TopMarginInch = v),
        Row.Number<PageSetup>("bottomMarginInch", "PageSetup.BottomMarginInch, in inches", static p => p.BottomMarginInch, static (p, v) => p.BottomMarginInch = v),
        Row.Number<PageSetup>("leftMarginInch", "PageSetup.LeftMarginInch, in inches", static p => p.LeftMarginInch, static (p, v) => p.LeftMarginInch = v),
        Row.Number<PageSetup>("rightMarginInch", "PageSetup.RightMarginInch, in inches", static p => p.RightMarginInch, static (p, v) => p.RightMarginInch = v),
        Row.Number<PageSetup>("headerMarginInch", "PageSetup.HeaderMarginInch, in inches", static p => p.HeaderMarginInch, static (p, v) => p.HeaderMarginInch = v),
        Row.Number<PageSetup>("footerMarginInch", "PageSetup.FooterMarginInch, in inches", static p => p.FooterMarginInch, static (p, v) => p.FooterMarginInch = v),
        HeaderFooter("header.left", 0, header: true),
        HeaderFooter("header.center", 1, header: true),
        HeaderFooter("header.right", 2, header: true),
        HeaderFooter("footer.left", 0, header: false),
        HeaderFooter("footer.center", 1, header: false),
        HeaderFooter("footer.right", 2, header: false),
        Row.Enum<PageSetup, PrintCommentsType>("printComments", "PageSetup.PrintComments", static p => p.PrintComments, static (p, v) => p.PrintComments = v),
        Row.Text<PageSetup>("printArea", "PageSetup.PrintArea, such as A1:F20; null without one", static p => p.PrintArea, static (p, v) => p.PrintArea = v),
        Row.Text<PageSetup>("printTitleRows", "PageSetup.PrintTitleRows, such as $1:$1", static p => p.PrintTitleRows, static (p, v) => p.PrintTitleRows = v),
        Row.Text<PageSetup>("printTitleColumns", "PageSetup.PrintTitleColumns, such as $A:$A", static p => p.PrintTitleColumns, static (p, v) => p.PrintTitleColumns = v),
    };

    /// <summary>A worksheet's protection settings: <c>sheet/Sales/protection</c>.</summary>
    internal static readonly ObjectKind<Protection> ProtectionKind = new("protection", "What a worksheet's protection allows; the protect_sheet and unprotect_sheet actions protect it.")
    {
        Row.Boolean<Protection>("allowFormattingCell", "Protection.AllowFormattingCell", static p => p.AllowFormattingCell, static (p, v) => p.AllowFormattingCell = v),
        Row.Boolean<Protection>("allowInsertingRow", "Protection.AllowInsertingRow", static p => p.AllowInsertingRow, static (p, v) => p.AllowInsertingRow = v),
        Row.Boolean<Protection>("allowInsertingColumn", "Protection.AllowInsertingColumn", static p => p.AllowInsertingColumn, static (p, v) => p.AllowInsertingColumn = v),
        Row.Boolean<Protection>("allowDeletingRow", "Protection.AllowDeletingRow", static p => p.AllowDeletingRow, static (p, v) => p.AllowDeletingRow = v),
        Row.Boolean<Protection>("allowDeletingColumn", "Protection.AllowDeletingColumn", static p => p.AllowDeletingColumn, static (p, v) => p.AllowDeletingColumn = v),
        Row.Boolean<Protection>("allowSorting", "Protection.AllowSorting", static p => p.AllowSorting, static (p, v) => p.AllowSorting = v),
        Row.Boolean<Protection>("allowFiltering", "Protection.AllowFiltering", static p => p.AllowFiltering, static (p, v) => p.AllowFiltering = v),
        Row.Boolean<Protection>("isProtectedWithPassword", "Protection.IsProtectedWithPassword", static p => p.IsProtectedWithPassword),
    };

    /// <summary>A row of a worksheet, addressed by its number as A1 writes it: <c>sheet/Sales/row/6</c>.</summary>
    internal static readonly ObjectKind<Aspose.Cells.Row> RowKind = new("row", "A row of a worksheet. Every row of the grid can be addressed; read lists the rows the engine holds.")
    {
        Row.Number<Aspose.Cells.Row>("height", "Row.Height, in points", static r => r.Height, static (r, v) => r.Height = v),
        Row.Boolean<Aspose.Cells.Row>("isHidden", "Row.IsHidden", static r => r.IsHidden, static (r, v) => r.IsHidden = v),
        Row.Integer<Aspose.Cells.Row>("groupLevel", "Row.GroupLevel; the group_rows and ungroup_rows actions change it", static r => r.GroupLevel),
        Row.Boolean<Aspose.Cells.Row>("isCollapsed", "Row.IsCollapsed", static r => r.IsCollapsed),
    };

    /// <summary>A column of a worksheet, addressed by its letters as A1 writes them: <c>sheet/Sales/column/C</c>.</summary>
    internal static readonly ObjectKind<Column> ColumnKind = new("column", "A column of a worksheet. Every column of the grid can be addressed; read lists the columns the engine holds.")
    {
        Row.Number<Column>("width", "Column.Width, in characters of the workbook's default font", static c => c.Width, static (c, v) => c.Width = v),
        Row.Boolean<Column>("isHidden", "Column.IsHidden", static c => c.IsHidden, static (c, v) => c.IsHidden = v),
        Row.Integer<Column>("groupLevel", "Column.GroupLevel; the group_columns and ungroup_columns actions change it", static c => c.GroupLevel),
        Row.Boolean<Column>("isCollapsed", "Column.IsCollapsed", static c => c.IsCollapsed),
    };

    /// <summary>A merged area, addressed by its A1 area: <c>sheet/Sales/merge/A1:C1</c>.</summary>
    internal static readonly ObjectKind<Aspose.Cells.Range> MergeKind = new("merge", "A merged area of a worksheet, one of Cells.GetMergedAreas(); its address is its area.")
    {
    };

    /// <summary>A comment, addressed by its cell's A1 name: <c>sheet/Sales/comment/B2</c>.</summary>
    internal static readonly ObjectKind<Comment> CommentKind = new("comment", "A comment of a cell.")
    {
        Row.Text<Comment>("note", "Comment.Note", static c => c.Note, static (c, v) => c.Note = v),
        Row.Text<Comment>("author", "Comment.Author", static c => c.Author, static (c, v) => c.Author = v),
        Row.Boolean<Comment>("isThreadedComment", "Comment.IsThreadedComment", static c => c.IsThreadedComment),
    };

    /// <summary>A hyperlink, addressed by its position in Worksheet.Hyperlinks: <c>sheet/Sales/hyperlink/0</c>.</summary>
    internal static readonly ObjectKind<Hyperlink> HyperlinkKind = new("hyperlink", "A hyperlink of a worksheet.")
    {
        Row.Text<Hyperlink>("address", "Hyperlink.Address", static h => h.Address, static (h, v) => h.Address = v),
        Row.Text<Hyperlink>("textToDisplay", "Hyperlink.TextToDisplay", static h => h.TextToDisplay, static (h, v) => h.TextToDisplay = v),
        Row.Text<Hyperlink>("screenTip", "Hyperlink.ScreenTip", static h => h.ScreenTip, static (h, v) => h.ScreenTip = v),
        Row.Text<Hyperlink>("area", "Hyperlink.Area, as A1 writes it", static h => CellsValues.A1(h.Area)),
        Row.Enum<Hyperlink, TargetModeType>("linkType", "Hyperlink.LinkType", static h => h.LinkType),
    };

    /// <summary>A data validation, addressed by its position in Worksheet.Validations: <c>sheet/Sales/validation/0</c>.</summary>
    internal static readonly ObjectKind<Validation> ValidationKind = new("validation", "A data validation of a worksheet.")
    {
        Row.Enum<Validation, ValidationType>("type", "Validation.Type", static v => v.Type, static (v, x) => v.Type = x),
        Row.Enum<Validation, OperatorType>("operator", "Validation.Operator", static v => v.Operator, static (v, x) => v.Operator = x),
        Row.Text<Validation>("formula1", "Validation.Formula1, such as =$A$1:$A$9 or East,West for a list", static v => v.Formula1, static (v, x) => v.Formula1 = x),
        Row.Text<Validation>("formula2", "Validation.Formula2", static v => v.Formula2, static (v, x) => v.Formula2 = x),
        Row.Boolean<Validation>("ignoreBlank", "Validation.IgnoreBlank", static v => v.IgnoreBlank, static (v, x) => v.IgnoreBlank = x),
        Row.Boolean<Validation>("inCellDropDown", "Validation.InCellDropDown", static v => v.InCellDropDown, static (v, x) => v.InCellDropDown = x),
        Row.Boolean<Validation>("showInput", "Validation.ShowInput", static v => v.ShowInput, static (v, x) => v.ShowInput = x),
        Row.Text<Validation>("inputMessage", "Validation.InputMessage", static v => v.InputMessage, static (v, x) => v.InputMessage = x),
        Row.Boolean<Validation>("showError", "Validation.ShowError", static v => v.ShowError, static (v, x) => v.ShowError = x),
        Row.Text<Validation>("errorMessage", "Validation.ErrorMessage", static v => v.ErrorMessage, static (v, x) => v.ErrorMessage = x),
        Row.Of<Validation, IReadOnlyList<string?>>("areas", ValueKinds.ListOf(ValueKinds.Text), "Validation.Areas, each as A1 writes it",
            static v => [.. v.Areas.Select(CellsValues.A1)]),
    };

    /// <summary>A conditional formatting, addressed by its position: <c>sheet/Sales/conditionalFormatting/0</c>.</summary>
    internal static readonly ObjectKind<FormatConditionCollection> ConditionalFormattingKind = new("conditionalFormatting", "A conditional formatting of a worksheet: its areas and its rules.")
    {
        Row.Of<FormatConditionCollection, IReadOnlyList<string?>>("areas", ValueKinds.ListOf(ValueKinds.Text), "FormatConditionCollection.GetCellArea(i) for each of its RangeCount areas, as A1 writes it",
            static f => [.. Enumerable.Range(0, f.RangeCount).Select(index => CellsValues.A1(f.GetCellArea(index)))]),
    };

    /// <summary>A rule of a conditional formatting, addressed by its position: <c>…/conditionalFormatting/0/rule/0</c>.</summary>
    internal static readonly ObjectKind<FormatCondition> RuleKind = new("rule", "A rule of a conditional formatting.")
    {
        Row.Enum<FormatCondition, FormatConditionType>("type", "FormatCondition.Type", static c => c.Type),
        Row.Enum<FormatCondition, OperatorType>("operator", "FormatCondition.Operator", static c => c.Operator, static (c, v) => c.Operator = v),
        Row.Text<FormatCondition>("formula1", "FormatCondition.Formula1", static c => c.Formula1, static (c, v) => c.Formula1 = v),
        Row.Text<FormatCondition>("formula2", "FormatCondition.Formula2", static c => c.Formula2, static (c, v) => c.Formula2 = v),
        Row.Color<FormatCondition>("colorScale.minColor", "FormatCondition.ColorScale.MinColor", static c => c.ColorScale.MinColor, static (c, v) => c.ColorScale.MinColor = v),
        Row.Color<FormatCondition>("colorScale.midColor", "FormatCondition.ColorScale.MidColor", static c => c.ColorScale.MidColor, static (c, v) => c.ColorScale.MidColor = v),
        Row.Color<FormatCondition>("colorScale.maxColor", "FormatCondition.ColorScale.MaxColor", static c => c.ColorScale.MaxColor, static (c, v) => c.ColorScale.MaxColor = v),
        Row.Boolean<FormatCondition>("colorScale.is3ColorScale", "FormatCondition.ColorScale.Is3ColorScale", static c => c.ColorScale.Is3ColorScale, static (c, v) => c.ColorScale.Is3ColorScale = v),
        Row.Color<FormatCondition>("dataBar.color", "FormatCondition.DataBar.Color", static c => c.DataBar.Color, static (c, v) => c.DataBar.Color = v),
        Row.Integer<FormatCondition>("top10.rank", "FormatCondition.Top10.Rank", static c => c.Top10.Rank, static (c, v) => c.Top10.Rank = v),
        Row.Boolean<FormatCondition>("top10.isPercent", "FormatCondition.Top10.IsPercent", static c => c.Top10.IsPercent, static (c, v) => c.Top10.IsPercent = v),
        Row.Boolean<FormatCondition>("top10.isBottom", "FormatCondition.Top10.IsBottom", static c => c.Top10.IsBottom, static (c, v) => c.Top10.IsBottom = v),
        Row.Enum<FormatCondition, IconSetType>("iconSet.type", "FormatCondition.IconSet.Type", static c => c.IconSet.Type, static (c, v) => c.IconSet.Type = v),
        RuleStyle("font.isBold", ValueKinds.Boolean, "Font.IsBold", static s => s.Font.IsBold, static (s, v) => s.Font.IsBold = v),
        RuleStyle("font.isItalic", ValueKinds.Boolean, "Font.IsItalic", static s => s.Font.IsItalic, static (s, v) => s.Font.IsItalic = v),
        RuleStyle("font.isStrikeout", ValueKinds.Boolean, "Font.IsStrikeout", static s => s.Font.IsStrikeout, static (s, v) => s.Font.IsStrikeout = v),
        RuleStyle("font.underline", ValueKinds.Enum<FontUnderlineType>(), "Font.Underline", static s => s.Font.Underline, static (s, v) => s.Font.Underline = v),
        RuleStyle("font.color", ValueKinds.Color, "Font.Color", static s => s.Font.Color, static (s, v) => s.Font.Color = v),
        RuleStyle("foregroundColor", ValueKinds.Color, "ForegroundColor", static s => s.ForegroundColor, static (s, v) => s.ForegroundColor = v),
        RuleStyle("backgroundColor", ValueKinds.Color, "BackgroundColor", static s => s.BackgroundColor, static (s, v) => s.BackgroundColor = v),
        RuleStyle("pattern", ValueKinds.Enum<BackgroundType>(), "Pattern", static s => s.Pattern, static (s, v) => s.Pattern = v),
        RuleStyle("custom", ValueKinds.Text, "Custom", static s => s.Custom, static (s, v) => s.Custom = v),
    };

    /// <summary>A table, addressed by its position in Worksheet.ListObjects: <c>sheet/Sales/table/0</c>.</summary>
    internal static readonly ObjectKind<ListObject> TableKind = new("table", "A table (ListObject) of a worksheet.")
    {
        Row.Text<ListObject>("displayName", "ListObject.DisplayName", static t => t.DisplayName, static (t, v) => t.DisplayName = v),
        Row.Text<ListObject>("area", "ListObject.StartRow, StartColumn, EndRow and EndColumn, as A1 writes the area; the resize_table action changes it",
            static t => CellsValues.A1(CellArea.CreateCellArea(t.StartRow, t.StartColumn, t.EndRow, t.EndColumn))),
        Row.Enum<ListObject, TableStyleType>("tableStyleType", "ListObject.TableStyleType", static t => t.TableStyleType, static (t, v) => t.TableStyleType = v),
        Row.Text<ListObject>("tableStyleName", "ListObject.TableStyleName", static t => t.TableStyleName, static (t, v) => t.TableStyleName = v),
        Row.Boolean<ListObject>("showHeaderRow", "ListObject.ShowHeaderRow", static t => t.ShowHeaderRow),
        Row.Boolean<ListObject>("showTotals", "ListObject.ShowTotals", static t => t.ShowTotals, static (t, v) => t.ShowTotals = v),
    };

    /// <summary>A picture, addressed by its shape id: <c>sheet/Sales/picture/2</c>.</summary>
    internal static readonly ObjectKind<Picture> PictureKind = new("picture", "A picture of a worksheet.")
    {
        Row.Text<Picture>("upperLeftCell", "Picture.UpperLeftRow and UpperLeftColumn, as A1 writes the cell", static p => CellsHelper.CellIndexToName(p.UpperLeftRow, p.UpperLeftColumn)),
        Row.Number<Picture>("widthPt", "Picture.WidthPt, in points", static p => p.WidthPt, static (p, v) => p.WidthPt = v),
        Row.Number<Picture>("heightPt", "Picture.HeightPt, in points", static p => p.HeightPt, static (p, v) => p.HeightPt = v),
    };

    /// <summary>A sparkline group, addressed by its position: <c>sheet/Sales/sparklineGroup/0</c>.</summary>
    internal static readonly ObjectKind<SparklineGroup> SparklineGroupKind = new("sparklineGroup", "A group of sparklines of a worksheet.")
    {
        Row.Enum<SparklineGroup, SparklineType>("type", "SparklineGroup.Type", static g => g.Type, static (g, v) => g.Type = v),
        Row.Of<SparklineGroup, Color?>("seriesColor", ValueKinds.Optional(ValueKinds.Color), "SparklineGroup.SeriesColor.Color",
            static g => g.SeriesColor?.Color,
            static (g, v) => (g.SeriesColor ?? throw TableErrors.Absent("SparklineGroup.SeriesColor")).Color = v ?? Color.Empty),
        Row.Of<SparklineGroup, IReadOnlyList<string?>>("sparklines", ValueKinds.ListOf(ValueKinds.Text), "SparklineGroup.Sparklines: each sparkline's cell, as A1 writes it, and its DataRange, written cell=dataRange",
            static g => [.. g.Sparklines.Cast<Sparkline>().Select(static line => $"{CellsHelper.CellIndexToName(line.Row, line.Column)}={line.DataRange}")]),
    };

    private static Row<PageSetup> HeaderFooter(string name, int section, bool header) => Row.Text<PageSetup>(
        name,
        $"PageSetup.{(header ? "GetHeader" : "GetFooter")}({section}), set with {(header ? "SetHeader" : "SetFooter")}({section}, script)",
        p => header ? p.GetHeader(section) : p.GetFooter(section),
        (p, v) =>
        {
            if (header)
            {
                p.SetHeader(section, v);
            }
            else
            {
                p.SetFooter(section, v);
            }
        });

    /// <summary>A row of a rule's style, <c>style.&lt;name&gt;</c>: read on FormatCondition.Style, set on it, which is then set back.</summary>
    private static Row<FormatCondition> RuleStyle<T>(string name, ValueKind<T> kind, string member, Func<Style, T> get, Action<Style, T> set) =>
        Row.Of<FormatCondition, T>(
            $"style.{name}",
            kind,
            $"FormatCondition.Style.{member}; set on the Style, which is then set back",
            c => get(c.Style),
            (c, v) =>
            {
                Style style = c.Style;
                set(style, v);
                c.Style = style;
            });

    private static void PlaceSheetObjects(DocumentTable<Workbook> table)
    {
        table.Place(WorkbookKind, NameKind, new Locator<Workbook, Name>
        {
            Segment = "name",
            Key = [KeyPart.Text],
            Find = static (w, key) => w.Worksheets.Names[key.Name()],
            List = static w => w.Worksheets.Names.Cast<Name>(),
            KeyOf = static (_, name) => Key.Of(name.FullText),
            Delete = static (w, name) => w.Worksheets.Names.Remove(name.FullText),
        });
        table.Adds(WorkbookKind, NameKind, new Adder<Workbook, Name>
        {
            Name = "name",
            Description = "NameCollection.Add(string text); the text is the name, or the sheet's name, !, and the name for a worksheet-level one",
            Arguments = [Argument.Of("text", ValueKinds.Text, "The name's text, such as Total or Sales!Local.")],
            Create = static (w, a) => w.Worksheets.Names[w.Worksheets.Names.Add(a.Get<string>("text"))],
        });
        table.Place(SheetKind, PageSetupKind, Single<Worksheet, PageSetup>("pageSetup", static s => s.PageSetup));
        table.Place(SheetKind, ProtectionKind, Single<Worksheet, Protection>("protection", static s => s.Protection));
        table.Place(SheetKind, RowKind, new Locator<Worksheet, Aspose.Cells.Row>
        {
            Segment = "row",
            Key = [KeyPart.RowName],
            Find = static (s, key) => s.Cells.Rows[CellsHelper.RowNameToIndex(key.Name())],
            List = static s => s.Cells.Rows.Cast<Aspose.Cells.Row>(),
            KeyOf = static (_, row) => Key.Of(CellsHelper.RowIndexToName(row.Index)),
            ListedWhenNamed = true,
        });
        table.Place(SheetKind, ColumnKind, new Locator<Worksheet, Column>
        {
            Segment = "column",
            Key = [KeyPart.ColumnName],
            Find = static (s, key) => s.Cells.Columns[CellsHelper.ColumnNameToIndex(key.Name())],
            List = static s => Enumerable.Range(0, s.Cells.Columns.Count).Select(index => s.Cells.Columns.GetColumnByIndex(index)),
            KeyOf = static (_, column) => Key.Of(CellsHelper.ColumnIndexToName(column.Index)),
            ListedWhenNamed = true,
        });
        table.Place(SheetKind, MergeKind, new Locator<Worksheet, Aspose.Cells.Range>
        {
            Segment = "merge",
            Key = [KeyPart.Area],
            Find = static (s, key) =>
            {
                CellArea area = CellsValues.CellArea(s, key.Name());
                return s.Cells.GetMergedAreas().Any(merged => merged.StartRow == area.StartRow && merged.StartColumn == area.StartColumn
                        && merged.EndRow == area.EndRow && merged.EndColumn == area.EndColumn)
                    ? Merged(s, area)
                    : null;
            },
            List = static s => s.Cells.GetMergedAreas().Select(area => Merged(s, area)),
            KeyOf = static (_, range) => Key.Of(range.Address),
            Delete = static (s, range) => s.Cells.UnMerge(range.FirstRow, range.FirstColumn, range.RowCount, range.ColumnCount),
        });
        table.Adds(SheetKind, MergeKind, new Adder<Worksheet, Aspose.Cells.Range>
        {
            Name = "merge",
            Description = "Cells.Merge(int firstRow, int firstColumn, int totalRows, int totalColumns), with the area's first row and column and its numbers of rows and columns",
            Arguments = [Argument.Of("area", CellsValues.Area, "The area to merge, such as A1:C1.")],
            Create = static (s, a) =>
            {
                CellArea area = CellsValues.CellArea(s, a.Get<string>("area"));
                s.Cells.Merge(area.StartRow, area.StartColumn, area.EndRow - area.StartRow + 1, area.EndColumn - area.StartColumn + 1);
                return Merged(s, area);
            },
        });
        table.Place(SheetKind, CommentKind, new Locator<Worksheet, Comment>
        {
            Segment = "comment",
            Key = [KeyPart.CellName],
            Find = static (s, key) => s.Comments[key.Name()],
            List = static s => s.Comments.Cast<Comment>(),
            KeyOf = static (_, comment) => Key.Of(CellsHelper.CellIndexToName(comment.Row, comment.Column)),
            Delete = static (s, comment) => s.Comments.RemoveAt(comment.Row, comment.Column),
        });
        table.Adds(SheetKind, CommentKind, new Adder<Worksheet, Comment>
        {
            Name = "comment",
            Description = "CommentCollection.Add(string cellName); on a cell that has a comment the engine returns that comment",
            Arguments = [Argument.Of("cellName", CellsValues.CellName, "The cell, such as B2.")],
            Create = static (s, a) => s.Comments[s.Comments.Add(a.Get<string>("cellName"))],
        });
        table.Place(SheetKind, HyperlinkKind, Positions<Worksheet, Hyperlink>("hyperlink", static s => s.Hyperlinks.Cast<Hyperlink>(), static (s, index) => s.Hyperlinks[index], static (s, index) => s.Hyperlinks.RemoveAt(index)));
        table.Adds(SheetKind, HyperlinkKind, new Adder<Worksheet, Hyperlink>
        {
            Name = "hyperlink",
            Description = "HyperlinkCollection.Add(string cellName, int totalRows, int totalColumns, string address)",
            Arguments =
            [
                Argument.Of("cellName", CellsValues.CellName, "The area's top left cell, such as B2."),
                Argument.Of("totalRows", ValueKinds.Integer, "How many rows the area has."),
                Argument.Of("totalColumns", ValueKinds.Integer, "How many columns the area has."),
                Argument.Of("address", ValueKinds.Text, "The address the link opens, such as https://example.com or Sheet2!A1."),
            ],
            Create = static (s, a) => s.Hyperlinks[s.Hyperlinks.Add(a.Get<string>("cellName"), a.Get<int>("totalRows"), a.Get<int>("totalColumns"), a.Get<string>("address"))],
        });
        table.Place(SheetKind, ValidationKind, Positions<Worksheet, Validation>("validation", static s => s.Validations.Cast<Validation>(), static (s, index) => s.Validations[index], static (s, index) => s.Validations.RemoveAt(index)));
        table.Adds(SheetKind, ValidationKind, new Adder<Worksheet, Validation>
        {
            Name = "validation",
            Description = "ValidationCollection.Add(CellArea ca)",
            Arguments = [Argument.Of("area", CellsValues.Area, "The area it validates, such as A2:A20.")],
            Create = static (s, a) => s.Validations[s.Validations.Add(CellsValues.CellArea(s, a.Get<string>("area")))],
        });
        table.Place(SheetKind, ConditionalFormattingKind, Positions<Worksheet, FormatConditionCollection>("conditionalFormatting",
            static s => Enumerable.Range(0, s.ConditionalFormattings.Count).Select(index => s.ConditionalFormattings[index]),
            static (s, index) => s.ConditionalFormattings[index], static (s, index) => s.ConditionalFormattings.RemoveAt(index)));
        table.Adds(SheetKind, ConditionalFormattingKind, new Adder<Worksheet, FormatConditionCollection>
        {
            Name = "conditionalFormatting",
            Description = "ConditionalFormattingCollection.Add(): a conditional formatting with no area and no rule; add_conditional_format_area gives it an area",
            Create = static (s, _) => s.ConditionalFormattings[s.ConditionalFormattings.Add()],
        });
        table.Place(ConditionalFormattingKind, RuleKind, Positions<FormatConditionCollection, FormatCondition>("rule",
            static f => Enumerable.Range(0, f.Count).Select(index => f[index]),
            static (f, index) => f[index], static (f, index) => f.RemoveCondition(index)));
        table.Adds(ConditionalFormattingKind, RuleKind, new Adder<FormatConditionCollection, FormatCondition>
        {
            Name = "rule",
            Description = "FormatConditionCollection.AddCondition(FormatConditionType type), or AddCondition(type, OperatorType operatorType, string formula1, string formula2) when operatorType is given",
            Arguments =
            [
                Argument.Of("type", ValueKinds.Enum<FormatConditionType>(), "The engine's FormatConditionType."),
                Argument.Optional("operatorType", ValueKinds.Enum<OperatorType>(), "The engine's OperatorType; with it the four-parameter overload is called."),
                Argument.Optional("formula1", ValueKinds.Text, "With operatorType: the first formula, such as =100; null when not given."),
                Argument.Optional("formula2", ValueKinds.Text, "With operatorType: the second formula, for Between and NotBetween; null when not given."),
            ],
            Create = static (f, a) =>
            {
                if (!a.Has("operatorType") && (a.Has("formula1") || a.Has("formula2")))
                {
                    throw TableErrors.InvalidArguments("the rule add takes args.formula1 and args.formula2 with args.operatorType");
                }

                int index = a.Has("operatorType")
                    ? f.AddCondition(a.Get<FormatConditionType>("type"), a.Get<OperatorType>("operatorType"),
                        a.Has("formula1") ? a.Get<string>("formula1") : null!, a.Has("formula2") ? a.Get<string>("formula2") : null!)
                    : f.AddCondition(a.Get<FormatConditionType>("type"));
                return f[index];
            },
        });
        table.Place(SheetKind, TableKind, Positions<Worksheet, ListObject>("table", static s => s.ListObjects.Cast<ListObject>(), static (s, index) => s.ListObjects[index], static (s, index) => s.ListObjects.RemoveAt(index)));
        table.Adds(SheetKind, TableKind, new Adder<Worksheet, ListObject>
        {
            Name = "table",
            Description = "ListObjectCollection.Add(string startCell, string endCell, bool hasHeaders)",
            Arguments =
            [
                Argument.Of("startCell", CellsValues.CellName, "The table's top left cell, such as A1."),
                Argument.Of("endCell", CellsValues.CellName, "The table's bottom right cell, such as F20."),
                Argument.Of("hasHeaders", ValueKinds.Boolean, "Whether the first row is the header row."),
            ],
            Create = static (s, a) => s.ListObjects[s.ListObjects.Add(a.Get<string>("startCell"), a.Get<string>("endCell"), a.Get<bool>("hasHeaders"))],
        });
        table.Place(SheetKind, PictureKind, new Locator<Worksheet, Picture>
        {
            Segment = "picture",
            Key = [KeyPart.Id],
            Find = static (s, key) => s.Pictures.Cast<Picture>().FirstOrDefault(picture => (uint)picture.Id == key.Id()),
            List = static s => s.Pictures.Cast<Picture>(),
            KeyOf = static (_, picture) => Key.Of((uint)picture.Id),
            Delete = static (s, picture) => s.Pictures.RemoveAt(IndexOf(s.Pictures.Cast<Picture>(), picture)),
        });
        table.Adds(SheetKind, PictureKind, new Adder<Worksheet, Picture>
        {
            Name = "picture",
            Description = "PictureCollection.Add(int upperLeftRow, int upperLeftColumn, Stream stream), with the cell's row and column",
            Arguments =
            [
                Argument.Of("upperLeftCell", CellsValues.CellName, "The cell the picture's top left corner is in, such as H2."),
                Argument.Of("image", ValueKinds.Image, "The image file."),
            ],
            Create = static (s, a) =>
            {
                CellsHelper.CellNameToIndex(a.Get<string>("upperLeftCell"), out int row, out int column);
                using var stream = new MemoryStream(a.Get<byte[]>("image"), writable: false);
                return s.Pictures[s.Pictures.Add(row, column, stream)];
            },
        });
        table.Place(SheetKind, SparklineGroupKind, Positions<Worksheet, SparklineGroup>("sparklineGroup",
            static s => Enumerable.Range(0, s.SparklineGroups.Count).Select(index => s.SparklineGroups[index]),
            static (s, index) => s.SparklineGroups[index], static (s, index) => s.SparklineGroups.RemoveAt(index)));
        table.Adds(SheetKind, SparklineGroupKind, new Adder<Worksheet, SparklineGroup>
        {
            Name = "sparklineGroup",
            Description = "SparklineGroupCollection.Add(SparklineType type, string dataRange, bool isVertical, CellArea locationRange)",
            Arguments =
            [
                Argument.Of("type", ValueKinds.Enum<SparklineType>(), "The engine's SparklineType."),
                Argument.Of("dataRange", ValueKinds.Text, "The data, such as Sales!B2:E5."),
                Argument.Of("isVertical", ValueKinds.Boolean, "Whether each sparkline takes a column of the data rather than a row."),
                Argument.Of("locationRange", CellsValues.Area, "The cells the sparklines are drawn in, such as F2:F5."),
            ],
            Create = static (s, a) => s.SparklineGroups[s.SparklineGroups.Add(a.Get<SparklineType>("type"), a.Get<string>("dataRange"), a.Get<bool>("isVertical"),
                CellsValues.CellArea(s, a.Get<string>("locationRange")))],
        });
    }

    /// <summary>The range of a merged area.</summary>
    private static Aspose.Cells.Range Merged(Worksheet sheet, CellArea area) =>
        sheet.Cells.CreateRange(area.StartRow, area.StartColumn, area.EndRow - area.StartRow + 1, area.EndColumn - area.StartColumn + 1);

    /// <summary>The one object of its kind below a parent, such as a sheet's page setup.</summary>
    private static Locator<TParent, T> Single<TParent, T>(string segment, Func<TParent, T> member)
        where TParent : class
        where T : class => new()
        {
            Segment = segment,
            Find = (parent, _) => member(parent),
            List = parent => [member(parent)],
            KeyOf = static (_, _) => Key.None,
        };

    /// <summary>Objects addressed by their position, from 0, in an engine collection.</summary>
    private static Locator<TParent, T> Positions<TParent, T>(string segment, Func<TParent, IEnumerable<T>> list, Func<TParent, int, T> at, Action<TParent, int> removeAt)
        where TParent : class
        where T : class => new()
        {
            Segment = segment,
            Key = [KeyPart.Index],
            Find = (parent, key) => key.Index() < list(parent).Count() ? at(parent, key.Index()) : null,
            List = list,
            KeyOf = (parent, item) => Key.Of(IndexOf(list(parent), item)),
            Delete = (parent, item) => removeAt(parent, IndexOf(list(parent), item)),
        };

    /// <summary>The position of an object in a collection, by reference.</summary>
    private static int IndexOf<T>(IEnumerable<T> items, T item)
        where T : class =>
        items.Select(static (candidate, index) => (candidate, index)).First(found => ReferenceEquals(found.candidate, item)).index;
}
