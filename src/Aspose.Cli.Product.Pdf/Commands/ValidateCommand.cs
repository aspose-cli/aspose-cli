using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class ValidateCommand
{
    public static CommandDefinition<PdfValidateRequest, PdfValidateResult> Create()
    {
        var profile = new Option<string>("--profile") { Required = true, Description = "PDF/A profile to validate against." }.WithInput(InputKind.None);
        profile.AcceptOnlyFromAmong("pdfa-1b", "pdfa-2b", "pdfa-3b");
        return new(
            "validate",
            "Validate a PDF against a PDF/A profile with Document.Validate; the result holds its return value and its log.",
            new CommandTraits { Input = PdfInputs.Document },
            [profile],
            (parse, standard) => new PdfValidateRequest
            {
                Input = standard.Input,
                Profile = parse.GetRequiredValue(profile),
                Password = standard.InputPassword,
            })
        {
            Examples =
            [
                "pdf validate archive.pdf --profile pdfa-2b",
                "pdf validate locked.pdf --profile pdfa-1b --password-env PDF_PASSWORD",
            ],
            Links =
            [
                CommandHelpLink.Schema(PdfModule.Manifest, "validate-result", "the fields of the validate result"),
            ],
        };
    }
}
