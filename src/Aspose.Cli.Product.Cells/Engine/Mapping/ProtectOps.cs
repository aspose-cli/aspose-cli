using Aspose.Cells;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Sheet protection. Passwords are resolved from a named environment variable
/// here, at the deepest layer, and handed straight to the engine — the secret
/// value never enters a result, a log or an error message.
/// </summary>
internal static class ProtectOps
{
    public static long? ProtectSheet(Worksheet sheet, ProtectSheetOp op)
    {
        string? password = ResolvePasswordEnv(op.PasswordEnv);
        if (password is null)
        {
            sheet.Protect(ProtectionType.All);
        }
        else
        {
            sheet.Protect(ProtectionType.All, password, oldPassword: null);
        }

        Protection protection = sheet.Protection;
        foreach (string action in op.Allow ?? [])
        {
            Allow(protection, action);
        }

        return null;
    }

    public static long? UnprotectSheet(Worksheet sheet, UnprotectSheetOp op)
    {
        string? password = ResolvePasswordEnv(op.PasswordEnv);
        if (password is null)
        {
            sheet.Unprotect();
        }
        else
        {
            sheet.Unprotect(password);
        }

        return null;
    }

    public static long? ProtectWorkbook(Workbook workbook, ProtectWorkbookOp op)
    {
        workbook.Protect(ProtectionType.Structure, ResolvePasswordEnv(op.PasswordEnv));
        return null;
    }

    public static long? UnprotectWorkbook(Workbook workbook, UnprotectWorkbookOp op)
    {
        workbook.Unprotect(ResolvePasswordEnv(op.PasswordEnv) ?? string.Empty);
        return null;
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

    private static string? ResolvePasswordEnv(string? envVar)
    {
        if (envVar is null)
        {
            return null;
        }

        string? value = Environment.GetEnvironmentVariable(envVar);
        if (string.IsNullOrEmpty(value))
        {
            // Names the variable, never a value. The executor attaches the op index.
            throw CellsErrors.OpsInvalid($"passwordEnv '{envVar}' is not set");
        }

        return value;
    }
}
