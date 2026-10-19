using System.CommandLine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Output;
using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class CreateCommand
{
    public static CommandDefinition<PdfCreateRequest, PdfCreateResult> Create()
    {
        var images = new Option<string[]>(PdfOptionNames.FromImages)
        {
            Description = "One or more image files, one per page in the image's orientation, scaled without distortion.",
            AllowMultipleArgumentsPerToken = true,
        }.WithInput(InputKind.File);
        var html = new Option<string?>("--from-html") { Description = "HTML input file." }.WithInput(InputKind.File);
        var allowNetwork = new Option<bool>(PdfOptionNames.AllowNetworkResources)
        {
            Description = "Let the HTML importer request the network resources trusted --from-html input names; the result lists every address. Local references stay beneath the HTML directory.",
        };
        var markdown = new Option<string?>("--from-markdown") { Description = "UTF-8 Markdown input file." }.WithInput(InputKind.File);
        var text = new Option<string?>("--from-text")
        {
            Description = "UTF-8 text input file, laid out line by line as plain text; Markdown uses --from-markdown.",
        }.WithInput(InputKind.File);
        var pageSize = new Option<string>("--page-size") { DefaultValueFactory = _ => PageSizePresets.A4, Description = "Page size of the new PDF." }.WithInput(InputKind.None);
        pageSize.AcceptOnlyFromAmong([.. PdfCreateRequest.PageSizes]);
        var margins = new Option<string>(PdfOptionNames.Margins) { DefaultValueFactory = _ => "36", Description = "One value or top,right,bottom,left in points." }.WithInput(InputKind.None);
        return new(
            "create",
            "Create a PDF from exactly one source family.",
            new CommandTraits
            {
                Output = OutputTarget.CreatedFile("PDF path to create.", PdfFormats.Document),
                Encrypt = PdfInputs.EncryptedPdf,
                UsesFonts = true,
            },
            [images, html, allowNetwork, markdown, text, pageSize, margins],
            (parse, standard) =>
            {
                PdfCreateSource source = Source(parse, standard, images, html, parse.GetValue(allowNetwork), markdown, text);
                ResolvedOutput output = standard.Output;
                Secret? encryptPassword = standard.EncryptPassword();
                return new PdfCreateRequest
                {
                    Output = output,
                    EncryptPassword = encryptPassword,
                    Source = source,
                    PageSize = parse.GetValue(pageSize) ?? PageSizePresets.A4,
                    Margins = ParseMargins(parse.GetValue(margins) ?? "36"),
                };
            },
            static (result, surface) => ResultText.Produced(surface, result.Output, "create"))
        {
            Examples =
            [
                "pdf create scans.pdf --from-images page1.png page2.png",
                "pdf create notes.pdf --from-markdown notes.md --page-size letter --margins 54",
            ],
            Links =
            [
                CommandHelpLink.Docs(PdfModule.Manifest, "assemble-review-report", "building a PDF from several sources"),
                CommandHelpLink.Docs(PdfModule.Manifest, "overview", "what each PDF command is for"),
            ],
        };
    }

    /// <summary>
    /// The one source option given, read as that option names it; --allow-network-resources
    /// applies to the HTML importer alone.
    /// </summary>
    private static PdfCreateSource Source(
        ParseResult parse,
        StandardInvocation standard,
        Option<string[]> images,
        Option<string?> html,
        bool allowNetwork,
        Option<string?> markdown,
        Option<string?> text)
    {
        int given = (parse.GetValue(images) is { Length: > 0 } ? 1 : 0)
            + (parse.GetValue(html) is null ? 0 : 1)
            + (parse.GetValue(markdown) is null ? 0 : 1)
            + (parse.GetValue(text) is null ? 0 : 1);
        if (given != 1)
        {
            throw CliErrors.Usage(["Choose exactly one of --from-images, --from-html, --from-markdown or --from-text."]);
        }

        PdfCreateSource source = standard.Source(html) is { } htmlFile ? new PdfHtmlSource(htmlFile, allowNetwork)
            : standard.Source(markdown) is { } markdownFile ? new PdfMarkdownSource(markdownFile)
            : standard.Source(text) is { } textFile ? new PdfTextSource(textFile)
            : new PdfImageSource(standard.Sources(images));

        if (allowNetwork && source is not PdfHtmlSource)
        {
            throw CliErrors.OptionInvalid(
                PdfOptionNames.AllowNetworkResources,
                "it applies only to --from-html",
                "The Markdown importer follows local file references inside fetched resources without any resource hook, so network resources stay refused for Markdown; convert the Markdown to HTML first.");
        }

        return source;
    }

    internal static PdfMargins ParseMargins(string text)
    {
        double[] values = PdfNumberList.Parse(text, PdfOptionNames.Margins, "Use one value or top,right,bottom,left in points.");
        if (values.Length == 1 && values[0] >= 0)
        {
            return new PdfMargins(values[0], values[0], values[0], values[0]);
        }

        if (values.Length == 4 && values.All(static value => value >= 0))
        {
            return new PdfMargins(values[0], values[1], values[2], values[3]);
        }

        throw CliErrors.OptionInvalid(
            PdfOptionNames.Margins,
            $"'{text}' must contain one or four non-negative values",
            "Use one value or top,right,bottom,left in points.");
    }
}
