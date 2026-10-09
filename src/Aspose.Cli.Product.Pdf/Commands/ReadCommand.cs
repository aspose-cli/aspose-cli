using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class ReadCommand
{
    public static CommandDefinition<PdfReadRequest, PdfReadResult> Create()
    {
        var pages = new PartRangeOption("page", "every page");
        var mode = new Option<string>("--mode")
        {
            Description = "Text projection: plain or layout.",
            DefaultValueFactory = _ => PdfReadModes.Plain,
        }.WithInput(InputKind.None);
        mode.AcceptOnlyFromAmong([.. PdfReadModes.All]);
        var maxChars = new MaxCharactersOption("the text of the returned pages");
        return new(
            "pages",
            "Read a bounded page-text window.",
            new CommandTraits { Input = PdfInputs.Document },
            [.. pages.Options, mode, .. maxChars.Options],
            (parse, standard) =>
            {
                int characters = maxChars.Read(parse);
                string? range = pages.Read(parse);
                return new PdfReadRequest
                {
                    Input = standard.Input,
                    Pages = range is null ? null : PageRange.Parse(range),
                    Mode = parse.GetValue(mode) ?? PdfReadModes.Plain,
                    MaxCharacters = characters,
                    Password = standard.InputPassword,
                };
            },
            Table)
        {
            Finish = static (_, request, result, standard) =>
                result with { Window = result.Window with { Next = Next(standard.Continuation(), request, result) } },
        };
    }

    internal static void Table(PdfReadResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"{result.Source.Path}: {result.PageCount} page(s) ({result.Mode})");
        foreach (PdfPageText page in result.Pages)
        {
            surface.Out.WriteLine();
            surface.Out.WriteLine($"--- page {page.Page}{(page.Truncated ? " (truncated)" : string.Empty)} ---");
            surface.Out.WriteLine(page.Text);
        }
    }

    /// <summary>The read that resumes where this one stopped, or null when it covered the selection.</summary>
    private static string? Next(ContinuationCommand resume, PdfReadRequest request, PdfReadResult result)
    {
        if (!result.Window.Truncated)
        {
            return null;
        }

        IReadOnlyList<int> selection = request.Pages?.Resolve(result.PageCount)
            ?? Enumerable.Range(1, result.PageCount).ToArray();
        return ReadContinuation.After(
                selection,
                [.. result.Pages.Select(static page => new ReadPart(page.Page, page.Truncated))],
                request.MaxCharacters) is { } continuation
            ? resume
                .Option("--pages", continuation.Parts)
                .Option("--mode", request.Mode)
                .Option(MaxCharactersOption.Name, continuation.MaxCharacters)
                .ToString()
            : null;
    }
}
