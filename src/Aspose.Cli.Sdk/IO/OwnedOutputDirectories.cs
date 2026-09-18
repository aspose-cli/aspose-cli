using System.ComponentModel;
using System.Runtime.InteropServices;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.IO;

/// <summary>Tracks explicitly declared output directories and only cleans directories this invocation created.</summary>
internal sealed class OwnedOutputDirectories(bool deferred)
{
    private static StringComparer Comparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private readonly HashSet<string> _declared = new(Comparer);
    private readonly Dictionary<string, FilePhysicalIdentity?> _created = new(Comparer);
    internal IEnumerable<string> Declared => _declared;

    internal void Ensure(string directory)
    {
        string path = Path.GetFullPath(directory);
        OutputPathValidator.EnsureSafeDirectory(path);
        var missing = new Stack<string>();
        for (string? current = path; current is not null && !Directory.Exists(current); current = Path.GetDirectoryName(current))
        { missing.Push(current); }
        string[] additions = missing.Where(item => !_declared.Contains(item)).Distinct(Comparer).ToArray();
        if (additions.Length > PublicationLimits.MaximumDirectories - _declared.Count)
        { throw CliErrors.OutputUnwritable(path, "the output directory budget was exceeded", phase: "output-set-admission"); }
        _declared.UnionWith(additions);
        if (deferred) { return; }
        while (missing.TryPop(out string? current))
        {
            OutputPathValidator.EnsureSafeDirectory(current);
            if (CreateDirectoryExclusively(current))
            { _created.Add(current, FilePublicationOwnedDelete.TryGetDirectoryIdentity(current)); }
        }
    }

    internal void CleanUp()
    {
        foreach ((string path, FilePhysicalIdentity? identity) in _created.OrderByDescending(item => item.Key.Length))
        {
            try
            {
                if (Directory.Exists(path)) { _ = FilePublicationOwnedDelete.TryDeleteDirectory(path, identity); }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { System.Diagnostics.Trace.TraceWarning("An output directory was preserved ({0}).", error.GetType().Name); }
        }
    }

    private static bool CreateDirectoryExclusively(string path)
    {
        // Ensure accepts only normalized local paths. Use extended syntax at the native boundary
        // so directory ownership does not depend on the executable's long-path manifest.
        bool created = OperatingSystem.IsWindows()
            ? CreateDirectoryWindows(@"\\?\" + path, IntPtr.Zero)
            : CreateDirectoryUnix(path, 511) == 0;
        if (created) { return true; }
        int error = Marshal.GetLastPInvokeError();
        if (error == (OperatingSystem.IsWindows() ? 183 : 17) && Directory.Exists(path)) { return false; }
        throw new IOException($"The output directory could not be created: '{path}'.", new Win32Exception(error));
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateDirectoryW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateDirectoryWindows(string path, IntPtr securityAttributes);

    [DllImport("libc", EntryPoint = "mkdir", SetLastError = true)]
    private static extern int CreateDirectoryUnix(string path, uint mode);
}
