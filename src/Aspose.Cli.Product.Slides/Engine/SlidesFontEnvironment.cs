using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Slides;
using Microsoft.Win32;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>Aspose.Slides font diagnostics behind the product-neutral <see cref="IFontEnvironment"/>.</summary>
internal sealed class SlidesFontEnvironment : IFontEnvironment
{
    /// <inheritdoc />
    public EngineFonts ListFonts() =>
        new EngineFonts
        {
            Sources = FontsLoader.GetFontFolders()
                .Select(static folder => new FontSource { Kind = "folder", Location = folder })
                .ToArray(),
        };

    /// <inheritdoc />
    /// <remarks>
    /// External fonts are the documented process-wide Slides sources, and the
    /// application loads no other, so clearing them restores the system fonts.
    /// </remarks>
    public IDisposable UseFonts(FontSearchProfile profile)
    {
        using (RegistryKey? fonts = Registry.CurrentUser.OpenSubKey(FontsKey))
        {
            EnsureReadable(fonts);
        }

        return FontScope.Enter(profile, static directories =>
        {
            FontsLoader.LoadExternalFonts(directories.ToArray());
            return FontsLoader.ClearCache;
        });
    }

    internal const string FontsKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts";

    /// <summary>
    /// Refuses, before the engine starts, a per-user Fonts key holding a value the engine cannot
    /// read (known issue SLIDES-FONT-REGISTRY in KNOWN-ISSUES.md): it reads every value as a string.
    /// </summary>
    internal static void EnsureReadable(RegistryKey? fonts)
    {
        if (fonts is null)
        {
            return;
        }

        string[] unreadable = fonts.GetValueNames()
            .Where(name => fonts.GetValueKind(name) is not (RegistryValueKind.String or RegistryValueKind.ExpandString))
            .Select(static name => name.Length == 0 ? "(Default)" : name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (unreadable.Length > 0)
        {
            string names = string.Join(", ", unreadable.Select(static name => $"'{name}'"));
            throw CliErrors.EnvironmentUnsupported(
                $@"The per-user font registry key HKCU\{FontsKey} holds {unreadable.Length} value(s) that are not strings, {names}, which the Slides engine cannot read.",
                $@"Remove {names} from HKCU\{FontsKey}; Windows writes only string values there.");
        }
    }
}
