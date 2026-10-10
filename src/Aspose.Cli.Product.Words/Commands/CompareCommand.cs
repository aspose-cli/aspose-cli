using System.CommandLine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Words.Commands;

internal static class CompareCommand
{
    public static CommandDefinition<WordsCompareRequest, WordsCompareResult> Create()
    {
        var ignoreFormatting = new Option<bool>("--ignore-formatting") { Description = "Ignore formatting-only changes." };
        var granularity = new Option<string>("--granularity")
        {
            Description = "Unit a change is marked in: word, or char for Chinese or Japanese text.",
            DefaultValueFactory = _ => WordsCompareRequest.DefaultGranularity,
        }.WithInput(InputKind.None);
        granularity.AcceptOnlyFromAmong("word", "char");
        var author = new Option<string?>("--author") { Description = "Author of the redline's revisions; default: Aspose CLI." }.WithInput(InputKind.None);
        return new(
            "compare",
            "Semantically compare two documents and optionally save a redline.",
            new CommandTraits
            {
                Input = new InputDocument("Original document.", "the original document", "left"),
                Other = new InputDocument("Changed document.", "the changed document", "right"),
                Output = OutputTarget.File("Optional redline output; its extension selects the format, such as .docx or .pdf.", WordsFormats.Writable),
                UsesFonts = true,
            },
            [ignoreFormatting, granularity, author],
            (parse, standard) =>
            {
                string? name = parse.GetValue(author);
                if (name is not null && string.IsNullOrWhiteSpace(name))
                {
                    throw CliErrors.OptionInvalid(
                        "--author",
                        "the author is empty",
                        "Pass the person or agent the redline's revisions are attributed to, or omit --author.");
                }

                return new WordsCompareRequest
                {
                    Left = standard.Input,
                    Right = standard.Other,
                    IgnoreFormatting = parse.GetValue(ignoreFormatting),
                    Granularity = parse.GetValue(granularity)!,
                    Author = name,
                    Output = standard.RequestedOutputPath() is null ? null : standard.Output,
                    LeftPassword = standard.InputPassword,
                    RightPassword = standard.OtherPassword,
                };
            },
            Table)
        {
            Examples =
            [
                "words compare original.docx changed.docx --output json",
                "words compare original.docx changed.docx --out redline.docx --author \"Legal Review\"",
            ],
        };
    }

    internal static void Table(WordsCompareResult result, TableSurface surface)
    {
        surface.Out.WriteLine(result.Identical ? "documents are identical" : "documents differ");
        surface.Out.WriteLine($"insertions: {result.Revisions.InsertionCount}   deletions: {result.Revisions.DeletionCount}   formatting: {result.Revisions.FormatChangeCount}   moves: {result.Revisions.MoveCount}");
        if (result.Samples.Count > 0)
        {
            var table = new TextTable("type", "sample");
            foreach (RevisionSample sample in result.Samples)
            {
                table.AddRow(sample.Type, sample.Text ?? string.Empty);
            }

            table.WriteTo(surface.Out, surface.Format);
        }

        if (result.Output is { } output)
        {
            surface.Out.WriteLine($"redline: {output.Path}");
        }
    }
}
