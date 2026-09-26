using System.CommandLine;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class ReadCommand
{
    public static Command Create(IProductCommandHost<IPdfEngine> host)
    {
        var pages = new Option<string?>("--pages") { Description = "1-based page range, e.g. 1-3,7,9-." }.WithInput(InputKind.None);
        var mode = new Option<string>("--mode")
        {
            Description = "Text projection: plain or layout.",
            DefaultValueFactory = _ => PdfReadModes.Plain,
        }.WithInput(InputKind.None);
        mode.AcceptOnlyFromAmong([.. PdfReadModes.All]);
        var maxChars = new MaxCharactersOption("Maximum projected characters.");
        return StandardCommand.Create(
            host,
            "pages",
            "Read a bounded page-text window.",
            new CommandTraits { Input = PdfCommands.Document },
            [pages, mode, .. maxChars.Options],
            (parse, standard) =>
            {
                int characters = maxChars.Read(parse);
                string? range = parse.GetValue(pages);
                string input = standard.Input;
                var request = new PdfReadRequest
                {
                    Pages = range is null ? null : PageRange.Parse(range),
                    Mode = parse.GetValue(mode) ?? PdfReadModes.Plain,
                    MaxCharacters = characters,
                    Password = standard.InputPassword,
                };
                PdfReadResult result = standard.OpenEngine().Read(input, request);
                return result with { Window = result.Window! with { Next = Next(standard.Continuation(), request, result) } };
            });
    }

    /// <summary>The read that resumes where this one stopped, or null when it covered the selection.</summary>
    private static string? Next(ContinuationCommand resume, PdfReadRequest request, PdfReadResult result)
    {
        if (!result.Window!.Truncated)
        {
            return null;
        }

        IReadOnlyList<int> selection = request.Pages?.Resolve(result.PageCount)
            ?? Enumerable.Range(1, result.PageCount).ToArray();
        return ReadContinuation.After(
                selection,
                [.. result.Pages.Select(static page => new ReadPart(page.Number, page.Truncated))],
                request.MaxCharacters) is { } continuation
            ? resume
                .Option("--pages", continuation.Parts)
                .Option("--mode", request.Mode)
                .Option(MaxCharactersOption.Name, continuation.MaxCharacters)
                .ToString()
            : null;
    }
}
