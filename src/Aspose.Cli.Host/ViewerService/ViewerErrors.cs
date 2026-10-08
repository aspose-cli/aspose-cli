using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Host.ViewerService;

/// <summary>Restores the product's own error from a worker response.</summary>
internal static class ViewerErrors
{
    public static CliException FromWorker(RenderWorkerResponse response, string source)
    {
        ArgumentNullException.ThrowIfNull(response);
        int exit = response.Exit > 0 ? response.Exit : (int)ExitCode.Internal;
        return CliErrors.FromRemote(
            response.Code ?? ErrorCodes.Internal.Name,
            (ExitCode)exit,
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

        throw CliErrors.ViewUnsupported(product, view, available);
    }
}
