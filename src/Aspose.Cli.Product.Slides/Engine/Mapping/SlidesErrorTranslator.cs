using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Slides.Engine.Mapping;

internal static class SlidesErrorTranslator
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
                $"Slides {operation} could not complete its file operation: {exception.Message}",
                hint: "Check file permissions, locks and free disk space, then retry.",
                innerException: exception);
        }
        catch (InvalidCastException exception)
        {
            throw new CliException(
                ErrorCodes.FeatureUnsupported,
                $"Slides {operation} could not initialize the host font catalog: {exception.Message}",
                hint: "On Windows, verify that values below the per-user and machine Fonts registry keys are string font-file entries; remove unrelated metadata from those keys or run in a clean host profile.",
                innerException: exception);
        }
        catch (Exception exception) when (
            exception.GetType().Assembly.GetName().Name?.StartsWith("Aspose.Slides", StringComparison.Ordinal) == true
            || exception is InvalidOperationException or ArgumentException or IndexOutOfRangeException)
        {
            throw new CliException(
                ErrorCodes.FeatureUnsupported,
                $"Slides {operation} could not process this presentation feature: {exception.Message}",
                hint: "Try a standard presentation copy with the problematic feature simplified, or use another supported target format.",
                innerException: exception);
        }
    }
}
