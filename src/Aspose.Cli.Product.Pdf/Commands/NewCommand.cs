using System.CommandLine;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class NewCommand
{
    public static Command Create(IProductCommandHost<IPdfEngine> host)
    {
        var file = new Argument<string>("file") { Description = "PDF path to create." }.WithInput(InputKind.None);
        var images = new Option<string[]>("--from-images")
        {
            Description = "One or more image files, one per output page.",
            AllowMultipleArgumentsPerToken = true,
        }.WithInput(InputKind.File);
        var html = new Option<string?>("--from-html") { Description = "HTML input file." }.WithInput(InputKind.File);
        var text = new Option<string?>("--from-text") { Description = "UTF-8 text or Markdown input file." }.WithInput(InputKind.File);
        var pageSize = new Option<string>("--page-size") { DefaultValueFactory = _ => "A4", Description = "A3, A4, Letter or Legal." }.WithInput(InputKind.None);
        pageSize.AcceptOnlyFromAmong("A3", "A4", "Letter", "Legal");
        var margins = new Option<string>("--margins") { DefaultValueFactory = _ => "36", Description = "One value or top,right,bottom,left in points." }.WithInput(InputKind.None);
        Option<bool> overwrite = OutputOptions.Overwrite();
        var command = new Command("create", "Create a PDF from exactly one source family.");
        command.Arguments.Add(file);
        command.Options.Add(images);
        command.Options.Add(html);
        command.Options.Add(text);
        command.Options.Add(pageSize);
        command.Options.Add(margins);
        command.Options.Add(overwrite);
        command.SetAction(parse => host.Run(parse, context =>
        {
            string[] imageValues = parse.GetValue(images) ?? [];
            string? htmlValue = parse.GetValue(html);
            string? textValue = parse.GetValue(text);
            int sources = imageValues.Length > 0 ? 1 : 0;
            sources += htmlValue is null ? 0 : 1;
            sources += textValue is null ? 0 : 1;
            if (sources != 1)
            {
                throw CliErrors.Usage(["Choose exactly one of --from-images, --from-html or --from-text."]);
            }

            string? textPath = textValue is null ? null : context.Paths.ResolveInput(textValue);
            return context.Port.Create(new NewPdfRequest
            {
                OutputPath = context.Paths.ResolveOutput(parse.GetRequiredValue(file)),
                Overwrite = parse.GetValue(overwrite),
                ImagePaths = imageValues.Length == 0
                    ? null
                    : imageValues.Select(context.Paths.ResolveInput).ToArray(),
                HtmlPath = htmlValue is null ? null : context.Paths.ResolveInput(htmlValue),
                TextPath = textPath,
                Markdown = textPath is not null
                    && string.Equals(Path.GetExtension(textPath), ".md", StringComparison.OrdinalIgnoreCase),
                PageSize = parse.GetValue(pageSize) ?? "A4",
                Margins = PdfOptions.ParseMargins(parse.GetValue(margins) ?? "36"),
            });
        }));
        return command;
    }
}
