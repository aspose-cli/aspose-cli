using Aspose.Cli.Host.Catalog;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.Preview;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Host.App;

/// <summary>
/// Owns App workspace state: open/upload, recent files, onboarding and
/// product preview preferences.
/// </summary>
internal sealed class AppWorkspace
{
    private readonly ProductCatalog _catalog;
    private readonly AppPreferencesStore _preferences;
    private readonly AppDocumentSession _sessions;
    private readonly AppLog _log;
    private readonly Action _touch;
    private readonly Action<string> _setRoute;
    private readonly Action _writeMarker;

    internal AppWorkspace(
        ProductCatalog catalog,
        AppPreferencesStore preferences,
        AppDocumentSession sessions,
        AppLog log,
        Action touch,
        Action<string> setRoute,
        Action writeMarker)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _preferences = preferences;
        _sessions = sessions;
        _log = log;
        _touch = touch;
        _setRoute = setRoute;
        _writeMarker = writeMarker;
    }

    internal void CompleteOnboarding()
    {
        _preferences.CompleteOnboarding();
        _setRoute(AppRoutes.Home);
    }

    internal AppApiResult PickAndOpen()
    {
        _touch();
        FilePickerResult picked = SystemFilePicker.PickFile(
            _catalog.DefaultOwnerExtensions);
        if (!picked.Available)
        {
            return new AppApiResult(
                true,
                Message: "A system file picker is not available. Choose a temporary preview copy instead.",
                FallbackUpload: true);
        }
        if (picked.Path is null)
        {
            return new AppApiResult(true, Message: "No file was selected.");
        }

        OpenPath(picked.Path, uploadedCopy: false);
        return new AppApiResult(true);
    }

    internal void OpenPath(string path, bool uploadedCopy)
    {
        _touch();
        _preferences.CompleteOnboarding();
        _sessions.Open(path, uploadedCopy);
        _setRoute(AppRoutes.Preview);
    }

    internal async Task UploadFileAsync(
        string fileName,
        Stream input,
        long contentLength,
        CancellationToken cancellationToken)
    {
        _touch();
        string path = await _sessions.StoreUploadAsync(fileName, input, contentLength, cancellationToken).ConfigureAwait(false);
        try
        {
            _preferences.CompleteOnboarding();
            _sessions.Open(path, uploadedCopy: true, Path.GetFileName(fileName));
        }
        catch { _sessions.DiscardUpload(path); throw; }
        _setRoute(AppRoutes.Preview);
    }

    internal void OpenRecent(string id)
    {
        string? path = _preferences.ResolveRecent(id);
        if (path is null)
        {
            throw CliErrors.FileNotFound("recent file");
        }
        OpenPath(path, uploadedCopy: false);
    }

    internal void RemoveRecent(string id) =>
        _preferences.RemoveRecent(id);

    internal void ClearRecent() =>
        _preferences.ClearRecent();

    internal AppApiResult UpdatePreferences(AppPreferenceRequest request)
    {
        ProductDefinition product = _catalog.ResolveById(
            request.Product ?? _sessions.ProductId ?? _catalog.DefaultProductId());
        PreviewErrors.EnsureViewSupported(
            product.Manifest.Id,
            request.DefaultView,
            product.View.Views.Select(static view => view.Id).ToArray());
        _preferences.Update(product.Manifest.Id, request.DefaultView, request.RememberRecentFiles);
        try
        {
            _sessions.RefreshPreferences(product.Manifest.Id, request.DefaultView);
            return new AppApiResult(true);
        }
        catch (Exception exception)
        {
            _log.Write($"preferences saved; preview refresh failed: {exception.GetType().Name}");
            return new AppApiResult(true, Code: "PREVIEW_REFRESH_FAILED",
                Message: "Preferences were saved, but the preview could not refresh. The previous preview is retained. Save the same settings to retry.");
        }
    }

    internal void ClearLocalData()
    {
        _preferences.ClearRecent();
        _sessions.ClearUploads();
        _log.Clear();
        _writeMarker();
    }

}
