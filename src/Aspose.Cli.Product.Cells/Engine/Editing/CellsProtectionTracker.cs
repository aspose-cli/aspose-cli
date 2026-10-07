using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>
/// Notes the operations that change a protected sheet or a protected workbook structure. The
/// engine applies every edit through worksheet and workbook protection, which only guide Excel's
/// UI, so the edit reports <see cref="WarningCodes.ProtectionNotEnforced"/>. Protection counts
/// as it stands when each operation runs: unprotecting checks the password, so a batch that
/// unprotects a sheet before changing it went through no protection. Protecting again what is
/// already protected changes that protection too: protect_workbook replaces a structure
/// protection without a password (one with a password is refused), and protect_sheet
/// replaces a sheet's protection settings but keeps its existing password. Writing the contents
/// of unlocked cells only is what Excel allows on a protected sheet, so it goes through nothing.
/// </summary>
internal sealed class CellsProtectionTracker
{
    private readonly List<string> _sheets = [];
    private readonly List<string> _keptPasswords = [];
    private bool _structure;

    /// <summary>
    /// What <paramref name="op"/> would change under protection, captured before it runs; pass
    /// it to <see cref="Record"/> once the operation succeeded.
    /// </summary>
    internal static ProtectedChange Observe(Workbook workbook, CellsOp op)
    {
        switch (op)
        {
            // Unprotecting checks the password.
            case UnprotectSheetOp or UnprotectWorkbookOp:
            // Workbook-level definitions and views and print settings, which Excel leaves open
            // on a protected sheet.
            case DefineNameOp or DeleteNameOp or SetDefaultFontOp or SetActiveSheetOp or SetSheetViewOp
                or FreezePanesOp or SetPageSetupOp or SetPrintAreaOp:
                return default;
            // Structure protection covers adding, removing, renaming, moving and hiding sheets,
            // and protecting the structure again.
            case AddSheetOp or ImportSheetOp or DeleteSheetOp or RenameSheetOp or MoveSheetOp
                or SetSheetVisibilityOp or SetTabColorOp or ProtectWorkbookOp:
                return new ProtectedChange(null, Sheets.StructureProtected(workbook), KeptPassword: false);
            default:
                return ChangedSheet(workbook, op) is { IsProtected: true } sheet && !WritesUnlockedCellsOnly(sheet, op)
                    ? new ProtectedChange(sheet.Name, Structure: false,
                        KeptPassword: op is ProtectSheetOp && sheet.Protection.IsProtectedWithPassword)
                    : default;
        }
    }

    /// <summary>Records what a successful operation changed under protection.</summary>
    internal void Record(ProtectedChange observed)
    {
        _structure |= observed.Structure;
        if (observed.Sheet is { } sheet)
        {
            AddOnce(_sheets, sheet);
            if (observed.KeptPassword)
            {
                AddOnce(_keptPasswords, sheet);
            }
        }
    }

    /// <summary>The warning for an output in <paramref name="format"/>, or null when no operation went through protection.</summary>
    internal Warning? Warning(string format)
    {
        if (_sheets.Count == 0 && !_structure)
        {
            return null;
        }

        var changed = new List<string>(2);
        if (_sheets.Count > 0)
        {
            string names = string.Join(", ", _sheets.Select(Sheets.QuotedName));
            changed.Add(_sheets.Count == 1 ? $"protected sheet {names}" : $"protected sheets {names}");
        }

        if (_structure)
        {
            changed.Add("the protected workbook structure");
        }

        string kept = CellsFormats.EncryptableIds.Contains(format, StringComparer.Ordinal)
            ? "The output keeps the protection unless the batch removed it with unprotect_sheet or unprotect_workbook."
            : $"A {format} output does not keep the protection; save to xlsx or another workbook format to keep it.";
        string passwords = _keptPasswords.Count == 0 ? string.Empty
            : $" protect_sheet kept the existing password of {(_keptPasswords.Count == 1 ? "sheet" : "sheets")} {string.Join(", ", _keptPasswords.Select(Sheets.QuotedName))}: only that password unprotects it, and a password the operation named has no effect; unprotect_sheet first to change it.";
        return new Warning
        {
            Code = WarningCodes.ProtectionNotEnforced,
            Message = $"The edit changed {string.Join(" and ", changed)}; protection only guides Excel, so the edit was applied through it." + passwords,
            Hint = "Confirm the change is authorized: " + (_sheets.Count == 0
                    ? "Excel adds, deletes, renames, moves and hides no sheets while the structure is protected. "
                    : _structure
                        ? "Excel lets users change only unlocked cells and the actions a sheet's protection allows, and no sheets while the structure is protected. "
                        : "Excel lets users change only unlocked cells and the actions a sheet's protection allows. ")
                + kept,
            Location = _sheets.Count == 1 && !_structure ? Sheets.QuotedName(_sheets[0]) : null,
        };
    }

    /// <summary>Whether the operation changes only the contents of cells Excel leaves unlocked on a protected sheet.</summary>
    private static bool WritesUnlockedCellsOnly(Worksheet sheet, CellsOp op)
    {
        if (op is not (SetValuesOp or SetFormulaOp or ClearRangeOp { What: ClearTargets.Contents })
            || OpsFootprint.TargetOf(op)?.Range is not { } target)
        {
            return false;
        }

        RangeRef range = A1.ParseRange(target).Range;
        for (int row = range.Start.Row; row <= range.End.Row; row++)
        {
            for (int column = range.Start.Column; column <= range.End.Column; column++)
            {
                if (sheet.Cells.GetCellStyle(row, column).IsLocked)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void AddOnce(List<string> sheets, string sheet)
    {
        if (!sheets.Contains(sheet, StringComparer.OrdinalIgnoreCase))
        {
            sheets.Add(sheet);
        }
    }

    /// <summary>
    /// The sheet an operation changes, resolved as its handler resolves it: a copy's destination
    /// sheet, else its own; null when it does not exist, since the operation then fails.
    /// </summary>
    private static Worksheet? ChangedSheet(Workbook workbook, CellsOp op)
    {
        try
        {
            Worksheet sheet = Sheets.Resolve(workbook, op);
            return op switch
            {
                CopyRangeOp copy => Sheets.ResolveRange(sheet, copy.To).Sheet,
                ImportRangeOp import => Sheets.ResolveRange(sheet, import.To).Sheet,
                _ => sheet,
            };
        }
        catch (CliException)
        {
            return null;
        }
    }
}

/// <summary>
/// What one operation changes under protection: a protected sheet, the protected workbook
/// structure, and whether it protected again a sheet whose password it kept.
/// </summary>
internal readonly record struct ProtectedChange(string? Sheet, bool Structure, bool KeptPassword);
