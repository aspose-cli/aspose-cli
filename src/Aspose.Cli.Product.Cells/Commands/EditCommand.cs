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

    public static CommandDefinition<EditRequest, EditResult> Create()
    {
        var noRecalc = new Option<bool>("--no-recalc") { Description = "Skip the automatic formula recalculation after applying the ops." };
        var bounded = new BoundedEditCommand<CellsOp, CellsOpsBatch>(Definition);
        return EditDefinition.Create<CellsOp, CellsOpsBatch, EditRequest, EditResult>(
            bounded,
            $"Apply a batch of edit ops atomically. Editable outputs: {string.Join(", ", CellsFormats.Editable.Select(static format => format.Id))}.",
            new CommandTraits
            {
                Input = CellsTraits.Workbook("Workbook to edit."),
                Encrypt = CellsTraits.EncryptedWorkbook,
                UsesFonts = true,
            },
            [noRecalc],
            (parse, batch, standard) =>
            {
                Secret? encryptPassword = standard.EncryptPassword();
                return new EditRequest
                {
                    Input = standard.Input,
                    Batch = batch.Batch,
                    Output = standard.Output,
                    Options = batch.Options,
                    Recalculate = !parse.GetValue(noRecalc),
                    OpSecrets = batch.Secrets,
                    Password = standard.InputPassword,
                    EncryptPassword = encryptPassword,
                    Verify = batch.Verify,
                };
            },
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
                CommandHelpLink.Schema(CellsModule.Manifest, "the operations JSON vocabulary"),
                CommandHelpLink.Docs(CellsModule.Manifest, "verification", "verification before delivering the file"),
            ]);
    }

    internal static void Table(EditResult edit, TableSurface surface)
    {
        ResultText.Edit(surface, edit.DryRun, edit.Output, edit.Applied, edit.Backup);
        if (edit.Verification is { } verification)
        {
            ResultText.Verification(
                surface,
                verification.Ok,
                verification.Issues,
                $"; {verification.DirectChanges.Count} direct, "
                + $"{verification.FormulaResultChanges.Count} formula-result, "
                + $"{verification.FormulaErrors.Count} formula error(s)");
        }
    }
}
