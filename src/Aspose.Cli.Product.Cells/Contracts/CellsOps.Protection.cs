using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

// Ops for sheet and workbook protection. A password is never carried literally: an op names
// the environment variable that holds it, and the command reads the variable.

/// <summary>Protects a sheet against edits: every action is locked except the ones allowed.</summary>
[Operation("protect_sheet")]
public sealed record ProtectSheetOp : CellsOp
{
    /// <summary>The environment variable that holds the protection password.</summary>
    [SecretEnv] public string? PasswordEnv { get; init; }

    /// <summary>The actions users may still perform.</summary>
    [AllowedValues(typeof(ProtectActions))] public IReadOnlyList<string> Allow { get; init; } = [];
}

/// <summary>Removes sheet protection.</summary>
[Operation("unprotect_sheet")]
public sealed record UnprotectSheetOp : CellsOp
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
public static class ProtectActions
{
    public const string FormatCells = "formatCells";
    public const string InsertRows = "insertRows";
    public const string InsertColumns = "insertColumns";
    public const string DeleteRows = "deleteRows";
    public const string DeleteColumns = "deleteColumns";
    public const string Sort = "sort";
    public const string AutoFilter = "autoFilter";
}
