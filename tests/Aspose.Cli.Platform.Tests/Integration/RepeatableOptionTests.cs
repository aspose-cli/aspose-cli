using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// An option that takes several values, <c>--detail</c>, <c>--from-images</c> and <c>--groups</c>,
/// takes one value per occurrence and is repeated for more, so the word after its value is the
/// next argument wherever the option stands.
/// </summary>
public sealed class RepeatableOptionTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public RepeatableOptionTests()
    {
        foreach (string name in new[] { "1.png", "2.png", "3.png", "4.png" })
        {
            File.WriteAllBytes(_workspace.File(name), ResourceHttpServer.Image);
        }
    }

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void Detail_BeforeTheFile_ReportsEveryRepeatedSection()
    {
        Require(_workspace.Run("cells", "create", "book.xlsx", "--sheet-names", "Data"));
        File.WriteAllText(
            _workspace.File("ops.json"),
            """
            { "ops": [
              { "op": "set_values", "sheet": "Data", "range": "A1:B3", "values": [["Region","Sales"],["East",10],["West",20]] },
              { "op": "define_name", "name": "SalesTotal", "refersTo": "=Data!$B$2:$B$3" },
              { "op": "insert_chart", "sheet": "Data", "kind": "column", "dataRange": "A1:B3", "at": "E2:J12", "title": "Sales" }
            ] }
            """);
        Require(_workspace.Run("cells", "edit", "book.xlsx", "--ops", "ops.json", "--in-place"));

        CliResult inspected = Require(_workspace.Run(
            "cells", "inspect", "--detail", "charts", "--detail", "names", "book.xlsx", "--output-format", "json"));

        JsonNode workbook = JsonNode.Parse(inspected.StdOut)!["workbook"]!;
        Assert.Single(workbook["charts"]!.AsArray());
        Assert.Equal("SalesTotal", Assert.Single(workbook["definedNames"]!.AsArray())!["name"]!.GetValue<string>());
    }

    [Fact]
    public void FromImages_Repeated_BeforeTheOutput_MakesOnePagePerImage()
    {
        Require(_workspace.Run("pdf", "create", "--from-images", "1.png", "--from-images", "2.png", "out.pdf"));

        Assert.Equal(2, PageCount("out.pdf"));
    }

    // Four pages, which evaluation mode still reads whole.
    [Fact]
    public void Groups_Repeated_BeforeTheFile_SplitsOnePartPerGroup()
    {
        Require(_workspace.Run(
            "pdf", "create", "four.pdf", "--from-images", "1.png", "--from-images", "2.png", "--from-images", "3.png", "--from-images", "4.png"));

        CliResult split = Require(_workspace.Run(
            "pdf", "split", "--by", "pages", "--groups", "1-2", "--groups", "3-4", "four.pdf", "--out-dir", "parts", "--output-format", "json"));

        JsonArray outputs = JsonNode.Parse(split.StdOut)!["outputs"]!.AsArray();
        Assert.Equal(["1-2", "3-4"], outputs.Select(static output => output!["pages"]!.GetValue<string>()));
        Assert.Equal(2, PageCount(Path.Combine("parts", "four.1.pdf")));
        Assert.Equal(2, PageCount(Path.Combine("parts", "four.2.pdf")));
    }

    private int PageCount(string file)
    {
        CliResult inspected = Require(_workspace.Run("pdf", "inspect", file, "--output-format", "json"));
        return JsonNode.Parse(inspected.StdOut)!["pdf"]!["pageCount"]!.GetValue<int>();
    }

    private static CliResult Require(CliResult result)
    {
        Assert.True(result.ExitCode == 0, result.StdErr + result.StdOut);
        return result;
    }
}
