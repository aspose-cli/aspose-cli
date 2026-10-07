using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

internal static class InfoCommand
{
    private static readonly string[] Details =
        ["outline", "sections", "styles", "fields", "bookmarks", "comments", "revisions", "images", "tables", "properties", "fonts"];

    public static Command Create(IProductCommandHost<IWordsEngine> host)
    {
        var preview = new Option<bool>("--preview") { Description = "Include a bounded outline preview." };
        var detail = new Option<string[]>("--detail")
        {
            Description = "Extra structural projections; repeatable.",
            AllowMultipleArgumentsPerToken = true,
        }.WithInput(InputKind.None);
        detail.AcceptOnlyFromAmong(Details);
        return StandardCommand.Create(
            host,
            "inspect",
            "Show document structure, safety state and metadata.",
            new CommandTraits { Input = WordsCommands.Document, UsesFonts = true },
            [preview, detail],
            (parse, standard) => standard.OpenEngine().GetInfo(standard.Input, new DocumentInfoRequest
            {
                IncludePreview = parse.GetValue(preview),
                Details = parse.GetValue(detail),
                Password = standard.InputPassword,
            }))
            .WithExamples(
            [
                "words inspect contract.docx --output json",
                "words inspect contract.docx --detail outline sections fields bookmarks --preview",
            ]);
    }
}
