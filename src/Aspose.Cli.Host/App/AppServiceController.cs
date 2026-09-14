using System.Diagnostics;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.Licensing;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Preview;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Host.App;

/// <summary>Starts, discovers and controls the per-user background App process.</summary>
internal sealed class AppServiceController
{
    private static readonly TimeSpan StartupTimeout =
        TimeSpan.FromSeconds(60);
    private readonly AppInstanceStore _instances;
    private readonly ProductCatalog _catalog;
    private readonly Func<CapabilitiesResult> _capabilities;

    public AppServiceController(
        ProductCatalog catalog,
        Func<CapabilitiesResult> capabilities)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _capabilities = capabilities
            ?? throw new ArgumentNullException(nameof(capabilities));
        try
        {
            _instances = new AppInstanceStore(AppPaths.Marker);
        }
        catch (Exception exception) when (
            AppStartupDiagnostics.IsExpected(exception))
        {
            throw AppStartupDiagnostics.Failure(
                AppStartupDiagnostics.ConfigurationStage(exception),
                exception);
        }
    }

    public AppResult StartOrActivate(
        GlobalValues globals,
        string route,
        string? filePath,
        bool openBrowser,
        FontSearchProfile fontProfile)
    {
        using LocalServiceOperationLock operationLock =
            LocalServiceOperationLock.Acquire(
                "app",
                "singleton",
                StartupTimeout + StartupTimeout);
        string normalizedRoute = filePath is null ? route : AppRoutes.Preview;
        AppInstance? marker = LiveMarker();
        if (marker is not null
            && !MatchesFontProfile(marker, fontProfile))
        {
            throw CliErrors.OptionInvalid(
                "--font-dir",
                "the running App uses a different font profile",
                "Run 'aspose-cli app stop', then start the App with the required --font-dir options.");
        }
        string? licenseIdentity = LicenseManager.InstanceIdentity(
            CompositionRoot.Create(_catalog, globals));
        if (marker is not null
            && (licenseIdentity is null
                || !string.Equals(marker.LicenseIdentity, licenseIdentity, StringComparison.Ordinal)))
        {
            StopOwned(marker);
            marker = null;
        }
        bool reused = marker is not null;
        if (marker is null)
        {
            LocalServiceChild child = StartBackground(
                globals,
                normalizedRoute,
                filePath,
                fontProfile,
                licenseIdentity);
            marker = WaitForMarker(child, licenseIdentity);
        }
        else
        {
            if (filePath is not null)
            {
                SendOpen(marker, filePath);
                marker = marker with
                {
                    Route = AppRoutes.Preview,
                    File = Path.GetFileName(filePath),
                };
            }

            AppControlResponse activated =
                AppControlEndpoint.Send(marker, "activate", normalizedRoute);
            if (!activated.Ok || activated.Result?.Url is null)
            {
                throw CliErrors.OptionInvalid(
                    "app",
                    activated.Message
                        ?? "the running App could not activate the requested page",
                    "Run 'aspose-cli app stop', then start the App again.");
            }

            marker = LiveMarker() ?? marker;
            if (openBrowser && ShouldOpenBrowser())
            {
                TryOpenBrowser(activated.Result.Url);
            }

            return activated.Result with
            {
                Reused = true,
                Route = normalizedRoute,
                File = marker.File,
            };
        }

        string url = PublicUrl(marker, normalizedRoute);
        if (openBrowser && ShouldOpenBrowser())
        {
            TryOpenBrowser(url);
        }

        return new AppResult
        {
            Running = true,
            Url = url,
            Port = marker.Port,
            Pid = marker.Pid,
            Reused = reused,
            Route = normalizedRoute,
            File = marker.File,
        };
    }

    public AppResult Status()
    {
        AppInstance? marker = LiveMarker();
        return marker is null
            ? new AppResult
            {
                Running = false,
                Reused = false,
                Route = AppRoutes.Home,
            }
            : new AppResult
            {
                Running = true,
                Url = PublicUrl(marker, marker.Route),
                Port = marker.Port,
                Pid = marker.Pid,
                Reused = true,
                Route = marker.Route,
                File = marker.File,
            };
    }

    public AppResult Stop()
    {
        using LocalServiceOperationLock operationLock =
            LocalServiceOperationLock.Acquire(
                "app",
                "singleton",
                StartupTimeout + StartupTimeout);
        AppInstance? marker = LiveMarker();
        if (marker is not null)
        {
            StopOwned(marker);
        }

        return new AppResult
        {
            Running = false,
            Reused = marker is not null,
            Route = marker?.Route ?? AppRoutes.Home,
            File = marker?.File,
        };
    }

    private void StopOwned(AppInstance marker)
    {
        LocalServiceProcessIdentity identity =
            Identity(marker);
        bool stopped = LocalServiceStopper.TryStop(
            identity,
            TimeSpan.FromSeconds(10),
            "local-service-stop",
            () =>
            {
                AppControlResponse response =
                    AppControlEndpoint.Send(
                        marker,
                        "stop");
                if (!response.Ok)
                {
                    throw CliErrors.OptionInvalid(
                        "app stop",
                        "the running instance rejected the stop request",
                        "Run 'aspose-cli app status' and retry.");
                }
            });
        if (!stopped)
        {
            throw CliErrors.OptionInvalid(
                "app stop",
                "the running App could not be confirmed stopped",
                "Retry 'aspose-cli app stop'; the protected marker remains available for recovery.");
        }

        _instances.DeleteIfOwned(marker.Token);
    }

    public AppInstance StartReplacement(
        GlobalValues globals,
        string route,
        string? filePath,
        FontSearchProfile fontProfile,
        string? uploadedFilePath = null,
        string? uploadedFileName = null)
    {
        using LocalServiceOperationLock operationLock =
            LocalServiceOperationLock.Acquire(
                "app",
                "singleton",
                StartupTimeout + StartupTimeout);
        string? licenseIdentity = LicenseManager.IsolatedInstanceIdentity(
            CompositionRoot.Create(_catalog, globals));
        return WaitForMarker(
            StartBackground(
                globals,
                route,
                filePath,
                fontProfile,
                licenseIdentity,
                uploadedFilePath,
                uploadedFileName),
            licenseIdentity);
    }

    public HostedCommandLifecycle StartForeground(
        GlobalValues globals,
        int port,
        string route,
        string? filePath,
        bool openBrowser,
        FontSearchProfile fontProfile)
    {
        AppHost host = CreateHost(globals, fontProfile);
        try
        {
            AppResult result = host.Start(port, route, filePath);
            if (openBrowser && ShouldOpenBrowser())
            {
                TryOpenBrowser(result.Url!);
            }
            return new HostedCommandLifecycle(
                result,
                once: false,
                Timeout.InfiniteTimeSpan,
                (_, cancellationToken) =>
                {
                    try
                    {
                        host.Wait(cancellationToken);
                        return WaitOutcome.Completed;
                    }
                    catch (OperationCanceledException)
                    {
                        return WaitOutcome.CancelRequested;
                    }
                },
                host.Dispose);
        }
        catch
        {
            host.Dispose();
            throw;
        }
    }

    public int RunService(GlobalValues globals, int port, string route, string? filePath)
    {
        using AppHost host = CreateHost(
            globals,
            ServiceStartSecretChannel.Current?.FontProfile
                ?? FontSearchProfile.Ambient);
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += handler;
        try
        {
            host.Start(port, route, filePath);
            host.Wait(cancellation.Token);
            return (int)ExitCode.Success;
        }
        catch (OperationCanceledException)
        {
            return (int)ExitCode.Success;
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
    }

    private AppInstance? LiveMarker()
    {
        // The store already validates PID + start time + nonce. The marker is
        // published only after the framed control server is listening.
        // Treating a transient connection gap as stale would delete an active
        // marker and reintroduce a duplicate-start race.
        return _instances.ReadLive();
    }

    private AppHost CreateHost(
        GlobalValues globals,
        FontSearchProfile fontProfile)
    {
        try
        {
            return new AppHost(
                _catalog,
                _capabilities,
                globals,
                fontProfile);
        }
        catch (Exception exception) when (
            AppStartupDiagnostics.IsExpected(exception))
        {
            throw AppStartupDiagnostics.Failure(
                AppStartupDiagnostics.ConfigurationStage(exception),
                exception);
        }
    }

    private static LocalServiceChild StartBackground(
        GlobalValues globals,
        string route,
        string? filePath,
        FontSearchProfile fontProfile,
        string? licenseIdentity,
        string? uploadedFilePath = null,
        string? uploadedFileName = null)
    {
        ProcessStartInfo start =
            SelfProcessLauncher.CreateBackground(
                "app",
                "Run the published 'aspose-cli' executable directly.");
        start.ArgumentList.Add("app");
        start.ArgumentList.Add("--serve");
        start.ArgumentList.Add("--port");
        start.ArgumentList.Add("0");
        start.ArgumentList.Add("--route");
        start.ArgumentList.Add(route);
        if (filePath is not null)
        {
            start.ArgumentList.Add(filePath);
        }

        string workDirectory = Path.GetFullPath(
            globals.WorkDir ?? Directory.GetCurrentDirectory());
        string token = Convert.ToHexString(
                System.Security.Cryptography
                    .RandomNumberGenerator.GetBytes(24))
            .ToLowerInvariant();
        string nonce = Convert.ToHexString(
                System.Security.Cryptography
                    .RandomNumberGenerator.GetBytes(24))
            .ToLowerInvariant();
        try
        {
            return SelfProcessLauncher.Start(
                start,
                new ServiceStartSecrets
                {
                    WorkDirectory = workDirectory,
                    LicensePath = globals.LicensePath is null
                        ? null
                        : Path.GetFullPath(
                            globals.LicensePath,
                            workDirectory),
                    ExpectedAppLicenseIdentity = licenseIdentity,
                    AppUploadedFilePath = uploadedFilePath,
                    AppUploadedFileName = uploadedFileName,
                    ServiceToken = token,
                    ServiceNonce = nonce,
                    FontProfile = fontProfile.IsAmbient ? null : fontProfile,
                });
        }
        catch (Exception exception) when (
            exception is IOException
                or InvalidOperationException
                or System.ComponentModel.Win32Exception)
        {
            throw CliErrors.OptionInvalid(
                "app",
                "the background process could not be started",
                "Run 'aspose-cli app --foreground' to see startup diagnostics.");
        }
    }

    private static bool MatchesFontProfile(
        AppInstance marker,
        FontSearchProfile profile) =>
        string.Equals(
            marker.FontProfileFingerprint
                ?? FontSearchProfile.Ambient.Fingerprint,
            profile.Fingerprint,
            StringComparison.Ordinal);

    private AppInstance WaitForMarker(
        LocalServiceChild child,
        string? licenseIdentity)
    {
        return LocalServiceStartHandshake.WaitForReady(
            child,
            StartupTimeout,
            "app",
            () =>
            {
                AppInstance? marker =
                    _instances.ReadReadyCandidate();
                return marker is not null
                    && LocalServiceStartHandshake.Matches(
                        marker.Version,
                        marker.Pid,
                        marker.StartTicksUtc,
                        marker.Nonce,
                        child)
                    && string.Equals(marker.LicenseIdentity, licenseIdentity, StringComparison.Ordinal)
                    ? marker
                    : null;
            },
            DiagnosticRedactor.Redact,
            (exitCode, error) =>
            {
                if (AppStartupDiagnostics.TryReadStage(
                        error,
                        out AppStartupStage stage))
                {
                    return AppStartupDiagnostics.Failure(
                        stage,
                        new InvalidOperationException(
                            $"The background App exited with code {exitCode}."));
                }
                if (error.Contains(
                        ErrorCodes.LoopbackPortInUse.Name,
                        StringComparison.Ordinal))
                {
                    return CliErrors.LoopbackPortInUse(0);
                }

                return CliErrors.OptionInvalid(
                    "app",
                    $"the background process exited with code {exitCode}",
                    "Run 'aspose-cli app --foreground' to inspect startup diagnostics.");
            });
    }

    private static void SendOpen(AppInstance marker, string filePath)
    {
        try
        {
            AppControlResponse response = AppControlEndpoint.Send(marker, "open", Path.GetFullPath(filePath));
            if (!response.Ok)
            {
                throw CliErrors.OptionInvalid("app file", response.Message ?? "the file could not be opened by the running App", "Check the file and open it from the App's Files page.");
            }
        }
        catch (Exception ex) when (ex is IOException or TimeoutException)
        {
            throw CliErrors.OptionInvalid("app", $"the running instance could not be reached ({ex.Message})", "Run 'aspose-cli app stop', then start it again.");
        }
    }

    private static LocalServiceProcessIdentity Identity(
        AppInstance marker) => new(
        marker.Pid,
        marker.StartTicksUtc,
        marker.Nonce);

    private static string PublicUrl(
        AppInstance marker,
        string route) =>
        $"http://127.0.0.1:{marker.Port}/"
        + (route == AppRoutes.Welcome ? string.Empty : route);

    private static bool ShouldOpenBrowser()
    {
        string? noOpen = Environment.GetEnvironmentVariable("ASPOSE_CLI_NO_OPEN");
        if (string.Equals(noOpen, "1", StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CI"))
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")))
        {
            return false;
        }

        return Environment.UserInteractive;
    }

    private static void TryOpenBrowser(string url)
    {
        try
        {
            using Process? browser = OperatingSystem.IsWindows()
                ? Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })
                : Process.Start(OperatingSystem.IsMacOS() ? "open" : "xdg-open", url);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Console.Error.WriteLine(
                AppStartupDiagnostics.BrowserWarning(ex, url));
        }
    }
}
