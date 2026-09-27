using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Product.Slides.Engine;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.TestKit;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>The CLI's handling of the Aspose.Slides known issues in KNOWN-ISSUES.md.</summary>
public sealed class SlidesKnownIssueTests
{
    [Fact]
    public void AnOutputOfAChartWithAnImplicitTitle_WarnsWithTheChartsSlide()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("implicit-title.pptx");
        File.Copy(Path.Combine(RepositoryPaths.Root, "tests", "acceptance", "slides-sdk-fidelity", "input.pptx"), input);
        string plain = fixture.CreatePresentation("plain.pptx", slides: 1);

        SlidesConvertResult affected = fixture.Engine.Convert(input, new PresentationConvertRequest
        {
            TargetFormatId = "pdf",
            OutputPath = fixture.File("implicit-title.pdf"),
        });
        SlidesConvertResult unaffected = fixture.Engine.Convert(plain, new PresentationConvertRequest
        {
            TargetFormatId = "pdf",
            OutputPath = fixture.File("plain.pdf"),
        });

        Warning warning = Assert.Single(affected.Warnings!, static item => item.Code == SlidesDiagnostics.ChartTitleOverlaid);
        Assert.Equal("slide 2", warning.Location);
        Assert.Contains("implicit automatic title", warning.Message, StringComparison.Ordinal);
        Assert.True(warning.AffectsCompleteness);
        Assert.DoesNotContain(unaffected.Warnings ?? [], static item => item.Code == SlidesDiagnostics.ChartTitleOverlaid);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void AFontRegistryValueThatIsNotAString_IsRefusedWithItsName()
    {
        using var directory = new TempDirectory();
        using RegistryKey fonts = PrivateFontsKey(directory.File("fonts.hive"));
        fonts.SetValue("Readable (TrueType)", "readable.ttf", RegistryValueKind.String);
        fonts.SetValue("Expanded (TrueType)", @"%WINDIR%\Fonts\arial.ttf", RegistryValueKind.ExpandString);
        SlidesFontEnvironment.EnsureReadable(fonts);
        SlidesFontEnvironment.EnsureReadable(null);

        fonts.SetValue("Other software", 1, RegistryValueKind.DWord);
        CliException refused = Assert.Throws<CliException>(() => SlidesFontEnvironment.EnsureReadable(fonts));

        Assert.Equal(ErrorCodes.FeatureUnsupported, refused.Code);
        Assert.Contains("'Other software'", refused.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Readable", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>A Fonts key in a new application hive file, which never touches the user's registry.</summary>
    [SupportedOSPlatform("windows")]
    private static RegistryKey PrivateFontsKey(string hiveFile)
    {
        int status = RegLoadAppKey(hiveFile, out IntPtr hive, 0xF003F, 0, 0);
        Assert.True(status == 0, $"RegLoadAppKey failed with {status}.");
        using RegistryKey root = RegistryKey.FromHandle(new SafeRegistryHandle(hive, ownsHandle: true));
        return root.CreateSubKey(SlidesFontEnvironment.FontsKey);
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegLoadAppKey(string file, out IntPtr key, int access, int options, int reserved);
}
