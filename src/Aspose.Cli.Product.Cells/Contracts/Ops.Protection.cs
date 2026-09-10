namespace Aspose.Cli.Product.Cells.Contracts;

// Ops for sheet protection. The password is never carried literally: an op
// names an environment variable (passwordEnv) that the engine reads at apply
// time, so a secret can never be written into an ops document on disk.

/// <summary>
/// Protects a sheet against edits. By default every action is locked; list the
/// still-permitted actions in <see cref="Allow"/>. Any password is read from
/// the environment variable named by <see cref="PasswordEnv"/>.
/// </summary>
public sealed record ProtectSheetOp() : Op(OpNames.ProtectSheet)
{
    /// <summary>Name of the environment variable holding the protection password.</summary>
    public string? PasswordEnv { get; init; }

    /// <summary>Actions the user may still perform; each one of <see cref="ProtectActions"/>.</summary>
    public IReadOnlyList<string>? Allow { get; init; }
}

/// <summary>Removes sheet protection.</summary>
public sealed record UnprotectSheetOp() : Op(OpNames.UnprotectSheet)
{
    /// <summary>Name of the environment variable holding the current password, if the sheet is password-protected.</summary>
    public string? PasswordEnv { get; init; }
}

/// <summary>
/// Protects the workbook structure — adding, deleting, moving and hiding sheets.
/// Any password is read from the environment variable named by <see cref="PasswordEnv"/>.
/// </summary>
public sealed record ProtectWorkbookOp() : Op(OpNames.ProtectWorkbook)
{
    /// <summary>Name of the environment variable holding the protection password.</summary>
    public string? PasswordEnv { get; init; }
}

/// <summary>Removes workbook structure protection.</summary>
public sealed record UnprotectWorkbookOp() : Op(OpNames.UnprotectWorkbook)
{
    /// <summary>Name of the environment variable holding the current password, if the workbook is password-protected.</summary>
    public string? PasswordEnv { get; init; }
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

    /// <summary>Every permitted action, in documentation order.</summary>
    public static IReadOnlyList<string> All { get; } =
        [FormatCells, InsertRows, InsertColumns, DeleteRows, DeleteColumns, Sort, AutoFilter];
}
