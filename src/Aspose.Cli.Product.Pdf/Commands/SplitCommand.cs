using System.CommandLine;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class SplitCommand
{
    public static Command Create(IProductCommandHost<IPdfEngine> host)
    {
        Argument<string> file = PdfOptions.File();
        var pages = new Option<string[]>("--pages")
        {
            Description = "One or more page groups, for example --pages 1-3 4-6.",
            AllowMultipleArgumentsPerToken = true,
        };
        var every = new Option<int?>("--every") { Description = "Pages per output part." };
        var bookmarks = new Option<bool>("--by-bookmarks") { Description = "Split at top-level bookmark destinations." };
        var outDirectory = new Option<string>("--out-dir") { Required = true, Description = "Output directory." };
        var name = new Option<string>("--name-template")
        {
            DefaultValueFactory = _ => "{stem}.{n}.pdf",
            Description = "File name using {stem}, {n}, {pages} or {bookmark}.",
        };
        Option<bool> overwrite = OutputOptions.Overwrite();
        var password = new PasswordOptions("--password", "the PDF");
        var command = new Command("split", "Split a PDF into an atomic output set.");
        command.Arguments.Add(file);
        command.Options.Add(pages);
        command.Options.Add(every);
        command.Options.Add(bookmarks);
        command.Options.Add(outDirectory);
        command.Options.Add(name);
        command.Options.Add(overwrite);
        password.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
        {
            string[] groupTexts = parse.GetValue(pages) ?? [];
            return context.Port.Split(
                context.Paths.ResolveInput(parse.GetRequiredValue(file)),
                new PdfSplitRequest
                {
                    PageGroups = groupTexts.Length == 0 ? null : groupTexts.Select(PageRange.Parse).ToArray(),
                    Every = parse.GetValue(every),
                    ByBookmarks = parse.GetValue(bookmarks),
                    OutputDirectory = PdfOptions.ResolveDirectory(parse, context, outDirectory),
                    NameTemplate = parse.GetValue(name) ?? "{stem}.{n}.pdf",
                    Overwrite = parse.GetValue(overwrite),
                    Password = password.Resolve(parse, context.Inputs),
                });
        }));
        return command;
    }
}
