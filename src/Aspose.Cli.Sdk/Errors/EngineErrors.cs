using System.Text.Json.Nodes;

namespace Aspose.Cli.Sdk.Errors;

/// <summary>The errors of a product's document engine.</summary>
public static class EngineErrors
{
    /// <summary>
    /// The <c>ENGINE_ERROR</c> of an engine call that threw, with the engine's exception as it is:
    /// its type and message in the message and in <c>details.engineException</c> and
    /// <c>details.engineMessage</c>.
    /// </summary>
    /// <param name="doing">What was running, such as <c>PDF</c> or <c>Saving the edited document</c>.</param>
    /// <param name="exception">The engine's exception.</param>
    /// <param name="details">Further details, such as the resources an import found nothing for.</param>
    public static CliException EngineFailed(string doing, Exception exception, JsonObject? details = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        string type = exception.GetType().FullName ?? exception.GetType().Name;
        JsonObject all = details?.DeepClone().AsObject() ?? [];
        all["engineException"] = type;
        all["engineMessage"] = exception.Message;
        return CliException.Create(
            ErrorCodes.EngineError,
            $"{doing}: the engine threw {type}: {exception.Message}",
            all,
            innerException: exception);
    }
}
