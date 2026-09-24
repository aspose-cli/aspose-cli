using System.CommandLine;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>
/// <c>cells edit</c>: applies one atomic batch from an <c>--ops</c> document
/// and/or one-cell <c>--set</c> directives through the shared bounded-edit skeleton.
/// </summary>
internal static class EditCommand
{
    private static readonly BoundedEditDefinition<Op, OpsBatch> Definition = new()
    {
        Catalog = CellsOps.Catalog,
        Contracts = ProductJsonContext.Definition,
        SetDirectives = new(
            "Set one cell as SHEET!CELL=VALUE; repeatable, applied after the --ops document. "
                + "A VALUE starting with '=' is a formula; otherwise TRUE/FALSE and numbers are typed and "
                + "anything else is text. Quote sheet names that need it: --set \"'My Sheet'!A1=5\". "
                + "Example: --set \"Sales!B3=42\" --set \"Sales!G2==E2*F2\".",
            SetDirectiveParser.Parse,
            static ops => new OpsBatch { Ops = ops }),
        VerifyDescription = "Verify the staged output and report its cell changes and formula errors.",
        NormalizePaths = static (op, paths) => op is InsertImageOp image
            ? image with { Path = paths.ResolveInput(image.Path) }
            : op,
        SecretVariables = static op => op switch
        {
            ProtectSheetOp value => [value.PasswordEnv],
            UnprotectSheetOp value => [value.PasswordEnv],
            ProtectWorkbookOp value => [value.PasswordEnv],
            UnprotectWorkbookOp value => [value.PasswordEnv],
            _ => [],
        },
    };

    public static Command Create(IProductCommandHost<IWorkbookEngine> host)
    {
        var noRecalc = new Option<bool>("--no-recalc") { Description = "Skip the automatic formula recalculation after applying the ops." };
        var bounded = new BoundedEditCommand<Op, OpsBatch>(Definition);
        return bounded.Create(
            host,
            "edit",
            $"Apply a batch of edit ops atomically. Editable outputs: {string.Join(", ", CellsFormats.EditIds)}.",
            new CommandTraits
            {
                Input = CellsCommands.Workbook("Workbook to edit."),
                Encrypt = CellsCommands.EncryptedWorkbook,
                UsesFonts = true,
            },
            [noRecalc],
            (parse, edit, standard) =>
            {
                string? encryptPassword = standard.EncryptPassword(CellsFormats.ForOutputPath(edit.Target.OutputPath));
                return standard.OpenEngine().ApplyOps(standard.Input, edit.Batch, new EditRequest
                {
                    OutputPath = edit.Target.OutputPath,
                    Overwrite = edit.Target.Overwrite,
                    BackupPath = edit.Target.BackupPath,
                    Options = edit.Options,
                    Recalculate = !parse.GetValue(noRecalc),
                    OpSecrets = edit.Secrets,
                    Password = standard.InputPassword,
                    EncryptPassword = encryptPassword,
                    Verify = edit.Verify,
                });
            },
            checkUsage: parse =>
            {
                if (parse.GetValue(noRecalc) && bounded.IsVerifyRequested(parse))
                {
                    throw CliErrors.OptionInvalid("--verify", "cannot be combined with --no-recalc", "Remove --no-recalc so formula-result verification is reliable.");
                }
            }).WithExamples(
            [
                "cells edit book.xlsx --in-place --set \"Sales!B3=42\" --set \"Sales!G2==E2*F2\"",
                "cells edit book.xlsx --in-place --ops '{\"ops\":[{\"op\":\"set_values\",\"sheet\":\"Sales\",\"range\":\"A1\",\"values\":[[1]]}]}'",
                "cells edit book.xlsx --in-place --backup --verify --ops ops.json",
            ],
            [
                CommandHelpLink.Docs(CellsModule.Manifest, "editing", "recipes for every operation family"),
                CommandHelpLink.Schema(CellsModule.Manifest, "the operations JSON vocabulary"),
                CommandHelpLink.Docs(CellsModule.Manifest, "verification", "verification before delivering the file"),
            ]);
    }
}
