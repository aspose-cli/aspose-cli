using System.CommandLine;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class EditCommand
{
    public static Command Create(IProductCommandHost<IPresentationEngine> host)
    {
        Argument<string> file = SlidesOptions.File();
        var ops = new Option<string>("--ops") { Required = true, Description = "Ops JSON path, inline JSON, or '-' for stdin." }.WithInput(InputKind.JsonSource);
        var output = new MutationFileOptions();
        var editOptions = new BoundedEditOptions();
        var verify = new Option<bool>("--verify") { Description = "Reopen and render touched slides after save." };
        var password = new PasswordOptions("--password", "the presentation");
        var encrypt = new PasswordOptions("--encrypt", "the output presentation", allowStdin: false);
        var command = new Command("edit", "Apply one validated, atomic presentation operation batch.");
        command.Arguments.Add(file);
        command.Options.Add(ops);
        output.AddTo(command);
        editOptions.AddTo(command);
        command.Options.Add(verify);
        password.AddTo(command);
        encrypt.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
        {
            string source = parse.GetRequiredValue(ops);
            SlidesOpsBatch batch = SlidesOpsParser.Parse(
                JsonInputSource.Read(source, context.Paths, context.Inputs, "--ops"));
            batch = NormalizePaths(batch, context);
            bool isDryRun = parse.GetValue(editOptions.DryRun);
            if (isDryRun && parse.GetValue(verify))
            {
                throw CliErrors.OptionInvalid(
                    "--verify",
                    "cannot be combined with --dry-run",
                    "Run the dry run, then save with --verify.");
            }

            string input = context.Paths.ResolveInput(parse.GetRequiredValue(file));
            MutationTarget target = output.Resolve(parse, context.Paths, input, requireBackup: true);
            return context.Port.ApplyOps(input, batch, new PresentationEditRequest
            {
                OutputPath = target.OutputPath,
                Overwrite = target.Overwrite,
                OverwriteArtifacts = target.OverwriteArtifacts,
                BackupPath = target.BackupPath,
                Options = editOptions.Read(parse, batch.IfMatch),
                Verify = parse.GetValue(verify),
                Password = password.Resolve(parse, context.Inputs, context.ReadEnvironment, stdinAvailable: source != "-"),
                EncryptPassword = encrypt.Resolve(parse, context.Inputs, context.ReadEnvironment),
            });
        }));
        return command;
    }

    private static SlidesOpsBatch NormalizePaths(
        SlidesOpsBatch batch,
        ProductCommandContext<IPresentationEngine> context) =>
        batch with
        {
            Ops = batch.Ops.Select(op => op switch
            {
                SetBackgroundOp { ImagePath: not null } value =>
                    value with { ImagePath = context.Paths.ResolveInput(value.ImagePath) },
                AppendPresentationOp value =>
                    value with { Path = context.Paths.ResolveInput(value.Path) },
                SlidesInsertImageOp value =>
                    value with { Path = context.Paths.ResolveInput(value.Path) },
                _ => op,
            }).ToArray(),
        };
}
