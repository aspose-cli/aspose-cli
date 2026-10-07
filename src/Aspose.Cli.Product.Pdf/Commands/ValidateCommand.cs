using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class ValidateCommand
{
    public static Command Create(IProductCommandHost<IPdfEngine> host)
    {
        var profile = new Option<string>("--profile") { Required = true, Description = "pdfa-1b, pdfa-2b or pdfa-3b." }.WithInput(InputKind.None);
        profile.AcceptOnlyFromAmong("pdfa-1b", "pdfa-2b", "pdfa-3b");
        return StandardCommand.Create(
            host,
            "validate",
            "Validate a PDF against a PDF/A profile.",
            new CommandTraits { Input = PdfCommands.Document },
            [profile],
            (parse, standard) => standard.OpenEngine().Validate(standard.Input, new PdfValidateRequest
            {
                Profile = parse.GetRequiredValue(profile),
                Password = standard.InputPassword,
            }));
    }
}
