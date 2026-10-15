using Aspose.Cli.Sdk.Operations;
using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>
/// Sheet protection using command-resolved secret values. Missing values are
/// reported when their operation runs, preserving indexed best-effort outcomes.
/// </summary>
internal static class ProtectOps
{
    public static void ProtectSheet(Worksheet sheet, ProtectSheetOp op, IReadOnlyDictionary<string, Secret>? secrets)
    {
        string? password = OperationSecrets.Resolve(secrets, op.PasswordEnv)?.Reveal();
        if (password is null)
        {
            sheet.Protect(ProtectionType.All);
        }
        else
        {
            sheet.Protect(ProtectionType.All, password, oldPassword: null);
        }

        // The actions allowed are exactly the ones listed: protecting a protected sheet again
        // replaces what it allowed, as a read reports it, rather than adding to it.
        Protection protection = sheet.Protection;
        foreach ((string action, _, Action<Protection, bool> set) in Actions)
        {
            set(protection, op.Allow.Contains(action, StringComparer.Ordinal));
        }
    }

    public static void UnprotectSheet(Worksheet sheet, UnprotectSheetOp op, IReadOnlyDictionary<string, Secret>? secrets)
    {
        string? password = OperationSecrets.Resolve(secrets, op.PasswordEnv)?.Reveal();
        Unprotect(() =>
        {
            if (password is null)
            {
                sheet.Unprotect();
            }
            else
            {
                sheet.Unprotect(password);
            }
        }, $"sheet '{sheet.Name}'", password);
    }

    public static void ProtectWorkbook(Workbook workbook, ProtectWorkbookOp op, IReadOnlyDictionary<string, Secret>? secrets)
    {
        OperationInvalidException.Require(
            !Sheets.StructurePasswordProtected(workbook),
            "the workbook structure is already protected with a password",
            "Run unprotect_workbook with that password's passwordEnv first, then protect_workbook.");
        workbook.Protect(ProtectionType.Structure, OperationSecrets.Resolve(secrets, op.PasswordEnv)?.Reveal());
    }

    public static void UnprotectWorkbook(Workbook workbook, UnprotectWorkbookOp op, IReadOnlyDictionary<string, Secret>? secrets)
    {
        string? password = OperationSecrets.Resolve(secrets, op.PasswordEnv)?.Reveal();
        Unprotect(() => workbook.Unprotect(password ?? string.Empty), "the workbook structure", password);
    }

    /// <summary>Reports a password the protection does not accept as a password error, not an engine failure.</summary>
    private static void Unprotect(Action unprotect, string target, string? password)
    {
        try
        {
            unprotect();
        }
        catch (CellsException exception) when (exception.Code == ExceptionType.IncorrectPassword)
        {
            throw CliErrors.ProtectionPassword(password is not null, target, "passwordEnv", exception);
        }
    }

    /// <summary>
    /// Each protect_sheet action with the protection flag that allows it, in the order the
    /// vocabulary lists them: protect_sheet sets the flags, a read lists the actions set.
    /// </summary>
    private static readonly (string Action, Func<Protection, bool> Get, Action<Protection, bool> Set)[] Actions =
    [
        (CellsProtectActions.FormatCells, static p => p.AllowFormattingCell, static (p, allowed) => p.AllowFormattingCell = allowed),
        (CellsProtectActions.InsertRows, static p => p.AllowInsertingRow, static (p, allowed) => p.AllowInsertingRow = allowed),
        (CellsProtectActions.InsertColumns, static p => p.AllowInsertingColumn, static (p, allowed) => p.AllowInsertingColumn = allowed),
        (CellsProtectActions.DeleteRows, static p => p.AllowDeletingRow, static (p, allowed) => p.AllowDeletingRow = allowed),
        (CellsProtectActions.DeleteColumns, static p => p.AllowDeletingColumn, static (p, allowed) => p.AllowDeletingColumn = allowed),
        (CellsProtectActions.Sort, static p => p.AllowSorting, static (p, allowed) => p.AllowSorting = allowed),
        (CellsProtectActions.AutoFilter, static p => p.AllowFiltering, static (p, allowed) => p.AllowFiltering = allowed),
    ];

    /// <summary>The protect_sheet actions a protection allows, in the order the vocabulary lists them.</summary>
    internal static IReadOnlyList<string> Allowed(Protection protection) =>
        [.. Actions.Where(item => item.Get(protection)).Select(static item => item.Action)];
}
