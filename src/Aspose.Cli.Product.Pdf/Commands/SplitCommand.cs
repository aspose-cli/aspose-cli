using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class SplitCommand
{
    public static CommandDefinition<PdfSplitRequest, PdfSplitResult> Create()
    {
        var pages = new Option<string[]>("--pages")
        {
            Description = "One or more page groups, for example --pages 1-3 4-6.",
            AllowMultipleArgumentsPerToken = true,
        }.WithInput(InputKind.None);
        var every = new Option<int?>("--every") { Description = "Pages per output part." };
        var bookmarks = new Option<bool>("--by-bookmarks") { Description = "Split at top-level bookmark destinations." };
        return new(
            "split",
            "Split a PDF into an atomic output set.",
            new CommandTraits
            {
                Input = PdfInputs.Document,
                Output = OutputTarget.Directory("Directory that receives the parts."),
            },
            [pages, every, bookmarks],
            (parse, standard) =>
            {
                string[] groupTexts = parse.GetValue(pages) ?? [];
                return new PdfSplitRequest
                {
                    Input = standard.Input,
                    PageGroups = groupTexts.Length == 0 ? null : groupTexts.Select(PageRange.Parse).ToArray(),
                    Every = parse.GetValue(every),
                    ByBookmarks = parse.GetValue(bookmarks),
                    Output = standard.DirectoryOutput,
                    Password = standard.InputPassword,
                };
            },
            Table);
    }

    internal static void Table(PdfSplitResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"wrote {result.Outputs.Count} PDF part(s)");
        foreach (PdfSplitOutput output in result.Outputs)
        {
            string bookmark = output.Bookmark is null ? string.Empty : $" [{output.Bookmark}]";
            surface.Out.WriteLine(
                $"  {output.Index}: pages {output.Pages}{bookmark} -> {output.Output.Path} ({TableText.Bytes(output.Output.SizeBytes)})");
        }
    }
}
