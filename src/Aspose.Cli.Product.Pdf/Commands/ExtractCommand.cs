using System.CommandLine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Output;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Pdf.Commands;

/// <summary>
/// Extracts assets, text or tables into a directory, or with <c>--what forms</c> exports form
/// data to a file; each has its own request and result, so the result is their common base.
/// </summary>
internal static class ExtractCommand
{
    public static CommandDefinition<IPdfExtractRequest, ResultEnvelope> Create()
    {
        var what = new Option<string>("--what") { Required = true, Description = "images, attachments, text, tables or forms." }.WithInput(InputKind.None);
        what.AcceptOnlyFromAmong([.. PdfExtractKinds.All, "forms"]);
        var pages = new PartRangeOption("page", "every page", onlyWith: "--what images, text or tables");
        var bom = new Option<bool>("--bom")
        {
            Description = "Start each table's CSV with a UTF-8 byte order mark, so Excel reads its non-English text "
                + "correctly; only with --what tables. Default: UTF-8 without one.",
        };
        return new(
            "extract",
            "Extract bounded PDF assets, text, tables or form data.",
            new CommandTraits
            {
                Input = PdfInputs.Document,
                Output = OutputTarget.FileOrDirectory(
                    "Form-data output file; only with --what forms. Default extension follows --to.",
                    "Safe extraction directory; required unless --what forms."),
                Target = TargetFormat.Among(
                    "Form export format: json, fdf or xfdf; only with --what forms. Default: the --out extension's.",
                    PdfFormats.FormData),
            },
            [what, .. pages.Options, bom],
            (parse, standard) =>
            {
                // Every usage check runs before the input is resolved or read.
                string kind = parse.GetRequiredValue(what);
                string? pageText = pages.Read(parse);
                bool byteOrderMark = parse.GetValue(bom);
                if (byteOrderMark && kind != "tables")
                {
                    throw CliErrors.OptionInvalid("--bom", $"--what {kind} writes no CSV tables", "Drop --bom.");
                }

                if (string.Equals(kind, "forms", StringComparison.Ordinal))
                {
                    if (pageText is not null || standard.RequestedOutputDirectory is not null)
                    {
                        throw CliErrors.OptionInvalid("--what", $"forms cannot be combined with {pages.Name} or --out-dir", "Use --what forms --to <json|fdf|xfdf> and optionally --out.");
                    }

                    if (!standard.TargetRequested && standard.RequestedOutputPath() is null)
                    {
                        throw CliErrors.OptionInvalid("--to", "is required with --what forms", "Use --to json, --to fdf or --to xfdf.");
                    }

                    return new PdfFormExportRequest
                    {
                        Input = standard.Input,
                        Output = standard.Output,
                        Password = standard.InputPassword,
                    };
                }

                string? formOption = standard.TargetRequested ? "--to" : standard.RequestedOutputPath() is not null ? "--out" : null;
                if (formOption is not null)
                {
                    throw CliErrors.OptionInvalid(
                        formOption,
                        "it applies to form data and is valid only with --what forms",
                        $"Name the directory that receives the {kind} with --out-dir instead.");
                }

                if (string.Equals(kind, "attachments", StringComparison.Ordinal) && pageText is not null)
                {
                    throw CliErrors.OptionInvalid(pages.Name, "attachments belong to the document rather than individual pages", $"Omit {pages.Name} when extracting attachments.");
                }

                PageRange? range = pageText is null ? null : PageRange.Parse(pageText);
                ResolvedDirectory directory = standard.DirectoryOutput;
                return new PdfExtractRequest
                {
                    Input = standard.Input,
                    What = kind,
                    Output = directory,
                    Pages = range,
                    Password = standard.InputPassword,
                    ByteOrderMark = byteOrderMark,
                };
            },
            table: null)
        {
            Renderers =
            [
                ProductOutputDefinition.Create<PdfExtractResult>(Table),
                ProductOutputDefinition.Create<PdfFormExportResult>(Table),
            ],
        };
    }

    internal static void Table(PdfExtractResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"extracted {result.Items.Count} {result.What} item(s)");
        foreach (PdfExtractedItem item in result.Items)
        {
            string page = item.Page.HasValue ? $" page {item.Page.Value}" : string.Empty;
            surface.Out.WriteLine($"  {item.Path} ({item.Kind}{page}, {TableText.Bytes(item.SizeBytes)})");
        }
    }

    internal static void Table(PdfFormExportResult result, TableSurface surface) =>
        surface.Out.WriteLine(
            $"exported form data: {result.Output.Path} ({result.Output.Format}, {TableText.Bytes(result.Output.SizeBytes)})");
}
