using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Xml.Linq;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Product.Slides.Engine;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.TestKit;
using Aspose.Slides;
using Aspose.Slides.Charts;
using Aspose.Slides.Export;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// The Aspose.Slides defects in KNOWN-ISSUES.md: each reproduced with the SDK alone, passing
/// while the pinned SDK still has it, and the CLI's handling of it. The class runs alone because
/// one reproduction redirects standard output.
/// </summary>
[Collection(ProcessRedirectionCollection.Name)]
public sealed class SlidesKnownIssueTests
{
    private const string FontRegistryHiveVariable = "ASPOSE_CLI_TEST_FONT_REGISTRY_HIVE";

    private static string ImplicitTitleDeck =>
        Path.Combine(RepositoryPaths.Root, "tests", "Aspose.Cli.Product.Slides.Tests", "Assets", "implicit-chart-title.pptx");

    [LicensedFact]
    public void TextThatShrinksOnOverflow_IsLaidOutPastTheRightOfItsFrame()
    {
        using var fixture = new SlidesEngineFixture();
        using var presentation = new Presentation();
        IAutoShape body = presentation.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 40, 100, 640, 257);
        body.TextFrame.TextFrameFormat.AutofitType = TextAutofitType.Normal;
        body.TextFrame.Paragraphs.Clear();
        for (int item = 0; item < 12; item++)
        {
            var paragraph = new Paragraph();
            paragraph.ParagraphFormat.MarginLeft = 34;
            paragraph.ParagraphFormat.Indent = -27;
            paragraph.ParagraphFormat.Bullet.Type = BulletType.Symbol;
            paragraph.ParagraphFormat.Bullet.Char = '•';
            paragraph.Portions.Add(new Portion("培训 Training 10：每季度一次线下集训，线上课程全年开放 on-demand courses for every partner"));
            paragraph.Portions[0].PortionFormat.FontHeight = 18;
            body.TextFrame.Paragraphs.Add(paragraph);
        }

        double right = body.TextFrame.Paragraphs.Max(static paragraph => paragraph.GetRect().Right);
        double textRight = body.Width - body.TextFrame.TextFrameFormat.GetEffective().MarginRight;

        KnownIssue.Reproduces(
            "SLIDES-AUTOFIT-RECT",
            right > textRight,
            $"the widest laid-out line ends at {right} pt, inside the frame's text area ending at {textRight} pt");
    }

    [LicensedFact]
    public void ImplicitAutomaticTitle_LoadsAsATitleOverThePlot()
    {
        using var fixture = new SlidesEngineFixture();
        using (ZipArchive package = ZipFile.OpenRead(ImplicitTitleDeck))
        using (Stream part = package.GetEntry("ppt/charts/chart1.xml")!.Open())
        {
            XNamespace c = "http://schemas.openxmlformats.org/drawingml/2006/chart";
            XElement chart = XDocument.Load(part).Root!.Element(c + "chart")!;
            Assert.Null(chart.Element(c + "title"));
            Assert.Equal("0", chart.Element(c + "autoTitleDeleted")!.Attribute("val")!.Value);
        }

        using var presentation = new Presentation(ImplicitTitleDeck);
        IChart loaded = presentation.Slides.SelectMany(static slide => slide.Shapes).OfType<IChart>().Single();

        KnownIssue.Reproduces(
            "SLIDES-CHART-TITLE",
            loaded.HasTitle && loaded.ChartTitle.Overlay,
            $"an implicit automatic title loads as HasTitle={loaded.HasTitle}, Overlay={loaded.HasTitle && loaded.ChartTitle.Overlay}");
    }

    [LicensedFact]
    public void ChineseRun_AlternatesBetweenChineseAndJapaneseFallbackFonts()
    {
        Requires.Windows();
        string fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        Assert.SkipUnless(
            File.Exists(Path.Combine(fonts, "simsun.ttc")) && File.Exists(Path.Combine(fonts, "msgothic.ttc")),
            "The defect shows only while both SimSun and MS Gothic are installed.");
        using var fixture = new SlidesEngineFixture();
        using var presentation = new Presentation();
        presentation.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 40, 40, 600, 80).TextFrame.Text = "问题与对策应收账款余额";
        using var svg = new MemoryStream();
        presentation.Slides[0].WriteAsSvg(svg);
        svg.Position = 0;

        string[] families = XDocument.Load(svg).Descendants()
            .Where(static element => element.Name.LocalName is "text" or "tspan"
                && element.Value.Any(static character => character is >= '一' and <= '鿿'))
            .Select(static element => (string?)element.Attribute("font-family"))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.NotEmpty(families);

        KnownIssue.Reproduces(
            "SLIDES-CJK-FALLBACK",
            families.Length > 1,
            $"one Chinese run is drawn in {string.Join(", ", families)}");
    }

    [LicensedFact]
    public void RenderingWithAFallbackRule_WritesToStandardOutput()
    {
        using var fixture = new SlidesEngineFixture();
        using var captured = new StringWriter();
        TextWriter original = Console.Out;
        using var presentation = new Presentation();
        try
        {
            Console.SetOut(captured);
            presentation.FontsManager.FontFallBackRulesCollection.Add(new FontFallBackRule(0x4E00, 0x9FFF, "SimSun"));
            presentation.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 40, 40, 600, 80).TextFrame.Text = "回退规则";
            presentation.Save(fixture.File("fallback.pdf"), SaveFormat.Pdf);
        }
        finally
        {
            Console.SetOut(original);
        }

        KnownIssue.Reproduces(
            "SLIDES-FALLBACK-STDOUT",
            captured.ToString().Length > 0,
            "rendering with a fallback rule wrote nothing to standard output");
    }

    /// <summary>
    /// Slides reads the per-user Fonts key once per process, so the reproduction reruns this test
    /// in a new test process whose current-user registry is an application hive file. The user's
    /// registry is never written.
    /// </summary>
    [LicensedFact]
    [SupportedOSPlatform("windows")]
    public void FontInitialization_BreaksOnARegistryValueThatIsNotAString()
    {
        Requires.Windows();
        string? hive = Environment.GetEnvironmentVariable(FontRegistryHiveVariable);
        if (hive is null)
        {
            using var directory = new TempDirectory();
            (int exitCode, string output) = RunThisTestAlone(directory.File("current-user.hive"));
            Assert.True(
                exitCode == 0 && output.Contains("Total: 1, Errors: 0, Failed: 0, Skipped: 0,", StringComparison.Ordinal),
                output);
            return;
        }

        using (RegistryKey fonts = PrivateFontsKey(hive))
        {
            fonts.SetValue("Readable (TrueType)", "arial.ttf", RegistryValueKind.String);
            fonts.SetValue("Other software", 1, RegistryValueKind.DWord);
        }
        int status = RegLoadAppKey(hive, out IntPtr redirected, 0xF003F, 0, 0);
        Assert.True(status == 0, $"RegLoadAppKey failed with {status}.");
        Exception? failure;
        try
        {
            status = RegOverridePredefKey(CurrentUser, redirected);
            Assert.True(status == 0, $"RegOverridePredefKey failed with {status}.");
            using var fixture = new SlidesEngineFixture();
            using var presentation = new Presentation();
            presentation.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 10, 10, 300, 50).TextFrame.Text = "Font registry";
            failure = Record.Exception(() => presentation.Save(fixture.File("registry.pptx"), SaveFormat.Pptx));
        }
        finally
        {
            _ = RegOverridePredefKey(CurrentUser, IntPtr.Zero);
            _ = RegCloseKey(redirected);
        }
        while (failure?.InnerException is { } inner)
        {
            failure = inner;
        }

        KnownIssue.Reproduces(
            "SLIDES-FONT-REGISTRY",
            failure is InvalidCastException,
            failure is null ? "the save succeeded with a REG_DWORD font value" : failure.ToString());
    }

    [Fact]
    public void AnOutputOfAChartWithAnImplicitTitle_WarnsWithTheChartsSlide()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("implicit-title.pptx");
        File.Copy(ImplicitTitleDeck, input);
        string plain = fixture.CreatePresentation("plain.pptx", slides: 1);

        SlidesConvertResult affected = fixture.Engine.Convert(input, new PresentationConvertRequest
        {
            Output = TestOutput.At(fixture.File("implicit-title.pdf"), format: "pdf"),
        });
        SlidesConvertResult unaffected = fixture.Engine.Convert(plain, new PresentationConvertRequest
        {
            Output = TestOutput.At(fixture.File("plain.pdf"), format: "pdf"),
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

    /// <summary>Runs <see cref="FontInitialization_BreaksOnARegistryValueThatIsNotAString"/> in a new test process.</summary>
    private static (int ExitCode, string Output) RunThisTestAlone(string hive)
    {
        var start = new ProcessStartInfo(Path.ChangeExtension(typeof(SlidesKnownIssueTests).Assembly.Location, ".exe"))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("-method");
        start.ArgumentList.Add($"{typeof(SlidesKnownIssueTests).FullName}.{nameof(FontInitialization_BreaksOnARegistryValueThatIsNotAString)}");
        start.Environment[FontRegistryHiveVariable] = hive;
        using Process process = Process.Start(start)!;
        Task<string> error = process.StandardError.ReadToEndAsync();
        string output = process.StandardOutput.ReadToEnd();
        Assert.True(process.WaitForExit(TimeSpan.FromMinutes(2)), "The font registry test process did not exit.");
        return (process.ExitCode, output + error.GetAwaiter().GetResult());
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

    private static readonly IntPtr CurrentUser = unchecked((int)0x80000001);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegLoadAppKey(string file, out IntPtr key, int access, int options, int reserved);

    [DllImport("advapi32.dll")]
    private static extern int RegOverridePredefKey(IntPtr predefined, IntPtr replacement);

    [DllImport("advapi32.dll")]
    private static extern int RegCloseKey(IntPtr key);
}
