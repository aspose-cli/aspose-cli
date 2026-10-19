using System.CommandLine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Words.Commands;

internal static class EditCommand
{
    private const string BookmarkPrefix = "bookmark:";

    private static readonly BoundedEditDefinition<WordsOp, WordsOpsBatch> Definition = new()
    {
        Contracts = ProductJsonContext.Definition,
        SetDirectives = new(
            "Replace a bookmark's text as bookmark:NAME=TEXT; repeatable, applied after the --ops document. "
                + "Example: --set \"bookmark:ClientName=Contoso Ltd.\".",
            ParseSet,
            static ops => new WordsOpsBatch { Ops = ops }),
        VerifyDescription = "Compare the staged document after save and reopen to report semantic verification.",
        Writes = WordsFormats.Editable,
    };

    public static CommandDefinition<WordsEditRequest, WordsEditResult> Create()
    {
        var trackChanges = new Option<bool>("--track-changes") { Description = "Track this batch as revisions." };
        var author = new Option<string?>("--author") { Description = "Revision author; required with --track-changes." }.WithInput(InputKind.None);
        return EditDefinition.Create<WordsOp, WordsOpsBatch, WordsEditRequest, WordsEditResult>(
            new BoundedEditCommand<WordsOp, WordsOpsBatch>(Definition),
            "Apply one validated, atomic Words operation batch.",
            new CommandTraits
            {
                Input = WordsInputs.Document with { Description = "Document to edit." },
                Encrypt = WordsInputs.EncryptedDocument,
                UsesFonts = true,
            },
            [trackChanges, author],
            (parse, edit, _) => new WordsEditRequest(edit)
            {
                TrackChanges = parse.GetValue(trackChanges),
                Author = parse.GetValue(author),
            },
            Table,
            checkUsage: parse =>
            {
                if (parse.GetValue(trackChanges) && string.IsNullOrWhiteSpace(parse.GetValue(author)))
                {
                    throw CliErrors.OptionInvalid(
                        "--author",
                        "--track-changes requires a non-empty author",
                        "Pass --author with the person or agent responsible for the edit.");
                }
            },
            examples:
            [
                "words edit contract.docx --in-place --backup --verify --set \"bookmark:Client=Contoso\"",
                "words edit contract.docx --in-place --backup --ops ops.json --verify",
            ],
            links:
            [
                CommandHelpLink.Docs(WordsModule.Manifest, "editing", "addressing and operation recipes"),
                CommandHelpLink.Schema<WordsOp>("the exact edit-batch contract"),
            ],
            // Appending documents is the import_sections operation of an edit.
            mistakenFor: ["merge"],
            mistakenForHint: "To merge documents, run 'words edit' on the first with an import_sections operation for each other document.");
    }

    internal static void Table(WordsEditResult result, TableSurface surface)
    {
        ResultText.Edit(surface, result.DryRun, result.Output, result.Applied, result.Backup);
        ResultText.Touched(surface, "pages", result.PagesTouched);
        ResultText.Verification(surface, result.Verification);
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
            Target = new WordsTarget { Bookmark = value[BookmarkPrefix.Length..equals] },
            Text = value[(equals + 1)..],
        };
    }
}
