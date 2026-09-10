using System.Diagnostics;
using Aspose.Cli.Host.Catalog;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Host.App;

/// <summary>Owns one local Web App instance and its current preview session.</summary>
internal sealed class AppHost : IDisposable
{
    private static readonly TimeSpan DefaultIdleWindow = TimeSpan.FromMinutes(30);
    private readonly object _gate = new();
    private readonly ProductCatalog _catalog;
    private readonly AppInstanceStore _instances;
    private readonly AppPreferencesStore _preferences;
    private readonly AppLicenseState _licenseState;
    private readonly AppLog _log;
    private readonly AppDocumentSession _sessions;
    private readonly AppWorkspace _workspace;
    private readonly AppLicenseWorkflow _licenses;
    private readonly AppStatusQuery _status;
    private readonly string _token;
    private readonly string _nonce;
    private readonly ManualResetEventSlim _stopped = new(initialState: false);
    private readonly long _processStartTicks;
    private readonly TimeSpan _idleWindow;
    private readonly FontSearchProfile _fontProfile;
    private AppHttpServer? _server;
    private AppControlEndpoint? _control;
    private string _route = AppRoutes.Home;
    private long _lastActivityTicks = Stopwatch.GetTimestamp();
    private bool _disposed;

    public AppHost(
        ProductCatalog catalog,
        CliEditionInfo edition,
        Func<CapabilitiesResult> capabilities,
        GlobalValues globals,
        FontSearchProfile fontProfile)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _fontProfile = fontProfile ?? throw new ArgumentNullException(nameof(fontProfile));
        ServiceStartSecrets? service =
            ServiceStartSecretChannel.Current;
        _token = service?.ServiceToken
            ?? Convert.ToHexString(
                    System.Security.Cryptography
                        .RandomNumberGenerator.GetBytes(24))
                .ToLowerInvariant();
        _nonce = service?.ServiceNonce
            ?? Convert.ToHexString(
                    System.Security.Cryptography
                        .RandomNumberGenerator.GetBytes(24))
                .ToLowerInvariant();
        _instances = new AppInstanceStore(AppPaths.Marker);
        _preferences = new AppPreferencesStore(catalog, AppPaths.Preferences);
        _licenseState = new AppLicenseState(catalog, globals);
        _log = new AppLog(AppPaths.Log);
        _sessions = new AppDocumentSession(
            catalog,
            _licenseState,
            _preferences,
            _log,
            AppPaths.SessionRoot(Environment.ProcessId),
            _fontProfile);
        _status = new AppStatusQuery(
            catalog,
            edition,
            capabilities,
            _preferences,
            _licenseState,
            _sessions);
        _workspace = new AppWorkspace(
            catalog,
            _preferences,
            _sessions,
            _log,
            Touch,
            SetRoute,
            WriteMarker);
        _licenses = new AppLicenseWorkflow(
            catalog,
            edition,
            capabilities,
            _licenseState,
            _preferences,
            _sessions,
            _log,
            ReleaseControl,
            RequestStop,
            WriteMarker,
            _status.InvalidateFontDiagnostic,
            _fontProfile);
        _sessions.Activity += Touch;
        using Process current = Process.GetCurrentProcess();
        _processStartTicks = current.StartTime.ToUniversalTime().Ticks;
        _idleWindow = ResolveIdleWindow();
    }

    public int Port => _server?.Port ?? 0;

    public string Token => _token;

    public string Url =>
        $"http://127.0.0.1:{Port}/";

    internal bool LicenseManagementApplicable => _catalog.Products.Any(
        static product => product.Manifest.Engine.LicenseApplicable);

    internal AppWorkspace Workspace => _workspace;

    internal AppLicenseWorkflow Licenses => _licenses;

    public AppResult Start(int requestedPort, string route, string? filePath)
    {
        _log.Write("app startup entered");
        _route = NormalizeRoute(route);
        _server = new AppHttpServer(
            this,
            requestedPort);
        int port;
        try
        {
            port = _server.Start();
        }
        catch (CliException exception) when (
            exception.Code ==
                ErrorCodes.LoopbackListenerUnavailable)
        {
            _server.Dispose();
            _server = null;
            throw AppStartupDiagnostics.Failure(
                AppStartupStage.Listener,
                exception);
        }
        catch (CliException)
        {
            _server.Dispose();
            _server = null;
            throw;
        }
        catch (Exception exception) when (
            AppStartupDiagnostics.IsExpected(exception))
        {
            _server.Dispose();
            _server = null;
            throw AppStartupDiagnostics.Failure(
                AppStartupStage.Listener,
                exception);
        }
        _log.Write("app HTTP listener bound");
        try
        {
            _sessions.ConfigureMount(
                _server.CreatePreviewMount());
            _log.Write("app preview router mounted");
            _control = new AppControlEndpoint(
                this,
                _nonce,
                _token);
            try
            {
                _control.Start();
            }
            catch (Exception exception) when (
                AppStartupDiagnostics.IsExpected(exception))
            {
                throw AppStartupDiagnostics.Failure(
                    AppStartupStage.Control,
                    exception);
            }
            _log.Write("app control endpoint bound");
            if (filePath is not null)
            {
                _workspace.OpenPath(
                    filePath,
                    uploadedCopy: false);
                if (route == AppRoutes.Settings)
                {
                    _route = AppRoutes.Settings;
                }
            }

            try
            {
                WriteMarker();
            }
            catch (Exception exception) when (
                AppStartupDiagnostics.IsExpected(exception))
            {
                throw AppStartupDiagnostics.Failure(
                    exception is UnauthorizedAccessException
                        ? AppStartupStage.Permissions
                        : AppStartupStage.Storage,
                    exception);
            }
            _log.Write("app ready marker published");
            _log.Write($"app started on loopback port {port}");
            return Result(reused: false);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public AppStatusView Status()
    {
        Touch();
        return _status.Build(_route);
    }

    public bool RoutePreview(
        System.Net.HttpListenerContext context,
        string path) =>
        _sessions.RoutePreview(context, path);

    public void Touch() => Interlocked.Exchange(ref _lastActivityTicks, Stopwatch.GetTimestamp());

    public void RequestStop()
    {
        _log.Write("app stop requested");
        _stopped.Set();
    }

    public void Wait(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        while (!_stopped.Wait(TimeSpan.FromSeconds(2), linked.Token))
        {
            if (_idleWindow != Timeout.InfiniteTimeSpan
                && Stopwatch.GetElapsedTime(Interlocked.Read(ref _lastActivityTicks)) >= _idleWindow)
            {
                _log.Write("app stopped after the idle window");
                _stopped.Set();
            }
        }
    }

    public AppResult Result(bool reused) => new()
    {
        Running = !_stopped.IsSet,
        Url = !_stopped.IsSet ? UrlForRoute(_route) : null,
        Port = !_stopped.IsSet ? Port : null,
        Pid = !_stopped.IsSet ? Environment.ProcessId : null,
        Reused = reused,
        Route = _route,
        File = _sessions.FileName,
        License = new LicenseInfo
        {
            Mode = _licenseState.Status(
                _sessions.ProductId ?? _catalog.DefaultProductId()).Mode,
        },
    };

    public AppResult Activate(string route)
    {
        lock (_gate)
        {
            SetRoute(route);
            Touch();
            return Result(reused: true);
        }
    }

    public void Dispose()
    {
        AppControlEndpoint? control;
        AppHttpServer? server;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            control = _control;
            _control = null;
            server = _server;
            _server = null;
        }

        _stopped.Set();
        try
        {
            control?.Dispose();
        }
        finally
        {
            bool stopped = server is null
                || server.Stop(AppHttpServer.DefaultStopTimeout);
            DeferredResourceCleanup.CompleteOrDefer(
                stopped,
                server is null
                    ? static () => { }
                    : server.WaitUntilStopped,
                () => DisposeStoppedResources(server),
                "aspose-app-owner-cleanup",
                "App server and document session");
        }
    }

    private void DisposeStoppedResources(AppHttpServer? server)
    {
        try
        {
            server?.Dispose();
        }
        finally
        {
            try
            {
                _sessions.Dispose();
            }
            finally
            {
                try
                {
                    _instances.DeleteIfOwned(_token);
                }
                finally
                {
                    _stopped.Dispose();
                }
            }
        }
    }

    private void SetRoute(string route)
    {
        _route = NormalizeRoute(route);
        WriteMarker();
    }

    private void ReleaseControl()
    {
        _control?.Dispose();
        _control = null;
    }

    private void WriteMarker()
    {
        if (Port == 0)
        {
            return;
        }

        _instances.Write(new AppInstance(
            Environment.ProcessId,
            _processStartTicks,
            Port,
            _token,
            _route,
            _sessions.FileName,
            _licenseState.Status(
                _sessions.ProductId
                    ?? _catalog.DefaultProductId()).Mode,
            Nonce: _nonce,
            FontProfileFingerprint: _fontProfile.Fingerprint));
    }

    private string UrlForRoute(string route) => Url + (route == AppRoutes.Welcome ? string.Empty : route);

    private static string NormalizeRoute(string route) => route switch
    {
        AppRoutes.Welcome => AppRoutes.Welcome,
        AppRoutes.Preview => AppRoutes.Preview,
        AppRoutes.Settings => AppRoutes.Settings,
        _ => AppRoutes.Home,
    };

    private static TimeSpan ResolveIdleWindow()
    {
        string? raw = Environment.GetEnvironmentVariable("ASPOSE_CLI_APP_IDLE_SECONDS");
        if (!int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int seconds))
        {
            return DefaultIdleWindow;
        }

        return seconds <= 0 ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(seconds);
    }

}
