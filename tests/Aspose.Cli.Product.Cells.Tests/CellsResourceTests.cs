using System.Text.Json.Nodes;
using Aspose.Cells;
using Aspose.Cells.Drawing;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// The files a workbook names: a linked picture the engine draws is supplied only from beneath
/// the input's folder, and nothing is fetched over the network.
/// </summary>
public sealed class CellsResourceTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    [Fact]
    public void LinkedPicture_BesideTheInput_IsSupplied()
    {
        File.WriteAllBytes(_workspace.File("logo.png"), ResourceHttpServer.Image);
        WriteLinked("linked.xlsx", _workspace.File("logo.png"));

        CliResult converted = _workspace.Run("cells", "convert", "linked.xlsx", "--to", "pdf");

        Assert.True(converted.ExitCode == 0, converted.StdErr);
        Assert.DoesNotContain("REMOTE_RESOURCES_BLOCKED", converted.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public void LinkedPicture_OutsideTheInputsFolder_IsLeftOutAndCounted()
    {
        using var outside = new TempDirectory();
        string picture = outside.File("logo.png");
        File.WriteAllBytes(picture, ResourceHttpServer.Image);
        WriteLinked("linked.xlsx", picture);

        CliResult converted = _workspace.Run("cells", "convert", "linked.xlsx", "--to", "pdf");

        Assert.True(converted.ExitCode == 0, converted.StdErr);
        Assert.Contains(JsonNode.Parse(converted.StdOut)!["warnings"]!.AsArray(),
            warning => warning!["code"]!.GetValue<string>() == "REMOTE_RESOURCES_BLOCKED");
    }

    [Fact]
    public async Task LinkedPicture_OverHttp_IsNotFetched()
    {
        await using var server = new ResourceHttpServer();
        WriteLinked("linked.xlsx", server.Url + "/logo.png");

        CliResult rendered = _workspace.Run("cells", "render", "linked.xlsx", "--sheet", "Data", "--out", "linked.png");

        Assert.True(rendered.ExitCode == 0, rendered.StdErr);
        Assert.Equal(0, server.RequestCount);
    }

    [Theory]
    [InlineData("ods", "FORMAT_UNSUPPORTED")]
    [InlineData("html", "FILE_CORRUPT")]
    [InlineData("mhtml", "FILE_CORRUPT")]
    public void Read_OfAFormatCellsOnlyWrites_IsRefused(string format, string code)
    {
        CliResult written = _workspace.Run("cells", "create", "book.xlsx");
        Assert.True(written.ExitCode == 0, written.StdErr);
        CliResult converted = _workspace.Run("cells", "convert", "book.xlsx", "--to", format, "--out", "page." + format);
        Assert.True(converted.ExitCode == 0, converted.StdErr);

        CliResult refused = _workspace.Run("cells", "read", "page." + format);

        Assert.NotEqual(0, refused.ExitCode);
        Assert.Equal(code, JsonNode.Parse(refused.StdErr)!["error"]!["code"]!.GetValue<string>());
    }

    private void WriteLinked(string name, string source)
    {
        using var workbook = new Workbook();
        Worksheet sheet = workbook.Worksheets[0];
        sheet.Name = "Data";
        sheet.Cells["A1"].PutValue("Linked picture");
        using var image = new MemoryStream(ResourceHttpServer.Image, writable: false);
        Picture picture = sheet.Pictures[sheet.Pictures.Add(2, 1, image)];
        picture.SourceFullName = source;
        picture.IsLink = true;
        workbook.Save(_workspace.File(name), SaveFormat.Xlsx);
    }

    public void Dispose() => _workspace.Dispose();
}
