using Aspose.Cli.Sdk.Ports;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.TestKit;
using Aspose.Words;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

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
    public void Cli_FontsCheckAndReviewUseTheFontDirectory()
    {
        using var fixture = new WordsFixture();
        using var workspace = new TempWorkspace();
        fixture.Gate.EnsureApplied();
        CreateDocument(workspace.File("fixture.docx"));

        FontDirectoryContract.Verify(workspace, "fixture.docx");
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
