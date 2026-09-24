using System.CommandLine;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Words.Commands;

internal static class EditCommand
{
    private const string BookmarkPrefix = "bookmark:";

    private static readonly BoundedEditDefinition<WordsOp, WordsOpsBatch> Definition = new()
    {
        Catalog = WordsOps.Catalog,
        Contracts = ProductJsonContext.Definition,
        SetDirectives = new(
            "Replace a bookmark's text as bookmark:NAME=TEXT; repeatable, applied after the --ops document. "
                + "Example: --set \"bookmark:ClientName=Contoso Ltd.\".",
            ParseSet,
            static ops => new WordsOpsBatch { Ops = ops }),
        VerifyDescription = "Compare the staged document after save and reopen to report semantic verification.",
        NormalizePaths = static (op, paths) => op switch
        {
            InsertImageOp value => value with { Path = paths.ResolveInput(value.Path) },
            AddWatermarkOp { ImagePath: not null } value => value with { ImagePath = paths.ResolveInput(value.ImagePath) },
            AppendDocumentOp value => value with { Path = paths.ResolveInput(value.Path) },
            MailMergeOp { Path: not null } value => value with { Path = paths.ResolveInput(value.Path) },
            _ => op,
        },
    };

    public static Command Create(IProductCommandHost<IDocumentEngine> host)
    {
        Argument<string> file = WordsOptions.File("Document to edit.");
        var edit = new BoundedEditCommand<WordsOp, WordsOpsBatch>(Definition);
        var trackChanges = new Option<bool>("--track-changes") { Description = "Track this batch as revisions." };
        var author = new Option<string?>("--author") { Description = "Revision author; required with --track-changes." }.WithInput(InputKind.None);
        var password = new PasswordOptions("--password", "the document");
        var encrypt = new PasswordOptions("--encrypt", "the output document", allowStdin: false);
        var fonts = new FontDirectoryOptions();

        var command = new Command("edit", "Apply one validated, atomic Words operation batch.");
        command.Arguments.Add(file);
        edit.AddTo(command);
        command.Options.Add(trackChanges);
        command.Options.Add(author);
        password.AddTo(command);
        encrypt.AddTo(command);
        fonts.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
        {
            string input = context.Paths.ResolveInput(parse.GetRequiredValue(file));
            BoundedEditInvocation<WordsOpsBatch> invocation = edit.Read(parse, context.Paths, context.Inputs, input);
            using IDisposable fontScope = fonts.Use(parse, context);
            return context.Port.ApplyOps(input, invocation.Batch, new WordsEditRequest
            {
                OutputPath = invocation.Target.OutputPath,
                Overwrite = invocation.Target.Overwrite,
                BackupPath = invocation.Target.BackupPath,
                Options = invocation.Options,
                Verify = invocation.Verify,
                TrackChanges = parse.GetValue(trackChanges),
                Author = parse.GetValue(author),
                Password = password.Resolve(parse, context.Inputs, context.ReadEnvironment, stdinAvailable: !invocation.OpsFromStandardInput),
                EncryptPassword = encrypt.Resolve(parse, context.Inputs, context.ReadEnvironment),
                OpSecrets = ResolveSecrets(invocation.Batch, context.ReadEnvironment),
            });
        }));
        return command;
    }

    private static WordsOp ParseSet(string value)
    {
        int equals = value.IndexOf('=');
        if (!value.StartsWith(BookmarkPrefix, StringComparison.Ordinal) || equals <= BookmarkPrefix.Length)
        {
            throw CliErrors.OptionInvalid("--set", $"invalid directive '{value}'", "Use bookmark:NAME=TEXT.");
        }

        return new SetTextOp
        {
            At = new WordsTarget { Bookmark = value[BookmarkPrefix.Length..equals] },
            Text = value[(equals + 1)..],
        };
    }

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
