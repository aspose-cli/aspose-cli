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
    }

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void Kind_BeforeTheFile_ListsEveryRepeatedKind()
    {
        Require(_workspace.Run("cells", "create", "book.xlsx"));
        File.WriteAllText(
            _workspace.File("ops.json"),
            """
            { "ops": [
              { "set": "sheet/Sheet1/range/A1:B3", "props": { "value": [["Region","Sales"],["East",10],["West",20]] } },
              { "add": "name", "args": { "text": "SalesTotal" }, "props": { "refersTo": "=Sheet1!$B$2:$B$3" } },
              { "add": "chart", "in": "sheet/Sheet1", "args": { "type": "Column", "area": "E2:J12" }, "props": { "title.text": "Sales" } }
            ] }
            """);
        Require(_workspace.Run("cells", "edit", "book.xlsx", "--ops", "ops.json", "--in-place"));

        CliResult read = Require(_workspace.Run(
            "cells", "read", "--kind", "chart", "--kind", "name", "book.xlsx", "--output-format", "json"));

        JsonArray objects = JsonNode.Parse(read.StdOut)!["objects"]!.AsArray();
        Assert.Single(objects, static item => item!["kind"]!.GetValue<string>() == "chart");
        Assert.Equal("SalesTotal", Assert.Single(objects, static item => item!["kind"]!.GetValue<string>() == "name")!["props"]!["fullText"]!.GetValue<string>());
    }

    // Four pages, which evaluation mode still reads whole.
    [Fact]
    public void Groups_Repeated_BeforeTheFile_SplitsOnePartPerGroup()
    {
        File.WriteAllText(_workspace.File("page.html"), "<html><body><p>One page</p></body></html>");
        Require(_workspace.Run("pdf", "convert", "page.html", "--out", "page.pdf"));
        Require(_workspace.Run("pdf", "merge", "page.pdf", "page.pdf", "page.pdf", "page.pdf", "--out", "four.pdf"));

        CliResult split = Require(_workspace.Run(
            "pdf", "split", "--by", "pages", "--groups", "1-2", "--groups", "3-4", "four.pdf", "--out-dir", "parts", "--output-format", "json"));

        JsonArray outputs = JsonNode.Parse(split.StdOut)!["outputs"]!.AsArray();
        Assert.Equal(["1-2", "3-4"], outputs.Select(static output => output!["pages"]!.GetValue<string>()));
        Assert.Equal(2, PageCount(Path.Combine("parts", "four.1.pdf")));
        Assert.Equal(2, PageCount(Path.Combine("parts", "four.2.pdf")));
    }

    private int PageCount(string file)
    {
        CliResult read = Require(_workspace.Run("pdf", "read", file, "--output-format", "json"));
        return JsonNode.Parse(read.StdOut)!["inventory"]!["read"]!["pages"]!.GetValue<int>();
    }

    private static CliResult Require(CliResult result)
    {
        Assert.True(result.ExitCode == 0, result.StdErr + result.StdOut);
        return result;
    }
}
