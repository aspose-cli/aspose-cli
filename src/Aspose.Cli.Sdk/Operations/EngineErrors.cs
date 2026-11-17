using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.Operations;

/// <summary>The errors of a product's document engine.</summary>
public static class EngineErrors
{
    /// <summary>
    /// The one wording of a failure inside a product's document engine.
    /// </summary>
    /// <param name="message">What failed, naming the product or the operation.</param>
    /// <param name="innerException">The engine failure.</param>
    /// <param name="details">Position details, such as the failing operation.</param>
    public static CliException EngineFailed(string message, Exception innerException, JsonObject? details = null) => CliException.Create(
        ErrorCodes.FeatureUnsupported,
        message,
        details: details,
        innerException: innerException);
}
