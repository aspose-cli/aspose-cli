using System.Net;
using Aspose.Cli.Sdk.IO.Budgets;

namespace Aspose.Cli.Host.ViewerService;

/// <summary>
/// The App the viewer service mounts on its origin. The service routes the App's requests
/// and control calls to it and disposes it with itself; the App is composed above the
/// service, over the documents the service holds.
/// </summary>
internal interface IHostedApp : IDisposable
{
    /// <summary>The address of the App's home page.</summary>
    string Url { get; }

    /// <summary>Answers a request on the service's listener, or returns false when it is not the App's.</summary>
    bool TryHandle(HttpListenerContext context, int port);

    /// <summary>Points the App at the page and file a command named.</summary>
    AppResult Open(ViewerAppRequest request, OperationDeadline? deadline);

    /// <summary>The page and file the App shows.</summary>
    AppResult Result(bool reused);
}
