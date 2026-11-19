using System.Text.Json.Nodes;
using Aspose.Cells;
using Aspose.Cells.Charts;
using Aspose.Cells.Pivot;
using Aspose.Cells.Tables;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>The actions on a workbook, its worksheets and the objects on them.</summary>
internal static partial class CellsActions
{
    private static void DeclareWorkbook(DocumentTable<Workbook> table)
    {
        table.Action(new TableAction<Workbook>
        {
            Name = "replace",
            Description = "Workbook.Replace(string placeHolder, string newValue, ReplaceOptions options), with new ReplaceOptions() and the options given set on it: replaces in the cells of every sheet, values and the strings in formulas. Returns how many cells it changed.",
            Arguments =
            [
                Argument.Of("placeHolder", ValueKinds.Text, "The text to find."),
                Argument.Of("newValue", ValueKinds.Text, "The text to put in its place."),
                Argument.Optional("caseSensitive", ValueKinds.Boolean, "ReplaceOptions.CaseSensitive."),
                Argument.Optional("matchEntireCellContents", ValueKinds.Boolean, "ReplaceOptions.MatchEntireCellContents."),
                Argument.Optional("regexKey", ValueKinds.Boolean, "ReplaceOptions.RegexKey: whether placeHolder is a regular expression."),
            ],
            Run = static (w, a, _) =>
            {
                var options = new ReplaceOptions();
                if (a.Has("caseSensitive"))
                {
                    options.CaseSensitive = a.Get<bool>("caseSensitive");
                }

                if (a.Has("matchEntireCellContents"))
                {
                    options.MatchEntireCellContents = a.Get<bool>("matchEntireCellContents");
                }

                if (a.Has("regexKey"))
                {
                    options.RegexKey = a.Get<bool>("regexKey");
                }

                return new ActionResult(JsonValue.Create(w.Replace(a.Get<string>("placeHolder"), a.Get<string>("newValue"), options)));
            },
        });
        table.Action(new TableAction<Workbook>
        {
            Name = "protect_workbook",
            Description = "Workbook.Protect(ProtectionType protectionType, string password). Returns the workbook's address.",
            Arguments =
            [
                Argument.Of("protectionType", ValueKinds.Enum<ProtectionType>(), "The engine's ProtectionType."),
                Argument.Of("passwordEnv", ValueKinds.SecretVariable, "The environment variable that holds the password."),
            ],
            Run = static (w, a, _) =>
            {
                w.Protect(a.Get<ProtectionType>("protectionType"), a.Get<Secret>("passwordEnv").Reveal());
                return new ActionResult(null, CellsTable.WorkbookKind.Name);
            },
        });
        table.Action(new TableAction<Workbook>
        {
            Name = "unprotect_workbook",
            Description = "Workbook.Unprotect(string password), with null when passwordEnv is not given. Returns the workbook's address.",
            Arguments = [Argument.Optional("passwordEnv", ValueKinds.SecretVariable, "The environment variable that holds the password.")],
            Run = static (w, a, _) =>
            {
                w.Unprotect(a.Has("passwordEnv") ? a.Get<Secret>("passwordEnv").Reveal() : null!);
                return new ActionResult(null, CellsTable.WorkbookKind.Name);
            },
        });
    }

    private static void DeclareSheets(DocumentTable<Workbook> table)
    {
        table.Action(new TableAction<Workbook>
        {
            Name = "copy_sheet",
            Description = "Worksheet.Copy(Worksheet sourceSheet, CopyOptions copyOptions), with new CopyOptions(), the source sheet in this workbook or in the one file names. Returns the sheet's address.",
            Arguments =
            [
                Sheet,
                Argument.AddressFoundByTheCall("sourceSheet", CellsTable.SheetKind, "The sheet copied, in file when it is given, else in this workbook."),
                Argument.Optional("file", ValueKinds.File, "The workbook the source sheet is in."),
                Argument.Optional("passwordEnv", ValueKinds.SecretVariable, "The environment variable that holds the password of that workbook."),
            ],
            Run = static (w, a, context) =>
            {
                Worksheet source = context.Find<Worksheet>(Source(w, a, context), a, "sourceSheet");
                a.Get<Worksheet>("sheet").Copy(source, new CopyOptions());
                return OnSheet(a);
            },
        });
        table.Action(new TableAction<Workbook>
        {
            Name = "freeze_panes",
            Description = "Worksheet.FreezePanes(string cellName, int freezedRows, int freezedColumns). Returns the sheet's address.",
            Arguments =
            [
                Sheet,
                Argument.Of("cellName", CellsValues.CellName, "The cell, such as B2."),
                Argument.Of("freezedRows", ValueKinds.Integer, "How many rows are frozen."),
                Argument.Of("freezedColumns", ValueKinds.Integer, "How many columns are frozen."),
            ],
            Run = static (_, a, _) =>
            {
                a.Get<Worksheet>("sheet").FreezePanes(a.Get<string>("cellName"), a.Get<int>("freezedRows"), a.Get<int>("freezedColumns"));
                return OnSheet(a);
            },
        });
        SheetCall(table, "unfreeze_panes", "Worksheet.UnFreezePanes()", static sheet => sheet.UnFreezePanes());
        SheetCall(table, "remove_autofilter", "Worksheet.RemoveAutoFilter()", static sheet => sheet.RemoveAutoFilter());
        table.Action(new TableAction<Workbook>
        {
            Name = "protect_sheet",
            Description = "Worksheet.Protect(ProtectionType type, string password, string oldPassword) with oldPassword null, or Protect(ProtectionType type) when passwordEnv is not given. Returns the sheet's address.",
            Arguments =
            [
                Sheet,
                Argument.Of("type", ValueKinds.Enum<ProtectionType>(), "The engine's ProtectionType."),
                Argument.Optional("passwordEnv", ValueKinds.SecretVariable, "The environment variable that holds the password."),
            ],
            Run = static (_, a, _) =>
            {
                Worksheet sheet = a.Get<Worksheet>("sheet");
                if (a.Has("passwordEnv"))
                {
                    sheet.Protect(a.Get<ProtectionType>("type"), a.Get<Secret>("passwordEnv").Reveal(), null!);
                }
                else
                {
                    sheet.Protect(a.Get<ProtectionType>("type"));
                }

                return OnSheet(a);
            },
        });
        table.Action(new TableAction<Workbook>
        {
            Name = "unprotect_sheet",
            Description = "Worksheet.Unprotect(string password), or Unprotect() when passwordEnv is not given. Returns the sheet's address.",
            Arguments = [Sheet, Argument.Optional("passwordEnv", ValueKinds.SecretVariable, "The environment variable that holds the password.")],
            Run = static (_, a, _) =>
            {
                Worksheet sheet = a.Get<Worksheet>("sheet");
                if (a.Has("passwordEnv"))
                {
                    sheet.Unprotect(a.Get<Secret>("passwordEnv").Reveal());
                }
                else
                {
                    sheet.Unprotect();
                }

                return OnSheet(a);
            },
        });
        SheetArea(table, "remove_validation_area", "ValidationCollection.RemoveArea(CellArea ca)", "The area, such as A2:A20.",
            static (sheet, area) => sheet.Validations.RemoveArea(area));
        SheetArea(table, "remove_conditional_format_area",
            "ConditionalFormattingCollection.RemoveArea(int startRow, int startColumn, int totalRows, int totalColumns), with the area's first row and column and its numbers of rows and columns",
            "The area, such as B2:B20.",
            static (sheet, area) => sheet.ConditionalFormattings.RemoveArea(area.StartRow, area.StartColumn, area.EndRow - area.StartRow + 1, area.EndColumn - area.StartColumn + 1));
        SheetArea(table, "clear_sparklines", "SparklineGroupCollection.ClearSparklines(CellArea cellArea)", "The cells whose sparklines are cleared, such as F2:F5.",
            static (sheet, area) => sheet.SparklineGroups.ClearSparklines(area));
    }

    private static void SheetCall(DocumentTable<Workbook> table, string name, string member, Action<Worksheet> call) =>
        table.Action(new TableAction<Workbook>
        {
            Name = name,
            Description = $"{member}. Returns the sheet's address.",
            Arguments = [Sheet],
            Run = (_, a, _) =>
            {
                call(a.Get<Worksheet>("sheet"));
                return OnSheet(a);
            },
        });

    private static void SheetArea(DocumentTable<Workbook> table, string name, string member, string area, Action<Worksheet, CellArea> call) =>
        table.Action(new TableAction<Workbook>
        {
            Name = name,
            Description = $"{member}. Returns the sheet's address.",
            Arguments = [Sheet, Argument.Of("area", CellsValues.Area, area)],
            Run = (_, a, _) =>
            {
                Worksheet sheet = a.Get<Worksheet>("sheet");
                call(sheet, CellsValues.CellArea(sheet, a.Get<string>("area")));
                return OnSheet(a);
            },
        });

    private static void DeclareObjects(DocumentTable<Workbook> table)
    {
        table.Action(new TableAction<Workbook>
        {
            Name = "set_chart_data",
            Description = "Chart.SetChartDataRange(string area, bool isVertical): replaces the chart's series by those of the area. Returns the chart's address.",
            Arguments =
            [
                Argument.Address("chart", CellsTable.ChartKind, "The chart."),
                Argument.Of("area", ValueKinds.Text, "The data, such as A1:E5 or Sales!A1:E5."),
                Argument.Of("isVertical", ValueKinds.Boolean, "Whether each series is a column of the area rather than a row."),
            ],
            Run = static (_, a, _) =>
            {
                a.Get<Chart>("chart").SetChartDataRange(a.Get<string>("area"), a.Get<bool>("isVertical"));
                return new ActionResult(null, a.AddressOf("chart"));
            },
        });
        table.Action(new TableAction<Workbook>
        {
            Name = "add_pivot_field",
            Description = "PivotTable.AddFieldToArea(PivotFieldType fieldType, string fieldName), or AddFieldToArea(fieldType, PivotTable.ValuesField) with valuesField. Returns what the engine returns: the field's position in the area, or -1 when the pivot table has no source field of that name; and the pivot table's address.",
            Arguments =
            [
                Argument.Address("pivot", CellsTable.PivotKind, "The pivot table."),
                Argument.Of("fieldType", ValueKinds.Enum<PivotFieldType>(), "The engine's PivotFieldType: the area the field is added to."),
                Argument.Optional("fieldName", ValueKinds.Text, "The source field's name, the header of its column; or valuesField."),
                Argument.Optional("valuesField", ValueKinds.Boolean, "true: the pivot table's ValuesField, the field that holds the value fields, such as to put them in columns; or fieldName."),
            ],
            Run = static (_, a, _) =>
            {
                PivotTable pivot = a.Get<PivotTable>("pivot");
                PivotFieldType area = a.Get<PivotFieldType>("fieldType");
                int added = (a.Has("fieldName"), a.Has("valuesField") && a.Get<bool>("valuesField")) switch
                {
                    (true, false) => pivot.AddFieldToArea(area, a.Get<string>("fieldName")),
                    (false, true) => pivot.AddFieldToArea(area, pivot.ValuesField),
                    _ => throw TableErrors.InvalidArguments("add_pivot_field takes args.fieldName or args.valuesField true, one of them"),
                };
                return new ActionResult(JsonValue.Create(added), a.AddressOf("pivot"));
            },
        });
        table.Action(new TableAction<Workbook>
        {
            Name = "calculate_pivot",
            Description = "PivotTable.CalculateData(): fills the pivot table's cells from its fields. Returns the pivot table's address.",
            Arguments = [Argument.Address("pivot", CellsTable.PivotKind, "The pivot table.")],
            Run = static (_, a, _) =>
            {
                a.Get<PivotTable>("pivot").CalculateData();
                return new ActionResult(null, a.AddressOf("pivot"));
            },
        });
        table.Action(new TableAction<Workbook>
        {
            Name = "refresh_pivot",
            Description = "PivotTable.PivotCache.Refresh(): reads the source data again into the pivot table's cache; the cells change with calculate_pivot. Returns the engine's PivotRefreshState, and the pivot table's address.",
            Arguments = [Argument.Address("pivot", CellsTable.PivotKind, "The pivot table.")],
            Run = static (_, a, _) =>
            {
                PivotRefreshState state = a.Get<PivotTable>("pivot").PivotCache.Refresh();
                return new ActionResult(JsonValue.Create(Enum.GetName(state) ?? state.ToString()), a.AddressOf("pivot"));
            },
        });
        table.Action(new TableAction<Workbook>
        {
            Name = "resize_table",
            Description = "ListObject.Resize(int startRow, int startColumn, int endRow, int endColumn, bool hasHeaders), with the area's corners. Returns the table's address.",
            Arguments =
            [
                Argument.Address("table", CellsTable.TableKind, "The table."),
                Argument.Of("area", CellsValues.Area, "The table's new area, such as A1:F21."),
                Argument.Of("hasHeaders", ValueKinds.Boolean, "Whether the area's first row is the header row."),
            ],
            Run = static (w, a, _) =>
            {
                CellArea area = CellsValues.CellArea(SheetOf(w, a.AddressOf("table")), a.Get<string>("area"));
                a.Get<ListObject>("table").Resize(area.StartRow, area.StartColumn, area.EndRow, area.EndColumn, a.Get<bool>("hasHeaders"));
                return new ActionResult(null, a.AddressOf("table"));
            },
        });
        table.Action(new TableAction<Workbook>
        {
            Name = "add_conditional_format_area",
            Description = "FormatConditionCollection.AddArea(CellArea cellArea). Returns what the engine returns, the area's position, and the conditional formatting's address.",
            Arguments =
            [
                Argument.Address("conditionalFormatting", CellsTable.ConditionalFormattingKind, "The conditional formatting."),
                Argument.Of("area", CellsValues.Area, "The area its rules apply to, such as B2:B20."),
            ],
            Run = static (w, a, _) =>
            {
                CellArea area = CellsValues.CellArea(SheetOf(w, a.AddressOf("conditionalFormatting")), a.Get<string>("area"));
                int added = a.Get<FormatConditionCollection>("conditionalFormatting").AddArea(area);
                return new ActionResult(JsonValue.Create(added), a.AddressOf("conditionalFormatting"));
            },
        });
    }
}
