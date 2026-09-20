using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.Preview;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Preview;

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
    private readonly string _token;
    private readonly long _startedAt = Environment.TickCount64;
    private int _disposed;

    /// <summary>Starts the service and hands its lifetime to the command host.</summary>
    public static HostedCommandLifecycle Start(
        GlobalValues globals,
        ServiceStartSecrets? secrets,
        int requestedPort)
    {
        var host = new ViewerServiceHost(globals, secrets, requestedPort);
        return new HostedCommandLifecycle(
            new ProductPreviewStatusResult { Sessions = [] },
            once: false,
            IdleWindow,
            host.Wait,
            host.Dispose);
    }

    private ViewerServiceHost(GlobalValues globals, ServiceStartSecrets? secrets, int requestedPort)
    {
        ArgumentNullException.ThrowIfNull(globals);
        _instance = LocalServiceOperationLock.Acquire(
            ViewerServiceCommands.Service,
            ViewerServiceCommands.LockKey("instance"),
            InstanceTimeout);
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
            _documents = new ViewerDocuments(_worker, PreviewSessionStorage.Create(), limits);
            _http = new ViewerHttpServer(_documents, requestedPort, limits);
            _http.Start();
            _control = new LocalServiceControlServer(
                new LocalServiceControlEndpoint(ViewerServiceCommands.Service, id),
                nonce,
                _token,
                Dispatch,
                afterResponse: AfterResponse);
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
        _store.DeleteIfOwned(_token);
        _control?.Dispose();
        _http?.Dispose();
        _documents?.Dispose();
        _worker?.Dispose();
        _stop.Dispose();
        _instance.Dispose();
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
            && _documents.All.All(document =>
                document.Events.ClientCount == 0 && document.IdleMilliseconds >= window);
    }

    private LocalServiceControlResponse Dispatch(LocalServiceControlRequest request)
    {
        try
        {
            switch (request.Command)
            {
                case ViewerServiceCommands.Open:
                    return Ok(Open(request.Payload), ViewerServiceJsonContext.Default.ViewerOpenResponse);
                case ViewerServiceCommands.Status:
                    return Ok(Status(), ViewerServiceJsonContext.Default.ViewerStatusResponse);
                case ViewerServiceCommands.Close:
                    return Ok(Close(request.Path), ViewerServiceJsonContext.Default.ViewerStopResponse);
                case ViewerServiceCommands.Refresh:
                    return Ok(Refresh(request.Path), ViewerServiceJsonContext.Default.ViewerStatusResponse);
                case ViewerServiceCommands.Stop:
                    return Ok(
                        new ViewerStopResponse
                        {
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

    private ViewerOpenResponse Open(string? payload)
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
        });
        return new ViewerOpenResponse
        {
            Document = State(document),
            Reused = open.Contains(document.Id),
        };
    }

    private ViewerStatusResponse Status() => new()
    {
        Pid = Environment.ProcessId,
        Url = string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{_http.Port}/"),
        Documents = _documents.All.Select(State).ToArray(),
    };

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
        string[] stopped = id is { Length: > 0 } && _documents.Close(id) ? [id] : [];
        return new ViewerStopResponse
        {
            Stopped = stopped,
            Documents = _documents.All.Select(State).ToArray(),
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
    /// The worker inherits the service's working directory, configuration and
    /// license selection, so it resolves exactly what the command would.
    /// </summary>
    private static ProcessStartInfo WorkerProcess(GlobalValues globals, ServiceStartSecrets? secrets)
    {
        ProcessStartInfo start = SelfProcessLauncher.CreateBackground(
            "preview",
            "Run the published 'aspose-cli' executable directly.");
        start.WorkingDirectory = secrets?.WorkDirectory
            ?? globals.WorkDir
            ?? Directory.GetCurrentDirectory();
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
