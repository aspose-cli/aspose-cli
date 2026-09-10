using System.Diagnostics;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.LocalServices;
using System.Net.Sockets;
using System.Text.Json;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Host.Preview;

/// <summary>Starts, discovers, reuses, and gracefully stops background preview processes.</summary>
internal sealed class PreviewServiceController
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(10);
    private readonly PreviewSessionStore _store = new();

    public PreviewStartState Start(
        ProductDefinition product,
        string workDirectory,
        string? licensePath,
        string file,
        int port,
        ProductPreviewRequest request,
        string? presentationEffect,
        bool openBrowser)
    {
        using LocalServiceOperationLock operationLock =
            LocalServiceOperationLock.Acquire(
                "preview",
                "registry",
                StartupTimeout + StartupTimeout);
        PreviewSessionMarker? existing = LiveMarkers().FirstOrDefault(marker =>
            string.Equals(
                marker.Product,
                product.Manifest.Id,
                StringComparison.Ordinal) &&
            PathsEqual(marker.File, file) &&
            string.Equals(marker.View, request.View, StringComparison.Ordinal) &&
            SelectorsEqual(marker.Selector, request.Selector) &&
            string.Equals(
                marker.FontProfileFingerprint,
                request.FontProfile?.Fingerprint,
                StringComparison.Ordinal));
        if (existing is not null)
        {
            LocalServiceControlResponse status =
                PreviewControlEndpoint.Status(existing);
            if (!status.Ok || status.Result is null)
            {
                throw CliErrors.OptionInvalid(
                    "preview",
                    status.Message
                        ?? "the preview could not report its current state",
                    "Stop the session and start it again.");
            }

            if (openBrowser)
            {
                PreviewRuntime.OpenBrowser(existing.Url, quiet: false);
            }

            return ToStart(existing, reused: true);
        }

        string id = Guid.NewGuid().ToString("N");
        string token = Convert.ToHexString(
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        string nonce = Convert.ToHexString(
                System.Security.Cryptography
                    .RandomNumberGenerator.GetBytes(24))
            .ToLowerInvariant();
        LocalServiceChild child = StartBackground(
            product,
            workDirectory,
            licensePath,
            file,
            port,
            request,
            presentationEffect,
            id,
            token,
            nonce);
        PreviewSessionMarker marker = WaitForMarker(
            child,
            id,
            port);
        if (openBrowser)
        {
            PreviewRuntime.OpenBrowser(marker.Url, quiet: false);
        }

        return ToStart(marker, reused: false);
    }

    public PreviewStatusState Status(string? id)
    {
        ValidateOptionalId(id);
        var sessions = new List<PreviewSessionState>();
        var warnings = new List<Warning>();
        foreach (PreviewSessionMarker marker in LiveMarkers()
                     .Where(marker => Matches(marker, id)))
        {
            PreviewInteractiveState? interaction = ReadInteraction(marker);
            if (interaction is null)
            {
                warnings.Add(new Warning
                {
                    Code = WarningCodes.PreviewStateUnavailable,
                    Message = $"Preview session '{marker.Id}' did not return its interactive state.",
                    Hint = "Retry 'aspose-cli preview status'; the preview remains available and stoppable.",
                    Location = $"preview/{marker.Id}",
                });
            }
            sessions.Add(ToState(marker, interaction));
        }
        return new PreviewStatusState(sessions, warnings);
    }

    public PreviewStopState Stop(string? id, bool all)
    {
        if (all == (id is not null))
        {
            throw CliErrors.OptionInvalid(
                "preview stop",
                "give exactly one session id or --all",
                "Run 'aspose-cli preview status' to list session ids.");
        }

        ValidateOptionalId(id);
        using LocalServiceOperationLock operationLock =
            LocalServiceOperationLock.Acquire(
                "preview",
                "registry",
                StartupTimeout + StartupTimeout);
        PreviewSessionMarker[] selected = LiveMarkers()
            .Where(marker => Matches(marker, id) && (all || marker.Id == id))
            .ToArray();
        var stopped = new List<string>();
        foreach (PreviewSessionMarker marker in selected)
        {
            LocalServiceProcessIdentity identity =
                Identity(marker);
            bool confirmed = LocalServiceStopper.TryStop(
                identity,
                StopTimeout,
                "local-service-stop",
                () =>
                {
                    LocalServiceControlResponse response =
                        PreviewControlEndpoint.Stop(marker);
                    if (!response.Ok)
                    {
                        throw CliErrors.OptionInvalid(
                            "preview stop",
                            response.Message
                                ?? "the preview rejected the authenticated stop request",
                            "Run 'aspose-cli preview status' and retry.");
                    }
                });
            if (!confirmed)
            {
                throw CliErrors.OptionInvalid(
                    "preview stop",
                    "the preview process could not be confirmed stopped",
                    "Retry the stop command; the protected marker remains available for recovery.");
            }

            _store.DeleteIfOwned(
                marker.Id,
                marker.Token);
            stopped.Add(marker.Id);
        }

        PreviewSessionState[] remaining = LiveMarkers()
            .Select(static marker => ToState(marker))
            .ToArray();
        return new PreviewStopState(stopped, remaining);
    }

    private IReadOnlyList<PreviewSessionMarker> LiveMarkers() =>
        _store.ReadLive();

    private static LocalServiceChild StartBackground(
        ProductDefinition product,
        string workDirectory,
        string? licensePath,
        string file,
        int port,
        ProductPreviewRequest request,
        string? presentationEffect,
        string id,
        string token,
        string nonce)
    {
        product.Preview.ValidateRequest(request, presentationEffect);
        ProcessStartInfo start =
            SelfProcessLauncher.CreateBackground(
                "preview",
                "Run the published 'aspose-cli' executable directly.");
        start.ArgumentList.Add("preview");
        start.ArgumentList.Add("__host");
        start.ArgumentList.Add(file);
        start.ArgumentList.Add("--product");
        start.ArgumentList.Add(product.Manifest.Id);
        start.ArgumentList.Add("--port");
        start.ArgumentList.Add(port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add("--view");
        start.ArgumentList.Add(request.View);
        if (presentationEffect is not null)
        {
            start.ArgumentList.Add("--presentation-effect");
            start.ArgumentList.Add(presentationEffect);
        }

        start.ArgumentList.Add("--preview-service-id");
        start.ArgumentList.Add(id);
        start.ArgumentList.Add("--quiet");
        start.ArgumentList.Add("--output");
        start.ArgumentList.Add("json");
        try
        {
            return SelfProcessLauncher.Start(
                start,
                new ServiceStartSecrets
                {
                    WorkDirectory = Path.GetFullPath(workDirectory),
                    LicensePath = licensePath is null
                        ? null
                        : Path.GetFullPath(
                            licensePath,
                            workDirectory),
                    Password = request.Password,
                    ServiceToken = token,
                    ServiceNonce = nonce,
                    PreviewSelector = request.Selector,
                    FontProfile = request.FontProfile,
                });
        }
        catch (Exception exception) when (
            exception is IOException
                or InvalidOperationException
                or System.ComponentModel.Win32Exception)
        {
            throw CliErrors.OptionInvalid(
                "preview",
                "the background process could not be started",
                "Run 'aspose-cli doctor', then retry 'aspose-cli preview <file> --verbose'.");
        }
    }

    private PreviewSessionMarker WaitForMarker(
        LocalServiceChild child,
        string id,
        int requestedPort)
    {
        return LocalServiceStartHandshake.WaitForReady(
            child,
            StartupTimeout,
            "preview",
            () =>
            {
                PreviewSessionMarker? marker =
                    _store.ReadReadyCandidate(id);
                return marker is not null
                    && LocalServiceStartHandshake.Matches(
                        marker.Version,
                        marker.Pid,
                        marker.StartTicksUtc,
                        marker.Nonce,
                        child)
                    ? marker
                    : null;
            },
            DiagnosticRedactor.Redact,
            (exitCode, error) =>
            {
                if (error.Contains(
                    ErrorCodes.LoopbackPortInUse.Name,
                    StringComparison.Ordinal))
                {
                    return CliErrors.LoopbackPortInUse(requestedPort);
                }

                return CliErrors.OptionInvalid(
                    "preview",
                    $"the background preview exited with code {exitCode}",
                    "Run 'aspose-cli doctor', then retry 'aspose-cli preview <file> --verbose'.");
            });
    }

    private static PreviewStartState ToStart(PreviewSessionMarker marker, bool reused) =>
        new(
            ToState(marker),
            reused,
            marker.Metadata);

    private static PreviewSessionState ToState(
        PreviewSessionMarker marker,
        PreviewInteractiveState? interaction = null) => new(
        marker.Id,
        marker.Product,
        marker.Url,
        marker.Pid,
        marker.File,
        marker.View,
        marker.Selector,
        interaction?.Revision,
        interaction?.State);

    private static PreviewInteractiveState? ReadInteraction(
        PreviewSessionMarker marker)
    {
        try
        {
            LocalServiceControlResponse response =
                PreviewControlEndpoint.Status(marker);
            if (response.Ok && response.Result is { } result)
            {
                return result.Deserialize(
                    PreviewLocalServiceJsonContext.Default.PreviewInteractiveState);
            }
        }
        catch (Exception exception) when (
            exception is IOException
                or SocketException
                or UnauthorizedAccessException
                or OperationCanceledException
                or InvalidDataException
                or JsonException)
        {
        }
        return null;
    }

    private static bool SelectorsEqual(
        ProductPreviewPayload? left,
        ProductPreviewPayload? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }
        if (left is null || right is null)
        {
            return false;
        }

        return string.Equals(left.ProductId, right.ProductId, StringComparison.Ordinal)
            && string.Equals(left.Kind, right.Kind, StringComparison.Ordinal)
            && left.SchemaVersion == right.SchemaVersion
            && string.Equals(left.SchemaId, right.SchemaId, StringComparison.Ordinal)
            && string.Equals(
                left.Payload.GetRawText(),
                right.Payload.GetRawText(),
                StringComparison.Ordinal);
    }

    private static bool Matches(PreviewSessionMarker marker, string? id) =>
        id is null || string.Equals(marker.Id, id, StringComparison.Ordinal);

    private static bool PathsEqual(string left, string right) => string.Equals(
        Path.GetFullPath(left),
        Path.GetFullPath(right),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static LocalServiceProcessIdentity Identity(
        PreviewSessionMarker marker) => new(
        marker.Pid,
        marker.StartTicksUtc,
        marker.Nonce);

    private static void ValidateOptionalId(string? id)
    {
        if (id is not null && !PreviewSessionStore.IsValidId(id))
        {
            throw CliErrors.OptionInvalid(
                "preview id",
                "the session id is invalid",
                "Run 'aspose-cli preview status' and copy an id exactly.");
        }
    }
}
