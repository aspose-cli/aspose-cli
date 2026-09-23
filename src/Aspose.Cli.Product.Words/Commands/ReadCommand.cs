using System.CommandLine;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

internal static class ReadCommand
{
    public static Command Create(IProductCommandHost<IDocumentEngine> host)
    {
        Argument<string> file = WordsOptions.File();
        var blocks = new Option<string?>("--blocks") { Description = "1-based block range, e.g. 1-20,25." }.WithInput(InputKind.None);
        var section = new Option<int?>("--section") { Description = "Read one 1-based section; with --blocks, only its blocks in that range." };
        var scope = new Option<string>("--scope")
        {
            Description = "Projection: text, full or outline.",
            DefaultValueFactory = _ => "text",
        }.WithInput(InputKind.None);
        scope.AcceptOnlyFromAmong([.. DocumentReadScopes.All]);
        var maxChars = new Option<int>("--max-chars") { DefaultValueFactory = _ => 20_000, Description = "Maximum returned content characters, including repeated text/run projections." };
        var maxBlocks = new Option<int>("--max-blocks") { DefaultValueFactory = _ => 200, Description = "Maximum projected blocks." };
        var password = new PasswordOptions("--password", "the document");

        var command = new Command("blocks", "Read a bounded, stable window of document blocks.");
        command.Arguments.Add(file);
        command.Options.Add(blocks);
        command.Options.Add(section);
        command.Options.Add(scope);
        command.Options.Add(maxChars);
        command.Options.Add(maxBlocks);
        password.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
        {
            string? range = parse.GetValue(blocks);
            int characters = parse.GetValue(maxChars);
            int count = parse.GetValue(maxBlocks);
            OptionGuards.EnsureInRange("--max-chars", characters, 1, ReadContinuation.MaximumCharacters, "Use a positive bounded character budget.");
            OptionGuards.EnsureInRange("--max-blocks", count, 1, 100_000, "Use a positive bounded block budget.");
            string input = context.Paths.ResolveInput(parse.GetRequiredValue(file));
            var request = new DocumentReadRequest
            {
                Blocks = range is null ? null : PageRange.Parse(range),
                Section = parse.GetValue(section),
                Scope = parse.GetValue(scope) ?? "text",
                MaxCharacters = characters,
                MaxBlocks = count,
                Password = password.Resolve(parse, context.Inputs, context.ReadEnvironment),
            };
            DocumentReadResult result = context.Port.Read(input, request);
            return result with { Next = Next(input, request, result) };
        }));
        return command;
    }

    /// <summary>
    /// The read that resumes where this one stopped, or null when it covered the selection.
    /// The section and scope filters travel with it, so the block range may name blocks the
    /// filters then skip.
    /// </summary>
    private static string? Next(string input, DocumentReadRequest request, DocumentReadResult result)
    {
        if (!result.Window.Truncated || result.Window.Of == 0)
        {
            return null;
        }

        IReadOnlyList<int> selection = request.Blocks?.Resolve(result.Window.Of)
            ?? Enumerable.Range(1, result.Window.Of).ToArray();
        if (ReadContinuation.After(
                selection,
                [.. result.Blocks.Select(static block => new ReadPart(block.I, block.ContentTruncated))],
                request.MaxCharacters) is not { } continuation)
        {
            return null;
        }

        var next = new ContinuationCommand("words", "query", "blocks")
            .Argument(input)
            .Option("--blocks", continuation.Parts);
        if (request.Section is { } section)
        {
            next.Option("--section", section);
        }

        return next
            .Option("--scope", request.Scope)
            .Option("--max-chars", continuation.MaxCharacters)
            .Option("--max-blocks", request.MaxBlocks)
            .ToString();
    }
}
