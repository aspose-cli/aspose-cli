using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// A batch of edit operations, the payload of <c>cells edit --ops</c>. One
/// vocabulary serves every surface (CLI today, an MCP wrapper tomorrow).
/// Batches are atomic by default: all ops apply, or the file is untouched.
/// </summary>
/// <remarks>
/// Authoring rules kept deliberately agent-friendly: <c>schema</c> is
/// optional on input and validated when present; an op's <c>sheet</c>
/// defaults to the active sheet; rows are 1-based numbers and columns are
/// letters, exactly as in A1 notation.
/// </remarks>
[ProductJsonRoot]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OpsBatch : BoundedOperationEnvelope<Op>;

/// <summary>Base of all edit operations; <c>op</c> is the discriminator.</summary>
[JsonConverter(typeof(Serialization.OpJsonConverter))]
public abstract record Op : BoundedOperation
{
    protected Op(string opName) => OpName = opName;

    /// <summary>
    /// The op name; one of <see cref="OpNames"/>. Excluded from member
    /// serialization — the converter writes it as the <c>op</c> discriminator.
    /// </summary>
    [JsonIgnore]
    public string OpName { get; }

    /// <summary>Target sheet; the active sheet when omitted.</summary>
    public string? Sheet { get; init; }
}

/// <summary>The op vocabulary of this build (wire names).</summary>
public static class OpNames
{
    public const string SetValues = "set_values";
    public const string SetFormula = "set_formula";
    public const string ClearRange = "clear_range";
    public const string CopyRange = "copy_range";
    public const string FormatRange = "format_range";
    public const string MergeCells = "merge_cells";
    public const string UnmergeCells = "unmerge_cells";
    public const string InsertRows = "insert_rows";
    public const string DeleteRows = "delete_rows";
    public const string InsertColumns = "insert_columns";
    public const string DeleteColumns = "delete_columns";
    public const string ResizeRows = "resize_rows";
    public const string ResizeColumns = "resize_columns";
    public const string AddSheet = "add_sheet";
    public const string RenameSheet = "rename_sheet";
    public const string DeleteSheet = "delete_sheet";
    public const string SetSheetVisibility = "set_sheet_visibility";
    public const string FreezePanes = "freeze_panes";
    public const string CreateChart = "create_chart";
    public const string CreatePivot = "create_pivot";
    public const string SetPageSetup = "set_page_setup";
    public const string SetPrintArea = "set_print_area";
    public const string InsertImage = "insert_image";
    public const string RefreshPivot = "refresh_pivot";
    public const string CreateTable = "create_table";
    public const string SetAutoFilter = "set_autofilter";
    public const string SortRange = "sort_range";
    public const string SetValidation = "set_validation";
    public const string DefineName = "define_name";
    public const string DeleteName = "delete_name";
    public const string AddComment = "add_comment";
    public const string EditComment = "edit_comment";
    public const string DeleteComment = "delete_comment";
    public const string ProtectSheet = "protect_sheet";
    public const string UnprotectSheet = "unprotect_sheet";
    public const string GroupRows = "group_rows";
    public const string UngroupRows = "ungroup_rows";
    public const string GroupColumns = "group_columns";
    public const string UngroupColumns = "ungroup_columns";
    public const string ClearValidation = "clear_validation";
    public const string RemoveDuplicates = "remove_duplicates";
    public const string ProtectWorkbook = "protect_workbook";
    public const string UnprotectWorkbook = "unprotect_workbook";
    public const string SetHyperlink = "set_hyperlink";
    public const string RemoveHyperlink = "remove_hyperlink";
    public const string UpdateChart = "update_chart";
    public const string AddConditionalFormat = "add_conditional_format";
    public const string ClearConditionalFormats = "clear_conditional_formats";
    public const string MoveSheet = "move_sheet";
    public const string SetBorders = "set_borders";
    public const string SetDefaultFont = "set_default_font";
    public const string SetTabColor = "set_tab_color";
    public const string SetSheetView = "set_sheet_view";
    public const string DeleteChart = "delete_chart";
    public const string AddSparkline = "add_sparkline";
    public const string SetActiveSheet = "set_active_sheet";

    /// <summary>
    /// Every op name, in documentation order. Derived from <see cref="CellsOps"/>
    /// so the name list, the CLR record types and the discriminator converter
    /// share one source of truth and cannot drift apart.
    /// </summary>
    public static IReadOnlyList<string> All => CellsOps.Names;
}
