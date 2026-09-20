using Aspose.Cli.Host.App;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.ViewerService;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Host.Commands;

/// <summary>
/// Starts the one local service that serves both faces of live preview: the
/// App a person works in, and the documents the viewer renders. Both commands
/// that can bring it up — the hidden service host and <c>app --foreground</c>
/// — start it exactly this way, so what a person sees never depends on which
/// command happened to start the service.
/// </summary>
internal static class ViewerServiceHosting
{
    public static HostedCommandLifecycle Start(
        GlobalValues globals,
        int requestedPort,
        ProductCatalog catalog,
        Func<CapabilitiesResult> capabilities,
        ViewerAppRequest? page = null,
        bool openBrowser = false)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        AppHost? mounted = null;
        HostedCommandLifecycle lifecycle = ViewerServiceHost.Start(
            globals,
            ServiceStartSecretChannel.Current,
            requestedPort,
            (documents, port, touch, stop) => mounted = new AppHost(
                catalog,
                capabilities,
                globals,
                FontSearchProfile.Ambient,
                documents,
                port,
                touch,
                stop));
        if (page is not null && mounted is { } app)
        {
            AppResult opened = app.Open(page.Route, page.File);
            if (openBrowser && opened.Url is { } url)
            {
                BrowserLauncher.Open(url);
            }
        }
        return lifecycle;
    }
}
