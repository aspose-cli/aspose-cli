using System.CommandLine;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>
/// <c>aspose-cli cells edit</c> — the core mutation command: applies a batch of
/// ops atomically, from a JSON document (<c>--ops</c>: file, stdin or inline)
/// and/or one-cell <c>--set</c> directives. One process invocation per batch
/// amortizes startup cost for agents.
/// </summary>
internal static class EditCommand
{
    public static Command Create(IProductCommandHost<IWorkbookEngine> host)
    {
        EditCommandBindings options = CreateOptions();
        Command edit = options.Command;

        edit.SetAction(parseResult => host.Run(parseResult, context =>
        {
            string? opsSource = parseResult.GetValue(options.Ops);
            string[] setDirectives = parseResult.GetValue(options.Set) ?? [];
            OpsBatch batch = BuildBatch(opsSource, setDirectives, context);

            string inputPath = context.Paths.ResolveInput(parseResult.GetRequiredValue(options.File));
            bool verify = parseResult.GetValue(options.Verify);
            bool dryRun = parseResult.GetValue(options.Edit.DryRun);
            bool noRecalc = parseResult.GetValue(options.NoRecalc);
            ValidateVerificationOptions(verify, dryRun, noRecalc);

            MutationTarget target = options.Output.Resolve(parseResult, context.Paths, inputPath, requireBackup: verify);
            string? inputPassword = options.Password.Resolve(parseResult, context.Inputs, context.ReadEnvironment, stdinAvailable: opsSource != "-");
            string? encryptPassword = options.Encrypt.Resolve(parseResult, context.Inputs, context.ReadEnvironment);
            EditResult result = context.Port.ApplyOps(inputPath, batch, new EditRequest
            {
                OutputPath = target.OutputPath,
                Overwrite = target.Overwrite,
                BackupPath = target.BackupPath,
                Options = options.Edit.Read(parseResult, batch.IfMatch),
                Recalculate = !noRecalc,
                OpSecrets = ResolveSecrets(batch, context.ReadEnvironment),
                Password = inputPassword,
                EncryptPassword = encryptPassword,
                Verify = verify,
            });

            // A file was produced (partial successes under --best-effort
            // included): let any live preview of it spotlight the change.
            if (!result.DryRun)
            {
                PublishPreviewHint(batch, target.OutputPath);
            }

            return result;
        }));

        return edit;
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
    private static EditCommandBindings CreateOptions()
    {
        var file = new Argument<string>("file") { Description = "Workbook to edit." }.WithInput(InputKind.File);
        var ops = new Option<string>("--ops")
        {
            Description = "The ops JSON: a path to the document, '-' to read it from stdin, or the "
                + "document itself when the value starts with { or [ (inline). To name a file "
                + "whose name starts with '[', prefix it with ./ . Vocabulary: aspose-cli schema v2/cells/ops.",
        }.WithInput(InputKind.JsonSource);
        var set = new Option<string[]>("--set")
        {
            Description = "Set one cell as SHEET!CELL=VALUE; repeatable, applied after the --ops document. "
                + "A VALUE starting with '=' is a formula; otherwise TRUE/FALSE and numbers are typed and "
                + "anything else is text. Quote sheet names that need it: --set \"'My Sheet'!A1=5\". "
                + "Example: --set \"Sales!B3=42\" --set \"Sales!G2==E2*F2\".",
        }.WithInput(InputKind.None);
        var output = new MutationFileOptions();
        var editOptions = new BoundedEditOptions();
        var noRecalc = new Option<bool>("--no-recalc") { Description = "Skip the automatic formula recalculation after applying the ops." };
        var verify = new Option<bool>("--verify") { Description = "Verify the staged output and report its cell changes and formula errors." };
        var password = new PasswordOptions("--password", "the workbook");
        var encrypt = new PasswordOptions("--encrypt", "the output file", allowStdin: false);
        var command = new Command("edit", $"Apply a batch of edit ops atomically. Editable outputs: {string.Join(", ", CellsFormats.EditIds)}.");
        command.Arguments.Add(file);
        command.Options.Add(ops);
        command.Options.Add(set);
        output.AddTo(command);
        editOptions.AddTo(command);
        command.Options.Add(noRecalc);
        command.Options.Add(verify);
        password.AddTo(command);
        encrypt.AddTo(command);
        return new(command, file, ops, set, output, editOptions, noRecalc, verify, password, encrypt);
    }

    private sealed record EditCommandBindings(
        Command Command,
        Argument<string> File,
        Option<string> Ops,
        Option<string[]> Set,
        MutationFileOptions Output,
        BoundedEditOptions Edit,
        Option<bool> NoRecalc,
        Option<bool> Verify,
        PasswordOptions Password,
        PasswordOptions Encrypt);

    private static void ValidateVerificationOptions(bool verify, bool dryRun, bool noRecalc)
    {
        if (verify && dryRun)
        {
            throw CliErrors.OptionInvalid("--verify", "cannot be combined with --dry-run", "Run the dry run first, then edit with --verify.");
        }

        if (verify && noRecalc)
        {
            throw CliErrors.OptionInvalid("--verify", "cannot be combined with --no-recalc", "Remove --no-recalc so formula-result verification is reliable.");
        }
    }

    /// <summary>
    /// Assembles the one atomic batch of this invocation: the <c>--ops</c>
    /// document (when given) followed by the ops compiled from each
    /// <c>--set</c> directive, in command-line order. Everything downstream —
    /// dry runs, recalculation, continue-on-error, the preview hint — sees one
    /// batch and needs no special case.
    /// </summary>
    private static OpsBatch BuildBatch(
        string? opsSource,
        IReadOnlyList<string> setDirectives,
        ProductCommandContext<IWorkbookEngine> context)
    {
        if (opsSource is null && setDirectives.Count == 0)
        {
            throw CliErrors.Usage(["Give --ops (an ops JSON document), --set (SHEET!CELL=VALUE), or both."]);
        }

        // Compile the sugar first: a bad directive fails before any document
        // IO — in particular before '--ops -' consumes stdin.
        var compiled = new Op[setDirectives.Count];
        for (int index = 0; index < setDirectives.Count; index++)
        {
            compiled[index] = SetDirectiveParser.Parse(setDirectives[index]);
        }

        if (opsSource is null)
        {
            return OpsParser.Prepare(new OpsBatch { Ops = compiled });
        }

        string opsText = JsonInputSource.Read(
            opsSource,
            context.Paths,
            context.Inputs,
            "--ops");
        OpsBatch document;
        try
        {
            document = OpsParser.Parse(opsText);
        }
        catch (CliException ex) when (
            ex.Code == ErrorCodes.OpsInvalid && opsSource != "-" && !opsText.Contains('"'))
        {
            // Valid ops JSON always contains double quotes, so an inline
            // value without a single one means the shell removed them —
            // Windows PowerShell does exactly that to embedded quotes in
            // native arguments. Same code and message; the hint teaches the
            // three ways out.
            throw new CliException(
                ex.Code,
                ex.Message,
                hint: $"{ex.Hint} The value reached the CLI without any double quotes — Windows PowerShell strips them from inline arguments: escape them as \\\" , pipe the document via --ops - , or use --set.",
                details: ex.Details,
                docs: ex.Docs);
        }

        return OpsParser.Prepare(compiled.Length == 0
            ? document
            : document with { Ops = [.. document.Ops, .. compiled] });
    }

    /// <summary>
    /// Points a running root preview of the output file at what
    /// this batch touched, over the best-effort sideband
    /// (<see cref="PreviewHintChannel"/>). The hint is decorative: no failure
    /// here may ever affect the edit, which already succeeded.
    /// </summary>
    private static void PublishPreviewHint(OpsBatch batch, string outputPath)
    {
        try
        {
            IReadOnlyList<CellsPreviewHint> targets = OpsFootprint.Collect(batch);
            if (targets.Count == 0)
            {
                return;
            }

            PreviewHintChannel.Write(outputPath, new PreviewHint(
                targets.Select(CellsPreviewPayloads.Hint).ToArray(),
                Guid.NewGuid().ToString("N"),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        }
        catch (Exception)
        {
            // Best effort by design; the edit result stands either way.
        }
    }
}
