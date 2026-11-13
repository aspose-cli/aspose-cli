using System.Text.Json.Nodes;
using Aspose.Cells;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

public sealed class CellsMutationBoundaryTests
{
    [Fact]
    public void AutoFitMeasuresFormulaResultsWrittenEarlierInTheSameBatch()
    {
        using var workspace = new TempWorkspace();
        Assert.Equal(0, workspace.Run("cells", "create", "source.xlsx", "--sheet-names", "Data").ExitCode);

        CliResult edited = workspace.Run("cells", "edit", "source.xlsx", "--ops",
            """{"ops":[{"op":"set_formula","sheet":"Data","range":"A1","formula":"=REPT(\"x\",40)"},{"op":"resize_columns","sheet":"Data","first":"A"}]}""",
            "--out", "edited.xlsx", "--output-format", "json");

        Assert.True(edited.ExitCode == 0, edited.StdErr);
        using var workbook = new Workbook(workspace.File("edited.xlsx"));
        Assert.True(workbook.Worksheets["Data"].Cells.GetColumnWidth(0) > 30);
    }

    /// <summary>
    /// In an Arial 10 workbook, where auto-fit leaves the General number -15600.75 rounded
    /// (CELLS-AUTOFIT-GENERAL-NUMBER), auto-fit widens only the column of such a number until it
    /// shows in full: text, a General number that fits, a formatted number, a date, a merged
    /// General number and General numbers rotated or stacked keep the width the engine's auto-fit
    /// gives them. Arial comes with Windows, so no font is substituted.
    /// </summary>
    [Fact]
    public void AutoFit_WidensOnlyTheColumnsOfGeneralNumbersTheEngineFitsRounded()
    {
        Requires.Windows();
        using var fixture = new CellsFixture();
        _ = fixture.LicenseState;
        using var workspace = new TempWorkspace();
        Assert.Equal(0, workspace.Run("cells", "create", "source.xlsx", "--sheet-names", "Data").ExitCode);
        CliResult built = workspace.Run("cells", "edit", "source.xlsx", "--in-place", "--ops", """
            {"ops":[
              {"op":"set_default_font","font":"Arial","fontSize":10},
              {"op":"set_values","sheet":"Data","range":"A1","values":[
                ["Amount","Customer","Count","Total","Due","Merged","","Rotated","Stacked"],
                [-15600.75,"Hengtong Trading Co.",21000,15600.75,46338,1234567.891,"",-15600.75,-15600.75]]},
              {"op":"format_range","sheet":"Data","range":"D2","style":{"numberFormat":"#,##0.00"}},
              {"op":"format_range","sheet":"Data","range":"E2","style":{"numberFormat":"yyyy-mm-dd"}},
              {"op":"merge_cells","sheet":"Data","range":"F2:G2"}
            ]}
            """, "--output-format", "json");
        Assert.True(built.ExitCode == 0, built.StdErr);
        // No operation rotates text, so the engine turns H2 up and stacks I2.
        using (var source = new Workbook(workspace.File("source.xlsx")))
        {
            foreach ((string name, int angle) in new[] { ("H2", 90), ("I2", 255) })
            {
                Cell cell = source.Worksheets["Data"].Cells[name];
                Style style = cell.GetStyle();
                style.RotationAngle = angle;
                cell.SetStyle(style);
            }
            source.Save(workspace.File("source.xlsx"));
        }

        CliResult fitted = workspace.Run("cells", "edit", "source.xlsx", "--ops",
            """{"ops":[{"op":"resize_columns","sheet":"Data","first":"A","last":"I"}]}""",
            "--out", "fitted.xlsx", "--output-format", "json");

        Assert.True(fitted.ExitCode == 0, fitted.StdErr);
        using var engine = new Workbook(workspace.File("source.xlsx"));
        Aspose.Cells.Cells expected = engine.Worksheets["Data"].Cells;
        engine.Worksheets["Data"].AutoFitColumns(0, 8);
        using var workbook = new Workbook(workspace.File("fitted.xlsx"));
        Aspose.Cells.Cells cells = workbook.Worksheets["Data"].Cells;
        Assert.Equal("-15600.8", expected["A2"].GetStringValue(CellValueFormatStrategy.DisplayString));
        Assert.Equal("-15600.75", cells["A2"].GetStringValue(CellValueFormatStrategy.DisplayString));
        Assert.Equal(
            [.. Enumerable.Range(1, 8).Select(expected.GetColumnWidthPixel)],
            [.. Enumerable.Range(1, 8).Select(cells.GetColumnWidthPixel)]);
    }

    /// <summary>
    /// Auto-fit leaves alone what the sheet hides, which the engine's own auto-fit shows: a
    /// hidden column, one of no width, columns a collapsed group hides, a hidden row, one of no
    /// height and rows a collapsed group hides each keep their state and size, while the visible
    /// columns and rows around them still fit their text.
    /// </summary>
    [Fact]
    public void AutoFit_LeavesHiddenRowsAndColumnsHidden()
    {
        using var fixture = new CellsFixture();
        _ = fixture.LicenseState;
        using var workspace = new TempWorkspace();
        using (var source = new Workbook())
        {
            Worksheet sheet = source.Worksheets[0];
            sheet.Name = "Data";
            Aspose.Cells.Cells cells = sheet.Cells;
            for (int index = 0; index < 7; index++)
            {
                cells[0, index].PutValue($"a header long enough to widen column {index}");
                Cell lines = cells[index + 1, 0];
                lines.PutValue("line one\nline two\nline three");
                Style style = lines.GetStyle();
                style.IsTextWrapped = true;
                lines.SetStyle(style);
            }

            cells.HideColumn(1);
            cells.SetColumnWidth(2, 0);
            cells.GroupColumns(3, 4, true);
            cells.HideRow(2);
            cells.SetRowHeight(3, 0);
            cells.GroupRows(4, 5, true);
            source.Save(workspace.File("source.xlsx"));
        }

        CliResult fitted = workspace.Run("cells", "edit", "source.xlsx", "--ops",
            """{"ops":[{"op":"resize_columns","sheet":"Data","first":"A","last":"G"},{"op":"resize_rows","sheet":"Data","first":1,"last":8}]}""",
            "--out", "fitted.xlsx", "--verify", "--output-format", "json");

        Assert.True(fitted.ExitCode == 0, fitted.StdErr);
        using var before = new Workbook(workspace.File("source.xlsx"));
        using var after = new Workbook(workspace.File("fitted.xlsx"));
        Aspose.Cells.Cells was = before.Worksheets["Data"].Cells, now = after.Worksheets["Data"].Cells;
        foreach (int column in new[] { 1, 2, 3, 4 })
        {
            Assert.Equal((true, was.GetColumnWidth(column)), (now.IsColumnHidden(column), now.GetColumnWidth(column)));
        }

        foreach (int row in new[] { 2, 3, 4, 5 })
        {
            Assert.Equal((true, was.GetRowHeight(row)), (now.IsRowHidden(row), now.GetRowHeight(row)));
        }

        Assert.All(new[] { 0, 5, 6 }, column => Assert.True(now.GetColumnWidth(column) > was.GetColumnWidth(column)));
        Assert.All(new[] { 1, 6, 7 }, row => Assert.True(now.GetRowHeight(row) > was.GetRowHeight(row)));
    }

    /// <summary>
    /// The quote prefix of a hyperlink's cell is its display's alone: an internal link with the
    /// display Go, on an empty cell, where the engine writes the quoted target with a quote prefix,
    /// and on a filled one, saves a cell without a prefix and a link that shows Go, while the display
    /// 'abc keeps its prefix and reads back as 'abc from the cell and from the link.
    /// </summary>
    [Fact]
    public void SetHyperlink_LeavesTheCellTheQuotePrefixOfItsDisplayAlone()
    {
        using var fixture = new CellsFixture();
        _ = fixture.LicenseState;
        using var workspace = new TempWorkspace();
        using (var source = new Workbook())
        {
            source.Worksheets[0].Name = "Sheet1";
            source.Worksheets.Add("My Sheet");
            source.Worksheets[0].Cells["B2"].PutValue("old");
            source.Save(workspace.File("links.xlsx"));
        }

        CliResult linked = workspace.Run("cells", "edit", "links.xlsx", "--in-place", "--verify", "--output-format", "json", "--ops", """
            {"ops":[
              {"op":"set_hyperlink","sheet":"Sheet1","cell":"A1","target":"'My Sheet'!A1","display":"Go"},
              {"op":"set_hyperlink","sheet":"Sheet1","cell":"A2","target":"Sheet1!A1","display":"Go"},
              {"op":"set_hyperlink","sheet":"Sheet1","cell":"B2","target":"'My Sheet'!A1","display":"Go"},
              {"op":"set_hyperlink","sheet":"Sheet1","cell":"A3","url":"https://example.com/","display":"'abc"}
            ]}
            """);

        Assert.True(linked.ExitCode == 0, linked.StdErr + linked.StdOut);
        using (var saved = new Workbook(workspace.File("links.xlsx")))
        {
            Worksheet sheet = saved.Worksheets["Sheet1"];
            foreach (string name in new[] { "A1", "A2", "B2" })
            {
                Hyperlink link = sheet.Hyperlinks.Cast<Hyperlink>().Single(item => CellsHelper.CellIndexToName(item.Area.StartRow, item.Area.StartColumn) == name);
                Assert.Equal((name, "Go", false, "Go"), (name, sheet.Cells[name].StringValue, sheet.Cells[name].GetStyle().QuotePrefix, link.TextToDisplay));
            }

            Assert.Equal(("'abc", true), (sheet.Cells["A3"].StringValue, sheet.Cells["A3"].GetStyle().QuotePrefix));
        }

        CliResult read = workspace.Run("cells", "read", "links.xlsx", "--sheet", "Sheet1", "--range", "A3", "--output-format", "json");
        Assert.Equal("'abc", JsonNode.Parse(read.StdOut)!["sheet"]!["cells"]![0]![0]!["v"]!.GetValue<string>());
        CliResult inspected = workspace.Run("cells", "inspect", "links.xlsx", "--detail", "hyperlinks", "--output-format", "json");
        Assert.Equal("'abc", JsonNode.Parse(inspected.StdOut)!["workbook"]!["hyperlinks"]!.AsArray()
            .Single(link => link!["range"]!.GetValue<string>() == "A3")!["display"]!.GetValue<string>());
    }

    /// <summary>
    /// set_values keeps the text #N/A with a quote prefix, which a number written over it drops,
    /// as typing a number over '#N/A in Excel does, while the text beside it keeps its prefix.
    /// </summary>
    [Fact]
    public void SetValues_NumberOverErrorText_DropsTheQuotePrefix()
    {
        using var workspace = new TempWorkspace();
        Assert.Equal(0, workspace.Run("cells", "create", "book.xlsx", "--sheet-names", "S").ExitCode);
        CliResult text = workspace.Run("cells", "edit", "book.xlsx", "--in-place", "--ops",
            """{"ops":[{"op":"set_values","sheet":"S","range":"A1","values":[["#N/A","#N/A"]]}]}""");
        Assert.True(text.ExitCode == 0, text.StdErr);
        using (var written = new Workbook(workspace.File("book.xlsx")))
        {
            Cell cell = written.Worksheets["S"].Cells["A1"];
            Assert.Equal((CellValueType.IsString, "#N/A", true), (cell.Type, cell.StringValue, cell.GetStyle().QuotePrefix));
        }

        CliResult number = workspace.Run("cells", "edit", "book.xlsx", "--in-place", "--ops",
            """{"ops":[{"op":"set_values","sheet":"S","range":"A1","values":[[5]]}]}""");

        Assert.True(number.ExitCode == 0, number.StdErr);
        using var reopened = new Workbook(workspace.File("book.xlsx"));
        Cell five = reopened.Worksheets["S"].Cells["A1"];
        Assert.Equal((CellValueType.IsNumeric, 5d, false), (five.Type, five.DoubleValue, five.GetStyle().QuotePrefix));
        Assert.True(reopened.Worksheets["S"].Cells["B1"].GetStyle().QuotePrefix);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ImageInputBudgetAbortsPublicationEvenInBestEffort(bool bestEffort, bool supervised)
    {
        using var workspace = new TempWorkspace();
        Assert.Equal(0, workspace.Run("cells", "create", "source.xlsx", "--sheet-names", "Data").ExitCode);
        byte[] image = new byte[128 * 1024];
        ResourceHttpServer.Image.CopyTo(image, 0);
        File.WriteAllBytes(workspace.File("large.png"), image);
        string operations = System.Text.Json.JsonSerializer.Serialize(new
        { ops = new[] { new { op = "insert_image", sheet = "Data", at = "A1", path = workspace.File("large.png") } } });
        var args = new List<string> { "cells", "edit", "source.xlsx", "--ops", operations,
            "--out", "result.xlsx", "--max-input-bytes", "16384", "--output-format", "json" };
        if (bestEffort) { args.Add("--best-effort"); }
        if (supervised) { args.AddRange(["--timeout", TestDeadline.CliTimeout]); }
        CliResult edited = workspace.Run(args.ToArray());
        Assert.NotEqual(0, edited.ExitCode);
        Assert.Contains("FILE_TOO_LARGE", edited.StdErr, StringComparison.Ordinal);
        Assert.False(File.Exists(workspace.File("result.xlsx")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EditingEncryptedInputPreservesItsPassword(bool changePassword)
    {
        using var workspace = new TempWorkspace();
        var environment = new Dictionary<string, string?> { ["OLD_PASSWORD"] = "old-test-password", ["NEW_PASSWORD"] = "new-test-password" };
        CliResult create = workspace.RunWithEnv(environment, "cells", "create", "secret.xlsx", "--sheet-names", "Data", "--encrypt-env", "OLD_PASSWORD", "--output-format", "json");
        Assert.True(create.ExitCode == 0, create.StdErr);
        var arguments = new List<string> { "cells", "edit", "secret.xlsx", "--password-env", "OLD_PASSWORD", "--set", "Data!A1=7", "--out", "edited.xlsx", "--timeout", TestDeadline.CliTimeout, "--output-format", "json" };
        if (changePassword) { arguments.AddRange(["--encrypt-env", "NEW_PASSWORD"]); }
        CliResult edit = workspace.RunWithEnv(environment, arguments.ToArray());
        Assert.True(edit.ExitCode == 0, edit.StdErr);
        Assert.True(JsonNode.Parse(edit.StdOut)!["output"]!["encrypted"]!.GetValue<bool>());
        // Both reads of the output run at once: with its password, and without one.
        CliResult inspect = null!, unopened = null!;
        Parallel.Invoke(
            () => inspect = workspace.RunWithEnv(environment, "cells", "inspect", "edited.xlsx", "--password-env", changePassword ? "NEW_PASSWORD" : "OLD_PASSWORD", "--output-format", "json"),
            () => unopened = workspace.Run("cells", "inspect", "edited.xlsx", "--output-format", "json"));
        Assert.True(inspect.ExitCode == 0, inspect.StdErr);
        Assert.True(JsonNode.Parse(inspect.StdOut)!["input"]!["encrypted"]!.GetValue<bool>());
        Assert.NotEqual(0, unopened.ExitCode);
        Assert.DoesNotContain("test-password", edit.StdOut + edit.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportingEncryptedInputToTextDisclosesTheLostProtection()
    {
        using var workspace = new TempWorkspace();
        var environment = new Dictionary<string, string?> { ["EXPORT_PASSWORD"] = "test-export-secret" };
        Assert.Equal(0, workspace.RunWithEnv(environment, "cells", "create", "source.xlsx", "--sheet-names", "Data",
            "--encrypt-env", "EXPORT_PASSWORD", "--output-format", "json").ExitCode);
        CliResult result = workspace.RunWithEnv(environment, "cells", "edit", "source.xlsx", "--password-env", "EXPORT_PASSWORD",
            "--set", "Data!A1=7", "--out", "result.csv", "--output-format", "json");
        Assert.True(result.ExitCode == 0, result.StdErr);
        JsonNode edited = JsonNode.Parse(result.StdOut)!;
        // The SDK's shared code, the one every product reports the lost encryption with.
        Assert.Contains(edited["warnings"]!.AsArray(), static warning => warning!["code"]!.GetValue<string>() == "ENCRYPTION_REMOVED");
        // A CSV cannot carry a password, so the output reports no encryption state.
        Assert.Null(edited["output"]!["encrypted"]);
        Assert.DoesNotContain("test-export-secret", result.StdOut + result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public void HtmlConvertIsSelfContainedAndDoesNotBlockLaterWrites()
    {
        using var workspace = new TempWorkspace();
        using (var workbook = new Workbook())
        {
            workbook.Worksheets[0].Name = "Data";
            workbook.Worksheets.Add("Other");
            workbook.Worksheets[0].Cells["A1"].PutValue("image");
            using var picture = new MemoryStream(ResourceHttpServer.Image);
            workbook.Worksheets[0].Pictures.Add(1, 1, picture);
            workbook.Save(workspace.File("source.xlsx"));
        }
        CliResult convert = workspace.Run("cells", "convert", "source.xlsx", "--to", "html", "--out", "converted.html", "--output-format", "json");
        Assert.True(convert.ExitCode == 0, convert.StdErr);
        Assert.Contains("data:image", File.ReadAllText(workspace.File("converted.html")), StringComparison.OrdinalIgnoreCase);
        CliResult next = workspace.Run("cells", "edit", "source.xlsx", "--set", "Data!A1=next", "--out", "next.xlsx", "--output-format", "json");
        Assert.True(next.ExitCode == 0, next.StdErr);
        Assert.Empty(Directory.EnumerateDirectories(Path.GetDirectoryName(workspace.File("source.xlsx"))!, ".aspose-publication-*"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VerificationExaminesTheCandidateInSupervisedExecution(bool inPlace)
    {
        using var workspace = new TempWorkspace();
        Assert.Equal(0, workspace.Run("cells", "create", "source.xlsx", "--sheet-names", "Data").ExitCode);
        var args = new List<string> { "cells", "edit", "source.xlsx", "--set", "Data!A1==1/0", "--verify", "--timeout", TestDeadline.CliTimeout, "--output-format", "json" };
        args.AddRange(inPlace ? ["--in-place"] : ["--out", "edited.xlsx"]);
        CliResult edited = workspace.Run(args.ToArray());
        Assert.True(edited.ExitCode == 8, edited.StdErr + edited.StdOut);
        JsonNode verification = JsonNode.Parse(edited.StdOut)!["verification"]!;
        Assert.False(verification["ok"]!.GetValue<bool>());
        Assert.NotEmpty(verification["formulaErrors"]!.AsArray());
        Assert.DoesNotContain("FILE_NOT_FOUND", edited.StdOut, StringComparison.Ordinal);
    }
}
