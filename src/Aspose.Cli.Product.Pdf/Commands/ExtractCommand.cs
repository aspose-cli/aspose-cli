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
                // Every usage check runs before the input is resolved or read.
                string kind = parse.GetRequiredValue(what);
                string? pageText = parse.GetValue(pages);
                string? format = parse.GetValue(to);
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

                    return standard.OpenEngine().ExportForm(standard.Input, new PdfFormExportRequest
                    {
                        TargetFormatId = format,
                        OutputPath = standard.OutputPath("." + format),
                        Overwrite = standard.Overwrite,
                        Password = standard.InputPassword,
                    });
                }

                string? formOption = format is not null ? "--to" : standard.RequestedOutputPath() is not null ? "--out" : null;
                if (formOption is not null)
                {
                    throw CliErrors.OptionInvalid(
                        formOption,
                        "it applies to form data and is valid only with --what forms",
                        $"Name the directory that receives the {kind} with --out-dir instead.");
                }

                if (string.Equals(kind, "attachments", StringComparison.Ordinal) && pageText is not null)
                {
                    throw CliErrors.OptionInvalid("--pages", "attachments belong to the document rather than individual pages", "Omit --pages when extracting attachments.");
                }

                PageRange? range = pageText is null ? null : PageRange.Parse(pageText);
                string directory = standard.OutputDirectory;
                return standard.OpenEngine().Extract(standard.Input, new PdfExtractRequest
                {
                    What = kind,
                    OutputDirectory = directory,
                    Pages = range,
                    Overwrite = standard.Overwrite,
                    Password = standard.InputPassword,
                });
            });
    }
}
