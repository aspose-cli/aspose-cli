using System.CommandLine;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>
/// <c>aspose-cli cells edit</c>: applies one atomic batch from an <c>--ops</c> document
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
            ? image with { Path = Path.GetFullPath(image.Path, paths.BaseDirectory) }
            : op,
    };

    public static Command Create(IProductCommandHost<IWorkbookEngine> host)
    {
        var file = new Argument<string>("file") { Description = "Workbook to edit." }.WithInput(InputKind.File);
        var edit = new BoundedEditCommand<Op, OpsBatch>(Definition);
        var noRecalc = new Option<bool>("--no-recalc") { Description = "Skip the automatic formula recalculation after applying the ops." };
        var password = new PasswordOptions("--password", "the workbook");
        var encrypt = new PasswordOptions("--encrypt", "the output file", allowStdin: false);
        var fonts = new FontDirectoryOptions();
        var command = new Command("edit", $"Apply a batch of edit ops atomically. Editable outputs: {string.Join(", ", CellsFormats.EditIds)}.");
        command.Arguments.Add(file);
        edit.AddTo(command);
        command.Options.Add(noRecalc);
        password.AddTo(command);
        encrypt.AddTo(command);
        fonts.AddTo(command);

        command.SetAction(parse => host.Run(parse, context =>
        {
            bool recalculate = !parse.GetValue(noRecalc);
            if (!recalculate && edit.IsVerifyRequested(parse))
            {
                throw CliErrors.OptionInvalid("--verify", "cannot be combined with --no-recalc", "Remove --no-recalc so formula-result verification is reliable.");
            }

            string input = context.Paths.ResolveInput(parse.GetRequiredValue(file));
            BoundedEditInvocation<OpsBatch> invocation = edit.Read(parse, context.Paths, context.Inputs, input);
            string? encryptPassword = encrypt.Resolve(parse, context.Inputs, context.ReadEnvironment);
            CellsFormats.RequireEncryptable(invocation.Target.OutputPath, encrypt.SelectedOption(parse));
            using IDisposable fontScope = fonts.Use(parse, context);
            return context.Port.ApplyOps(input, invocation.Batch, new EditRequest
            {
                OutputPath = invocation.Target.OutputPath,
                Overwrite = invocation.Target.Overwrite,
                BackupPath = invocation.Target.BackupPath,
                Options = invocation.Options,
                Recalculate = recalculate,
                OpSecrets = ResolveSecrets(invocation.Batch, context.ReadEnvironment),
                Password = password.Resolve(parse, context.Inputs, context.ReadEnvironment, stdinAvailable: !invocation.OpsFromStandardInput),
                EncryptPassword = encryptPassword,
                Verify = invocation.Verify,
            });
        }));

        return command;
    }

    private static IReadOnlyDictionary<string, string?> ResolveSecrets(
        OpsBatch batch, Func<string, string?> readEnvironment) =>
        batch.Ops.Select(static op => op switch
        {
            ProtectSheetOp value => value.PasswordEnv,
            UnprotectSheetOp value => value.PasswordEnv,
            ProtectWorkbookOp value => value.PasswordEnv,
            UnprotectWorkbookOp value => value.PasswordEnv,
            _ => null,
        }).OfType<string>().Distinct(StringComparer.Ordinal)
            .ToDictionary(static name => name, readEnvironment, StringComparer.Ordinal);
}
