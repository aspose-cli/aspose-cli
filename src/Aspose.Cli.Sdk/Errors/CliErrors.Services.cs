using System.Text.Json.Nodes;

namespace Aspose.Cli.Sdk.Errors;

// The errors of the local services, the App and the CLI processes they start.
public static partial class CliErrors
{
    /// <summary>
    /// Restores an error that another process of this CLI reported, such as a local service, a
    /// render worker or a child command, as this process's own. The code and exit code are the
    /// other process's, which built the error with a factory, so it is restated unchanged.
    /// </summary>
    /// <param name="code">The reported code.</param>
    /// <param name="exitCode">The exit code the other process reported or ended with.</param>
    /// <param name="message">The reported message.</param>
    /// <param name="hint">The reported hint, if any.</param>
    /// <param name="details">The reported details, if any.</param>
    /// <param name="docs">The reported documentation topic, if any.</param>
    public static CliException FromRemote(
        string code,
        ExitCode exitCode,
        string message,
        string? hint = null,
        JsonObject? details = null,
        string? docs = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return new CliException(new ErrorCode(code, exitCode), message, hint, details, docs);
    }

    /// <summary>
    /// A process this CLI started, or the channel to it, failed without reporting an error of
    /// its own, so the failure is this CLI's: an internal error.
    /// </summary>
    /// <param name="message">What could not be done.</param>
    /// <param name="hint">How to see why, if the caller knows.</param>
    public static CliException Internal(string message, string? hint = null) => new(
        ErrorCodes.Internal,
        message,
        hint: hint);

    /// <summary>Another App or preview service holds the per-user service lock.</summary>
    public static CliException ServiceBusy() => new(
        ErrorCodes.AppBusy,
        "Another App or preview service is starting, running or stopping for this user.",
        hint: $"Retry in a moment. If it persists, run '{DistributionInfo.CommandName} app stop' and start again.");

    /// <summary>The App is stopping, so it takes no further change.</summary>
    public static CliException AppStopping() => new(
        ErrorCodes.AppBusy,
        "The App is stopping and cannot accept changes.",
        hint: "Start the App again to continue.");

    /// <summary>A view the product does not declare; the details list the ones it does and the closest.</summary>
    /// <param name="product">The product asked for the view.</param>
    /// <param name="view">The view as the caller named it.</param>
    /// <param name="available">The views the product declares.</param>
    public static CliException ViewUnsupported(string product, string view, IReadOnlyList<string> available)
    {
        var mistake = Mistake.Of(view, available);
        var details = new JsonObject();
        mistake.WriteTo(details);
        return new CliException(
            ErrorCodes.FeatureUnsupported,
            $"View '{view}' is not supported by the {product} product.",
            hint: mistake.Hint($"Use one of: {string.Join(", ", available)}."),
            details: details);
    }
}
