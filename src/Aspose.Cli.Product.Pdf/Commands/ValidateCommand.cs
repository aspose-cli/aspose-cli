using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class ValidateCommand
{
    public static CommandDefinition<PdfValidateRequest, PdfValidateResult> Create()
    {
        var profile = new Option<string>("--profile") { Required = true, Description = "pdfa-1b, pdfa-2b or pdfa-3b." }.WithInput(InputKind.None);
        profile.AcceptOnlyFromAmong("pdfa-1b", "pdfa-2b", "pdfa-3b");
        return new(
            "validate",
            "Validate a PDF against a PDF/A profile.",
            new CommandTraits { Input = PdfInputs.Document },
            [profile],
            (parse, standard) => new PdfValidateRequest
            {
                Input = standard.Input,
                Profile = parse.GetRequiredValue(profile),
                Password = standard.InputPassword,
            },
            Table);
    }

    internal static void Table(PdfValidateResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"{result.Profile}: {(result.Valid ? "valid" : "not valid")}");
        foreach (string issue in result.Issues)
        {
            surface.Out.WriteLine($"  {issue}");
        }
    }
}
