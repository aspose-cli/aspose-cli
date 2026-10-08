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
    public static long? ProtectSheet(Worksheet sheet, ProtectSheetOp op, IReadOnlyDictionary<string, string>? secrets)
    {
        string? password = OperationSecrets.Resolve(secrets, op.PasswordEnv);
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

    public static long? UnprotectSheet(Worksheet sheet, UnprotectSheetOp op, IReadOnlyDictionary<string, string>? secrets)
    {
        string? password = OperationSecrets.Resolve(secrets, op.PasswordEnv);
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

    public static long? ProtectWorkbook(Workbook workbook, ProtectWorkbookOp op, IReadOnlyDictionary<string, string>? secrets)
    {
        OperationInvalidException.Require(
            !Sheets.StructurePasswordProtected(workbook),
            "the workbook structure is already protected with a password",
            "Run unprotect_workbook with that password's passwordEnv first, then protect_workbook.");
        workbook.Protect(ProtectionType.Structure, OperationSecrets.Resolve(secrets, op.PasswordEnv));
        return null;
    }

    public static long? UnprotectWorkbook(Workbook workbook, UnprotectWorkbookOp op, IReadOnlyDictionary<string, string>? secrets)
    {
        string? password = OperationSecrets.Resolve(secrets, op.PasswordEnv);
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
            case ProtectActions.FormatCells:
                protection.AllowFormattingCell = true;
                break;
            case ProtectActions.InsertRows:
                protection.AllowInsertingRow = true;
                break;
            case ProtectActions.InsertColumns:
                protection.AllowInsertingColumn = true;
                break;
            case ProtectActions.DeleteRows:
                protection.AllowDeletingRow = true;
                break;
            case ProtectActions.DeleteColumns:
                protection.AllowDeletingColumn = true;
                break;
            case ProtectActions.Sort:
                protection.AllowSorting = true;
                break;
            case ProtectActions.AutoFilter:
                protection.AllowFiltering = true;
                break;
            default:
                break; // the parser guarantees a known action
        }
    }
}
