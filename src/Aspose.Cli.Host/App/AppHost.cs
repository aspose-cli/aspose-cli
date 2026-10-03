using Aspose.Cli.Host.Catalog;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.ViewerService;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;

namespace Aspose.Cli.Host.App;

/// <summary>
/// The App mounted on the viewer service: the workspace pages, their API and
/// the document they show. It has no process, port or lifetime of its own —
/// the service owns all three — so opening a file here and opening it with
/// 'preview' reach the same renderer and the same address.
/// </summary>
internal sealed partial class AppHost : IDisposable
{
    private readonly object _gate = new();
    private readonly ProductCatalog _catalog;
    private readonly AppPreferencesStore _preferences;
    private readonly AppCliGateway _cli;
    private readonly AppLog _log;
    private readonly AppDocumentSession _sessions;
    private readonly AppWorkspace _workspace;
    private readonly Func<CapabilitiesResult> _capabilities;
    private readonly AppStatusQuery _status;
    private readonly Action _touch;
    private readonly Action _stop;
    private readonly GlobalValues _globals;
    private readonly int _port;
    private string _route = AppRoutes.Home;
    private bool _disposed;
    private int _warming;

    /// <summary>The products this App opens files with.</summary>
    internal ProductCatalog Catalog => _catalog;

    public AppHost(
        ProductCatalog catalog,
        Func<CapabilitiesResult> capabilities,
        GlobalValues globals,
        ViewerDocuments documents,
        int port,
        Action touch,
        Action stop)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _capabilities = capabilities;
        _globals = globals;
        _port = port;
        _touch = touch ?? throw new ArgumentNullException(nameof(touch));
        _stop = stop ?? throw new ArgumentNullException(nameof(stop));
        _preferences = new AppPreferencesStore(catalog, AppPaths.Preferences);
        _cli = new AppCliGateway(catalog, globals, AppPaths.ConfigDirectory);
        _log = new AppLog(AppPaths.Log);
        _sessions = new AppDocumentSession(
            catalog,
            CreateCommandContext,
            _preferences,
            _log,
            documents,
            id => $"http://127.0.0.1:{port}/d/{id}/",
            AppPaths.SessionRoot(Environment.ProcessId));
        _status = new AppStatusQuery(
            catalog,
            capabilities,
            _preferences,
            _cli,
            _sessions);
        _workspace = new AppWorkspace(
            catalog,
            _preferences,
            _sessions,
            _log,
            Touch,
            SetRoute);
        _sessions.Activity += Touch;
    }

    /// <summary>
    /// A context for the App's own work: routing a file to its product and
    /// bounding what it reads. It applies no licence, because the App renders
    /// nothing — the worker behind the viewer does, in its own process.
    /// </summary>
    private CommandContext CreateCommandContext() => CompositionRoot.Create(_catalog, _globals);

    public int Port => _port;

    public string Url => $"http://127.0.0.1:{_port}/";

    internal bool LicenseManagementApplicable => _catalog.Products.Any(
        static product => product.Manifest.Engine.LicenseApplicable);

    /// <summary>
    /// Points the App at the page a command asked for, and opens the file it
    /// named. The service is already listening, so nothing starts here.
    /// </summary>
    public AppResult Open(string route, string? filePath, OperationDeadline? deadline = null) => Mutate(() =>
    {
        _route = NormalizeRoute(route);
        if (filePath is not null)
        {
            _workspace.OpenPath(filePath, uploadedCopy: false, deadline);
            if (route != AppRoutes.Settings)
            {
                _route = AppRoutes.Preview;
            }
        }
        _log.Write($"app mounted on loopback port {_port}");
        if (Interlocked.Exchange(ref _warming, 1) == 0)
        {
            // The browser's first status follows; let it find the license and fonts answered.
            _ = Task.Run(_status.Warm).ContinueWith(
                static task => _ = task.Exception, TaskContinuationOptions.OnlyOnFaulted);
        }
        return Result(reused: false);
    }, deadline?.Token ?? CancellationToken.None);

    public AppStatusView Status()
    {
        Touch();
        return _status.Build(_route);
    }

    /// <summary>Keeps the service alive while someone is using the App.</summary>
    public void Touch() => _touch();

    /// <summary>Ends the service from the App's Exit action.</summary>
    public void RequestStop()
    {
        _log.Write("app stop requested");
        _stop();
    }

    public AppResult Result(bool reused) => new()
    {
        Running = true,
        Url = UrlForRoute(_route),
        Port = _port,
        Pid = Environment.ProcessId,
        Reused = reused,
        Route = _route,
        File = _sessions.FileName,
    };

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
        }
        _mutationGate.Wait();
        try { _sessions.Dispose(); }
        finally { _mutationGate.Release(); _mutationGate.Dispose(); }
    }

    private void SetRoute(string route) => _route = NormalizeRoute(route);

    private string UrlForRoute(string route) => Url + (route == AppRoutes.Welcome ? string.Empty : route);

    private static string NormalizeRoute(string route) => route switch
    {
        AppRoutes.Welcome => AppRoutes.Welcome,
        AppRoutes.Preview => AppRoutes.Preview,
        AppRoutes.Settings => AppRoutes.Settings,
        _ => AppRoutes.Home,
    };

}
