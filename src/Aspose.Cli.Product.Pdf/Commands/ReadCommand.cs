using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class ReadCommand
{
    public static CommandDefinition<PdfReadRequest, PdfReadResult> Create()
    {
        var pages = new PartRangeOption("page", "every page");
        var content = new Option<string>("--content")
        {
            Description = "How each page is returned: in reading order, or with the spacing of the page kept.",
            DefaultValueFactory = _ => PdfReadContents.Text,
        }.WithInput(InputKind.None);
        content.AcceptOnlyFromAmong([.. PdfReadContents.All]);
        var maxChars = new MaxCharactersOption("the text of the returned pages");
        return new(
            "read",
            "Read a bounded page-text window.",
            new CommandTraits { Input = PdfInputs.Document },
            [.. pages.Options, content, .. maxChars.Options],
            (parse, standard) =>
            {
                int characters = maxChars.Read(parse);
                return new PdfReadRequest
                {
                    Input = standard.Input,
                    Pages = pages.ReadRange(parse),
                    Content = parse.GetValue(content) ?? PdfReadContents.Text,
                    MaxCharacters = characters,
                    Password = standard.InputPassword,
                };
            },
            Table)
        {
            MistakenFor = ["query"],
            Finish = (_, request, result, standard) =>
                result with { Window = result.Window with { Next = Next(standard.Continuation(), pages.Name, request, result) } },
            Examples =
            [
                "pdf read report.pdf --pages 1-5 --output-format json",
                "pdf read report.pdf --pages 3 --content layout --max-chars 5000",
            ],
            Links =
            [
                CommandHelpLink.Docs(PdfModule.Manifest, "overview", "what each PDF command is for"),
                new("aspose-cli docs reading", "reading every product within bounded windows"),
            ],
        };
    }

    internal static void Table(PdfReadResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"{result.Input.Path}: {result.PageCount} page(s) ({result.Content})");
        foreach (PdfPageText page in result.Pages)
        {
            surface.Out.WriteLine();
            surface.Out.WriteLine($"--- page {page.Page}{(page.Truncated ? " (truncated)" : string.Empty)} ---");
            surface.Out.WriteLine(page.Text);
        }
    }

    /// <summary>The read that resumes where this one stopped, or null when it covered the selection.</summary>
    private static string? Next(ContinuationCommand resume, string pagesOption, PdfReadRequest request, PdfReadResult result)
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
                .Option(pagesOption, continuation.Parts)
                .Option("--content", request.Content)
                .Option(MaxCharactersOption.Name, continuation.MaxCharacters)
                .ToString()
            : null;
    }
}
