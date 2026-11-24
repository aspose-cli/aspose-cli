using System.CommandLine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

/// <summary>
/// <c>pdf sign</c>: the parameters of PdfFileSignature.Sign, each stated by the caller: the page, whether
/// the signature is visible, the engine's rectangle of a visible one, and what the PKCS7 signature
/// carries. A parameter not given is not set.
/// </summary>
internal static class SignCommand
{
    public static CommandDefinition<PdfSignRequest, PdfSignResult> Create()
    {
        var certificate = new Option<string>("--certificate")
        {
            Required = true,
            Description = "PKCS#12 certificate path (.pfx or .p12).",
        }.WithInput(InputKind.File);
        PasswordOptions certificatePassword = PasswordOptions.Required(PdfOptionNames.CertificatePasswordPrefix, "the PKCS#12 certificate");
        var visible = new Option<bool>("--visible")
        {
            Description = "The visible parameter of PdfFileSignature.Sign: the signature has an appearance on the page. Takes --rect.",
        };
        var page = new Option<int>("--page")
        {
            Required = true,
            Description = "The page parameter of PdfFileSignature.Sign, from 1.",
        };
        var rect = new Option<string?>(PdfOptionNames.Rect)
        {
            Description = "The rectangle of a visible signature, the engine's System.Drawing.Rectangle the call takes: x,y,width,height in whole points, x and y the lower-left corner in the page's user space. Required with --visible.",
        }.WithInput(InputKind.None);
        var reason = new Option<string?>(PdfOptionNames.Reason) { Description = "PKCS7.Reason." }.WithInput(InputKind.None);
        var location = new Option<string?>(PdfOptionNames.Location) { Description = "PKCS7.Location." }.WithInput(InputKind.None);
        var contact = new Option<string?>(PdfOptionNames.Contact) { Description = "PKCS7.ContactInfo." }.WithInput(InputKind.None);
        var font = new Option<string?>("--font") { Description = "SignatureCustomAppearance.FontFamilyName of a visible signature; not given, the engine's default appearance." }.WithInput(InputKind.None);
        return new(
            "sign",
            "Sign a PDF with a PKCS#7 signature through PdfFileSignature.Sign, and read the new signature back from the saved file.",
            new CommandTraits
            {
                Input = PdfInputs.Document with { PasswordSubject = "the input PDF" },
                Output = OutputTarget.File("Signed PDF path. Default: <input>.signed.pdf.", PdfFormats.Document, derivedMarker: ".signed"),
                UsesFonts = true,
            },
            [certificate, .. certificatePassword.Options, visible, page, rect, reason, location, contact, font],
            (parse, standard) =>
            {
                bool isVisible = parse.GetValue(visible);
                string? rectangleText = parse.GetValue(rect);
                if (isVisible != rectangleText is not null)
                {
                    throw CliErrors.OptionInvalid(PdfOptionNames.Rect, isVisible ? "a visible signature takes its rectangle" : "an invisible signature has no rectangle");
                }

                int pageNumber = parse.GetValue(page);
                OptionGuards.EnsureInRange("--page", pageNumber, 1, int.MaxValue);
                System.Drawing.Rectangle? rectangle = rectangleText is null ? null : ParseRect(rectangleText);
                return new PdfSignRequest
                {
                    Input = standard.Input,
                    CertificatePath = standard.RequiredInputFile(certificate),
                    Output = standard.Output,
                    CertificatePassword = standard.Password(certificatePassword),
                    Password = standard.InputPassword,
                    Page = pageNumber,
                    Visible = isVisible,
                    Rect = rectangle,
                    Reason = parse.GetValue(reason),
                    Location = parse.GetValue(location),
                    Contact = parse.GetValue(contact),
                    Font = parse.GetValue(font),
                };
            })
        {
            Examples =
            [
                "pdf sign contract.pdf --page 1 --certificate signer.pfx --certificate-password-env PDF_SIGNING_PASSWORD",
                "pdf sign contract.pdf --page 2 --certificate signer.pfx --certificate-password-stdin --visible --rect 36,36,180,60 --reason Approved --out signed.pdf",
            ],
            Links =
            [
                CommandHelpLink.Schema(PdfModule.Manifest, "sign-result", "the fields of the sign result"),
            ],
        };
    }

    private static System.Drawing.Rectangle ParseRect(string text)
    {
        string[] parts = text.Split(',');
        int[] values = new int[4];
        if (parts.Length != 4 || parts.Select((part, index) => int.TryParse(part.Trim(), System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out values[index])).Any(static parsed => !parsed))
        {
            throw CliErrors.OptionInvalid(PdfOptionNames.Rect, $"'{text}' is not x,y,width,height, four whole numbers");
        }

        return new System.Drawing.Rectangle(values[0], values[1], values[2], values[3]);
    }
}
