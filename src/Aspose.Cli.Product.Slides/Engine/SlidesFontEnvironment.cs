using System.Runtime.Versioning;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Ports;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Results;
using Aspose.Slides;
using Microsoft.Win32;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>Aspose.Slides font diagnostics behind the product-neutral font port.</summary>
internal sealed class SlidesFontEnvironment : IFontEnvironment
{
    private readonly ILicenseGate _licenseGate;
    private readonly SlidesPresentationLoader _loader;

    public SlidesFontEnvironment(
        ILicenseGate licenseGate,
        ResourceBudgetLedger resourceBudgets)
    {
        _licenseGate = licenseGate ?? throw new ArgumentNullException(nameof(licenseGate));
        _loader = new SlidesPresentationLoader(resourceBudgets);
    }

    /// <inheritdoc />
    public FontListResult ListFonts() =>
        new FontListResult
        {
            Sources = FontsLoader.GetFontFolders()
                .Select(static folder => new FontSource { Type = "folder", Location = folder })
                .ToArray(),
        };

    /// <inheritdoc />
    /// <remarks>
    /// External fonts are the documented process-wide Slides sources, and the
    /// application loads no other, so clearing them restores the system fonts.
    /// </remarks>
    public IDisposable UseFonts(FontSearchProfile profile)
    {
        if (OperatingSystem.IsWindows())
        {
            using RegistryKey? fonts = Registry.CurrentUser.OpenSubKey(FontsKey);
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
    [SupportedOSPlatform("windows")]
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
            throw new CliException(
                ErrorCodes.FeatureUnsupported,
                $@"The per-user font registry key HKCU\{FontsKey} holds {unreadable.Length} value(s) that are not strings, {names}, which the Slides engine cannot read.",
                $@"Remove {names} from HKCU\{FontsKey}; Windows writes only string values there.");
        }
    }

    /// <inheritdoc />
    // Loading and laying out run with standard output muted (known issue SLIDES-FALLBACK-STDOUT).
    public FontCheckResult CheckFonts(string filePath, FontCheckRequest request) =>
        SlidesStandardOutput.Muted(() => CheckFontsCore(filePath, request));

    private FontCheckResult CheckFontsCore(string filePath, FontCheckRequest request)
    {
        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedPresentation loaded = _loader.Open(filePath, request.Password);
        Dictionary<string, string> substitutions = loaded.Presentation.FontsManager.GetSubstitutions()
            .ToDictionary(static item => item.OriginalFontName, static item => item.SubstitutedFontName, StringComparer.Ordinal);
        FontAvailability[] fonts = loaded.Presentation.FontsManager.GetFonts()
            .Select(static font => font.FontName)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(name => new FontAvailability
            {
                Name = name,
                Available = !substitutions.ContainsKey(name),
                SubstitutedBy = substitutions.GetValueOrDefault(name),
            })
            .ToArray();
        return new FontCheckResult
        {
            Source = new SourceInfo
            {
                Path = filePath,
                Format = loaded.FormatId,
                SizeBytes = new FileInfo(filePath).Length,
            },
            AllAvailable = fonts.All(static font => font.Available),
            Fonts = fonts,
            License = EnvelopeParts.License(state),
        };
    }
}
