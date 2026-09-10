using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

/// <summary>
/// Final anti-corruption boundary for SDK failures not already translated by
/// load, save or operation components.
/// </summary>
internal static class WordsErrorTranslator
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
        catch (EngineOpException exception)
        {
            throw Unsupported(operation, exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new CliException(
                ErrorCodes.OutputUnwritable,
                $"Words {operation} could not complete its file operation: {exception.Message}",
                hint: "Check file permissions, locks and free disk space, then retry.",
                innerException: exception);
        }
        catch (Exception exception) when (IsEngineFailure(exception))
        {
            throw Unsupported(operation, exception);
        }
    }

    private static bool IsEngineFailure(Exception exception) =>
        exception.GetType().Assembly.GetName().Name == "Aspose.Words"
        || exception is InvalidOperationException
        || exception is ArgumentException
        || exception.GetType().Namespace?.StartsWith("SkiaSharp", StringComparison.Ordinal) == true;

    private static CliException Unsupported(string operation, Exception exception) => new(
        ErrorCodes.FeatureUnsupported,
        $"Words {operation} could not process this document feature: {exception.Message}",
        hint: "Try a DOCX copy with the problematic feature simplified, or use another supported command and report the document shape if the failure persists.",
        innerException: exception);
}
