namespace Aspose.Cli.Sdk.Rendering;

/// <summary>
/// The one process-wide gate for engine font configuration. The document
/// engines keep their font sources in process-global state, so a profile is
/// applied for the length of one scope and the previous sources are restored
/// when it ends. Scopes are exclusive: a second scope, of any product, waits
/// until the first has restored the engine, so no render ever observes the
/// fonts of another document's profile.
/// </summary>
public static class FontScope
{
    private static readonly object Gate = new();

    /// <summary>
    /// Enters the gate and, for an explicit profile, applies its directories.
    /// </summary>
    /// <param name="profile">Directories to add to the system fonts.</param>
    /// <param name="apply">
    /// Adds the directories to the engine and returns the action that restores
    /// the engine's previous font sources. Not called for the ambient profile.
    /// </param>
    public static IDisposable Enter(
        FontSearchProfile profile,
        Func<IReadOnlyList<string>, Action> apply)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(apply);
        Monitor.Enter(Gate);
        try
        {
            return new Scope(profile.IsAmbient ? null : apply(profile.Directories));
        }
        catch
        {
            Monitor.Exit(Gate);
            throw;
        }
    }

    private sealed class Scope(Action? restore) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }
            try
            {
                restore?.Invoke();
            }
            finally
            {
                Monitor.Exit(Gate);
            }
        }
    }
}
