using System.CommandLine;
using System.Globalization;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class PdfOptions
{
    public static Argument<string> File() => new Argument<string>("file")
    {
        Description = "PDF document to open.",
    }.WithInput(InputKind.File);

    public static PdfMargins ParseMargins(string text)
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

    public static PdfSignatureRect ParseSignatureRect(string text)
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
