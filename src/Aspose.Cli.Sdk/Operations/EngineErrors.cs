using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Sdk.Operations;

/// <summary>The errors of a product's document engine, whose hints tell file access failures apart.</summary>
public static class EngineErrors
{
    /// <summary>
    /// The one wording of a failure inside a product's document engine. The engine usually
    /// rejects a feature of this document, but when every document fails the same way the
    /// local environment it reads (fonts, imaging libraries) is the cause, so the hint names both.
    /// </summary>
    /// <param name="message">What failed, naming the product or the operation.</param>
    /// <param name="innerException">The engine failure.</param>
    /// <param name="details">Position details, such as the failing operation.</param>
    public static CliException EngineFailed(string message, Exception innerException, JsonObject? details = null) => CliException.Create(
        ErrorCodes.FeatureUnsupported,
        message,
        // A file the engine could not open says nothing about the document's features; truncated
        // or malformed data (EndOfStreamException, other IOExceptions) does.
        hint: IsFileAccessFailure(innerException) || IsFileAccessFailure(innerException.InnerException)
            ? "The engine could not open a file the message names: close any program that holds it, check that it "
                + "can be read, and run the command again."
            : "The engine may not support a feature this document uses: simplify or remove that content or operation, "
                + "or retry with a standard copy of the document or another output format. If other documents fail the "
                + "same way, the local environment (for example its installed fonts) is the cause, not the document.",
        details: details,
        innerException: innerException);

    private static bool IsFileAccessFailure(Exception? exception) => exception is UnauthorizedAccessException
        or FileNotFoundException
        or DirectoryNotFoundException
        || exception is IOException io && FileAccessProbe.IsSharingViolation(io);
}
