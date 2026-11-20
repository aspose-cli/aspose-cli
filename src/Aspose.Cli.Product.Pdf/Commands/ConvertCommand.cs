using System.CommandLine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Product.Pdf.Commands;

/// <summary>
/// <c>pdf convert</c>: a PDF, or an HTML page, saved in another format with the engine's call for
/// it. --pages applies where the engine's own save selects pages: html and tiff output. A PDF/A
/// output takes --convert-error-action, the parameter of Document.Convert the engine has no default for.
/// </summary>
internal static class ConvertCommand
{
    private const string ErrorAction = "--convert-error-action";

    public static CommandDefinition<PdfConvertRequest, PdfConvertResult> Create()
    {
        var pages = new PartRangeOption("page", "every page", onlyWith: "--to html or tiff", formats: ["html", "tiff"]);
        var action = new Option<string?>(ErrorAction)
        {
            Description = "What Document.Convert does with what the PDF/A profile does not allow, a member name of the engine's ConvertErrorAction, such as Delete or None. Required for a pdfa output.",
        }.WithInput(InputKind.None);
        return new(
            "convert",
            "Convert a PDF, or an HTML page the engine imports, to another format with the engine's call for it. An html output is one file: it holds its images, fonts and styles itself.",
            new CommandTraits
            {
                Input = PdfInputs.Document,
                Output = OutputTarget.File("Output path; defaults to a sibling using the target extension."),
                UsesFonts = true,
                Target = TargetFormat.Convert("Target format.", PdfFormats.Definitions),
            },
            [.. pages.Options, action],
            (parse, standard) =>
            {
                ResolvedOutput output = standard.Output;
                string format = output.Format.Id;
                if (pages.Read(parse) is not null && !pages.AppliesTo(format))
                {
                    throw CliErrors.OptionInvalid(pages.Name, $"a '{format}' output is the whole document; it applies only with --to html or tiff");
                }

                string? errorAction = parse.GetValue(action);
                bool archive = PdfFormats.IsArchive(format);
                if (archive && errorAction is null)
                {
                    throw CliErrors.OptionInvalid(ErrorAction, $"a '{format}' output is made with Document.Convert, whose ConvertErrorAction has no default");
                }

                if (!archive && errorAction is not null)
                {
                    throw CliErrors.OptionInvalid(ErrorAction, $"applies only to a pdfa output, not to '{format}'");
                }

                return new PdfConvertRequest
                {
                    Input = standard.Input,
                    Output = output,
                    Pages = pages.ReadRange(parse),
                    Password = standard.InputPassword,
                    ConvertErrorAction = errorAction,
                };
            })
        {
            Examples =
            [
                "pdf convert report.pdf --out report.docx",
                "pdf convert report.pdf --to pdfa-2b --convert-error-action Delete --out archive.pdf",
                "pdf convert page.html --out page.pdf",
            ],
            Links =
            [
                CommandHelpLink.Docs(PdfModule.Manifest, "overview", "what each PDF command is for"),
            ],
        };
    }
}
