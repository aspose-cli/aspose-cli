using System.Net;
using Aspose.Cli.Host.ViewerService;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Host.App;

/// <summary>The App as the viewer service mounts it: its host and the handler of its requests.</summary>
internal sealed class AppServiceMount : IHostedApp
{
    private readonly AppHost _app;
    private readonly AppRequestHandler _handler;

    public AppServiceMount(AppHost app)
    {
        _app = app ?? throw new ArgumentNullException(nameof(app));
        _handler = new AppRequestHandler(app);
    }

    public string Url => _app.Url;

    public bool TryHandle(HttpListenerContext context, int port)
    {
        if (!AppRequestHandler.Owns(context.Request.Url?.AbsolutePath ?? "/"))
        {
            return false;
        }
        _handler.Handle(context, port);
        return true;
    }

    public AppResult Open(ViewerAppRequest request, OperationDeadline deadline) =>
        _app.Open(request.Route, request.File, AppLicenseContext.Of(request), deadline);

    public AppResult Result(bool reused) => _app.Result(reused);

    public void Dispose() => _app.Dispose();
}
