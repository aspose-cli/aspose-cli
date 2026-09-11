using System.CommandLine;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class SignCommand
{
    public static Command Create(IProductCommandHost<IPdfEngine> host)
    {
        Argument<string> file = PdfOptions.File();
        var certificate = new Option<string>("--certificate")
        {
            Required = true,
            Description = "PKCS#12 certificate path (.pfx or .p12).",
        }.WithInput(InputKind.None);
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
        var output = new OutputFileOptions("Signed PDF path. Default: <input>.signed.pdf.");
        var password = new PasswordOptions("--password", "the input PDF");

        var command = new Command("sign", "Apply a PKCS#7 signature and verify the saved signature field.");
        command.Arguments.Add(file);
        command.Options.Add(certificate);
        command.Options.Add(certificatePasswordEnv);
        command.Options.Add(visible);
        command.Options.Add(page);
        command.Options.Add(rect);
        command.Options.Add(reason);
        command.Options.Add(location);
        command.Options.Add(contact);
        output.AddTo(command);
        password.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
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
            string input = context.Paths.ResolveInput(parse.GetRequiredValue(file));
            string certificatePath = context.Paths.ResolveInput(parse.GetRequiredValue(certificate));
            string variable = parse.GetRequiredValue(certificatePasswordEnv);
            string? certificatePassword = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrEmpty(certificatePassword))
            {
                throw CliErrors.OptionInvalid(
                    "--certificate-password-env",
                    $"environment variable '{variable}' is missing or empty",
                    "Set the variable to the PKCS#12 password and run the command again.");
            }

            return context.Port.Sign(input, new PdfSignRequest
            {
                CertificatePath = certificatePath,
                CertificatePassword = certificatePassword,
                OutputPath = output.ResolvePath(parse, context.Paths, input, ".signed.pdf"),
                Overwrite = output.Overwrite(parse),
                Password = password.Resolve(parse, context.Inputs),
                Page = pageNumber,
                Visible = isVisible,
                Rect = rectangleText is null ? null : PdfOptions.ParseSignatureRect(rectangleText),
                Reason = parse.GetValue(reason),
                Location = parse.GetValue(location),
                Contact = parse.GetValue(contact),
            });
        }));
        return command;
    }
}
