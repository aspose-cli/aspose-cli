using System.CommandLine;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class ReadCommand
{
    public static Command Create(IProductCommandHost<IPdfEngine> host)
    {
        Argument<string> file = PdfOptions.File();
        var pages = new Option<string?>("--pages") { Description = "1-based page range, e.g. 1-3,7,9-." }.WithInput(InputKind.None);
        var mode = new Option<string>("--mode")
        {
            Description = "Text projection: plain or layout.",
            DefaultValueFactory = _ => PdfReadModes.Plain,
        }.WithInput(InputKind.None);
        mode.AcceptOnlyFromAmong([.. PdfReadModes.All]);
        var maxChars = new Option<int>("--max-chars")
        {
            Description = "Maximum projected characters.",
            DefaultValueFactory = _ => 20_000,
        };
        var password = new PasswordOptions("--password", "the PDF");
        var command = new Command("pages", "Read a bounded page-text window.");
        command.Arguments.Add(file);
        command.Options.Add(pages);
        command.Options.Add(mode);
        command.Options.Add(maxChars);
        password.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
        {
            int characters = parse.GetValue(maxChars);
            OptionGuards.EnsureInRange(
                "--max-chars", characters, 1, ReadContinuation.MaximumCharacters,
                "Use a positive bounded character budget.");
            string? range = parse.GetValue(pages);
            string input = context.Paths.ResolveInput(parse.GetRequiredValue(file));
            var request = new PdfReadRequest
            {
                Pages = range is null ? null : PageRange.Parse(range),
                Mode = parse.GetValue(mode) ?? PdfReadModes.Plain,
                MaxCharacters = characters,
                Password = password.Resolve(parse, context.Inputs, context.ReadEnvironment),
            };
            PdfReadResult result = context.Port.Read(input, request);
            return result with { Next = Next(input, request, result) };
        }));
        return command;
    }

    /// <summary>The read that resumes where this one stopped, or null when it covered the selection.</summary>
    private static string? Next(string input, PdfReadRequest request, PdfReadResult result)
    {
        if (!result.Window.Truncated || result.Window.Of == 0)
        {
            return null;
        }

        IReadOnlyList<int> selection = request.Pages?.Resolve(result.Window.Of)
            ?? Enumerable.Range(1, result.Window.Of).ToArray();
        return ReadContinuation.After(
                selection,
                [.. result.Pages.Select(static page => new ReadPart(page.Number, page.Truncated))],
                request.MaxCharacters) is { } continuation
            ? new ContinuationCommand("pdf", "query", "pages")
                .Argument(input)
                .Option("--pages", continuation.Parts)
                .Option("--mode", request.Mode)
                .Option("--max-chars", continuation.MaxCharacters)
                .ToString()
            : null;
    }
}
