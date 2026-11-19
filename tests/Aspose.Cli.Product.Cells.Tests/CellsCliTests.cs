using System.Text;
using System.Text.Json.Nodes;
using Aspose.Cells;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// The Cells commands through the built CLI, which runs without a license: the options each
/// command states to the engine, what it writes and what its result says.
/// </summary>
public sealed class CellsCliTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    [Fact]
    public void ConvertCsv_ReadsTheTextWithTheOptionsStated()
    {
        File.WriteAllText(_workspace.File("export.csv"), "Region;Q1;Day\nEast;1,5;02.01.2026\n", Encoding.UTF8);

        CliResult converted = _workspace.Run("cells", "convert", "export.csv", "--to", "xlsx",
            "--separator", ";", "--encoding", "utf-8", "--convert-numbers", "true", "--convert-dates", "true", "--culture", "de-DE");

        Assert.True(converted.ExitCode == 0, converted.StdErr);
        using var workbook = new Workbook(_workspace.File("export.xlsx"));
        Aspose.Cells.Cells cells = workbook.Worksheets[0].Cells;
        Assert.Equal(1.5, cells["B2"].DoubleValue);
        Assert.Equal(CellValueType.IsDateTime, cells["C2"].Type);
        Assert.Equal(new DateTime(2026, 1, 2), cells["C2"].DateTimeValue);
    }

    [Theory]
    [InlineData("--separator")]
    [InlineData("--encoding")]
    [InlineData("--convert-numbers")]
    [InlineData("--convert-dates")]
    public void ConvertCsv_WithoutATextOption_IsRefusedNamingIt(string left)
    {
        File.WriteAllText(_workspace.File("export.csv"), "a,b\n1,2\n");
        string[] options = ["--separator", ",", "--encoding", "utf-8", "--convert-numbers", "false", "--convert-dates", "false"];
        int at = Array.IndexOf(options, left);

        CliResult refused = _workspace.Run(["cells", "convert", "export.csv", "--to", "xlsx", .. options[..at], .. options[(at + 2)..]]);

        Assert.Equal(2, refused.ExitCode);
        Assert.Contains(left, JsonNode.Parse(refused.StdErr)!["error"]!["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(File.Exists(_workspace.File("export.xlsx")));
    }

    /// <summary>
    /// Without a license the engine writes the first sheet to CSV whatever sheet is active
    /// (CELLS-EVALUATION-CSV-FIRST-SHEET); <see cref="CellsConvertTests"/> covers the sheet named.
    /// </summary>
    [Fact]
    public void ConvertToCsv_TakesOneSheet()
    {
        WriteSales("sales.xlsx");

        CliResult one = _workspace.Run("cells", "convert", "sales.xlsx", "--to", "csv", "--out", "one.csv");
        CliResult two = _workspace.Run("cells", "convert", "sales.xlsx", "--to", "csv", "--sheet", "Sales", "--sheet", "Summary", "--out", "two.csv");

        Assert.True(one.ExitCode == 0, one.StdErr);
        Assert.StartsWith("Region,Q1", File.ReadAllText(_workspace.File("one.csv")), StringComparison.Ordinal);
        Assert.Equal(2, two.ExitCode);
        Assert.Equal("OPTION_INVALID", JsonNode.Parse(two.StdErr)!["error"]!["code"]!.GetValue<string>());
        Assert.False(File.Exists(_workspace.File("two.csv")));
    }

    [Fact]
    public void ConvertToHtml_WritesOneFile()
    {
        WriteSales("sales.xlsx");

        CliResult converted = _workspace.Run("cells", "convert", "sales.xlsx", "--to", "html", "--out", "sales.html");

        Assert.True(converted.ExitCode == 0, converted.StdErr);
        Assert.Equal(["sales.html", "sales.xlsx"], Directory.EnumerateFileSystemEntries(_workspace.Path).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ConvertToPdf_WritesTheNamedSheets()
    {
        WriteSales("sales.xlsx");

        CliResult converted = _workspace.Run("cells", "convert", "sales.xlsx", "--to", "pdf", "--sheet", "Summary", "--out", "summary.pdf");

        Assert.True(converted.ExitCode == 0, converted.StdErr);
        JsonNode result = JsonNode.Parse(converted.StdOut)!;
        Assert.Equal("Summary", result["sheets"]![0]!.GetValue<string>());
        Assert.True(new FileInfo(_workspace.File("summary.pdf")).Length > 0);
    }

    [Fact]
    public void Render_DrawsTheAreaOfTheNamedSheet_OrEverySheet()
    {
        WriteSales("sales.xlsx");

        CliResult rendered = _workspace.Run("cells", "render", "sales.xlsx", "--sheet", "Sales", "--area", "A1:B2", "--out", "sales.png");
        CliResult every = _workspace.Run("cells", "render", "sales.xlsx", "--out", "all.png");

        Assert.True(rendered.ExitCode == 0, rendered.StdErr);
        JsonNode output = Assert.Single(JsonNode.Parse(rendered.StdOut)!["outputs"]!.AsArray())!;
        Assert.True(File.Exists(output["path"]!.GetValue<string>()));
        Assert.True(every.ExitCode == 0, every.StdErr);
        string[] sheets = [.. JsonNode.Parse(every.StdOut)!["outputs"]!.AsArray().Select(static item => item!["sheet"]!.GetValue<string>())];
        Assert.Contains("Sales", sheets);
        Assert.Contains("Summary", sheets);
        Assert.True(File.Exists(_workspace.File("all.Sales.png")));
    }

    /// <summary>
    /// A sheet the engine lays out on no page, an empty or a hidden one, stops the render with
    /// its name, whether --sheet names it or the render draws every sheet, and nothing is written.
    /// </summary>
    [Theory]
    [InlineData("empty")]
    [InlineData("hidden")]
    public void Render_OfASheetWithNoPage_IsRenderFailedNamingIt(string kind)
    {
        using (var workbook = new Workbook())
        {
            Worksheet data = workbook.Worksheets[0];
            data.Name = "Data";
            data.Cells["A1"].PutValue("Shown");
            Worksheet other = workbook.Worksheets.Add("Other");
            if (kind == "hidden")
            {
                other.Cells["A1"].PutValue("Hidden");
                other.IsVisible = false;
            }

            workbook.Save(_workspace.File("book.xlsx"), SaveFormat.Xlsx);
        }

        CliResult every = _workspace.Run("cells", "render", "book.xlsx", "--out", "book.png");
        CliResult named = _workspace.Run("cells", "render", "book.xlsx", "--sheet", "Other", "--out", "other.png");

        Assert.All([every, named], refused =>
        {
            JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
            Assert.Equal("RENDER_FAILED", error["code"]!.GetValue<string>());
            Assert.Equal("Other", error["details"]!["sheet"]!.GetValue<string>());
        });
        Assert.Empty(Directory.EnumerateFiles(_workspace.Path, "*.png"));
    }

    [Fact]
    public void Create_FromATemplate_WritesItsSheets()
    {
        WriteSales("template.xltx", SaveFormat.Xltx);

        CliResult created = _workspace.Run("cells", "create", "book.xlsx", "--template", "template.xltx");

        Assert.True(created.ExitCode == 0, created.StdErr);
        using var workbook = new Workbook(_workspace.File("book.xlsx"));
        Assert.Contains(workbook.Worksheets, sheet => sheet.Name == "Summary");
    }

    [Fact]
    public void EditInPlace_LeavesOnlyTheWorkbook_AndSaysItRanWithoutALicense()
    {
        WriteSales("sales.xlsx");

        CliResult edited = _workspace.Run("cells", "edit", "sales.xlsx", "--in-place",
            "--ops", """{"ops":[{"set":"sheet/Sales/cell/B2","props":{"value":99}}]}""");

        Assert.True(edited.ExitCode == 0, edited.StdErr);
        JsonNode result = JsonNode.Parse(edited.StdOut)!;
        Assert.Equal("evaluation", result["license"]!["mode"]!.GetValue<string>());
        Assert.Equal(99, Assert.Single(result["ops"]!.AsArray())!["now"]!["value"]!.GetValue<double>());
        Assert.Equal(["sales.xlsx"], Directory.EnumerateFileSystemEntries(_workspace.Path).Select(Path.GetFileName));
    }

    [Theory]
    [InlineData("read")]
    [InlineData("edit")]
    public void ReadAndEdit_OfCsvText_AreFormatUnsupported(string verb)
    {
        File.WriteAllText(_workspace.File("export.csv"), "a,b\n1,2\n");
        string[] args = verb == "read"
            ? ["cells", "read", "export.csv"]
            : ["cells", "edit", "export.csv", "--out", "export.xlsx", "--ops", """{"ops":[{"do":"calculate"}]}"""];

        CliResult refused = _workspace.Run(args);

        Assert.NotEqual(0, refused.ExitCode);
        Assert.Equal("FORMAT_UNSUPPORTED", JsonNode.Parse(refused.StdErr)!["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public void ConvertToCsv_WithBom_StartsWithTheMark_AndWithoutItDoesNot()
    {
        WriteSales("sales.xlsx");

        CliResult marked = _workspace.Run("cells", "convert", "sales.xlsx", "--to", "csv", "--bom", "--out", "marked.csv");
        CliResult plain = _workspace.Run("cells", "convert", "sales.xlsx", "--to", "csv", "--out", "plain.csv");

        Assert.True(marked.ExitCode == 0, marked.StdErr);
        Assert.True(plain.ExitCode == 0, plain.StdErr);
        Assert.True(File.ReadAllBytes(_workspace.File("marked.csv")).AsSpan().StartsWith(Encoding.UTF8.Preamble));
        Assert.False(File.ReadAllBytes(_workspace.File("plain.csv")).AsSpan().StartsWith(Encoding.UTF8.Preamble));
    }

    /// <summary>
    /// An action's password comes from the variable its passwordEnv names and appears in no output;
    /// the engine's own refusal of a wrong one is ENGINE_ERROR, and nothing is written.
    /// </summary>
    [Fact]
    public void ProtectAndUnprotectSheet_TakeThePasswordOfTheirVariable_AndNameItNowhere()
    {
        const string Password = "sheet-secret-4f2a";
        WriteSales("sales.xlsx");
        var right = new Dictionary<string, string?> { ["SHEET_PASSWORD"] = Password };
        var wrong = new Dictionary<string, string?> { ["SHEET_PASSWORD"] = "not-the-password" };
        const string Unprotect = """{"ops":[{"do":"unprotect_sheet","args":{"sheet":"sheet/Sales","passwordEnv":"SHEET_PASSWORD"}}]}""";

        CliResult protect = _workspace.RunWithEnv(right, "cells", "edit", "sales.xlsx", "--in-place", "--ops",
            """{"ops":[{"do":"protect_sheet","args":{"sheet":"sheet/Sales","type":"All","passwordEnv":"SHEET_PASSWORD"}}]}""");
        CliResult refused = _workspace.RunWithEnv(wrong, "cells", "edit", "sales.xlsx", "--out", "refused.xlsx", "--ops", Unprotect);
        CliResult unprotect = _workspace.RunWithEnv(right, "cells", "edit", "sales.xlsx", "--out", "open.xlsx", "--ops", Unprotect);

        Assert.True(protect.ExitCode == 0, protect.StdErr);
        Assert.True(JsonNode.Parse(protect.StdOut)!["ops"]![0]!["now"]!["isProtected"]!.GetValue<bool>());
        Assert.Equal("ENGINE_ERROR", JsonNode.Parse(refused.StdErr)!["error"]!["code"]!.GetValue<string>());
        Assert.False(File.Exists(_workspace.File("refused.xlsx")));
        Assert.True(unprotect.ExitCode == 0, unprotect.StdErr);
        Assert.False(JsonNode.Parse(unprotect.StdOut)!["ops"]![0]!["now"]!["isProtected"]!.GetValue<bool>());
        Assert.All([protect, refused, unprotect], run => Assert.DoesNotContain(Password, run.StdOut + run.StdErr, StringComparison.Ordinal));
    }

    [Fact]
    public void EncryptedWorkbook_IsReadAsTheFormatItHolds_WithItsPassword()
    {
        WriteSales("plain.xlsx");
        var password = new Dictionary<string, string?> { ["BOOK_PASSWORD"] = "book-secret-77c1" };
        CliResult encrypted = _workspace.RunWithEnv(password, "cells", "convert", "plain.xlsx", "--to", "xlsx", "--out", "locked.xlsx", "--encrypt-env", "BOOK_PASSWORD");
        Assert.True(encrypted.ExitCode == 0, encrypted.StdErr);

        CliResult read = _workspace.RunWithEnv(password, "cells", "read", "locked.xlsx", "--password-env", "BOOK_PASSWORD");
        CliResult missing = _workspace.Run("cells", "read", "locked.xlsx");

        Assert.True(read.ExitCode == 0, read.StdErr);
        JsonNode input = JsonNode.Parse(read.StdOut)!["input"]!;
        Assert.Equal("xlsx", input["format"]!.GetValue<string>());
        Assert.True(input["encrypted"]!.GetValue<bool>());
        Assert.Equal("PASSWORD_REQUIRED", JsonNode.Parse(missing.StdErr)!["error"]!["code"]!.GetValue<string>());
    }

    private void WriteSales(string name, SaveFormat format = SaveFormat.Xlsx)
    {
        using var workbook = new Workbook();
        Worksheet sales = workbook.Worksheets[0];
        sales.Name = "Sales";
        sales.Cells["A1"].PutValue("Region");
        sales.Cells["B1"].PutValue("Q1");
        sales.Cells["A2"].PutValue("East");
        sales.Cells["B2"].PutValue(1234.5);
        Style style = sales.Cells["B2"].GetStyle();
        style.Custom = "#,##0";
        sales.Cells["B2"].SetStyle(style);
        Worksheet summary = workbook.Worksheets.Add("Summary");
        summary.Cells["A1"].PutValue("Revenue");
        summary.Cells["B1"].Formula = "=Sales!B2";
        summary.Cells["B1"].SetStyle(style);
        workbook.CalculateFormula();
        workbook.Save(_workspace.File(name), format);
    }

    public void Dispose() => _workspace.Dispose();
}
