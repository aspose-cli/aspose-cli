using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>Initializes the Slides font cache without changing the host registry.</summary>
internal static class SlidesFontCatalog
{
    private const string WindowsFontsKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts";
    private static readonly nint HkeyCurrentUser = unchecked((nint)(int)0x80000001u);
    private static readonly nint HkeyLocalMachine = unchecked((nint)(int)0x80000002u);
    private const int KeyRead = 0x20019;
    private static readonly object Sync = new();
    private static bool _initialized;

    public static void EnsureInitialized()
    {
        if (!OperatingSystem.IsWindows() || Volatile.Read(ref _initialized))
        {
            return;
        }

        lock (Sync)
        {
            if (_initialized)
            {
                return;
            }

            if (!HasNonStringUserFontValues())
            {
                Volatile.Write(ref _initialized, true);
                return;
            }

            int status = RegOpenKeyEx(
                HkeyLocalMachine,
                "SOFTWARE",
                0,
                KeyRead,
                out nint machineSoftware);
            if (status != 0)
            {
                throw new Win32Exception(status);
            }

            try
            {
                RunWithTemporaryUserRegistryOverride(
                    machineSoftware,
                    replacement => RegOverridePredefKey(
                        HkeyCurrentUser,
                        replacement),
                    static () =>
                    {
                        // Aspose.Slides caches the resolved font folders. The process-local
                        // remap hides malformed per-user metadata only during this SDK call.
                        _ = Aspose.Slides.FontsLoader.GetFontFolders();
                        using var probe = new Aspose.Slides.Presentation();
                        probe.Slides[0].Shapes.AddAutoShape(
                            Aspose.Slides.ShapeType.Rectangle,
                            0,
                            0,
                            10,
                            10).TextFrame.Text = "A";
                        using Aspose.Slides.IImage image = probe.Slides[0].GetImage(0.1f, 0.1f);
                    });
            }
            finally
            {
                _ = RegCloseKey(machineSoftware);
            }

            Volatile.Write(ref _initialized, true);
        }
    }

    internal static void RunWithTemporaryUserRegistryOverride(
        nint replacement,
        Func<nint, int> applyOverride,
        Action initialize)
    {
        ArgumentNullException.ThrowIfNull(applyOverride);
        ArgumentNullException.ThrowIfNull(initialize);

        int status = applyOverride(replacement);
        if (status != 0)
        {
            throw new Win32Exception(status);
        }

        try
        {
            initialize();
        }
        finally
        {
            int restoreStatus = applyOverride(nint.Zero);
            if (restoreStatus != 0)
            {
                throw new Win32Exception(restoreStatus);
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool HasNonStringUserFontValues()
    {
        using RegistryKey? fonts = Registry.CurrentUser.OpenSubKey(WindowsFontsKey);
        return fonts is not null && fonts.GetValueNames().Any(name =>
        {
            RegistryValueKind kind = fonts.GetValueKind(name);
            return kind is not (RegistryValueKind.String or RegistryValueKind.ExpandString);
        });
    }

    [DllImport("advapi32.dll")]
    private static extern int RegOverridePredefKey(nint hKey, nint hNewHKey);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegOpenKeyExW")]
    private static extern int RegOpenKeyEx(
        nint hKey,
        string subKey,
        int options,
        int desiredAccess,
        out nint result);

    [DllImport("advapi32.dll")]
    private static extern int RegCloseKey(nint hKey);
}
