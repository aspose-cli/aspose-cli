using System.Text.Json.Nodes;
using Aspose.Cells;
using Aspose.Cells.Charts;
using Aspose.Cells.Drawing;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// Every part of a workbook that holds text a reader sees, each with a marker of its own:
/// <c>search --content all</c> finds each marker and names its part; <c>replace_text</c> on a sheet
/// replaces the markers of its text cells and names each other part in a <c>TEXT_NOT_REPLACED</c>;
/// a search of that sheet after it finds the markers exactly in the parts named; and
/// <c>--verify</c> passes. A title or text box linked to a cell is the cell's when the command
/// reaches the cell's sheet, and its own, naming the cell, when it does not. Each marker is five
/// characters, which evaluation mode reads whole, so the check runs without a license too.
/// </summary>
public sealed class CellsTextSurfaceTests : IDisposable
{
    private const string Marker = "Zq";

    /// <summary>
    /// The text cells of sheet Data, which replace_text writes, by the rest of their marker: Sbx and
    /// Sti are also shown by a text box and a chart title of Data linked to them without a sheet name.
    /// </summary>
    private static readonly string[] Written = ["Cel", "Dti", "Dbx", "Sbx", "Sti"];

    /// <summary>The other parts of sheet Data, by the rest of their marker, with the part they are.</summary>
    private static readonly Dictionary<string, string> Named = new(StringComparer.Ordinal)
    {
        ["For"] = "formula",
        ["Ty1"] = "typed-value",
        ["Ty2"] = "typed-value",
        ["Com"] = "comment",
        ["Thr"] = "comment",
        ["Hea"] = "header-footer",
        ["Foo"] = "header-footer",
        ["Cht"] = "chart-title",
        ["Cat"] = "axis-title",
        ["Val"] = "axis-title",
        ["Sec"] = "axis-title",
        ["Ser"] = "chart-data",
        ["Lab"] = "data-label",
        ["Cal"] = "alt-text",
        ["Alt"] = "alt-text",
        ["Box"] = "text-box",
        ["Grp"] = "text-box",
    };

    /// <summary>The parts of sheet Data linked to a cell of sheet Other, by the rest of their marker, with the part they are and the cell.</summary>
    private static readonly Dictionary<string, (string Part, string Cell)> Linked = new(StringComparer.Ordinal)
    {
        ["Lti"] = ("chart-title", "Other!A1"),
        ["Lbx"] = ("text-box", "Other!A2"),
    };

    private readonly TempWorkspace _workspace = new();

    public CellsTextSurfaceTests() => ScenarioLicense.Project(_workspace.Path);

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void EveryPart_IsFound_AndEitherReplacedOrNamed()
    {
        using var fixture = new CellsFixture();
        _ = fixture.LicenseState;
        Workbook(_workspace.File("parts.xlsx"));

        JsonNode[] every = Hits("parts.xlsx", sheet: null);
        Assert.All(Written, part => Assert.Null(Single(every, part)["part"]));
        Assert.All(Named, part => Assert.Equal(part.Value, Single(every, part.Key)["part"]?.GetValue<string>()));
        // Searching every sheet, linked text is found once, in the cell it shows.
        Assert.All(Linked, part => Assert.Equal(part.Value.Cell, $"{Single(every, part.Key)["sheet"]}!{Single(every, part.Key)["cell"]}"));
        JsonNode[] data = Hits("parts.xlsx", "Data");
        Assert.All(Linked, part => Assert.Equal((part.Value.Part, part.Value.Cell),
            (Single(data, part.Key)["part"]!.GetValue<string>(), Single(data, part.Key)["linkedCell"]!.GetValue<string>())));
        // Text linked to a cell of the same sheet is found once, in the cell, whichever way the link is written.
        Assert.All(Written, part => Assert.Null(Single(data, part)["part"]));

        JsonNode[] named = Replace("{\"sheet\":\"Data\",", "replaced.xlsx");
        Assert.Equal(
            Named.Values.Concat(Linked.Values.Select(static linked => linked.Part)).Distinct().Order(StringComparer.Ordinal),
            named.Select(static warning => Part(warning)).Distinct().Order(StringComparer.Ordinal));
        Assert.Single(named, static warning => Part(warning) == "typed-value");
        Assert.Equal(Linked.Count, named.Count(static warning => warning["message"]!.GetValue<string>().Contains("which shows cell", StringComparison.Ordinal)));
        Assert.All(Linked.Values, linked => Assert.Contains(named, warning => warning["message"]!.GetValue<string>().Contains("which shows cell " + linked.Cell, StringComparison.Ordinal)));

        JsonNode[] after = Hits("replaced.xlsx", "Data");
        Assert.All(Written, part => Assert.DoesNotContain(after, hit => Holds(hit, part)));
        Assert.All(Named.Keys.Concat(Linked.Keys), part => Assert.Contains(after, hit => Holds(hit, part)));
        Assert.Equal(
            named.Select(static warning => Part(warning)).Distinct().Order(StringComparer.Ordinal),
            after.Select(static hit => hit["part"]?.GetValue<string>() ?? "text").Distinct().Order(StringComparer.Ordinal));

        // Without a sheet, replace_text works on the active sheet, Data, and names the same parts.
        Assert.Equal(
            named.Select(static warning => Part(warning)).Order(StringComparer.Ordinal),
            Replace("{", "active.xlsx").Select(static warning => Part(warning)).Order(StringComparer.Ordinal));
    }

    private JsonNode[] Hits(string file, string? sheet)
    {
        CliResult search = _workspace.Run([.. new[] { "cells", "search", file, "--pattern", Marker, "--content", "all", "--max-hits", "100", "--output-format", "json" },
            .. sheet is null ? Array.Empty<string>() : ["--sheet", sheet]]);
        Assert.True(search.ExitCode == 0, search.StdOut + search.StdErr);
        return [.. JsonNode.Parse(search.StdOut)!["hits"]!.AsArray().OfType<JsonNode>()];
    }

    // replace_text with --verify, the operation opening with its sheet or none; the TEXT_NOT_REPLACED warnings.
    private JsonNode[] Replace(string opening, string output)
    {
        CliResult edit = _workspace.Run("cells", "edit", "parts.xlsx", "--ops", $$"""{"ops":[{{opening}}"op":"replace_text","find":"{{Marker}}","replace":"Ok"}]}""",
            "--out", output, "--verify", "--output-format", "json");
        Assert.True(edit.ExitCode == 0, edit.StdOut + edit.StdErr);
        JsonNode result = JsonNode.Parse(edit.StdOut)!;
        Assert.True(result["verification"]!["ok"]!.GetValue<bool>(), result["verification"]!.ToJsonString());
        return [.. (result["warnings"] as JsonArray ?? []).OfType<JsonNode>().Where(static warning => warning["code"]!.GetValue<string>() == "TEXT_NOT_REPLACED")];
    }

    private static JsonNode Single(JsonNode[] hits, string part) => Assert.Single(hits, hit => Holds(hit, part));

    private static bool Holds(JsonNode hit, string part) =>
        hit["context"]!.GetValue<string>().Contains(Marker + part, StringComparison.Ordinal);

    private static string Part(JsonNode warning) => warning["details"]!["part"]!.GetValue<string>();

    /// <summary>Sheet Data with a marker in each part, two of them linked to cells of sheet Other, whose chart and text box are linked to cells of Data.</summary>
    private static void Workbook(string path)
    {
        using var workbook = new Workbook();
        Worksheet data = workbook.Worksheets[0];
        data.Name = "Data";
        Worksheet other = workbook.Worksheets[workbook.Worksheets.Add()];
        other.Name = "Other";
        data.Cells["A1"].PutValue(Marker + "Cel");
        data.Cells["A2"].Formula = $"=IF(FALSE,\"{Marker}For\",\"no\")";
        foreach ((string cell, string tag) in new[] { ("A3", "Ty1"), ("A4", "Ty2") })
        {
            data.Cells[cell].PutValue(5);
            Style style = data.Cells[cell].GetStyle();
            style.Custom = $"\"{Marker}{tag}\" 0";
            data.Cells[cell].SetStyle(style);
        }
        data.Cells["B5"].PutValue(1);
        data.Cells["B6"].PutValue(2);
        data.Cells["D1"].PutValue(Marker + "Dti");
        data.Cells["D2"].PutValue(Marker + "Dbx");
        data.Cells["D3"].PutValue(Marker + "Sbx");
        data.Cells["D4"].PutValue(Marker + "Sti");
        other.Cells["A1"].PutValue(Marker + "Lti");
        other.Cells["A2"].PutValue(Marker + "Lbx");

        data.Comments[data.Comments.Add("B1")].Note = Marker + "Com";
        data.Comments.AddThreadedComment("B2", Marker + "Thr", workbook.Worksheets.ThreadedCommentAuthors[workbook.Worksheets.ThreadedCommentAuthors.Add("Reviewer", "r", "None")]);
        data.PageSetup.SetHeader(1, Marker + "Hea");
        data.PageSetup.SetFooter(1, Marker + "Foo");

        Chart chart = data.Charts[data.Charts.Add(ChartType.Column, 8, 0, 22, 6)];
        int first = chart.NSeries.Add("B5:B6", true);
        chart.NSeries[first].Name = Marker + "Ser";
        chart.NSeries[first].Points[0].DataLabels.Text = Marker + "Lab";
        int second = chart.NSeries.Add("B5:B6", true);
        chart.NSeries[second].Type = ChartType.Line;
        chart.NSeries[second].PlotOnSecondAxis = true;
        chart.SecondValueAxis.IsVisible = true;
        chart.Title.Text = Marker + "Cht";
        chart.CategoryAxis.Title.Text = Marker + "Cat";
        chart.ValueAxis.Title.Text = Marker + "Val";
        chart.SecondValueAxis.Title.Text = Marker + "Sec";
        chart.ChartObject.AlternativeText = Marker + "Cal";
        Chart linked = data.Charts[data.Charts.Add(ChartType.Column, 8, 7, 22, 12)];
        linked.NSeries.Add("B5:B6", true);
        linked.Title.LinkedSource = "=Other!$A$1";

        data.Shapes.AddTextBox(24, 0, 0, 0, 40, 160).Text = Marker + "Box";
        data.Shapes.AddRectangle(24, 0, 4, 0, 40, 160).AlternativeText = Marker + "Alt";
        Shape inner = data.Shapes.AddTextBox(28, 0, 0, 0, 40, 160);
        inner.Text = Marker + "Grp";
        data.Shapes.Group([inner, data.Shapes.AddTextBox(28, 0, 4, 0, 40, 160)]);
        Shape same = data.Shapes.AddTextBox(36, 0, 0, 0, 40, 160);
        same.LinkedCell = "$D$3";
        same.Text = Marker + "Sbx";
        Chart sameTitle = data.Charts[data.Charts.Add(ChartType.Column, 8, 13, 22, 18)];
        sameTitle.NSeries.Add("B5:B6", true);
        sameTitle.Title.LinkedSource = "=$D$4";
        Shape box = data.Shapes.AddTextBox(32, 0, 0, 0, 40, 160);
        box.LinkedCell = "Other!$A$2";
        box.Text = Marker + "Lbx";

        Chart back = other.Charts[other.Charts.Add(ChartType.Column, 4, 0, 18, 6)];
        back.NSeries.Add("Data!B5:B6", true);
        back.Title.LinkedSource = "=Data!$D$1";
        Shape backBox = other.Shapes.AddTextBox(20, 0, 0, 0, 40, 160);
        backBox.LinkedCell = "Data!$D$2";
        backBox.Text = Marker + "Dbx";
        workbook.Worksheets.ActiveSheetIndex = data.Index;
        workbook.Save(path);
    }
}
