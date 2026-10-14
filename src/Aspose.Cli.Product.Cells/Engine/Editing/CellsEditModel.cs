using Aspose.Cells;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Operations;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>
/// The workbook a <c>cells edit</c> batch changes, as the SDK runner applies operations to it and
/// snapshots them (<see cref="CellsSnapshots"/>). Applying an operation charges the cells it
/// writes before it writes them and records a change to a protected sheet or structure.
/// </summary>
internal sealed class CellsEditModel : IOperationModel<CellsOp>
{
    private readonly ResourceBudgetLedger _budgets;
    private readonly Workbook _workbook;
    private readonly CellsMutationHandlers _handlers;
    private readonly CellsSnapshots _snapshots;
    private readonly CellsProtectionTracker _protection;
    private readonly HashSet<CellsOp> _applied = new(ReferenceEqualityComparer.Instance);

    /// <param name="budgets">The invocation's budgets, charged with the cells each operation writes.</param>
    /// <param name="workbook">The workbook being edited.</param>
    /// <param name="secrets">The secrets the operations name by environment variable.</param>
    /// <param name="inputs">Reads and charges the files that operations read.</param>
    /// <param name="sources">Opens the workbooks that operations import from.</param>
    /// <param name="protection">Records each change to a protected sheet or structure.</param>
    internal CellsEditModel(
        ResourceBudgetLedger budgets,
        Workbook workbook,
        IReadOnlyDictionary<string, Secret>? secrets,
        InputResourceScope inputs,
        CellsImportSources sources,
        CellsProtectionTracker protection)
    {
        _budgets = budgets;
        _workbook = workbook;
        _protection = protection;
        _handlers = new CellsMutationHandlers(workbook, secrets, inputs, sources);
        _snapshots = new CellsSnapshots(workbook, _applied.Contains);
    }

    /// <summary>True once an operation that names no sheet was applied to the active sheet.</summary>
    internal bool DefaultedToActiveSheet => _handlers.DefaultedToActiveSheet;

    /// <summary>The anchor cells set_formula wrote, which the batch checks for unknown functions.</summary>
    internal IReadOnlyList<Cell> FormulaAnchors => _handlers.FormulaAnchors;

    public OperationSnapshot Snapshot(CellsOp operation) => Engine(() => operation.Accept(_snapshots));

    public void Apply(CellsOp operation)
    {
        // Charge the cells an operation writes before it writes them: a tiny op over a whole
        // sheet must fail on the budget, not after billions of assignments.
        _budgets.Consume(CellsBudgetDomains.Cells, OpsFootprint.CellCost(operation), "items", "edit");
        ProtectedChange protectedTarget = CellsProtectionTracker.Observe(_workbook, operation);
        _applied.Add(operation);
        _handlers.Run(operation);
        _protection.Record(protectedTarget);
    }

    /// <summary>What a failed operation addressed: its range or cell, its sheet, or the part of the workbook it changes.</summary>
    public IReadOnlyList<string> AttemptedTargets(CellsOp operation)
    {
        CellsEditTarget? target = OpsFootprint.TargetOf(operation);
        string? sheet = target?.Sheet ?? (operation is CellsSheetOp ? _workbook.Worksheets[_workbook.Worksheets.ActiveSheetIndex].Name : null);
        return (sheet, target?.Range) switch
        {
            ({ } name, { } range) => [CellsSnapshots.RangeAddress(name, range)],
            ({ } name, null) => [CellsSnapshots.Address("sheet", name)],
            _ => [WorkbookAddress(operation)],
        };
    }

    public NoEffect? Explain(CellsOp operation) => operation switch
    {
        ClearRangeOp or ClearValidationOp or ClearConditionalFormatsOp => new NoEffect(NoEffectReasons.NoChange,
            $"Operation {operation.Id} ({CellsOp.Catalog.NameOf(operation)}) changed nothing: the range held nothing to clear."),
        ClearAutoFilterOp or ClearPrintAreaOp or ClearTabColorOp or UnfreezePanesOp => new NoEffect(NoEffectReasons.NoChange,
            $"Operation {operation.Id} ({CellsOp.Catalog.NameOf(operation)}) changed nothing: the sheet had nothing to remove."),
        _ => null,
    };

    private static string WorkbookAddress(CellsOp operation) => operation switch
    {
        DefineNameOp define => $"name/{TargetName.Escape(define.Name)}",
        DeleteNameOp delete => $"name/{TargetName.Escape(delete.Name)}",
        SetDefaultFontOp => "workbook/default-font",
        ProtectWorkbookOp or UnprotectWorkbookOp => "workbook/protection",
        InsertSheetOp insert => CellsSnapshots.Address("sheet", insert.Name),
        ImportSheetOp import => CellsSnapshots.Address("sheet", import.Name ?? import.SourceSheet ?? Path.GetFileNameWithoutExtension(import.Path)),
        _ => "workbook/all",
    };

    /// <summary>Calls Aspose.Cells; a failure inside it becomes an engine failure.</summary>
    private static T Engine<T>(Func<T> call)
    {
        try
        {
            return call();
        }
        catch (CellsException exception)
        {
            throw new EngineOpException(exception.Message, exception);
        }
    }
}
