using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Host.ViewerService;

/// <summary>Restores the product's own error from a worker response.</summary>
internal static class ViewerErrors
{
    /// <summary>Another App or preview service holds the per-user service lock.</summary>
    public static CliException ServiceBusy() => new(
        ErrorCodes.AppBusy,
        "Another App or preview service is starting, running or stopping for this user.",
        hint: "Retry in a moment. If it persists, run 'aspose-cli app stop' and start again.");

    public static CliException FromWorker(RenderWorkerResponse response, string source)
    {
        ArgumentNullException.ThrowIfNull(response);
        int exit = response.Exit > 0 ? response.Exit : (int)ExitCode.Internal;
        return new CliException(
            new ErrorCode(response.Code ?? ErrorCodes.Internal.Name, (ExitCode)exit),
            response.Message ?? $"The viewer could not render {Path.GetFileName(source)}.");
    }

    /// <summary>Rejects a view the product does not declare, naming the ones it does.</summary>
    public static void EnsureViewSupported(
        string product,
        string view,
        IReadOnlyList<string> available)
    {
        if (available.Contains(view, StringComparer.Ordinal))
        {
            return;
        }

        var values = new JsonArray();
        foreach (string item in available)
        {
            values.Add(item);
        }

        throw new CliException(
            ErrorCodes.FeatureUnsupported,
            $"View '{view}' is not supported by the {product} product.",
            hint: $"Use one of: {string.Join(", ", available)}.",
            details: new JsonObject { ["available"] = values });
    }
}
