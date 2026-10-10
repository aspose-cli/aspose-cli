using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Words.Commands;

internal static class ReadCommand
{
    public static CommandDefinition<DocumentReadRequest, DocumentReadResult> Create()
    {
        var blocks = new Option<string?>("--blocks") { Description = "1-based block range, e.g. 1-20,25." }.WithInput(InputKind.None);
        var section = new Option<int?>("--section") { Description = "Read one 1-based section; with --blocks, only its blocks in that range." };
        var scope = new Option<string>("--scope")
        {
            Description = "Projection: text, full or outline.",
            DefaultValueFactory = _ => "text",
        }.WithInput(InputKind.None);
        scope.AcceptOnlyFromAmong([.. DocumentReadScopes.All]);
        var maxChars = new MaxCharactersOption("the text of the returned blocks, including repeated text and run projections");
        var maxBlocks = new Option<int>("--max-blocks") { DefaultValueFactory = _ => 200, Description = "Maximum projected blocks." };
        return new(
            "blocks",
            "Read a bounded, stable window of document blocks.",
            new CommandTraits { Input = WordsInputs.Document },
            [blocks, section, scope, .. maxChars.Options, maxBlocks],
            (parse, standard) =>
            {
                string? range = parse.GetValue(blocks);
                int characters = maxChars.Read(parse);
                int count = parse.GetValue(maxBlocks);
                OptionGuards.EnsureInRange("--max-blocks", count, 1, 100_000, "Use a positive bounded block budget.");
                return new DocumentReadRequest
                {
                    Input = standard.Input,
                    Blocks = range is null ? null : PageRange.Parse(range),
                    Section = parse.GetValue(section),
                    Scope = parse.GetValue(scope) ?? "text",
                    MaxCharacters = characters,
                    MaxBlocks = count,
                    Password = standard.InputPassword,
                };
            },
            Table)
        {
            Finish = static (_, request, result, standard) =>
                result with { Window = result.Window with { Next = Next(standard.Continuation(), request, result) } },
            Examples =
            [
                "words query blocks contract.docx --blocks 1-30 --scope full --output json",
                "words query blocks contract.docx --section 2 --scope text",
            ],
        };
    }

    internal static void Table(DocumentReadResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"{result.Source.Path} (scope {result.Scope}, {result.BlockCount} blocks in the document)");
        var table = new TextTable("block", "type", "section", "style", "text");
        foreach (BlockData block in result.Blocks)
        {
            table.AddRow(TableText.Int(block.Block), block.Type, TableText.Int(block.Section), block.Style ?? "-", block.Text ?? $"[{block.RowCount}x{block.ColumnCount} table]");
        }

        table.WriteTo(surface.Out, surface.Format);
    }

    /// <summary>
    /// The read that resumes where this one stopped, or null when it covered the selection.
    /// The section and scope filters travel with it, so the block range may name blocks the
    /// filters then skip.
    /// </summary>
    private static string? Next(ContinuationCommand resume, DocumentReadRequest request, DocumentReadResult result)
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
            .Option("--scope", request.Scope)
            .Option(MaxCharactersOption.Name, continuation.MaxCharacters)
            .Option("--max-blocks", request.MaxBlocks)
            .ToString();
    }
}
