using Aspose.Cli.Sdk.Operations;
using Aspose.Cells;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Sheet protection using command-resolved secret values. Missing values are
/// reported when their operation runs, preserving indexed best-effort outcomes.
/// </summary>
internal static class ProtectOps
{
    public static long? ProtectSheet(Worksheet sheet, ProtectSheetOp op, IReadOnlyDictionary<string, string?>? secrets)
    {
        string? password = ResolvePassword(op.PasswordEnv, secrets);
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

    public static long? UnprotectSheet(Worksheet sheet, UnprotectSheetOp op, IReadOnlyDictionary<string, string?>? secrets)
    {
        string? password = ResolvePassword(op.PasswordEnv, secrets);
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

    public static long? ProtectWorkbook(Workbook workbook, ProtectWorkbookOp op, IReadOnlyDictionary<string, string?>? secrets)
    {
        workbook.Protect(ProtectionType.Structure, ResolvePassword(op.PasswordEnv, secrets));
        return null;
    }

    public static long? UnprotectWorkbook(Workbook workbook, UnprotectWorkbookOp op, IReadOnlyDictionary<string, string?>? secrets)
    {
        workbook.Unprotect(ResolvePassword(op.PasswordEnv, secrets) ?? string.Empty);
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

    private static string? ResolvePassword(string? envVar, IReadOnlyDictionary<string, string?>? secrets)
    {
        if (envVar is null)
        {
            return null;
        }

        string? value = secrets?.GetValueOrDefault(envVar);
        if (string.IsNullOrEmpty(value))
        {
            // Names the variable, never a value. The executor attaches the op index.
            throw new OperationInvalidException($"passwordEnv '{envVar}' is not set");
        }

        return value;
    }
}
