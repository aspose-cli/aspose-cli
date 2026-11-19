using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cells;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>The actions on a worksheet's rows, columns and ranges.</summary>
internal static partial class CellsActions
{
    private static readonly Argument Sheet = Argument.Address("sheet", CellsTable.SheetKind, "The worksheet.");

    private static readonly Argument UpdateReference = Argument.Optional("updateReference", ValueKinds.Boolean, "UpdateReference of the options: whether references to the moved cells are updated.");

    /// <summary>The sort keys: <c>[{"column": "B", "order": "Ascending"}]</c>.</summary>
    private static readonly ValueKind<IReadOnlyList<(string Column, SortOrder Order)>> SortKeys = ValueKinds.Create<IReadOnlyList<(string Column, SortOrder Order)>>(
        "sort-keys",
        "A list of keys, each {\"column\": the column's letters as A1 writes them, \"order\": a member name of the engine's SortOrder}.",
        static () => new JsonObject
        {
            ["type"] = "array",
            ["minItems"] = 1,
            ["items"] = new JsonObject
            {
                ["type"] = "object",
                ["required"] = new JsonArray("column", "order"),
                ["additionalProperties"] = false,
                ["properties"] = new JsonObject { ["column"] = new JsonObject { ["type"] = "string" }, ["order"] = new JsonObject { ["type"] = "string" } },
            },
        },
        static _ => null,
        static value =>
        {
            if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() == 0)
            {
                throw ValueKinds.Expected("a list of one or more keys", value);
            }

            ValueKind<SortOrder> order = ValueKinds.Enum<SortOrder>();
            return [.. value.EnumerateArray().Select(key => key.ValueKind == JsonValueKind.Object && key.EnumerateObject().Count() == 2
                && key.TryGetProperty("column", out JsonElement column) && key.TryGetProperty("order", out JsonElement sorted)
                    ? (CellsValues.ColumnName.Decode(column), order.Decode(sorted))
                    : throw ValueKinds.Expected("a key with column and order", key))];
        });

    private static void DeclareRowsAndColumns(DocumentTable<Workbook> table)
    {
        table.Action(new TableAction<Workbook>
        {
            Name = "insert_rows",
            Description = "Cells.InsertRows(int rowIndex, int totalRows, InsertOptions options), with new InsertOptions() and updateReference set on it when given, the row's index from its number. Returns the sheet's address.",
            Arguments = [Sheet, Argument.Of("row", CellsValues.RowNumber, "The row the new rows are inserted before, its number as A1 writes it."), Argument.Of("totalRows", ValueKinds.Integer, "How many rows."), UpdateReference],
            Run = static (_, a, _) =>
            {
                var options = new InsertOptions();
                if (a.Has("updateReference"))
                {
                    options.UpdateReference = a.Get<bool>("updateReference");
                }

                a.Get<Worksheet>("sheet").Cells.InsertRows(CellsValues.RowIndex(a.Get<int>("row")), a.Get<int>("totalRows"), options);
                return OnSheet(a);
            },
        });
        table.Action(new TableAction<Workbook>
        {
            Name = "delete_rows",
            Description = "Cells.DeleteRows(int rowIndex, int totalRows, DeleteOptions options), with new DeleteOptions() and updateReference set on it when given. Returns what the engine returns, and the sheet's address.",
            Arguments = [Sheet, Argument.Of("row", CellsValues.RowNumber, "The first row deleted, its number as A1 writes it."), Argument.Of("totalRows", ValueKinds.Integer, "How many rows."), UpdateReference],
            Run = static (_, a, _) =>
            {
                var options = new DeleteOptions();
                if (a.Has("updateReference"))
                {
                    options.UpdateReference = a.Get<bool>("updateReference");
                }

                bool deleted = a.Get<Worksheet>("sheet").Cells.DeleteRows(CellsValues.RowIndex(a.Get<int>("row")), a.Get<int>("totalRows"), options);
                return new ActionResult(JsonValue.Create(deleted), a.AddressOf("sheet"));
            },
        });
        table.Action(new TableAction<Workbook>
        {
            Name = "insert_columns",
            Description = "Cells.InsertColumns(int columnIndex, int totalColumns, InsertOptions options), with new InsertOptions() and updateReference set on it when given. Returns the sheet's address.",
            Arguments = [Sheet, Argument.Of("column", CellsValues.ColumnName, "The column the new columns are inserted before, its letters as A1 writes them."), Argument.Of("totalColumns", ValueKinds.Integer, "How many columns."), UpdateReference],
            Run = static (_, a, _) =>
            {
                var options = new InsertOptions();
                if (a.Has("updateReference"))
                {
                    options.UpdateReference = a.Get<bool>("updateReference");
                }

                a.Get<Worksheet>("sheet").Cells.InsertColumns(CellsValues.ColumnIndex(a.Get<string>("column")), a.Get<int>("totalColumns"), options);
                return OnSheet(a);
            },
        });
        table.Action(new TableAction<Workbook>
        {
            Name = "delete_columns",
            Description = "Cells.DeleteColumns(int columnIndex, int totalColumns, DeleteOptions options), with new DeleteOptions() and updateReference set on it when given. Returns the sheet's address.",
            Arguments = [Sheet, Argument.Of("column", CellsValues.ColumnName, "The first column deleted, its letters as A1 writes them."), Argument.Of("totalColumns", ValueKinds.Integer, "How many columns."), UpdateReference],
            Run = static (_, a, _) =>
            {
                var options = new DeleteOptions();
                if (a.Has("updateReference"))
                {
                    options.UpdateReference = a.Get<bool>("updateReference");
                }

                a.Get<Worksheet>("sheet").Cells.DeleteColumns(CellsValues.ColumnIndex(a.Get<string>("column")), a.Get<int>("totalColumns"), options);
                return OnSheet(a);
            },
        });
        table.Action(new TableAction<Workbook>
        {
            Name = "autofit_rows",
            Description = "Worksheet.AutoFitRows(int startRow, int endRow), the rows' indexes from their numbers. Returns the sheet's address.",
            Arguments = [Sheet, Argument.Of("startRow", CellsValues.RowNumber, "The first row, its number as A1 writes it."), Argument.Of("endRow", CellsValues.RowNumber, "The last row.")],
            Run = static (_, a, _) =>
            {
                a.Get<Worksheet>("sheet").AutoFitRows(CellsValues.RowIndex(a.Get<int>("startRow")), CellsValues.RowIndex(a.Get<int>("endRow")));
                return OnSheet(a);
            },
        });
        table.Action(new TableAction<Workbook>
        {
            Name = "autofit_columns",
            Description = "Worksheet.AutoFitColumns(int firstColumn, int lastColumn), the columns' indexes from their letters. Returns the sheet's address.",
            Arguments = [Sheet, Argument.Of("firstColumn", CellsValues.ColumnName, "The first column, its letters as A1 writes them."), Argument.Of("lastColumn", CellsValues.ColumnName, "The last column.")],
            Run = static (_, a, _) =>
            {
                a.Get<Worksheet>("sheet").AutoFitColumns(CellsValues.ColumnIndex(a.Get<string>("firstColumn")), CellsValues.ColumnIndex(a.Get<string>("lastColumn")));
                return OnSheet(a);
            },
        });
        Group(table, "group_rows", "Cells.GroupRows(int firstIndex, int lastIndex, bool isHidden)", rows: true, grouping: true);
        Group(table, "ungroup_rows", "Cells.UngroupRows(int firstIndex, int lastIndex)", rows: true, grouping: false);
        Group(table, "group_columns", "Cells.GroupColumns(int firstIndex, int lastIndex, bool isHidden)", rows: false, grouping: true);
        Group(table, "ungroup_columns", "Cells.UngroupColumns(int firstIndex, int lastIndex)", rows: false, grouping: false);
    }

    private static void Group(DocumentTable<Workbook> table, string name, string member, bool rows, bool grouping)
    {
        Argument first = rows
            ? Argument.Of("firstRow", CellsValues.RowNumber, "The first row, its number as A1 writes it.")
            : Argument.Of("firstColumn", CellsValues.ColumnName, "The first column, its letters as A1 writes them.");
        Argument last = rows
            ? Argument.Of("lastRow", CellsValues.RowNumber, "The last row.")
            : Argument.Of("lastColumn", CellsValues.ColumnName, "The last column.");
        Argument hidden = Argument.Of("isHidden", ValueKinds.Boolean, rows ? "Whether the grouped rows are hidden." : "Whether the grouped columns are hidden.");
        table.Action(new TableAction<Workbook>
        {
            Name = name,
            Description = $"{member}, the indexes from the {(rows ? "rows' numbers" : "columns' letters")}. Returns the sheet's address.",
            Arguments = grouping ? [Sheet, first, last, hidden] : [Sheet, first, last],
            Run = (_, a, _) =>
            {
                Aspose.Cells.Cells cells = a.Get<Worksheet>("sheet").Cells;
                int from = rows ? CellsValues.RowIndex(a.Get<int>("firstRow")) : CellsValues.ColumnIndex(a.Get<string>("firstColumn"));
                int to = rows ? CellsValues.RowIndex(a.Get<int>("lastRow")) : CellsValues.ColumnIndex(a.Get<string>("lastColumn"));
                switch (rows, grouping)
                {
                    case (true, true):
                        cells.GroupRows(from, to, a.Get<bool>("isHidden"));
                        break;
                    case (true, false):
                        cells.UngroupRows(from, to);
                        break;
                    case (false, true):
                        cells.GroupColumns(from, to, a.Get<bool>("isHidden"));
                        break;
                    default:
                        cells.UngroupColumns(from, to);
                        break;
                }

                return OnSheet(a);
            },
        });
    }

    private static void DeclareRanges(DocumentTable<Workbook> table)
    {
        table.Action(new TableAction<Workbook>
        {
            Name = "sort",
            Description = "Workbook.DataSorter: Clear(), HasHeaders set, AddKey(int key, SortOrder order) for each key in order, then Sort(Cells cells, CellArea area). Returns the engine's array, and the range's address.",
            Arguments =
            [
                Argument.Address("range", CellsTable.RangeKind, "The range to sort."),
                Argument.Of("hasHeaders", ValueKinds.Boolean, "DataSorter.HasHeaders: whether the range's first row is a header row."),
                Argument.Of("keys", SortKeys, "The keys, the first deciding first."),
            ],
            Run = static (w, a, _) =>
            {
                Aspose.Cells.Range range = a.Get<Aspose.Cells.Range>("range");
                DataSorter sorter = w.DataSorter;
                // The sorter is the workbook's own and keeps its keys between sorts.
                sorter.Clear();
                sorter.HasHeaders = a.Get<bool>("hasHeaders");
                foreach ((string column, SortOrder order) in a.Get<IReadOnlyList<(string Column, SortOrder Order)>>("keys"))
                {
                    sorter.AddKey(CellsValues.ColumnIndex(column), order);
                }

                int[]? moved = sorter.Sort(range.Worksheet.Cells, Area(range));
                return new ActionResult(new JsonArray([.. (moved ?? []).Select(static index => (JsonNode)index)]), a.AddressOf("range"));
            },
        });
        table.Action(new TableAction<Workbook>
        {
            Name = "remove_duplicates",
            Description = "Cells.RemoveDuplicates(int startRow, int startColumn, int endRow, int endColumn, bool hasHeaders, int[] columnOffsets), with the range's corners. Returns the range's address.",
            Arguments =
            [
                Argument.Address("range", CellsTable.RangeKind, "The range."),
                Argument.Of("hasHeaders", ValueKinds.Boolean, "Whether the range's first row is a header row."),
                Argument.Of("columnOffsets", ValueKinds.ListOf(ValueKinds.Integer), "The columns compared, each its offset from the range's first column, from 0."),
            ],
            Run = static (_, a, _) =>
            {
                Aspose.Cells.Range range = a.Get<Aspose.Cells.Range>("range");
                range.Worksheet.Cells.RemoveDuplicates(range.FirstRow, range.FirstColumn, range.FirstRow + range.RowCount - 1, range.FirstColumn + range.ColumnCount - 1,
                    a.Get<bool>("hasHeaders"), [.. a.Get<IReadOnlyList<int>>("columnOffsets")]);
                return new ActionResult(null, a.AddressOf("range"));
            },
        });
        Clear(table, "clear_contents", "ClearContents", static (cells, area) => cells.ClearContents(area));
        Clear(table, "clear_formats", "ClearFormats", static (cells, area) => cells.ClearFormats(area));
        Clear(table, "clear_range", "ClearRange", static (cells, area) => cells.ClearRange(area));
        table.Action(new TableAction<Workbook>
        {
            Name = "copy_range",
            Description = "Range.Copy(Range range, PasteOptions options), with new PasteOptions() and pasteType set on it, the source range in this workbook or in the one file names. Returns the range's address.",
            Arguments =
            [
                Argument.Address("range", CellsTable.RangeKind, "The range copied to."),
                Argument.AddressFoundByTheCall("sourceRange", CellsTable.RangeKind, "The range copied, in file when it is given, else in this workbook."),
                Argument.Of("pasteType", ValueKinds.Enum<PasteType>(), "PasteOptions.PasteType."),
                Argument.Optional("file", ValueKinds.File, "The workbook the source range is in."),
                Argument.Optional("passwordEnv", ValueKinds.SecretVariable, "The environment variable that holds the password of that workbook."),
            ],
            Run = static (w, a, context) =>
            {
                Aspose.Cells.Range source = context.Find<Aspose.Cells.Range>(Source(w, a, context), a, "sourceRange");
                a.Get<Aspose.Cells.Range>("range").Copy(source, new PasteOptions { PasteType = a.Get<PasteType>("pasteType") });
                return new ActionResult(null, a.AddressOf("range"));
            },
        });
    }

    private static void Clear(DocumentTable<Workbook> table, string name, string member, Action<Aspose.Cells.Cells, CellArea> clear) =>
        table.Action(new TableAction<Workbook>
        {
            Name = name,
            Description = $"Cells.{member}(CellArea range). Returns the range's address.",
            Arguments = [Argument.Address("range", CellsTable.RangeKind, "The range.")],
            Run = (_, a, _) =>
            {
                Aspose.Cells.Range range = a.Get<Aspose.Cells.Range>("range");
                clear(range.Worksheet.Cells, Area(range));
                return new ActionResult(null, a.AddressOf("range"));
            },
        });

    private static ActionResult OnSheet(Arguments a) => new(null, a.AddressOf("sheet"));

    /// <summary>The engine's cell area of a range.</summary>
    private static CellArea Area(Aspose.Cells.Range range) =>
        CellArea.CreateCellArea(range.FirstRow, range.FirstColumn, range.FirstRow + range.RowCount - 1, range.FirstColumn + range.ColumnCount - 1);

    /// <summary>The workbook an address argument the call finds names its object in: the one file names, opened, or this one.</summary>
    private static Workbook Source(Workbook w, Arguments a, TableActionContext<Workbook> context) =>
        a.Has("file") ? context.Open(a.Get<string>("file"), a.Has("passwordEnv") ? a.Get<Secret>("passwordEnv") : null).Document : w;

    /// <summary>The worksheet a canonical address starts with, <c>sheet/&lt;name&gt;/…</c>: a worksheet's name holds no slash.</summary>
    private static Worksheet SheetOf(Workbook w, string address)
    {
        string name = address.Split('/')[1];
        return w.Worksheets.First(sheet => sheet.Name == name);
    }
}
