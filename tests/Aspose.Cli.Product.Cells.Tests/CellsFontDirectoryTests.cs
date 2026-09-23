using Aspose.Cells;
using Aspose.Cli.Sdk.Ports;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

public sealed class CellsFontDirectoryTests
{
    [Fact]
    public void UseFonts_AddsTheDirectoryOnlyForTheScope()
    {
        using var fixture = new CellsFixture();
        string fonts = fixture.Temp.File("fonts");
        FontFixtures.WriteUniqueFont(fonts);
        string input = CreateWorkbook(fixture, fixture.Temp.File("fixture.xlsx"));
        IFontEnvironment environment = fixture.Engine;

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
        using var fixture = new CellsFixture();
        using var workspace = new TempWorkspace();
        CreateWorkbook(fixture, workspace.File("fixture.xlsx"));

        FontDirectoryContract.Verify(workspace, "fixture.xlsx");
    }

    private static bool FixtureAvailable(IFontEnvironment environment, string input) =>
        environment.CheckFonts(input, new FontCheckRequest()).Fonts
            .Single(static font => font.Name == FontFixtures.UniqueFamily)
            .Available;

    private static string CreateWorkbook(CellsFixture fixture, string path)
    {
        fixture.Gate.EnsureApplied();
        using var workbook = new Workbook();
        Cell cell = workbook.Worksheets[0].Cells["A1"];
        cell.PutValue("Fixture text drawn with a font from an explicit directory.");
        Style style = cell.GetStyle();
        style.Font.Name = FontFixtures.UniqueFamily;
        cell.SetStyle(style);
        workbook.Save(path);
        return path;
    }
}
