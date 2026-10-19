using System.CommandLine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Output;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Pdf.Commands;

/// <summary>
/// Extracts images, attachments, text, tables or form data into a directory;
/// <c>--form-format</c> chooses the format of the form data.
/// </summary>
internal static class ExtractCommand
{
    public static CommandDefinition<PdfExtractRequest, PdfExtractResult> Create()
    {
        var what = new Option<string>("--what") { Required = true, Description = "What to extract; forms writes the form data to one file." }.WithInput(InputKind.None);
        what.AcceptOnlyFromAmong([.. PdfExtractKinds.All]);
        var pages = new PartRangeOption("page", "every page", onlyWith: "--what images, text or tables");
        var formFormat = new Option<string?>("--form-format")
        {
            Description = "Form data format; required with --what forms, and only with it.",
        }.WithInput(InputKind.None);
        formFormat.AcceptOnlyFromAmong([.. PdfFormats.FormData.Select(static format => format.Id)]);
        var bom = new BomOption("each table's CSV", onlyWith: "--what tables");
        return new(
            "extract",
            "Extract bounded PDF assets, text, tables or form data.",
            new CommandTraits
            {
                Input = PdfInputs.Document,
                Output = OutputTarget.Directory("Safe extraction directory."),
            },
            [what, formFormat, .. pages.Options, .. bom.Options],
            (parse, standard) =>
            {
                // Every usage check runs before the input is resolved or read.
                string kind = parse.GetRequiredValue(what);
                string? pageText = pages.Read(parse);
                bool byteOrderMark = bom.Read(parse, kind == PdfExtractKinds.Tables ? null : $"--what {kind} writes no CSV tables");

                FormatDescriptor? format = parse.GetValue(formFormat) is { } id
                    ? PdfFormats.FormData.Single(candidate => candidate.Id == id)
                    : null;
                if (kind == PdfExtractKinds.Forms)
                {
                    if (format is null)
                    {
                        throw CliErrors.OptionInvalid("--form-format", "is required with --what forms", "Use --form-format json, fdf or xfdf.");
                    }
                }
                else if (format is not null)
                {
                    throw CliErrors.OptionInvalid(
                        "--form-format",
                        "it chooses the format of form data and is valid only with --what forms",
                        $"Drop --form-format; --what {kind} writes its files in their own formats.");
                }

                if (kind is PdfExtractKinds.Attachments or PdfExtractKinds.Forms && pageText is not null)
                {
                    throw CliErrors.OptionInvalid(pages.Name, $"{kind} belong to the document rather than individual pages", $"Omit {pages.Name} when extracting {kind}.");
                }

                PageRange? range = pages.ReadRange(parse);
                ResolvedDirectory directory = standard.DirectoryOutput;
                return new PdfExtractRequest
                {
                    Input = standard.Input,
                    What = kind,
                    Output = directory,
                    Pages = range,
                    Password = standard.InputPassword,
                    ByteOrderMark = byteOrderMark,
                    FormFormat = format,
                };
            },
            Table)
        {
            Examples =
            [
                "pdf extract report.pdf --what images --out-dir images",
                "pdf extract report.pdf --what tables --pages 2-4 --bom --out-dir tables",
                "pdf extract form.pdf --what forms --form-format json --out-dir form-data",
            ],
            Links =
            [
                CommandHelpLink.Docs(PdfModule.Manifest, "overview", "what each PDF command is for"),
                CommandHelpLink.Docs(PdfModule.Manifest, "forms-security", "form data export and import"),
            ],
        };
    }

    internal static void Table(PdfExtractResult result, TableSurface surface)
    {
        ResultText.Files(surface, "extracted", $"{result.What} item", [.. result.Outputs.Select(static item =>
            new ProducedFile(item.Page is { } page ? $"page {page}" : null, item.Path, item.Format, item.SizeBytes))]);
    }
}
