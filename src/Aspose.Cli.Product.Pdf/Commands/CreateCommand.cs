using System.CommandLine;
using System.Globalization;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class CreateCommand
{
    public static CommandDefinition<NewPdfRequest, PdfWriteResult> Create()
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
        var pageSize = new Option<string>("--page-size") { DefaultValueFactory = _ => "A4", Description = "A3, A4, Letter or Legal." }.WithInput(InputKind.None);
        pageSize.AcceptOnlyFromAmong([.. PdfPageSizes.Names]);
        var margins = new Option<string>("--margins") { DefaultValueFactory = _ => "36", Description = "One value or top,right,bottom,left in points." }.WithInput(InputKind.None);
        return new(
            "create",
            "Create a PDF from exactly one source family.",
            new CommandTraits
            {
                Output = OutputTarget.CreatedFile("PDF path to create.", PdfFormats.Document),
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
                return new NewPdfRequest
                {
                    Output = output,
                    ImagePaths = imagePaths,
                    HtmlPath = htmlPath,
                    AllowNetworkResources = parse.GetValue(allowNetwork),
                    TextPath = textPath,
                    PageSize = parse.GetValue(pageSize) ?? "A4",
                    Margins = ParseMargins(parse.GetValue(margins) ?? "36"),
                };
            },
            PdfText.Written);
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
