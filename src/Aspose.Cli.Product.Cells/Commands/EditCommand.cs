using System.CommandLine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>
/// <c>cells edit</c>: applies one atomic batch from an <c>--ops</c> document
/// and/or one-cell <c>--set</c> directives through the shared bounded-edit skeleton.
/// </summary>
internal static class EditCommand
{
    private static readonly BoundedEditDefinition<CellsOp, CellsOpsBatch> Definition = new()
    {
        Contracts = ProductJsonContext.Definition,
        SetDirectives = new(
            "Set one cell as SHEET!CELL=VALUE; repeatable, applied after the --ops document. "
                + "A VALUE starting with '=' is a formula; otherwise TRUE/FALSE and numbers are typed and "
                + "anything else is text. Quote sheet names that need it: --set \"'My Sheet'!A1=5\". "
                + "Example: --set \"Sales!B3=42\" --set \"Sales!G2==E2*F2\".",
            SetDirectiveParser.Parse,
            static ops => new CellsOpsBatch { Ops = ops }),
        VerifyDescription = "Verify the staged output and report its cell changes and formula errors.",
        Writes = CellsFormats.Editable,
    };

    public static CommandDefinition<CellsEditRequest, CellsEditResult> Create()
    {
        var noRecalc = new Option<bool>("--no-recalc") { Description = "Skip the automatic formula recalculation after applying the ops." };
        var bounded = new BoundedEditCommand<CellsOp, CellsOpsBatch>(Definition);
        return EditDefinition.Create<CellsOp, CellsOpsBatch, CellsEditRequest, CellsEditResult>(
            bounded,
            $"Apply a batch of edit ops atomically. Editable outputs: {string.Join(", ", CellsFormats.Editable.Select(static format => format.Id))}.",
            new CommandTraits
            {
                Input = CellsInputs.Workbook("Workbook to edit."),
                Encrypt = CellsInputs.EncryptedWorkbook,
                UsesFonts = true,
            },
            [noRecalc],
            (parse, edit, _) => new CellsEditRequest(edit) { Recalculate = !parse.GetValue(noRecalc) },
            Table,
            checkUsage: parse =>
            {
                if (parse.GetValue(noRecalc) && bounded.IsVerifyRequested(parse))
                {
                    throw CliErrors.OptionInvalid("--verify", "cannot be combined with --no-recalc", "Remove --no-recalc so formula-result verification is reliable.");
                }
            },
            examples:
            [
                "cells edit book.xlsx --in-place --set \"Sales!B3=42\" --set \"Sales!G2==E2*F2\"",
                "cells edit book.xlsx --in-place --ops '{\"ops\":[{\"op\":\"set_values\",\"sheet\":\"Sales\",\"range\":\"A1\",\"values\":[[1]]}]}'",
                "cells edit book.xlsx --in-place --backup --verify --ops ops.json",
            ],
            links:
            [
                CommandHelpLink.Docs(CellsModule.Manifest, "editing", "recipes for every operation family"),
                CommandHelpLink.Schema<CellsOp>("the operations JSON vocabulary"),
                CommandHelpLink.Docs(CellsModule.Manifest, "verification", "verification before delivering the file"),
            ]);
    }

    internal static void Table(CellsEditResult edit, TableSurface surface)
    {
        ResultText.Edit(surface, edit.DryRun, edit.Output, edit.Applied, edit.Backup);
        ResultText.Verification(
            surface,
            edit.Verification,
            edit.Verification is { } verification
                ? $"{verification.DirectChanges.Count} direct, {verification.FormulaResultChanges.Count} formula-result, "
                    + $"{verification.FormulaErrors.Count} formula error(s)"
                : null);
    }
}
