using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;

namespace Aspose.Cli.Host.ViewerService;

/// <summary>
/// How a command reaches the viewer service: it finds the running one through
/// its marker, or starts it once — a start lock keeps two commands racing for
/// the same service from starting two. Failures come back as the error the
/// service would have raised in the command's own process.
/// </summary>
internal sealed class ViewerServiceClient
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan StartLockTimeout = TimeSpan.FromSeconds(30);

    private readonly ViewerServiceStore _store = new();

    /// <summary>Opens a document, starting the service when none is running.</summary>
    public ViewerOpenResponse Open(GlobalValues globals, ViewerOpenRequest request, int requestedPort, OperationDeadline? deadline = null)
    {
        ArgumentNullException.ThrowIfNull(globals);
        ArgumentNullException.ThrowIfNull(request);
        using var ownedDeadline = StartDeadline(globals, deadline);
        OperationDeadline operation = ownedDeadline ?? deadline!;
        return Send(
            Running(globals, requestedPort, operation),
            ViewerServiceCommands.Open,
            path: null,
            payload: JsonSerializer.Serialize(request, ViewerServiceJsonContext.Default.ViewerOpenRequest),
            ViewerServiceJsonContext.Default.ViewerOpenResponse, operation);
    }

    /// <summary>
    /// Points the App at a page, starting the service when none is running.
    /// The App is part of the service, so this is also how the App starts.
    /// </summary>
    public ViewerAppResponse App(GlobalValues globals, ViewerAppRequest page, int requestedPort, OperationDeadline? deadline = null)
    {
        ArgumentNullException.ThrowIfNull(globals);
        ArgumentNullException.ThrowIfNull(page);
        using var ownedDeadline = StartDeadline(globals, deadline);
        OperationDeadline operation = ownedDeadline ?? deadline!;
        return Send(
            Running(globals, requestedPort, operation),
            ViewerServiceCommands.App,
            path: null,
            payload: JsonSerializer.Serialize(page, ViewerServiceJsonContext.Default.ViewerAppRequest),
            ViewerServiceJsonContext.Default.ViewerAppResponse, operation);
    }

    /// <summary>What the running service has open, or null when none runs.</summary>
    public ViewerStatusResponse? Status(OperationDeadline? deadline = null) =>
        _store.ReadLive() is { } marker
            ? Send(marker, ViewerServiceCommands.Status, null, null,
                ViewerServiceJsonContext.Default.ViewerStatusResponse, deadline)
            : null;

    /// <summary>Closes one document, or the whole service.</summary>
    public ViewerStopResponse? Stop(string? id, bool all)
    {
        ViewerServiceMarker? marker = _store.ReadLive();
        if (marker is null)
        {
            return null;
        }
        return all || id is null
            ? Send(marker, ViewerServiceCommands.Stop, null, null,
                ViewerServiceJsonContext.Default.ViewerStopResponse)
            : Send(marker, ViewerServiceCommands.Close, id, null,
                ViewerServiceJsonContext.Default.ViewerStopResponse);
    }

    /// <summary>Renders one open document again, or all of them.</summary>
    public ViewerStatusResponse? Refresh(string? id = null) =>
        _store.ReadLive() is { } marker
            ? Send(marker, ViewerServiceCommands.Refresh, id, null,
                ViewerServiceJsonContext.Default.ViewerStatusResponse)
            : null;

    private ViewerServiceMarker Running(GlobalValues globals, int requestedPort, OperationDeadline deadline)
    {
        deadline.ThrowIfExpired("service-discovery");
        if (_store.ReadLive() is { } running)
        {
            return running;
        }
        LocalServiceOperationLock starting;
        try
        {
            starting = LocalServiceOperationLock.Acquire(
                ViewerServiceCommands.Service,
                ViewerServiceCommands.LockKey("start"),
                Remaining(deadline, StartLockTimeout));
        }
        catch (TimeoutException)
        {
            deadline.ThrowIfExpired("service-start-lock");
            throw ViewerErrors.ServiceBusy();
        }
        using (starting)
        {
            deadline.ThrowIfExpired("service-start");
            return _store.ReadLive() ?? Start(globals, requestedPort, deadline);
        }
    }

    private ViewerServiceMarker Start(GlobalValues globals, int requestedPort, OperationDeadline deadline)
    {
        deadline.ThrowIfExpired("service-start");
        ProcessStartInfo start = SelfProcessLauncher.CreateBackground(
            "preview",
            "Run the published 'aspose-cli' executable directly.");
        start.ArgumentList.Add(ViewerServiceCommands.CommandName);
        if (requestedPort > 0)
        {
            start.ArgumentList.Add("--port");
            start.ArgumentList.Add(requestedPort.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        start.ArgumentList.Add("--quiet");
        start.ArgumentList.Add("--output");
        start.ArgumentList.Add("json");
        start.WorkingDirectory = SelfProcessLauncher.ServiceWorkingDirectory;
        string workDirectory = Path.GetFullPath(globals.WorkDir ?? Directory.GetCurrentDirectory());
        LocalServiceChild child;
        try
        {
            child = SelfProcessLauncher.Start(start, new ServiceStartSecrets
            {
                WorkDirectory = workDirectory,
                LicensePath = globals.LicensePath is null
                    ? null
                    : Path.GetFullPath(globals.LicensePath, workDirectory),
                ServiceToken = LocalHttpRequestSecurity.RandomToken(),
                ServiceNonce = LocalHttpRequestSecurity.RandomToken(),
            });
        }
        catch (Exception exception) when (
            exception is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            throw CliErrors.OptionInvalid(
                "preview",
                "the viewer service could not be started",
                "Run 'aspose-cli doctor', then retry 'aspose-cli preview <file> --verbose'.");
        }

        return LocalServiceStartHandshake.WaitForReady(
            child,
            StartupTimeout,
            "preview",
            () => _store.ReadCandidate() is { } candidate
                && LocalServiceStartHandshake.Matches(
                    candidate.Version,
                    candidate.Pid,
                    candidate.StartTicksUtc,
                    candidate.Nonce,
                    child)
                ? candidate
                : null,
            DiagnosticRedactor.Redact,
            (exitCode, error) => error.Contains(ErrorCodes.LoopbackPortInUse.Name, StringComparison.Ordinal)
                ? CliErrors.LoopbackPortInUse(requestedPort)
                : CliErrors.OptionInvalid(
                    "preview",
                    $"the viewer service exited with code {exitCode}",
                    "Run 'aspose-cli doctor', then retry 'aspose-cli preview <file> --verbose'."),
            deadline);
    }

    private static OperationDeadline? StartDeadline(GlobalValues globals, OperationDeadline? parent)
    {
        TimeSpan render = LocalServiceResourceLimits.Resolve().RenderTimeout;
        TimeSpan budget = globals.TimeoutSeconds is { } seconds
            ? TimeSpan.FromSeconds(Math.Min(seconds, render.TotalSeconds)) : render;
        long ceiling = checked(Environment.TickCount64 + (long)budget.TotalMilliseconds);
        if (parent?.ExpiresAtTick is { } expires && expires <= ceiling) { return null; }
        return OperationDeadline.FromAbsoluteTick(budget,
            Math.Min(parent?.ExpiresAtTick ?? ceiling, ceiling), parent?.Token ?? CancellationToken.None);
    }

    private static TimeSpan Remaining(OperationDeadline deadline, TimeSpan ceiling)
    {
        TimeSpan remaining = deadline.Remaining ?? ceiling;
        deadline.ThrowIfExpired("service-start-lock");
        return remaining < ceiling ? remaining : ceiling;
    }

    private static T Send<T>(
        ViewerServiceMarker marker,
        string command,
        string? path,
        string? payload,
        JsonTypeInfo<T> type,
        OperationDeadline? deadline = null)
    {
        LocalServiceControlResponse response = LocalServiceControlServer.Send(
            new LocalServiceControlEndpoint(ViewerServiceCommands.Service, marker.Id),
            marker.Nonce,
            marker.Token,
            command,
            path,
            payload: payload,
            deadline: deadline);
        if (!response.Ok)
        {
            deadline?.ThrowIfExpired("preview-control");
            throw Failure(response);
        }
        return response.Result is { } result
            ? result.Deserialize(type)
                ?? throw new InvalidDataException("The viewer service returned an empty result.")
            : throw new InvalidDataException("The viewer service returned no result.");
    }

    /// <summary>Raises the service's failure as the command's own.</summary>
    private static CliException Failure(LocalServiceControlResponse response)
    {
        ViewerFailure? failure = null;
        try
        {
            failure = response.Result?.Deserialize(ViewerServiceJsonContext.Default.ViewerFailure);
        }
        catch (JsonException)
        {
            // The message below still says what happened.
        }
        return failure is null
            ? new CliException(
                ErrorCodes.Internal,
                response.Message ?? "The viewer service refused the request.")
            : new CliException(new ErrorCode(failure.Code, (ExitCode)failure.Exit), failure.Message);
    }
}
