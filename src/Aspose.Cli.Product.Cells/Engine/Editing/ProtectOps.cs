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
    public static long? ProtectSheet(Worksheet sheet, ProtectSheetOp op, IReadOnlyDictionary<string, Secret>? secrets)
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

        Protection protection = sheet.Protection;
        foreach (string action in op.Allow)
        {
            Allow(protection, action);
        }

        return null;
    }

    public static long? UnprotectSheet(Worksheet sheet, UnprotectSheetOp op, IReadOnlyDictionary<string, Secret>? secrets)
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
        return null;
    }

    public static long? ProtectWorkbook(Workbook workbook, ProtectWorkbookOp op, IReadOnlyDictionary<string, Secret>? secrets)
    {
        OperationInvalidException.Require(
            !Sheets.StructurePasswordProtected(workbook),
            "the workbook structure is already protected with a password",
            "Run unprotect_workbook with that password's passwordEnv first, then protect_workbook.");
        workbook.Protect(ProtectionType.Structure, OperationSecrets.Resolve(secrets, op.PasswordEnv)?.Reveal());
        return null;
    }

    public static long? UnprotectWorkbook(Workbook workbook, UnprotectWorkbookOp op, IReadOnlyDictionary<string, Secret>? secrets)
    {
        string? password = OperationSecrets.Resolve(secrets, op.PasswordEnv)?.Reveal();
        Unprotect(() => workbook.Unprotect(password ?? string.Empty), "the workbook structure", password);
        return null;
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

    private static void Allow(Protection protection, string action)
    {
        switch (action)
        {
            case CellsProtectActions.FormatCells:
                protection.AllowFormattingCell = true;
                break;
            case CellsProtectActions.InsertRows:
                protection.AllowInsertingRow = true;
                break;
            case CellsProtectActions.InsertColumns:
                protection.AllowInsertingColumn = true;
                break;
            case CellsProtectActions.DeleteRows:
                protection.AllowDeletingRow = true;
                break;
            case CellsProtectActions.DeleteColumns:
                protection.AllowDeletingColumn = true;
                break;
            case CellsProtectActions.Sort:
                protection.AllowSorting = true;
                break;
            case CellsProtectActions.AutoFilter:
                protection.AllowFiltering = true;
                break;
            default:
                break; // the parser guarantees a known action
        }
    }
}
