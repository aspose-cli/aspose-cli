using System.Diagnostics;
using System.Globalization;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Host.Preview;

/// <summary>Immutable input for starting one product preview server.</summary>
internal sealed record PreviewStartOptions(
    ProductBinding Binding,
    ProductDefinition Product,
    ResourceBudgetLedger ResourceBudgets,
    string FilePath,
    int RequestedPort,
    ProductPreviewRequest Request,
    string? PresentationEffect = null,
    Action<string>? Diagnostic = null,
    string? DisplayName = null);

/// <summary>Security and route values supplied by an embedding loopback host.</summary>
internal sealed record PreviewMountOptions(
    string DocumentPath,
    string CsrfToken);

/// <summary>
/// One rendered preview session without listener ownership. Standalone
/// Preview and App mounting share this exact session, snapshot and router core.
/// </summary>
internal sealed class MountedPreview : IDisposable
{
    private readonly PreviewRequestPipeline _requests;
    private int _disposed;

    public MountedPreview(
        PreviewRenderOutcome outcome,
        PreviewSessionStorage storage,
        PreviewSession session,
        PreviewVersionStore store,
        PreviewViewPublicationStore viewPublications,
        LiveEventHub hub,
        LicenseState license,
        string view,
        string documentPath,
        PreviewRequestPipeline requests)
    {
        Outcome = outcome;
        Storage = storage;
        Session = session;
        Store = store;
        ViewPublications = viewPublications;
        Hub = hub;
        License = license;
        View = view;
        DocumentPath = documentPath;
        _requests = requests;
    }

    public PreviewRenderOutcome Outcome { get; }
    public PreviewSessionStorage Storage { get; }
    public PreviewSession Session { get; }
    public PreviewVersionStore Store { get; }
    public PreviewViewPublicationStore ViewPublications { get; }
    public LiveEventHub Hub { get; }
    public LicenseState License { get; }
    public string View { get; }
    public string DocumentPath { get; }
    internal PreviewRequestPipeline Requests => _requests;

    public bool Route(
        System.Net.HttpListenerContext context,
        int boundPort,
        string path) =>
        _requests.RouteAuthorized(context, boundPort, path);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        PreviewResourceCleanup.Dispose(
            Session,
            Hub,
            ViewPublications,
            Store,
            Storage);
    }
}

/// <summary>
/// Preserves preview resource ownership until all renderer activity has left
/// the stores. Every construction and normal teardown path uses this order.
/// </summary>
internal static class PreviewResourceCleanup
{
    public static void Dispose(
        PreviewSession? session,
        LiveEventHub? hub,
        PreviewViewPublicationStore? viewPublications,
        PreviewVersionStore? store,
        PreviewSessionStorage? storage)
    {
        bool stopped = session is null
            || session.Stop(PreviewSession.DefaultStopTimeout);
        DeferredResourceCleanup.CompleteOrDefer(
            stopped,
            session is null
                ? static () => { }
                : session.WaitUntilStopped,
            () => DisposeOwnedResources(
                hub,
                viewPublications,
                store,
                storage),
            "aspose-preview-render-cleanup",
            "preview renderer resources");
    }

    private static void DisposeOwnedResources(
        LiveEventHub? hub,
        PreviewViewPublicationStore? viewPublications,
        PreviewVersionStore? store,
        PreviewSessionStorage? storage)
    {
        DisposeOne(hub);
        DisposeOne(viewPublications);
        DisposeOne(store);
        DisposeOne(storage);
    }

    private static void DisposeOne(IDisposable? resource)
    {
        try
        {
            resource?.Dispose();
        }
        catch (Exception exception)
        {
            Trace.TraceWarning(
                "Preview resource cleanup failed for {0}: {1}",
                resource?.GetType().Name ?? "unknown resource",
                exception.GetType().Name);
        }
    }
}

/// <summary>Already-running preview parts whose ownership can transfer to CommandExecutor.</summary>
internal sealed class RunningPreview : IDisposable
{
    private readonly MountedPreview _content;
    private bool _transferred;
    private int _disposed;

    public RunningPreview(
        MountedPreview content,
        PreviewServer server,
        int port,
        string url)
    {
        _content = content;
        Server = server;
        Port = port;
        Url = url;
    }

    public PreviewRenderOutcome Outcome => _content.Outcome;
    public PreviewSession Session => _content.Session;
    public PreviewServer Server { get; }
    public PreviewVersionStore Store => _content.Store;
    public LiveEventHub Hub => _content.Hub;
    public LicenseState License => _content.License;
    public string View => _content.View;
    public int Port { get; }
    public string Url { get; }

    public HostedCommandLifecycle Supervise(
        ResultEnvelope startup,
        bool once,
        TimeSpan idleAfter,
        IDisposable? control = null)
    {
        _transferred = true;
        return new HostedCommandLifecycle(
            startup,
            once,
            idleAfter,
            (deadline, cancellationToken) =>
                Session.Wait(deadline, idleAfter, cancellationToken),
            () =>
            {
                try
                {
                    control?.Dispose();
                }
                finally
                {
                    DisposeServerAndContent(Server, _content);
                }
            });
    }

    public void Dispose()
    {
        if (_transferred
            || Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        DisposeServerAndContent(Server, _content);
    }

    internal static void DisposeServerAndContent(
        PreviewServer server,
        MountedPreview content)
    {
        DeferredResourceCleanup.CompleteOrDefer(
            server.Stop(PreviewServer.DefaultStopTimeout),
            server.WaitUntilStopped,
            () => DisposeStoppedServerAndContent(server, content),
            "aspose-preview-owner-cleanup",
            "preview server and content");
    }

    private static void DisposeStoppedServerAndContent(
        PreviewServer server,
        MountedPreview content)
    {
        try
        {
            server.Dispose();
        }
        finally
        {
            content.Dispose();
        }
    }
}

/// <summary>Shared construction and presentation lifecycle for every product preview.</summary>
internal static class PreviewRuntime
{
    private const string IdleSecondsVariable = "ASPOSE_CLI_WATCH_IDLE_SECONDS";
    private const int DefaultIdleSeconds = 1800;
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromMilliseconds(200);

    public static RunningPreview Start(PreviewStartOptions options)
    {
        MountedPreview content = CreateContent(
            options,
            documentPath: "/",
            LocalHttpRequestSecurity.RandomToken(),
            sameOriginMount: false);
        PreviewServer? server = null;
        try
        {
            server = new PreviewServer(
                options.RequestedPort,
                content.Requests);
            int port = server.Start();
            string url = string.Create(
                CultureInfo.InvariantCulture,
                $"http://127.0.0.1:{port}/");
            return new RunningPreview(
                content,
                server,
                port,
                url);
        }
        catch
        {
            if (server is null)
            {
                content.Dispose();
            }
            else
            {
                RunningPreview.DisposeServerAndContent(server, content);
            }
            throw;
        }
    }

    public static MountedPreview Mount(
        PreviewStartOptions options,
        PreviewMountOptions mount)
    {
        ArgumentNullException.ThrowIfNull(mount);
        if (!mount.DocumentPath.StartsWith("/", StringComparison.Ordinal)
            || mount.DocumentPath.Length <= 1
            || mount.DocumentPath.EndsWith("/", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A mounted preview document path must be an absolute non-root path without a trailing slash.",
                nameof(mount));
        }
        return CreateContent(
            options,
            mount.DocumentPath,
            mount.CsrfToken,
            sameOriginMount: true);
    }

    private static MountedPreview CreateContent(
        PreviewStartOptions options,
        string documentPath,
        string csrf,
        bool sameOriginMount)
    {
        ArgumentNullException.ThrowIfNull(options);
        string path = Path.GetFullPath(options.FilePath);
        ProductPreviewDefinition preview = options.Product.Preview;
        preview.ValidateRequest(
            options.Request,
            options.PresentationEffect);
        ProductPreviewPresentation presentation =
            preview.CreatePresentation(options.PresentationEffect);
        PreviewSessionStorage? sessionStorage = null;
        PreviewVersionStore? store = null;
        PreviewViewPublicationStore? viewPublications = null;
        LiveEventHub? hub = null;
        PreviewSession? session = null;

        try
        {
            sessionStorage = PreviewSessionStorage.Create();
            string sessionRoot = sessionStorage.Root;
            store = new PreviewVersionStore(sessionRoot);
            viewPublications = new PreviewViewPublicationStore(
                Path.Combine(sessionRoot, "views"),
                LocalServiceResourceLimits.Resolve());
            hub = new LiveEventHub();
            bool hasStateRenderer = preview.SupportsState;
            session = new PreviewSession(
                path,
                options.ResourceBudgets,
                preview.CreateRenderer(
                    options.Binding,
                    path,
                    options.Request),
                hasStateRenderer,
                store,
                viewPublications,
                hub,
                QuietPeriod,
                target => preview.ValidatePayload(
                    target,
                    ProductPreviewPayloadKinds.Hint));
            if (options.Diagnostic is not null)
            {
                session.Diagnostic += options.Diagnostic;
            }

            PreviewRenderOutcome outcome = session.RenderInitial();
            LicenseState license = options.Binding.LicenseGate.EnsureApplied();
            string scriptNonce = LocalHttpRequestSecurity.RandomToken();
            string stateStorageKey = LocalHttpRequestSecurity.RandomToken();
            PreviewViewStateEndpoint? stateEndpoint =
                hasStateRenderer
                    ? new PreviewViewStateEndpoint(
                        state => preview.ValidatePayload(
                            state,
                            ProductPreviewPayloadKinds.State),
                        session.PublishView,
                        viewPublications)
                    : null;
            var requests = new PreviewRequestPipeline(
                new PreviewRequestOptions(
                    options.Request.View,
                    presentation.ClientScript,
                    () => session.Current,
                    hub,
                    csrf,
                    scriptNonce,
                    stateStorageKey,
                    RefreshRequested: session.RenderNow,
                    StateEndpoint: stateEndpoint,
                    ShellStylesheet: presentation.ShellStylesheet,
                    DocumentName: options.DisplayName ?? Path.GetFileName(path),
                    EvalMode: license == LicenseState.Evaluation,
                    DocumentPath: documentPath,
                    SameOriginMount: sameOriginMount));
            return new MountedPreview(
                outcome,
                sessionStorage,
                session,
                store,
                viewPublications,
                hub,
                license,
                options.Request.View,
                documentPath,
                requests);
        }
        catch
        {
            PreviewResourceCleanup.Dispose(
                session,
                hub,
                viewPublications,
                store,
                sessionStorage);
            throw;
        }
    }

    public static void OpenIfRequested(bool open, bool once, string url, bool quiet)
    {
        if (!open)
        {
            return;
        }

        if (once)
        {
            if (!quiet)
            {
                Console.Error.WriteLine("preview: --open is ignored with --once (the server exits immediately).");
            }

            return;
        }

        OpenBrowser(url, quiet);
    }
    public static void OpenBrowser(string url, bool quiet)
    {
        try
        {
            Process? browser = OperatingSystem.IsWindows()
                ? Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })
                : Process.Start(OperatingSystem.IsMacOS() ? "open" : "xdg-open", url);
            browser?.Dispose();
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            if (!quiet)
            {
                Console.Error.WriteLine($"preview: could not open the browser: {exception.Message}");
            }
        }
    }

    public static TimeSpan ResolveIdleWindow()
    {
        string? raw = Environment.GetEnvironmentVariable(IdleSecondsVariable);
        if (string.IsNullOrWhiteSpace(raw)
            || !int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds))
        {
            return TimeSpan.FromSeconds(DefaultIdleSeconds);
        }

        return seconds <= 0 ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(seconds);
    }

}
