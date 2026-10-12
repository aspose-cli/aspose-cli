using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

// Ops for sheet and workbook protection. A password is never carried literally: an op names
// the environment variable that holds it, and the command reads the variable.

/// <summary>Protects a sheet against edits: every action is locked except the ones allowed.</summary>
[Operation("protect_sheet")]
public sealed record ProtectSheetOp : CellsSheetOp
{
    /// <summary>The environment variable that holds the protection password.</summary>
    [SecretEnv] public string? PasswordEnv { get; init; }

    /// <summary>The actions users may still perform.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsSheetProtectionInfo/properties/allow")]
    [AllowedValues(typeof(CellsProtectActions))] public IReadOnlyList<string> Allow { get; init; } = [];
}

/// <summary>Removes sheet protection.</summary>
[Operation("unprotect_sheet")]
public sealed record UnprotectSheetOp : CellsSheetOp
{
    /// <summary>The environment variable that holds the current password of a password-protected sheet.</summary>
    [SecretEnv] public string? PasswordEnv { get; init; }
}

/// <summary>Protects the workbook structure: adding, deleting, moving and hiding sheets.</summary>
[Operation("protect_workbook")]
public sealed record ProtectWorkbookOp : CellsOp
{
    /// <summary>The environment variable that holds the protection password.</summary>
    [SecretEnv] public string? PasswordEnv { get; init; }
}

/// <summary>Removes workbook structure protection.</summary>
[Operation("unprotect_workbook")]
public sealed record UnprotectWorkbookOp : CellsOp
{
    /// <summary>The environment variable that holds the current password of a password-protected workbook.</summary>
    [SecretEnv] public string? PasswordEnv { get; init; }
}

/// <summary>Accepted items of <see cref="ProtectSheetOp.Allow"/>.</summary>
public static class CellsProtectActions
{
    public const string FormatCells = "format-cells";
    public const string InsertRows = "insert-rows";
    public const string InsertColumns = "insert-columns";
    public const string DeleteRows = "delete-rows";
    public const string DeleteColumns = "delete-columns";
    public const string Sort = "sort";
    public const string AutoFilter = "autofilter";
}
