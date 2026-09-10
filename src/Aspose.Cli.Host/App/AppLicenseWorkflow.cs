using Aspose.Cli.Host.Catalog;
using Aspose.Cli.Host.Licensing;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Host.App;

/// <summary>Owns App license installation, removal and restart workflow.</summary>
internal sealed class AppLicenseWorkflow
{
    private const long MaxLicenseBytes = 1024 * 1024;
    private readonly ProductCatalog _catalog;
    private readonly CliEditionInfo _edition;
    private readonly Func<CapabilitiesResult> _capabilities;
    private readonly AppLicenseState _licenses;
    private readonly AppPreferencesStore _preferences;
    private readonly AppDocumentSession _sessions;
    private readonly AppLog _log;
    private readonly Action _releaseControl;
    private readonly Action _requestStop;
    private readonly Action _writeMarker;
    private readonly Action _invalidateDiagnostics;
    private readonly FontSearchProfile _fontProfile;

    internal AppLicenseWorkflow(
        ProductCatalog catalog,
        CliEditionInfo edition,
        Func<CapabilitiesResult> capabilities,
        AppLicenseState licenses,
        AppPreferencesStore preferences,
        AppDocumentSession sessions,
        AppLog log,
        Action releaseControl,
        Action requestStop,
        Action writeMarker,
        Action invalidateDiagnostics,
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
        _requestStop = requestStop;
        _writeMarker = writeMarker;
        _invalidateDiagnostics = invalidateDiagnostics;
        _fontProfile = fontProfile ?? throw new ArgumentNullException(nameof(fontProfile));
    }

    internal void Install(
        string fileName,
        Stream input,
        long contentLength,
        string? productId)
    {
        ProductLicenseProvisioning.EnsureApplicable(
            _catalog,
            productId,
            "installation");
        if (!string.Equals(
                Path.GetExtension(fileName),
                ".lic",
                StringComparison.OrdinalIgnoreCase))
        {
            throw CliErrors.OptionInvalid(
                "license",
                "the selected file does not use the .lic extension",
                "Choose the .lic file supplied by Aspose.");
        }
        if (contentLength > MaxLicenseBytes)
        {
            throw CliErrors.FileTooLarge(
                contentLength,
                MaxLicenseBytes);
        }

        string temporaryDirectory =
            PrivateUserStorage.CreateTemporaryDirectory("license");
        string temporary = Path.Combine(
            temporaryDirectory,
            $"aspose-license-{Guid.NewGuid():N}.lic");
        try
        {
            using (FileStream output =
                   PrivateUserStorage.CreateFile(temporary))
            {
                BoundedStreamCopy.Copy(
                    input,
                    output,
                    MaxLicenseBytes,
                    total => CliErrors.FileTooLarge(
                        total,
                        MaxLicenseBytes));
                output.Flush(flushToDisk: true);
            }

            IReadOnlyList<string> installed = _licenses.Install(
                temporary,
                productId);
            _preferences.CompleteOnboarding();
            _sessions.ReopenForLicenseOrPreferences();
            _invalidateDiagnostics();
            _writeMarker();
            _log.Write(
                $"license installed for {string.Join(", ", installed)} and preview refreshed");
        }
        finally
        {
            LocalFileCleanup.DeleteFile(temporary);
            LocalFileCleanup.DeleteDirectory(
                temporaryDirectory,
                recursive: false);
        }
    }

    internal string RemoveAndRestart(string? productId)
    {
        string target = productId
            ?? _sessions.ProductId
            ?? _catalog.DefaultProductId();
        _licenses.RemoveUserLicense(target);
        string? originalFile = _sessions.OriginalFilePath;
        _releaseControl();

        AppInstance replacement =
            new AppServiceController(
                _catalog,
                _edition,
                _capabilities).StartReplacement(
                _licenses.Globals,
                AppRoutes.Settings,
                originalFile,
                _fontProfile);
        string url =
            $"http://127.0.0.1:{replacement.Port}/settings";
        _log.Write(
            $"saved {target} license removed; replacement App started");
        _ = Task.Run(async () =>
        {
            await Task.Delay(250).ConfigureAwait(false);
            _requestStop();
        });
        return url;
    }

}
