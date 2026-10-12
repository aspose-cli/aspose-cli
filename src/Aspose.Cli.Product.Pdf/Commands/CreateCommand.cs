using System.CommandLine;
using System.Globalization;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Output;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class CreateCommand
{
    public static CommandDefinition<PdfCreateRequest, PdfCreateResult> Create()
    {
        var images = new Option<string[]>("--from-images")
        {
            Description = "One or more image files, one per page in the image's orientation, scaled without distortion.",
            AllowMultipleArgumentsPerToken = true,
        }.WithInput(InputKind.File);
        var html = new Option<string?>("--from-html") { Description = "HTML input file." }.WithInput(InputKind.File);
        var allowNetwork = new Option<bool>("--allow-network-resources")
        {
            Description = "Let the HTML importer request the network resources trusted --from-html input names; the result lists every address. Local references stay beneath the HTML directory.",
        };
        var text = new Option<string?>("--from-text") { Description = "UTF-8 text or Markdown input file." }.WithInput(InputKind.File);
        var pageSize = new Option<string>("--page-size") { DefaultValueFactory = _ => PageSizePresets.A4, Description = "Page size of the new PDF." }.WithInput(InputKind.None);
        pageSize.AcceptOnlyFromAmong([.. PdfCreateRequest.PageSizes]);
        var margins = new Option<string>("--margins") { DefaultValueFactory = _ => "36", Description = "One value or top,right,bottom,left in points." }.WithInput(InputKind.None);
        return new(
            "create",
            "Create a PDF from exactly one source family.",
            new CommandTraits
            {
                Output = OutputTarget.CreatedFile("PDF path to create.", PdfFormats.Document),
                Encrypt = PdfInputs.EncryptedPdf,
                UsesFonts = true,
            },
            [images, html, allowNetwork, text, pageSize, margins],
            (parse, standard) =>
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

                string[]? imagePaths = imageValues.Length == 0 ? null : standard.InputFiles(images);
                string? htmlPath = standard.InputFile(html);
                string? textPath = standard.InputFile(text);
                ResolvedOutput output = standard.Output;
                Secret? encryptPassword = standard.EncryptPassword();
                return new PdfCreateRequest
                {
                    Output = output,
                    EncryptPassword = encryptPassword,
                    ImagePaths = imagePaths,
                    HtmlPath = htmlPath,
                    AllowNetworkResources = parse.GetValue(allowNetwork),
                    TextPath = textPath,
                    PageSize = parse.GetValue(pageSize) ?? PageSizePresets.A4,
                    Margins = ParseMargins(parse.GetValue(margins) ?? "36"),
                };
            },
            static (result, surface) => ResultText.Produced(surface, result.Output, "create"))
        {
            Examples =
            [
                "pdf create scans.pdf --from-images page1.png page2.png",
                "pdf create notes.pdf --from-text notes.md --page-size letter --margins 54",
            ],
            Links =
            [
                CommandHelpLink.Docs(PdfModule.Manifest, "assemble-review-report", "building a PDF from several sources"),
                CommandHelpLink.Docs(PdfModule.Manifest, "overview", "what each PDF command is for"),
            ],
        };
    }

    private static PdfMargins ParseMargins(string text)
    {
        string[] tokens = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        double[] values;
        try
        {
            values = tokens.Select(token => double.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();
        }
        catch (FormatException)
        {
            throw CliErrors.OptionInvalid("--margins", $"'{text}' is not numeric", "Use one value or top,right,bottom,left in points.");
        }

        if (values.Length == 1 && values[0] >= 0)
        {
            return new PdfMargins(values[0], values[0], values[0], values[0]);
        }

        if (values.Length == 4 && values.All(static value => value >= 0))
        {
            return new PdfMargins(values[0], values[1], values[2], values[3]);
        }

        throw CliErrors.OptionInvalid(
            "--margins",
            $"'{text}' must contain one or four non-negative values",
            "Use one value or top,right,bottom,left in points.");
    }
}
