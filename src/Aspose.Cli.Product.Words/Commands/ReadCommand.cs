using System.CommandLine;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

internal static class ReadCommand
{
    public static Command Create(IProductCommandHost<IWordsEngine> host)
    {
        var blocks = new Option<string?>("--blocks") { Description = "1-based block range, e.g. 1-20,25." }.WithInput(InputKind.None);
        var section = new Option<int?>("--section") { Description = "Read one 1-based section; with --blocks, only its blocks in that range." };
        var scope = new Option<string>("--scope")
        {
            Description = "Projection: text, full or outline.",
            DefaultValueFactory = _ => "text",
        }.WithInput(InputKind.None);
        scope.AcceptOnlyFromAmong([.. DocumentReadScopes.All]);
        var maxChars = new MaxCharactersOption("Maximum returned content characters, including repeated text/run projections.");
        var maxBlocks = new Option<int>("--max-blocks") { DefaultValueFactory = _ => 200, Description = "Maximum projected blocks." };
        return StandardCommand.Create(
            host,
            "blocks",
            "Read a bounded, stable window of document blocks.",
            new CommandTraits { Input = WordsCommands.Document },
            [blocks, section, scope, .. maxChars.Options, maxBlocks],
            (parse, standard) =>
            {
                string? range = parse.GetValue(blocks);
                int characters = maxChars.Read(parse);
                int count = parse.GetValue(maxBlocks);
                OptionGuards.EnsureInRange("--max-blocks", count, 1, 100_000, "Use a positive bounded block budget.");
                string input = standard.Input;
                var request = new DocumentReadRequest
                {
                    Blocks = range is null ? null : PageRange.Parse(range),
                    Section = parse.GetValue(section),
                    Scope = parse.GetValue(scope) ?? "text",
                    MaxCharacters = characters,
                    MaxBlocks = count,
                    Password = standard.InputPassword,
                };
                DocumentReadResult result = standard.OpenEngine().Read(input, request);
                return result with { Window = result.Window! with { Next = Next(standard.Continuation(), request, result) } };
            })
            .WithExamples(
            [
                "words query blocks contract.docx --blocks 1-30 --scope full --output json",
                "words query blocks contract.docx --section 2 --scope text",
            ]);
    }

    /// <summary>
    /// The read that resumes where this one stopped, or null when it covered the selection.
    /// The section and scope filters travel with it, so the block range may name blocks the
    /// filters then skip.
    /// </summary>
    private static string? Next(ContinuationCommand resume, DocumentReadRequest request, DocumentReadResult result)
    {
        if (!result.Window!.Truncated || result.BlockCount == 0)
        {
            return null;
        }

        IReadOnlyList<int> selection = request.Blocks?.Resolve(result.BlockCount, WordsDiagnostics.BlockNotFound, "block")
            ?? Enumerable.Range(1, result.BlockCount).ToArray();
        if (ReadContinuation.After(
                selection,
                [.. result.Blocks.Select(static block => new ReadPart(block.I, block.ContentTruncated))],
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
