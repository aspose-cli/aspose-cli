using System.Text.Json.Nodes;
using Aspose.Cli.Host.Catalog;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Host.App;

internal sealed partial class AppHost
{
    private readonly SemaphoreSlim _mutationGate = new(1, 1);
    private volatile MutationState _mutationState;
    private enum MutationState { Accepting, Stopping }

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

    internal string InstallLicense(Stream input, long length, string? productId) => Save(() =>
    {
        if (length > LicenseInstaller.MaximumBytes) { throw CliErrors.FileTooLarge(length, LicenseInstaller.MaximumBytes); }
        IReadOnlyList<string> installed = _licenseState.Install(input, productId);
        _preferences.CompleteOnboarding();
        _log.Write($"license installed for {string.Join(", ", installed)}");
    });

    internal string RemoveLicense(string? productId) => Save(() =>
    {
        string target = productId ?? _sessions.Snapshot?.ProductId ?? _catalog.DefaultProductId();
        _licenseState.Remove(target);
        _log.Write($"saved {target} license removed");
    });

    /// <summary>
    /// Saves a license change and tells the browser where to continue. The
    /// App holds no engine of its own any more, so nothing restarts: the
    /// viewer service recycles its renderer and the next render applies the
    /// license.
    /// </summary>
    private string Save(Action saveConfiguration)
    {
        // Cross-process singleton -> App mutation gate -> short session state lock.
        using LocalServiceOperationLock singleton = AppServiceController.AcquireOperationLock();
        return Mutate(() =>
        {
            saveConfiguration();
            _sessions.Refresh();
            return $"http://127.0.0.1:{Port}{AppRoutes.Settings}";
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
            throw new CliException(ErrorCodes.AppBusy, "The App is stopping and cannot accept changes.",
                hint: "Start the App again to continue.");
        }
    }
}
