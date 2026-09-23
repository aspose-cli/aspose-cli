using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Aspose.Cli.Host.App;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;

namespace Aspose.Cli.Host.ViewerService;

/// <summary>
/// The viewer service process, one per user. It holds no product, engine or
/// license of its own: it watches files, renders them through a warm worker
/// child and serves the revisions on loopback. Commands reach it over a
/// control channel only the current user can open, a second start finds this
/// one through its marker instead of binding another port, and the service
/// ends itself once nothing has been rendered or looked at for a while.
/// </summary>
internal sealed class ViewerServiceHost : IDisposable
{
    internal static readonly TimeSpan IdleWindow = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan InstanceTimeout = TimeSpan.FromSeconds(2);

    private readonly LocalServiceOperationLock _instance;
    private readonly ViewerServiceStore _store = new();
    private readonly ManualResetEventSlim _stop = new(false);
    private readonly RenderWorkerSupervisor _worker;
    private readonly ViewerDocuments _documents;
    private readonly ViewerHttpServer _http;
    private readonly LocalServiceControlServer _control;
    private readonly AppHost? _app;
    private readonly string _token;
    private readonly long _startedAt = Environment.TickCount64;
    private long _lastAppActivity = Environment.TickCount64;
    private int _disposed;

    /// <summary>Starts the service and hands its lifetime to the command host.</summary>
    public static HostedCommandLifecycle Start(
        GlobalValues globals,
        ServiceStartSecrets? secrets,
        int requestedPort,
        Func<ViewerDocuments, int, Action, Action, AppHost>? app = null)
    {
        var host = new ViewerServiceHost(globals, secrets, requestedPort, app);
        return new HostedCommandLifecycle(
            new ProductPreviewStatusResult { Sessions = [] },
            host.Wait,
            host.Dispose);
    }

    private ViewerServiceHost(
        GlobalValues globals,
        ServiceStartSecrets? secrets,
        int requestedPort,
        Func<ViewerDocuments, int, Action, Action, AppHost>? app)
    {
        ArgumentNullException.ThrowIfNull(globals);
        try
        {
            _instance = LocalServiceOperationLock.Acquire(
                ViewerServiceCommands.Service,
                ViewerServiceCommands.LockKey("instance"),
                InstanceTimeout);
        }
        catch (TimeoutException)
        {
            throw ViewerErrors.ServiceBusy();
        }
        try
        {
            LocalServiceResourceLimits limits = LocalServiceResourceLimits.Resolve();
            string id = LocalHttpRequestSecurity.RandomToken();
            string nonce = secrets?.ServiceNonce ?? LocalHttpRequestSecurity.RandomToken();
            _token = secrets?.ServiceToken ?? LocalHttpRequestSecurity.RandomToken();
            _worker = new RenderWorkerSupervisor(
                () => WorkerProcess(globals, secrets),
                limits.RenderTimeout,
                static message => Trace.TraceInformation("aspose-cli viewer: {0}", message));
            _documents = new ViewerDocuments(_worker, ViewerStorage.Create(), limits);
            _http = new ViewerHttpServer(_documents, requestedPort, limits);
            _http.Start();
            // The App is mounted on this origin, so the page that frames a
            // document and the document itself come from one address.
            _app = app?.Invoke(_documents, _http.Port, RecordActivity, _stop.Set);
            if (_app is { } mounted)
            {
                var handler = new AppRequestHandler(mounted);
                _http.Mount((context, port) =>
                {
                    if (!AppRequestHandler.Owns(context.Request.Url?.AbsolutePath ?? "/"))
                    {
                        return false;
                    }
                    handler.Handle(context, port);
                    return true;
                });
            }
            _control = new LocalServiceControlServer(
                new LocalServiceControlEndpoint(ViewerServiceCommands.Service, id),
                nonce,
                _token,
                Dispatch,
                afterResponse: AfterResponse,
                operationTimeout: limits.RenderTimeout);
            _control.Start();
            _store.Write(new ViewerServiceMarker(
                id,
                _token,
                Environment.ProcessId,
                LocalServiceProcessIdentity.Current(nonce).StartTicksUtc,
                _http.Port,
                string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{_http.Port}/"),
                nonce));
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }
        Exception? failure = null;
        try { _store.DeleteIfOwned(_token); } catch (Exception exception) { failure = exception; }
        foreach (IDisposable? resource in new IDisposable?[] { _control, _http, _documents, _app, _worker, _stop, _instance })
        {
            try { resource?.Dispose(); } catch (Exception exception) { failure ??= exception; }
        }
        if (failure is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw(); }
    }

    /// <summary>
    /// Runs until a command stops the service, the person interrupts it, or
    /// nothing has been rendered or looked at for the idle window.
    /// </summary>
    private WaitOutcome Wait(TimeSpan? deadline, CancellationToken cancellationToken)
    {
        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return WaitOutcome.CancelRequested;
            }
            if (_stop.IsSet)
            {
                return WaitOutcome.Completed;
            }
            if (deadline is { } budget
                && Environment.TickCount64 - _startedAt >= (long)budget.TotalMilliseconds)
            {
                return WaitOutcome.DeadlineExpired;
            }
            if (IsIdle())
            {
                return WaitOutcome.IdleExpired;
            }
            if (cancellationToken.WaitHandle.WaitOne(PollInterval))
            {
                return WaitOutcome.CancelRequested;
            }
        }
    }

    private bool IsIdle()
    {
        long window = (long)IdleWindow.TotalMilliseconds;
        return Environment.TickCount64 - _startedAt >= window
            && Environment.TickCount64 - Interlocked.Read(ref _lastAppActivity) >= window
            && _documents.All.All(document =>
                document.Events.ClientCount == 0 && document.IdleMilliseconds >= window);
    }

    private LocalServiceControlResponse Dispatch(LocalServiceControlRequest request, OperationDeadline deadline)
    {
        try
        {
            switch (request.Command)
            {
                case ViewerServiceCommands.Open:
                    return Ok(Open(request.Payload, deadline), ViewerServiceJsonContext.Default.ViewerOpenResponse);
                case ViewerServiceCommands.Status:
                    return Ok(Status(), ViewerServiceJsonContext.Default.ViewerStatusResponse);
                case ViewerServiceCommands.Close:
                    return Ok(Close(request.Path), ViewerServiceJsonContext.Default.ViewerStopResponse);
                case ViewerServiceCommands.Refresh:
                    return Ok(Refresh(request.Path), ViewerServiceJsonContext.Default.ViewerStatusResponse);
                case ViewerServiceCommands.App:
                    return Ok(App(request.Payload, deadline), ViewerServiceJsonContext.Default.ViewerAppResponse);
                case ViewerServiceCommands.Stop:
                    // Withdraw discovery before acknowledging shutdown; later status calls must not join a closing pipe.
                    _store.DeleteIfOwned(_token);
                    return Ok(
                        new ViewerStopResponse
                        {
                            Pid = Environment.ProcessId,
                            Stopped = _documents.All.Select(static document => document.Id).ToArray(),
                            Documents = [],
                        },
                        ViewerServiceJsonContext.Default.ViewerStopResponse);
                default:
                    return Failure(CliErrors.OptionInvalid(
                        "preview",
                        $"the viewer service does not answer '{request.Command}'",
                        "Update the CLI so its commands and its service match."));
            }
        }
        catch (CliException exception)
        {
            return Failure(exception);
        }
        catch (OperationCanceledException) when (deadline.IsExpired)
        {
            return Failure(CliErrors.OperationTimeout(
                Math.Max(1, (int)Math.Ceiling(deadline.OriginalBudget!.Value.TotalSeconds)), "preview"));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            return Failure(new CliException(ErrorCodes.Internal, exception.Message));
        }
    }

    private void AfterResponse(LocalServiceControlRequest request)
    {
        if (string.Equals(request.Command, ViewerServiceCommands.Stop, StringComparison.Ordinal))
        {
            _stop.Set();
        }
    }

    private ViewerOpenResponse Open(string? payload, OperationDeadline deadline)
    {
        ViewerOpenRequest request = payload is null
            ? throw new InvalidDataException("The open request carries no document.")
            : JsonSerializer.Deserialize(payload, ViewerServiceJsonContext.Default.ViewerOpenRequest)
                ?? throw new InvalidDataException("The open request is empty.");
        var open = new HashSet<string>(
            _documents.All.Select(static document => document.Id),
            StringComparer.Ordinal);
        LiveDocument document = _documents.Open(request.File, new LiveDocumentOptions
        {
            Product = request.Product,
            View = request.View,
            Effect = request.Effect,
            Password = request.Password,
            License = request.License,
            FontDirectories = request.FontDirectories,
            MaxInputBytes = request.MaxInputBytes,
        }, ViewerDocuments.PreviewHolder, deadline);
        return new ViewerOpenResponse
        {
            Pid = Environment.ProcessId,
            Document = State(document),
            Reused = open.Contains(document.Id),
        };
    }

    /// <summary>
    /// Points the App at a page and opens the file a command named there. The
    /// service is already listening, so the browser has somewhere to go the
    /// moment this answers.
    /// </summary>
    private ViewerAppResponse App(string? payload, OperationDeadline deadline)
    {
        if (_app is not { } app)
        {
            throw CliErrors.OptionInvalid(
                "app",
                "this service was started without the App",
                "Run 'aspose-cli preview stop --all', then 'aspose-cli app' again.");
        }
        ViewerAppRequest request = payload is null
            ? throw new InvalidDataException("The app request carries no page.")
            : JsonSerializer.Deserialize(payload, ViewerServiceJsonContext.Default.ViewerAppRequest)
                ?? throw new InvalidDataException("The app request is empty.");
        AppResult opened = app.Open(request.Route, request.File, deadline);
        RecordActivity();
        return new ViewerAppResponse
        {
            Url = opened.Url ?? app.Url,
            Port = _http.Port,
            Pid = Environment.ProcessId,
            Route = opened.Route,
            File = opened.File,
        };
    }

    /// <summary>Someone is using the App; the idle window starts again.</summary>
    private void RecordActivity()
    {
        foreach (LiveDocument document in _documents.All)
        {
            document.RecordActivity();
        }
        Interlocked.Exchange(ref _lastAppActivity, Environment.TickCount64);
    }

    private ViewerStatusResponse Status()
    {
        AppResult? app = _app?.Result(reused: true);
        return new ViewerStatusResponse
        {
            Pid = Environment.ProcessId,
            Url = string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{_http.Port}/"),
            Documents = _documents.All.Where(static document => document.Current is not null).Select(State).ToArray(),
            AppRoute = app?.Route,
            AppFile = app?.File,
        };
    }

    /// <summary>
    /// Renders one document, or every open document, again. A license that
    /// was installed while a document was open reaches it this way.
    /// </summary>
    private ViewerStatusResponse Refresh(string? id)
    {
        foreach (LiveDocument document in _documents.All)
        {
            if (id is null || document.Id == id)
            {
                document.Refresh();
            }
        }
        return Status();
    }

    private ViewerStopResponse Close(string? id)
    {
        string[] stopped = id is { Length: > 0 } && _documents.Release(id, ViewerDocuments.PreviewHolder)
            ? [id]
            : [];
        return new ViewerStopResponse
        {
            Pid = Environment.ProcessId,
            Stopped = stopped,
            Documents = _documents.All.Where(static document => document.Current is not null).Select(State).ToArray(),
        };
    }

    private ViewerDocumentState State(LiveDocument document) => new()
    {
        Id = document.Id,
        File = document.SourcePath,
        Product = document.Current?.Product ?? string.Empty,
        View = document.Current?.View ?? string.Empty,
        Url = _http.Url(document.Id),
        Revision = document.Current?.Number ?? 0,
        License = document.Current?.License ?? string.Empty,
    };

    /// <summary>
    /// The worker receives the service's working directory, configuration and
    /// license selection, so it resolves exactly what the command would, while
    /// the process itself runs in the service directory.
    /// </summary>
    private static ProcessStartInfo WorkerProcess(GlobalValues globals, ServiceStartSecrets? secrets)
    {
        ProcessStartInfo start = SelfProcessLauncher.CreateBackground(
            "preview",
            "Run the published 'aspose-cli' executable directly.");
        start.WorkingDirectory = SelfProcessLauncher.ServiceWorkingDirectory;
        start.ArgumentList.Add("--workdir");
        start.ArgumentList.Add(Path.GetFullPath(
            secrets?.WorkDirectory ?? globals.WorkDir ?? Directory.GetCurrentDirectory()));
        if ((secrets?.LicensePath ?? globals.LicensePath) is { } license)
        {
            start.ArgumentList.Add("--license");
            start.ArgumentList.Add(license);
        }
        start.ArgumentList.Add("--quiet");
        return start;
    }

    private static LocalServiceControlResponse Ok<T>(
        T result,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type) =>
        new(
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            Ok: true,
            Result: JsonSerializer.SerializeToElement(result, type));

    private static LocalServiceControlResponse Failure(CliException exception) =>
        new(
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            Ok: false,
            Message: exception.Message,
            Result: JsonSerializer.SerializeToElement(
                new ViewerFailure(exception.Code.Name, (int)exception.Code.ExitCode, exception.Message),
                ViewerServiceJsonContext.Default.ViewerFailure));
}
