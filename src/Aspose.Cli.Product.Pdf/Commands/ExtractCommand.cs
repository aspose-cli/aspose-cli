using System.CommandLine;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class ExtractCommand
{
    public static Command Create(IProductCommandHost<IPdfEngine> host)
    {
        var what = new Option<string>("--what") { Required = true, Description = "images, attachments, text, tables or forms." }.WithInput(InputKind.None);
        what.AcceptOnlyFromAmong([.. PdfExtractKinds.All, "forms"]);
        var pages = new Option<string?>("--pages") { Description = "Optional page range for images, text or tables." }.WithInput(InputKind.None);
        var to = new Option<string?>("--to") { Description = "Form export format: json, fdf or xfdf; only with --what forms." }.WithInput(InputKind.None);
        to.AcceptOnlyFromAmong("json", "fdf", "xfdf");
        return StandardCommand.Create(
            host,
            "extract",
            "Extract bounded PDF assets, text, tables or form data.",
            new CommandTraits
            {
                Input = PdfCommands.Document,
                Output = OutputTarget.FileOrDirectory(
                    "Form-data output file; only with --what forms. Default extension follows --to.",
                    "Safe extraction directory; required unless --what forms."),
            },
            [what, pages, to],
            (parse, standard) =>
            {
                string kind = parse.GetRequiredValue(what);
                string? pageText = parse.GetValue(pages);
                string? format = parse.GetValue(to);
                string input = standard.Input;
                if (string.Equals(kind, "forms", StringComparison.Ordinal))
                {
                    if (pageText is not null || standard.RequestedOutputDirectory is not null)
                    {
                        throw CliErrors.OptionInvalid("--what", "forms cannot be combined with --pages or --out-dir", "Use --what forms --to <json|fdf|xfdf> and optionally --out.");
                    }

                    if (format is null)
                    {
                        throw CliErrors.OptionInvalid("--to", "is required with --what forms", "Use --to json, --to fdf or --to xfdf.");
                    }

                    return standard.Port.ExportForm(input, new PdfFormExportRequest
                    {
                        TargetFormatId = format,
                        OutputPath = standard.OutputPath("." + format),
                        Overwrite = standard.Overwrite,
                        Password = standard.InputPassword,
                    });
                }

                if (format is not null || standard.Overwrite || standard.RequestedOutputPath() is not null)
                {
                    throw CliErrors.OptionInvalid("--to/--out/--overwrite", "form-output options are only valid with --what forms", "Remove them or use --what forms.");
                }

                return standard.Port.Extract(input, new PdfExtractRequest
                {
                    What = kind,
                    OutputDirectory = standard.OutputDirectory,
                    Pages = pageText is null ? null : PageRange.Parse(pageText),
                    Password = standard.InputPassword,
                });
            });
    }
}
