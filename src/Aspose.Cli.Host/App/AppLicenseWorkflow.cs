using System.Text.Json.Nodes;
using Aspose.Cli.Host.Catalog;
using Aspose.Cli.Host.Licensing;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Host.App;

/// <summary>Owns App license installation, removal and restart workflow.</summary>
internal sealed class AppLicenseWorkflow
{
    internal const string RestartFailedMessage =
        "The license configuration was saved, but the App could not restart. The current App is still running.";
    private readonly ProductCatalog _catalog;
    private readonly CliEditionInfo _edition;
    private readonly Func<CapabilitiesResult> _capabilities;
    private readonly LicenseManager _licenses;
    private readonly AppPreferencesStore _preferences;
    private readonly AppDocumentSession _sessions;
    private readonly AppLog _log;
    private readonly Action _releaseControl;
    private readonly Action _restoreControl;
    private readonly Action _requestStop;
    private readonly FontSearchProfile _fontProfile;

    internal AppLicenseWorkflow(
        ProductCatalog catalog,
        CliEditionInfo edition,
        Func<CapabilitiesResult> capabilities,
        LicenseManager licenses,
        AppPreferencesStore preferences,
        AppDocumentSession sessions,
        AppLog log,
        Action releaseControl,
        Action restoreControl,
        Action requestStop,
        FontSearchProfile fontProfile)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _edition = edition ?? throw new ArgumentNullException(nameof(edition));
        _capabilities = capabilities
            ?? throw new ArgumentNullException(nameof(capabilities));
        _licenses = licenses;
        _preferences = preferences;
        _sessions = sessions;
        _log = log;
        _releaseControl = releaseControl;
        _restoreControl = restoreControl;
        _requestStop = requestStop;
        _fontProfile = fontProfile ?? throw new ArgumentNullException(nameof(fontProfile));
    }

    internal string InstallAndRestart(Stream input, long contentLength, string? productId)
    {
        LicenseManager.EnsureApplicable(_catalog, productId, "installation");
        if (contentLength > LicenseInstaller.MaximumBytes)
        {
            throw CliErrors.FileTooLarge(contentLength, LicenseInstaller.MaximumBytes);
        }
        IReadOnlyList<string> installed = _licenses.Install(input, productId);
        _preferences.CompleteOnboarding();
        _log.Write($"license installed for {string.Join(", ", installed)}");
        return Restart();
    }

    internal string RemoveAndRestart(string? productId)
    {
        string target = productId
            ?? _sessions.ProductId
            ?? _catalog.DefaultProductId();
        _licenses.Remove(target);
        _log.Write($"saved {target} license removed");
        return Restart();
    }

    private string Restart()
    {
        string? originalFile = _sessions.OriginalFilePath;
        _releaseControl();

        AppInstance replacement;
        try
        {
            replacement =
                new AppServiceController(
                    _catalog,
                    _edition,
                    _capabilities).StartReplacement(
                    _licenses.Globals,
                    AppRoutes.Settings,
                    originalFile,
                    _fontProfile,
                    _sessions.UploadedFilePath,
                    _sessions.FileName);
        }
        catch (Exception exception)
        {
            _restoreControl();
            _log.Write($"license saved, restart failed: {exception.GetType().Name}");
            throw new CliException(ErrorCodes.AppStartupFailed,
                RestartFailedMessage,
                hint: "Repair or reopen the current document, then restart the App to use the saved license.",
                details: new JsonObject { ["licenseSaved"] = true },
                innerException: exception);
        }
        string url =
            $"http://127.0.0.1:{replacement.Port}/settings";
        _log.Write("license configuration changed; replacement App started");
        _ = Task.Run(async () =>
        {
            await Task.Delay(250).ConfigureAwait(false);
            _requestStop();
        });
        return url;
    }

}
