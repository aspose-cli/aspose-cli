using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.TestKit;
using Aspose.Words;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

[Collection(ProcessFontSourcesCollection.Name)]
public sealed class WordsFontDirectoryTests
{
    [Fact]
    public void UseFonts_AddsTheDirectoryOnlyForTheScope()
    {
        using var fixture = new WordsFixture();
        string fonts = fixture.Temp.File("fonts");
        FontFixtures.WriteUniqueFont(fonts);
        string input = CreateDocument(fixture.Temp.File("fixture.docx"));
        WordsFontEnvironment environment = fixture.Fonts;

        Assert.False(FixtureAvailable(environment, input));
        using (environment.UseFonts(FontSearchProfile.Explicit([fonts])))
        {
            Assert.True(FixtureAvailable(environment, input));
        }
        Assert.False(FixtureAvailable(environment, input));
    }

    [Fact]
    public void CheckFonts_ResolvesAnInstalledFontByItsLocalizedName()
    {
        Requires.Windows();
        Assert.SkipUnless(
            File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "simsun.ttc")),
            "Requires the SimSun font, which Chinese documents name 宋体.");
        using var fixture = new WordsFixture();
        var builder = new DocumentBuilder();
        builder.Font.Name = "宋体";
        builder.Writeln("正文使用宋体。");
        builder.Font.Name = "NoSuchFont 2026";
        builder.Writeln("A font this machine does not have.");
        string input = fixture.Temp.File("localized.docx");
        builder.Document.Save(input);

        IReadOnlyList<Aspose.Cli.Sdk.Contracts.FontAvailability> fonts = fixture.Fonts.CheckFonts(input, new FontCheckRequest()).Fonts;

        Assert.True(fonts.Single(static font => font.Name == "宋体").Available);
        Aspose.Cli.Sdk.Contracts.FontAvailability missing = fonts.Single(static font => font.Name == "NoSuchFont 2026");
        Assert.False(missing.Available);
        Assert.NotNull(missing.SubstitutedBy);
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void Cli_FontsCheckAndReviewUseTheFontDirectory()
    {
        using var fixture = new WordsFixture();
        using var workspace = new TempWorkspace();
        CreateDocument(workspace.File("fixture.docx"));

        FontDirectoryContract.Verify(workspace, "fixture.docx");
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void Cli_ConvertAndCompareLayOutWithTheFontDirectory()
    {
        using var fixture = new WordsFixture();
        using var workspace = new TempWorkspace();
        CreateDocument(workspace.File("fixture.docx"));
        CreateDocument(workspace.File("changed.docx"));

        FontDirectoryContract.VerifyPdfOutput(
            workspace, "convert", output => ["words", "convert", "fixture.docx", "--to", "pdf", "--out", output]);
        FontDirectoryContract.VerifyPdfOutput(
            workspace, "compare", output => ["words", "compare", "fixture.docx", "changed.docx", "--out", output]);
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void Cli_RenderDrawsTheFontDirectoryFont()
    {
        using var fixture = new WordsFixture();
        using var workspace = new TempWorkspace();
        CreateDocument(workspace.File("fixture.docx"));
        FontFixtures.WriteUniqueFont(workspace.File("fonts"));

        CliResult ambient = workspace.Run("words", "render", "fixture.docx", "--out", "ambient.png");
        CliResult fonts = workspace.Run("words", "render", "fixture.docx", "--out", "fonts.png", "--font-dir", "fonts");

        Assert.True(ambient.ExitCode == 0, ambient.StdErr);
        Assert.True(fonts.ExitCode == 0, fonts.StdErr);
        Assert.NotEqual(
            File.ReadAllBytes(workspace.File("ambient.png")),
            File.ReadAllBytes(workspace.File("fonts.png")));
    }

    private static bool FixtureAvailable(IFontEnvironment environment, string input) =>
        environment.CheckFonts(input, new FontCheckRequest()).Fonts
            .Single(static font => font.Name == FontFixtures.UniqueFamily)
            .Available;

    private static string CreateDocument(string path)
    {
        var builder = new DocumentBuilder();
        builder.Font.Name = FontFixtures.UniqueFamily;
        builder.Writeln("Fixture text drawn with a font from an explicit directory.");
        builder.Document.Save(path);
        return path;
    }
}
