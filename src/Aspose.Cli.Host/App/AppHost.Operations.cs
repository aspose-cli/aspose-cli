using System.Text.Json.Nodes;
using Aspose.Cli.Host.Catalog;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Host.App;

internal sealed partial class AppHost
{
    internal const string RestartFailedMessage =
        "The license configuration was saved, but the App could not restart. The current App is still running.";
    private readonly SemaphoreSlim _mutationGate = new(1, 1);
    private volatile MutationState _mutationState;
    private enum MutationState { Accepting, Restarting, Stopping }

    internal void CompleteOnboarding() => Mutate(_workspace.CompleteOnboarding);
    internal AppApiResult PickAndOpen() => Mutate(_workspace.PickAndOpen);
    internal void OpenPath(string path) => Mutate(() => _workspace.OpenPath(path, uploadedCopy: false));
    internal void OpenRecent(string id) => Mutate(() => _workspace.OpenRecent(id));
    internal void RemoveRecent(string id) => Mutate(() => _workspace.RemoveRecent(id));
    internal void ClearRecent() => Mutate(_workspace.ClearRecent);
    internal Task ClearLocalDataAsync(CancellationToken token) => MutateAsync(() =>
    { _workspace.ClearLocalData(); return Task.CompletedTask; }, token);
    internal AppApiResult UpdatePreferences(AppPreferenceRequest request) => Mutate(() => _workspace.UpdatePreferences(request));

    internal Task UploadFileAsync(string name, Stream input, long length, CancellationToken token) =>
        MutateAsync(() => _workspace.UploadFileAsync(name, input, length, token), token);

    private async Task MutateAsync(Func<Task> action, CancellationToken token)
    {
        EnsureMutable();
        await _mutationGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            EnsureMutable();
            Touch();
            await action().ConfigureAwait(false);
        }
        finally { _mutationGate.Release(); }
    }

    internal void PrepareStop() => Mutate(() => _mutationState = MutationState.Stopping);

    internal string InstallLicenseAndRestart(Stream input, long length, string? productId) => Restart(() =>
    {
        if (length > LicenseInstaller.MaximumBytes) { throw CliErrors.FileTooLarge(length, LicenseInstaller.MaximumBytes); }
        IReadOnlyList<string> installed = _licenseState.Install(input, productId);
        _preferences.CompleteOnboarding();
        _log.Write($"license installed for {string.Join(", ", installed)}");
    });

    internal string RemoveLicenseAndRestart(string? productId) => Restart(() =>
    {
        string target = productId ?? _sessions.Snapshot?.ProductId ?? _catalog.DefaultProductId();
        _licenseState.Remove(target);
        _log.Write($"saved {target} license removed");
    });

    private string Restart(Action saveConfiguration)
    {
        // Cross-process singleton -> App mutation gate -> short session state lock.
        using LocalServiceOperationLock singleton = AppServiceController.AcquireOperationLock();
        return Mutate(() =>
        {
            _mutationState = MutationState.Restarting;
            try
            {
                using AppDocumentHandoff document = _sessions.CaptureForRestart();
                saveConfiguration();
                ReleaseControl();
                AppInstance replacement;
                try
                {
                    replacement = new AppServiceController(_catalog, _capabilities).StartReplacementUnderLock(
                        _licenseState.Globals, AppRoutes.Settings, document.OriginalFilePath, _fontProfile,
                        document.UploadedFilePath, document.FileName);
                }
                catch (Exception error)
                {
                    StartControl();
                    WriteMarker();
                    _log.Write($"license saved, restart failed: {error.GetType().Name}");
                    throw new CliException(ErrorCodes.AppStartupFailed, RestartFailedMessage,
                        hint: "Repair or reopen the current document, then restart the App to use the saved license.",
                        details: new JsonObject { ["licenseSaved"] = true }, innerException: error);
                }
                _log.Write("license configuration changed; replacement App is ready");
                return $"http://127.0.0.1:{replacement.Port}/settings";
            }
            catch { _mutationState = MutationState.Accepting; throw; }
        });
    }

    private void Mutate(Action action) => Mutate(() => { action(); return true; });

    private T Mutate<T>(Func<T> action)
    {
        EnsureMutable();
        while (!_mutationGate.Wait(25)) { EnsureMutable(); }
        try { EnsureMutable(); Touch(); return action(); }
        finally { _mutationGate.Release(); }
    }

    private void EnsureMutable()
    {
        if (_disposed || _mutationState != MutationState.Accepting)
        {
            throw new CliException(ErrorCodes.AppBusy, "The App is restarting or stopping and cannot accept changes.",
                hint: "Continue at the replacement App address, or start the App again.");
        }
    }
}
