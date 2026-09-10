using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Pdf.Engine.Mapping;

internal static class PdfErrorTranslator
{
    public static T Execute<T>(string operation, Func<T> action)
    {
        try
        {
            return action();
        }
        catch (CliException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException
            || exception is IOException and not (FileNotFoundException or FileLoadException))
        {
            throw new CliException(
                ErrorCodes.OutputUnwritable,
                $"PDF {operation} could not complete its file operation: {exception.Message}",
                hint: "Check file permissions, locks and free disk space, then retry.",
                innerException: exception);
        }
        catch (Exception exception) when (
            exception.GetType().Assembly.GetName().Name == "Aspose.PDF"
            || exception is InvalidOperationException or ArgumentException or IndexOutOfRangeException)
        {
            throw new CliException(
                ErrorCodes.FeatureUnsupported,
                $"PDF {operation} could not process this document feature: {exception.Message}",
                hint: "Try a standard PDF copy with the problematic feature simplified, or use another supported target format.",
                innerException: exception);
        }
    }
}
