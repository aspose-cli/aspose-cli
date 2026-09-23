using System.CommandLine;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Words.Commands;

internal static class EditCommand
{
    public static Command Create(IProductCommandHost<IDocumentEngine> host)
    {
        Argument<string> file = WordsOptions.File("Document to edit.");
        var ops = new Option<string?>("--ops") { Description = "Ops JSON path, inline JSON, or '-' for stdin." }.WithInput(InputKind.JsonSource);
        var set = new Option<string[]>("--set") { Description = "Bookmark sugar: bookmark:Name=text; repeatable." }.WithInput(InputKind.None);
        var output = new MutationFileOptions();
        var editOptions = new BoundedEditOptions();
        var verify = new Option<bool>("--verify") { Description = "Compare the staged document after save and reopen to report semantic verification." };
        var trackChanges = new Option<bool>("--track-changes") { Description = "Track this batch as revisions." };
        var author = new Option<string?>("--author") { Description = "Revision author; required with --track-changes." }.WithInput(InputKind.None);
        var password = new PasswordOptions("--password", "the document");
        var encrypt = new PasswordOptions("--encrypt", "the output document", allowStdin: false);

        var command = new Command("edit", "Apply one validated, atomic Words operation batch.");
        command.Arguments.Add(file);
        command.Options.Add(ops);
        command.Options.Add(set);
        output.AddTo(command);
        editOptions.AddTo(command);
        command.Options.Add(verify);
        command.Options.Add(trackChanges);
        command.Options.Add(author);
        password.AddTo(command);
        encrypt.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
        {
            string? opsSource = parse.GetValue(ops);
            string[] directives = parse.GetValue(set) ?? [];
            WordsOpsBatch batch = BuildBatch(opsSource, directives, context);
            batch = NormalizePaths(batch, context);
            bool isDryRun = parse.GetValue(editOptions.DryRun);
            if (isDryRun && parse.GetValue(verify))
            {
                throw CliErrors.OptionInvalid("--verify", "cannot be combined with --dry-run", "Run the dry run, then save with --verify.");
            }

            string input = context.Paths.ResolveInput(parse.GetRequiredValue(file));
            MutationTarget target = output.Resolve(parse, context.Paths, input, requireBackup: true);
            WordsEditResult result = context.Port.ApplyOps(input, batch, new WordsEditRequest
            {
                OutputPath = target.OutputPath,
                Overwrite = target.Overwrite,
                BackupPath = target.BackupPath,
                Options = editOptions.Read(parse, batch.IfMatch),
                Verify = parse.GetValue(verify),
                TrackChanges = parse.GetValue(trackChanges),
                Author = parse.GetValue(author),
                Password = password.Resolve(parse, context.Inputs, context.ReadEnvironment, stdinAvailable: opsSource != "-"),
                EncryptPassword = encrypt.Resolve(parse, context.Inputs, context.ReadEnvironment),
                OpSecrets = ResolveSecrets(batch, context.ReadEnvironment),
            });
            return result;
        }));
        return command;
    }

    internal static WordsOpsBatch BuildBatch(
        string? source,
        IReadOnlyList<string> directives,
        ProductCommandContext<IDocumentEngine> context)
    {
        if (source is null && directives.Count == 0)
        {
            throw CliErrors.Usage(["Give --ops, --set, or both."]);
        }

        WordsOpsBatch? document = source is null
            ? null
            : WordsOpsParser.Parse(JsonInputSource.Read(source, context.Paths, context.Inputs, "--ops"));
        var sugar = directives.Select(ParseSet).Cast<WordsOp>().ToArray();
        return document is null
            ? new WordsOpsBatch { Ops = sugar }
            : sugar.Length == 0 ? document : document with { Ops = [.. document.Ops, .. sugar] };
    }

    private static SetTextOp ParseSet(string value)
    {
        int equals = value.IndexOf('=');
        const string prefix = "bookmark:";
        if (!value.StartsWith(prefix, StringComparison.Ordinal) || equals <= prefix.Length)
        {
            throw CliErrors.OptionInvalid("--set", $"invalid directive '{value}'", "Use bookmark:Name=text.");
        }

        return new SetTextOp
        {
            At = new WordsTarget { Bookmark = value[prefix.Length..equals] },
            Text = value[(equals + 1)..],
        };
    }

    private static WordsOpsBatch NormalizePaths(
        WordsOpsBatch batch,
        ProductCommandContext<IDocumentEngine> context) =>
        batch with
        {
            Ops = batch.Ops.Select(op => op switch
            {
                InsertImageOp value => value with { Path = context.Paths.ResolveInput(value.Path) },
                AddWatermarkOp { ImagePath: not null } value => value with { ImagePath = context.Paths.ResolveInput(value.ImagePath) },
                AppendDocumentOp value => value with { Path = context.Paths.ResolveInput(value.Path) },
                MailMergeOp { Path: not null } value => value with { Path = context.Paths.ResolveInput(value.Path) },
                _ => op,
            }).ToArray(),
        };

    private static IReadOnlyDictionary<int, string>? ResolveSecrets(WordsOpsBatch batch, Func<string, string?> readEnvironment)
    {
        var values = new Dictionary<int, string>();
        for (int index = 0; index < batch.Ops.Count; index++)
        {
            string? variable = batch.Ops[index] switch
            {
                ProtectOp value => value.PasswordEnv,
                UnprotectOp value => value.PasswordEnv,
                _ => null,
            };
            if (variable is null)
            {
                continue;
            }

            string? secret = readEnvironment(variable);
            if (string.IsNullOrEmpty(secret))
            {
                throw CliErrors.OptionInvalid("passwordEnv", $"environment variable '{variable}' is missing or empty", "Set it before running the edit.");
            }

            values[index] = secret;
        }

        return values.Count == 0 ? null : values;
    }
}
