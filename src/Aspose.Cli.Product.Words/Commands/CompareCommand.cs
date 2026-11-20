using System.CommandLine;
using System.Globalization;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

/// <summary>
/// <c>words compare</c>: Document.Compare(changed, author, dateTime, CompareOptions) on a copy of
/// the original, saved as the redline. The engine's call takes the author and the date and time,
/// so both are required; each CompareOptions member not given keeps the engine's default.
/// </summary>
internal static class CompareCommand
{
    private const string DateTimeForm = "yyyy-MM-ddTHH:mm:ss";

    public static CommandDefinition<WordsCompareRequest, WordsCompareResult> Create()
    {
        var author = new Option<string>("--author") { Required = true, Description = "The author of the redline's revisions, Document.Compare's author." }.WithInput(InputKind.None);
        var dateTime = new Option<string>("--date-time") { Required = true, Description = $"The date and time of the redline's revisions, Document.Compare's dateTime, written {DateTimeForm}." }.WithInput(InputKind.None);
        var granularity = new Option<string?>("--granularity") { Description = "CompareOptions.Granularity, a member name of the engine's Granularity, such as WordLevel or CharLevel. Default: the engine's." }.WithInput(InputKind.None);
        Option<bool>[] ignore =
        [
            new("--ignore-formatting") { Description = "Set CompareOptions.IgnoreFormatting to true." },
            new("--ignore-case-changes") { Description = "Set CompareOptions.IgnoreCaseChanges to true." },
            new("--ignore-comments") { Description = "Set CompareOptions.IgnoreComments to true." },
            new("--ignore-tables") { Description = "Set CompareOptions.IgnoreTables to true." },
            new("--ignore-fields") { Description = "Set CompareOptions.IgnoreFields to true." },
            new("--ignore-footnotes") { Description = "Set CompareOptions.IgnoreFootnotes to true." },
            new("--ignore-textboxes") { Description = "Set CompareOptions.IgnoreTextboxes to true." },
            new("--ignore-headers-and-footers") { Description = "Set CompareOptions.IgnoreHeadersAndFooters to true." },
        ];
        return new(
            "compare",
            "Compare a changed document with the original, Document.Compare, and write the redline: a copy of the original whose revisions are the differences.",
            new CommandTraits
            {
                Input = new InputDocument("Original document.", "the original document", "left"),
                Other = new InputDocument("Changed document.", "the changed document", "right"),
                Output = OutputTarget.File("The redline's path; its extension selects the format, such as .docx or .pdf.", WordsFormats.Writable),
                UsesFonts = true,
            },
            [author, dateTime, granularity, .. ignore],
            (parse, standard) =>
            {
                string text = parse.GetRequiredValue(dateTime);
                if (!DateTime.TryParseExact(text, DateTimeForm, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime when))
                {
                    throw CliErrors.OptionInvalid("--date-time", $"'{text}' is not a date and time written {DateTimeForm}");
                }

                return new WordsCompareRequest
                {
                    Left = standard.Input,
                    Right = standard.Other,
                    Author = parse.GetRequiredValue(author),
                    DateTime = when,
                    Granularity = parse.GetValue(granularity),
                    Ignore = [.. ignore.Where(option => parse.GetValue(option)).Select(static option => option.Name)],
                    Output = standard.Output,
                    LeftPassword = standard.InputPassword,
                    RightPassword = standard.OtherPassword,
                };
            })
        {
            Examples =
            [
                "words compare original.docx changed.docx --author \"Legal Review\" --date-time 2026-03-01T09:00:00 --out redline.docx",
                "words compare original.docx changed.docx --author Review --date-time 2026-03-01T09:00:00 --ignore-formatting --granularity CharLevel --out redline.pdf",
            ],
            Links =
            [
                CommandHelpLink.Docs(WordsModule.Manifest, "overview", "what each Words command is for"),
            ],
        };
    }
}
