using System.CommandLine;
using System.Globalization;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class SignCommand
{
    public static Command Create(IProductCommandHost<IPdfEngine> host)
    {
        var certificate = new Option<string>("--certificate")
        {
            Required = true,
            Description = "PKCS#12 certificate path (.pfx or .p12).",
        }.WithInput(InputKind.File);
        var certificatePasswordEnv = new Option<string>("--certificate-password-env")
        {
            Required = true,
            Description = "Environment variable containing the certificate password.",
        }.WithInput(InputKind.None, ParameterValueSource.EnvironmentVariableName, secret: true);
        var visible = new Option<bool>("--visible")
        {
            Description = "Place a visible signature appearance on the selected page.",
        };
        var page = new Option<int>("--page")
        {
            DefaultValueFactory = _ => 1,
            Description = "1-based page for the signature field.",
        };
        var rect = new Option<string?>("--rect")
        {
            Description = "Visible rectangle x,y,width,height in PDF points; default 36,36,180,60.",
        }.WithInput(InputKind.None);
        var reason = new Option<string?>("--reason") { Description = "Signing reason stored in the signature." }.WithInput(InputKind.None);
        var location = new Option<string?>("--location") { Description = "Signing location stored in the signature." }.WithInput(InputKind.None);
        var contact = new Option<string?>("--contact") { Description = "Signer contact stored in the signature." }.WithInput(InputKind.None);
        return StandardCommand.Create(
            host,
            "sign",
            "Apply a PKCS#7 signature and verify the saved signature field.",
            new CommandTraits
            {
                Input = PdfCommands.Document with { PasswordSubject = "the input PDF" },
                Output = OutputTarget.File("Signed PDF path. Default: <input>.signed.pdf.", PdfFormats.Document, derivedMarker: ".signed"),
                UsesFonts = true,
            },
            [certificate, certificatePasswordEnv, visible, page, rect, reason, location, contact],
            (parse, standard) =>
            {
                bool isVisible = parse.GetValue(visible);
                string? rectangleText = parse.GetValue(rect);
                if (!isVisible && rectangleText is not null)
                {
                    throw CliErrors.OptionInvalid(
                        "--rect",
                        "a rectangle has no effect on an invisible signature",
                        "Pass --visible with --rect, or omit --rect.");
                }

                int pageNumber = parse.GetValue(page);
                OptionGuards.EnsureInRange("--page", pageNumber, 1, int.MaxValue, "Use a 1-based page number.");
                string input = standard.Input;
                string certificatePath = standard.RequiredInputFile(certificate);
                ResolvedOutput output = standard.Output;
                string variable = parse.GetRequiredValue(certificatePasswordEnv);
                string? certificatePassword = standard.ReadEnvironment(variable);
                if (string.IsNullOrEmpty(certificatePassword))
                {
                    throw CliErrors.SecretMissing("--certificate-password-env", variable);
                }

                return standard.OpenEngine().Sign(input, new PdfSignRequest
                {
                    CertificatePath = certificatePath,
                    CertificatePassword = certificatePassword,
                    Output = output,
                    Password = standard.InputPassword,
                    Page = pageNumber,
                    Visible = isVisible,
                    Rect = rectangleText is null ? null : ParseRect(rectangleText),
                    Reason = parse.GetValue(reason),
                    Location = parse.GetValue(location),
                    Contact = parse.GetValue(contact),
                });
            });
    }

    private static PdfSignatureRect ParseRect(string text)
    {
        string[] tokens = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        double[] values;
        try
        {
            values = tokens.Select(token =>
                double.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();
        }
        catch (FormatException)
        {
            throw CliErrors.OptionInvalid(
                "--rect", $"'{text}' is not numeric", "Use x,y,width,height in PDF points.");
        }

        if (values.Length != 4 || values[2] <= 0 || values[3] <= 0)
        {
            throw CliErrors.OptionInvalid(
                "--rect",
                $"'{text}' must contain x,y and positive width,height",
                "Use x,y,width,height in PDF points, for example 36,36,180,60.");
        }

        return new PdfSignatureRect(values[0], values[1], values[2], values[3]);
    }
}
