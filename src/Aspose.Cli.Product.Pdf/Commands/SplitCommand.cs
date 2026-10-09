using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class SplitCommand
{
    public static Command Create(IProductCommandHost<IPdfEngine> host)
    {
        var pages = new Option<string[]>("--pages")
        {
            Description = "One or more page groups, for example --pages 1-3 4-6.",
            AllowMultipleArgumentsPerToken = true,
        }.WithInput(InputKind.None);
        var every = new Option<int?>("--every") { Description = "Pages per output part." };
        var bookmarks = new Option<bool>("--by-bookmarks") { Description = "Split at top-level bookmark destinations." };
        return StandardCommand.Create(
            host,
            "split",
            "Split a PDF into an atomic output set.",
            new CommandTraits
            {
                Input = PdfCommands.Document,
                Output = OutputTarget.Directory("Directory that receives the parts."),
            },
            [pages, every, bookmarks],
            (parse, standard) =>
            {
                string[] groupTexts = parse.GetValue(pages) ?? [];
                return standard.OpenEngine().Split(standard.Input, new PdfSplitRequest
                {
                    PageGroups = groupTexts.Length == 0 ? null : groupTexts.Select(PageRange.Parse).ToArray(),
                    Every = parse.GetValue(every),
                    ByBookmarks = parse.GetValue(bookmarks),
                    Output = standard.DirectoryOutput,
                    Password = standard.InputPassword,
                });
            });
    }
}
