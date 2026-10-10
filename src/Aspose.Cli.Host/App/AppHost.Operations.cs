using Aspose.Cli.Host.Catalog;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Host.App;

internal sealed partial class AppHost
{
    private readonly SemaphoreSlim _mutationGate = new(1, 1);
    private volatile MutationState _mutationState;
    private enum MutationState { Accepting, Stopping }

    internal void CompleteOnboarding() => Mutate(_workspace.CompleteOnboarding);
    internal AppApiResult PickAndOpen() => Mutate(_workspace.PickAndOpen);
    internal void OpenRecent(string id) => Mutate(() => _workspace.OpenRecent(id));
    internal void ActivateDocument(string id) => Mutate(() => _workspace.Activate(id));
    internal void CloseDocument(string id) => Mutate(() => _workspace.Close(id));
    internal void ShowDocument(string id, string view) => Mutate(() => _workspace.Show(id, view));
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

    internal void InstallLicense(Stream input, long length, string? productId) => Save(() =>
    {
        if (length > LicenseInstaller.MaximumBytes) { throw CliErrors.FileTooLarge(length, LicenseInstaller.MaximumBytes); }
        // The file the person chose is staged in the per-user temporary root
        // and is gone again before this answers.
        string directory = UserStorage.CreateTemporaryDirectory(
            "app-license",
            Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        string staged = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".lic");
        IReadOnlyList<string> installed;
        try
        {
            using (FileStream destination = new(staged, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                BoundedStreamCopy.CopyAsync(
                    input,
                    destination,
                    LicenseInstaller.MaximumBytes,
                    total => CliErrors.FileTooLarge(total, LicenseInstaller.MaximumBytes))
                    .GetAwaiter().GetResult();
            }
            installed = _cli.InstallLicense(staged, productId);
        }
        finally
        {
            LocalFileCleanup.DeleteDirectory(directory);
        }
        _preferences.CompleteOnboarding();
        _log.Write($"license installed for {string.Join(", ", installed)}");
    });

    internal void RemoveLicense(string? productId) => Save(() =>
    {
        string target = productId ?? _sessions.Snapshot?.ProductId ?? _catalog.DefaultProductId();
        _cli.RemoveLicense(target);
        _log.Write($"saved {target} license removed");
    });

    /// <summary>
    /// Saves a license change. The App holds no engine of its own, so nothing
    /// restarts: the viewer service recycles its renderer and the next render
    /// applies the license.
    /// </summary>
    /// <remarks>
    /// The license child publishes under the SDK's own storage locks, so the
    /// App adds only its mutation gate, then the short session state lock.
    /// </remarks>
    private void Save(Action saveConfiguration) =>
        Mutate(() =>
        {
            saveConfiguration();
            _sessions.Refresh();
        });

    private void Mutate(Action action) => Mutate(() => { action(); return true; });

    private T Mutate<T>(Func<T> action, CancellationToken cancellationToken = default)
    {
        EnsureMutable();
        while (!_mutationGate.Wait(25, cancellationToken)) { EnsureMutable(); }
        try { EnsureMutable(); Touch(); return action(); }
        finally { _mutationGate.Release(); }
    }

    private void EnsureMutable()
    {
        if (_disposed || _mutationState != MutationState.Accepting)
        {
            throw CliErrors.AppStopping();
        }
    }
}
