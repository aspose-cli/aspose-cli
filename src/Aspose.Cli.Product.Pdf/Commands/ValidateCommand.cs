using System.CommandLine;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class ValidateCommand
{
    public static Command Create(IProductCommandHost<IPdfEngine> host)
    {
        Argument<string> file = PdfOptions.File();
        var profile = new Option<string>("--profile") { Required = true, Description = "pdfa-1b, pdfa-2b or pdfa-3b." };
        profile.AcceptOnlyFromAmong("pdfa-1b", "pdfa-2b", "pdfa-3b");
        var password = new PasswordOptions("--password", "the PDF");
        var command = new Command("validate", "Validate a PDF against a PDF/A profile.");
        command.Arguments.Add(file);
        command.Options.Add(profile);
        password.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
            context.Port.Validate(
                context.Paths.ResolveInput(parse.GetRequiredValue(file)),
                new PdfValidateRequest
                {
                    Profile = parse.GetRequiredValue(profile),
                    Password = password.Resolve(parse, context.Inputs),
                })));
        return command;
    }
}
