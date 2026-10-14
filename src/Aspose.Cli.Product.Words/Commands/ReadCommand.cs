using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Words.Commands;

internal static class ReadCommand
{
    public static CommandDefinition<WordsReadRequest, WordsReadResult> Create()
    {
        var blocks = new Option<string?>("--blocks") { Description = "1-based block range, e.g. 1-20,25." }.WithInput(InputKind.None);
        var section = new Option<int?>("--section") { Description = "Read one 1-based section; with --blocks, only its blocks in that range." };
        var content = new Option<string>("--content")
        {
            Description = "How much of each block to return: its text, only the headings, or the text with its runs and paragraph format.",
            DefaultValueFactory = _ => WordsReadContents.Text,
        }.WithInput(InputKind.None);
        content.AcceptOnlyFromAmong([.. WordsReadContents.All]);
        var maxChars = new MaxCharactersOption("the text of the returned blocks, including repeated text and run projections");
        var maxBlocks = new Option<int>("--max-blocks") { DefaultValueFactory = _ => 200, Description = "Maximum projected blocks." }.InRange(1, 100_000);
        return new(
            "read",
            "Read a bounded, stable window of document blocks.",
            new CommandTraits { Input = WordsInputs.Document },
            [blocks, section, content, .. maxChars.Options, maxBlocks],
            (parse, standard) =>
            {
                string? range = parse.GetValue(blocks);
                int characters = maxChars.Read(parse);
                int count = parse.GetInRange(maxBlocks, "Use a positive bounded block budget.");
                return new WordsReadRequest
                {
                    Input = standard.Input,
                    Blocks = range is null ? null : PageRange.ParseOption(blocks.Name, range),
                    Section = parse.GetValue(section),
                    Content = parse.GetValue(content) ?? WordsReadContents.Text,
                    MaxCharacters = characters,
                    MaxBlocks = count,
                    Password = standard.InputPassword,
                };
            },
            Table)
        {
            MistakenFor = ["query"],
            Finish = static (_, request, result, standard) =>
                result with { Window = result.Window with { Next = Next(standard.Continuation(), request, result) } },
            Examples =
            [
                "words read contract.docx --blocks 1-30 --content full --output-format json",
                "words read contract.docx --section 2 --content outline",
            ],
            Links =
            [
                CommandHelpLink.Docs(WordsModule.Manifest, "editing", "the document block model and edit operations"),
                new("aspose-cli docs reading", "reading every product within bounded windows"),
            ],
        };
    }

    internal static void Table(WordsReadResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"{result.Input.Path} (content {result.Content}, {result.BlockCount} blocks in the document)");
        var table = new TextTable("block", "kind", "section", "style", "text");
        foreach (WordsBlockData block in result.Blocks)
        {
            table.AddRow(TableText.Int(block.Block), block.Kind, TableText.Int(block.Section), block.Style, block.Text ?? $"[{block.RowCount}x{block.ColumnCount} table]");
        }

        table.WriteTo(surface.Out, surface.Format);
    }

    /// <summary>
    /// The read that resumes where this one stopped, or null when it covered the selection.
    /// The section and content filters travel with it, so the block range may name blocks the
    /// filters then skip.
    /// </summary>
    private static string? Next(ContinuationCommand resume, WordsReadRequest request, WordsReadResult result)
    {
        if (!result.Window.Truncated || result.BlockCount == 0)
        {
            return null;
        }

        IReadOnlyList<int> selection = request.Blocks?.Resolve(result.BlockCount, WordsDiagnostics.BlockNotFound, "block")
            ?? Enumerable.Range(1, result.BlockCount).ToArray();
        if (ReadContinuation.After(
                selection,
                [.. result.Blocks.Select(static block => new ReadPart(block.Block, block.ContentTruncated))],
                request.MaxCharacters) is not { } continuation)
        {
            return null;
        }

        ContinuationCommand next = resume
            .Option("--blocks", continuation.Parts);
        if (request.Section is { } section)
        {
            next.Option("--section", section);
        }

        return next
            .Option("--content", request.Content)
            .Option(MaxCharactersOption.Name, continuation.MaxCharacters)
            .Option("--max-blocks", request.MaxBlocks)
            .ToString();
    }
}
