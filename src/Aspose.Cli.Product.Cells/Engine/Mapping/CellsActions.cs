using System.Text.Json.Nodes;
using Aspose.Cells;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// The Cells actions: one name for one engine call, whose parameters the caller states. The object
/// a call is made on is named by its address; a place on the same sheet by its A1 name. An action
/// that changes an object returns its address, which the report reads back from the saved file.
/// </summary>
internal static partial class CellsActions
{
    internal static void Declare(DocumentTable<Workbook> table)
    {
        DeclareWorkbook(table);
        DeclareRowsAndColumns(table);
        DeclareRanges(table);
        DeclareSheets(table);
        DeclareObjects(table);
        table.Action(new TableAction<Workbook>
        {
            Name = "calculate",
            Description = "Workbook.CalculateFormula(): calculates every formula of the workbook; neither opening nor saving a workbook calculates it. Returns nothing.",
            Run = static (w, _, _) =>
            {
                w.CalculateFormula();
                return new ActionResult(null);
            },
        });
        table.Action(new TableAction<Workbook>
        {
            Name = "apply_style",
            Description = "Range.ApplyStyle(Style style, StyleFlag flag), with style a new Workbook.CreateStyle() with the members given set: applies to every cell of the range the members flag names. CellShading applies Pattern, ForegroundColor and BackgroundColor together; NumberFormat applies Number and Custom together. Returns nothing.",
            Arguments =
            [
                Argument.Address("range", CellsTable.RangeKind, "The range whose cells the style applies to; a whole column such as C:C or row such as 5:5 sets that column's or row's style."),
                Argument.Of("style", CellsStyles.Settings, "The members of the new style, by the names of a cell's style rows."),
                Argument.Of("flag", CellsStyleFlags.Kind, "The StyleFlag properties set to true."),
            ],
            Run = static (w, a, _) =>
            {
                Style style = w.CreateStyle();
                a.Get<Action<Style>>("style")(style);
                a.Get<Aspose.Cells.Range>("range").ApplyStyle(style, a.Get<StyleFlag>("flag"));
                return new ActionResult(null);
            },
        });
        table.Action(new TableAction<Workbook>
        {
            Name = "set_shared_formula",
            Description = "Cell.SetSharedFormula(string sharedFormula, int rowNumber, int columnNumber): sets the formula on the area of rowNumber rows and columnNumber columns from the cell, each cell's references moved by its place. Returns the cell's address.",
            Arguments =
            [
                Argument.Address("cell", CellsTable.CellKind, "The area's top left cell, whose formula is sharedFormula."),
                Argument.Of("sharedFormula", ValueKinds.Text, "The formula of the top left cell, such as =A1+B1."),
                Argument.Of("rowNumber", ValueKinds.Integer, "How many rows the area has."),
                Argument.Of("columnNumber", ValueKinds.Integer, "How many columns the area has."),
            ],
            Run = static (_, a, _) =>
            {
                a.Get<Cell>("cell").SetSharedFormula(a.Get<string>("sharedFormula"), a.Get<int>("rowNumber"), a.Get<int>("columnNumber"));
                string at = a.AddressOf("cell");
                return new ActionResult(JsonValue.Create(at), at);
            },
        });
        table.Action(new TableAction<Workbook>
        {
            Name = "set_dynamic_array_formula",
            Description = "Cell.SetDynamicArrayFormula(string arrayFormula, FormulaParseOptions options, bool calculateValue), with new FormulaParseOptions(): sets a formula whose result spills over the cells beside it. Returns the area it spills over, the address of that range.",
            Arguments =
            [
                Argument.Address("cell", CellsTable.CellKind, "The cell the formula is in."),
                Argument.Of("arrayFormula", ValueKinds.Text, "The formula, such as =SORT(A2:A20)."),
                Argument.Of("calculateValue", ValueKinds.Boolean, "Whether the engine calculates the result now; without, the spilled cells hold no value until the calculate action."),
            ],
            Run = static (_, a, _) =>
            {
                Cell cell = a.Get<Cell>("cell");
                CellArea area = cell.SetDynamicArrayFormula(a.Get<string>("arrayFormula"), new FormulaParseOptions(), a.Get<bool>("calculateValue"));
                // The range is on the cell's sheet: its address is the cell's with the range's area in place of the cell's name.
                string address = a.AddressOf("cell");
                string at = $"{address[..address.LastIndexOf("/cell/", StringComparison.Ordinal)]}/range/{CellsValues.A1(area)}";
                return new ActionResult(JsonValue.Create(at), at);
            },
        });
    }
}
