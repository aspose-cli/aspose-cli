using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

internal static class InspectCommand
{
    private static readonly Dictionary<string, string> DetailNotes = new(StringComparer.Ordinal)
    {
        [WordsInspectDetails.Outline] = "headings",
        [WordsInspectDetails.Sections] = "page setup, headers and footers",
        [WordsInspectDetails.Properties] = "title, author, subject and keywords",
    };

    public static CommandDefinition<WordsInspectRequest, WordsInspectResult> Create()
    {
        var detail = new DetailOption(WordsInspectDetails.All, DetailNotes);
        return new(
            "inspect",
            "Show document structure, safety state and metadata.",
            new CommandTraits { Input = WordsInputs.Document, UsesFonts = true },
            [.. detail.Options],
            (parse, standard) => new WordsInspectRequest
            {
                Input = standard.Input,
                Details = detail.Read(parse),
                Password = standard.InputPassword,
            })
        {
            Examples =
            [
                "words inspect contract.docx --output-format json",
                "words inspect contract.docx --detail outline --detail sections --detail fields --detail bookmarks",
            ],
            Links =
            [
                CommandHelpLink.Docs(WordsModule.Manifest, "overview", "what each Words command is for"),
                new("aspose-cli docs reading", "reading every product within bounded windows"),
            ],
        };
    }
}
