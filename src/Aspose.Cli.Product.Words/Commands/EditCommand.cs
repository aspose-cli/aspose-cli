using System.CommandLine;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;

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
        SecretVariables = static op => [WordsOps.PasswordVariable(op)],
    };

    public static Command Create(IProductCommandHost<IDocumentEngine> host)
    {
        var trackChanges = new Option<bool>("--track-changes") { Description = "Track this batch as revisions." };
        var author = new Option<string?>("--author") { Description = "Revision author; required with --track-changes." }.WithInput(InputKind.None);
        return new BoundedEditCommand<WordsOp, WordsOpsBatch>(Definition).Create(
            host,
            "edit",
            "Apply one validated, atomic Words operation batch.",
            new CommandTraits
            {
                Input = WordsCommands.Document with { Description = "Document to edit." },
                Encrypt = WordsCommands.EncryptedDocument,
                UsesFonts = true,
            },
            [trackChanges, author],
            (parse, edit, standard) =>
            {
                string? encryptPassword = standard.EncryptPassword(WordsFormats.ForOutput(edit.Target.OutputPath));
                return standard.OpenEngine().ApplyOps(standard.Input, edit.Batch, new WordsEditRequest
                {
                    OutputPath = edit.Target.OutputPath,
                    Overwrite = edit.Target.Overwrite,
                    BackupPath = edit.Target.BackupPath,
                    Options = edit.Options,
                    Verify = edit.Verify,
                    TrackChanges = parse.GetValue(trackChanges),
                    Author = parse.GetValue(author),
                    Password = standard.InputPassword,
                    EncryptPassword = encryptPassword,
                    OpSecrets = edit.Secrets,
                });
            },
            checkUsage: parse =>
            {
                if (parse.GetValue(trackChanges) && string.IsNullOrWhiteSpace(parse.GetValue(author)))
                {
                    throw CliErrors.OptionInvalid(
                        "--author",
                        "--track-changes requires a non-empty author",
                        "Pass --author with the person or agent responsible for the edit.");
                }
            })
            .WithExamples(
            [
                "words edit contract.docx --in-place --backup --verify --set \"bookmark:Client=Contoso\"",
                "words edit contract.docx --in-place --backup --ops ops.json --verify",
            ],
            [
                CommandHelpLink.Docs(WordsModule.Manifest, "editing", "addressing and operation recipes"),
                CommandHelpLink.Schema(WordsModule.Manifest, "the exact edit-batch contract"),
            ]);
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
}
