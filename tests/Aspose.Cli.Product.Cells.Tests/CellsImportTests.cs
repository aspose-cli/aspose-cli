using System.Text.Json;
using Aspose.Cells;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Engine.Editing;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.IO;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// import_range and import_sheet against the real engine: another workbook's cells and sheets
/// land in the edited workbook, and the source is only read. The CLI suite covers the same
/// behavior in evaluation mode.
/// </summary>
public sealed class CellsImportTests : IClassFixture<CellsFixture>
{
    private readonly CellsFixture _fixture;

    public CellsImportTests(CellsFixture fixture) => _fixture = fixture;

    [Fact]
    public void ImportRange_ValuesReplacesFormulasWithResultsAndKeepsNumberFormats()
    {
        string source = CreateSource("values-source.xlsx");
        byte[] before = File.ReadAllBytes(source);

        using Workbook result = Apply(
            _fixture.CreateSalesWorkbook("values.xlsx"),
            $$"""{ "op": "import_range", "sheet": "Second", "path": {{Json(source)}}, "from": "Totals!A1:B4", "to": "C2" }""");

        Aspose.Cells.Cells cells = result.Worksheets["Second"].Cells;
        Assert.Equal("Region", cells["C2"].StringValue);
        Assert.Equal(1200.5d, cells["D3"].DoubleValue);
        // Totals!B4, the SUM, lands two rows below D3.
        Assert.False(cells["D5"].IsFormula);
        Assert.Equal(2000.5d, cells["D5"].DoubleValue);
        Assert.Equal("#,##0.00", cells["D3"].GetStyle().Custom);
        Assert.NotEqual(FillColor, Rgb(cells["D3"].GetStyle()));
        Assert.Equal("second-sheet-marker", cells["A1"].StringValue);
        Assert.Equal(before, File.ReadAllBytes(source));
    }

    [Fact]
    public void ImportRange_AllKeepsFormulasAndFormattingAndPointsSheetReferencesAtThisWorkbook()
    {
        string source = CreateSource("all-source.xlsx");

        using Workbook result = Apply(
            _fixture.CreateSalesWorkbook("all.xlsx"),
            $$"""{ "op": "import_range", "sheet": "Data", "path": {{Json(source)}}, "from": "Totals!B2:B5", "to": "Second!B2", "content": "all" }""");

        Aspose.Cells.Cells cells = result.Worksheets["Second"].Cells;
        Assert.Equal(1200.5d, cells["B2"].DoubleValue);
        Assert.Equal("=SUM(B2:B3)", cells["B4"].Formula);
        Assert.Equal(2000.5d, cells["B4"].DoubleValue);
        Assert.Equal("#,##0.00", cells["B2"].GetStyle().Custom);
        Assert.Equal(FillColor, Rgb(cells["B2"].GetStyle()));
        // This workbook has no Rates sheet, so the reference to the source's Rates sheet breaks
        // instead of linking to the source file.
        Assert.Equal("=B4*#REF!", cells["B5"].Formula);
        Assert.Equal(0, result.Worksheets.ExternalLinks.Count);
    }

    [Fact]
    public void ImportRange_AnUnqualifiedSourceRangeLiesOnTheSourcesFirstSheet()
    {
        string source = CreateSource("first-source.xlsx");

        using Workbook result = Apply(
            _fixture.CreateSalesWorkbook("first.xlsx"),
            $$"""{ "op": "import_range", "sheet": "Data", "path": {{Json(source)}}, "from": "A1", "to": "E1" }""");

        Assert.Equal("first-sheet", result.Worksheets["Data"].Cells["E1"].StringValue);
    }

    [Fact]
    public void ImportRange_ReadsADelimitedTextSource()
    {
        string source = _fixture.Temp.File("eu.csv");
        File.WriteAllText(source, "Region,Sales\nDE,1200\nFR,800\n");

        using Workbook result = Apply(
            _fixture.CreateSalesWorkbook("csv.xlsx"),
            $$"""{ "op": "import_range", "sheet": "Second", "path": {{Json(source)}}, "from": "A1:B3", "to": "A5" }""");

        Aspose.Cells.Cells cells = result.Worksheets["Second"].Cells;
        Assert.Equal("FR", cells["A7"].StringValue);
        Assert.Equal(800d, cells["B7"].DoubleValue);
    }

    [Fact]
    public void ImportSheet_AddsTheNamedSheetAtItsPositionWithContentAndFormatting()
    {
        string source = CreateSource("sheet-source.xlsx");

        using Workbook result = Apply(
            _fixture.CreateSalesWorkbook("sheet.xlsx"),
            $$"""{ "op": "import_sheet", "sheet": "Totals", "path": {{Json(source)}}, "name": "EU", "position": 1 }""");

        Assert.Equal("EU", result.Worksheets[1].Name);
        Aspose.Cells.Cells cells = result.Worksheets["EU"].Cells;
        Assert.Equal("=SUM(B2:B3)", cells["B4"].Formula);
        Assert.Equal(2000.5d, cells["B4"].DoubleValue);
        Assert.Equal(FillColor, Rgb(cells["B2"].GetStyle()));
        Assert.Equal("=B4*#REF!", cells["B5"].Formula);
        Assert.Equal(0, result.Worksheets.ExternalLinks.Count);
    }

    [Fact]
    public void ImportSheet_ReferencesToASheetThisWorkbookHasPointAtIt()
    {
        string source = CreateSource("rates-source.xlsx");

        using Workbook result = Apply(
            _fixture.CreateSalesWorkbook("rates.xlsx"),
            $$"""
            { "op": "import_sheet", "sheet": "Rates", "path": {{Json(source)}} },
            { "op": "import_sheet", "sheet": "Totals", "path": {{Json(source)}} }
            """);

        Aspose.Cells.Cells cells = result.Worksheets["Totals"].Cells;
        Assert.Equal("=B4*Rates!A1", cells["B5"].Formula);
        Assert.Equal(4001d, cells["B5"].DoubleValue);
        Assert.Equal(0, result.Worksheets.ExternalLinks.Count);
    }

    [Fact]
    public void AddingAndImportingASheetEachCountTheSheetTheyAdd()
    {
        string source = CreateSource("counted-source.xlsx");

        EditResult result = ApplyResult(
            _fixture.CreateSalesWorkbook("counted.xlsx"),
            $$"""
            { "op": "add_sheet", "name": "Blank" },
            { "op": "import_sheet", "sheet": "Totals", "path": {{Json(source)}} }
            """);

        Assert.Equal([1L, 1L], result.Applied.Select(static outcome => outcome.ItemsAffected));
    }

    [Fact]
    public void ImportSheet_DefaultsToTheSourcesFirstSheetAndItsName()
    {
        string source = CreateSource("default-source.xlsx");

        using Workbook result = Apply(
            _fixture.CreateSalesWorkbook("default.xlsx"),
            $$"""{ "op": "import_sheet", "path": {{Json(source)}} }""");

        Worksheet imported = result.Worksheets[result.Worksheets.Count - 1];
        Assert.Equal("Notes", imported.Name);
        Assert.Equal("first-sheet", imported.Cells["A1"].StringValue);
    }

    [Fact]
    public void ImportSheet_CanDuplicateASheetOfTheEditedFile()
    {
        string book = _fixture.CreateSalesWorkbook("self.xlsx");

        using Workbook result = Apply(
            book,
            $$"""{ "op": "import_sheet", "sheet": "Data", "path": {{Json(book)}}, "name": "Data copy" }""");

        Assert.Equal("=SUM(B2:B2)", result.Worksheets["Data copy"].Cells["B3"].Formula);
        Assert.Equal("Region", result.Worksheets["Data"].Cells["A1"].StringValue);
    }

    [Fact]
    public void ImportSheet_RefusesANameTheWorkbookUses()
    {
        string source = CreateSource("taken-source.xlsx");

        CliException error = Assert.Throws<CliException>(() => Apply(
            _fixture.CreateSalesWorkbook("taken.xlsx"),
            $$"""{ "op": "import_sheet", "sheet": "Totals", "path": {{Json(source)}}, "name": "second" }"""));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains("'Second'", error.Details!["reason"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void ImportSheet_BestEffortFailureLeavesNoSheetBehind()
    {
        string source = CreateSource("best-effort-source.xlsx");
        EditResult result = _fixture.Engine.ApplyOps(
            _fixture.CreateSalesWorkbook("best-effort.xlsx"),
            Parse($$"""
                { "ops": [
                  { "op": "import_sheet", "sheet": "Totals", "path": {{Json(source)}}, "name": "Data" },
                  { "op": "set_values", "sheet": "Second", "range": "A2", "values": [["after"]] }
                ] }
                """),
            new EditRequest
            {
                Output = TestOutput.At(_fixture.Temp.File("best-effort.out.xlsx"), overwrite: true),
                Options = new EditCommandOptions { BestEffort = true },
            });

        Assert.Equal([OpStatuses.Failed, OpStatuses.Ok], result.Applied.Select(static outcome => outcome.Status));
        using var workbook = new Workbook(result.Output!.Path);
        Assert.Equal(["Data", "Second", "Backstage"], workbook.Worksheets.Cast<Worksheet>().Select(static sheet => sheet.Name));
    }

    [Theory]
    [InlineData("""{ "op": "import_sheet", "sheet": "Ghost", "path": "{source}" }""")]
    [InlineData("""{ "op": "import_range", "sheet": "Data", "path": "{source}", "from": "Ghost!A1:B2", "to": "E1" }""")]
    public void AMissingSourceSheet_ListsTheSourcesSheets(string operation)
    {
        string source = CreateSource("missing-source.xlsx");

        CliException error = Assert.Throws<CliException>(() => Apply(
            _fixture.CreateSalesWorkbook("missing.xlsx"),
            operation.Replace("\"{source}\"", Json(source), StringComparison.Ordinal)));

        Assert.Equal(CellsDiagnostics.SheetNotFound, error.Code);
        Assert.Equal("Ghost", error.Details!["requested"]!.GetValue<string>());
        Assert.Equal(["Notes", "Totals", "Rates"], error.Details["available"]!.AsArray().Select(static name => name!.GetValue<string>()));
    }

    [Fact]
    public void ImportSheet_RefusesAWorkbookNameBothWorkbooksDefineDifferently()
    {
        string source = CreateSource("names-source.xlsx");
        using (var workbook = new Workbook(source))
        {
            int index = workbook.Worksheets.Names.Add("Rate");
            workbook.Worksheets.Names[index].RefersTo = "=Rates!$A$1";
            workbook.Worksheets["Totals"].Cells["C5"].Formula = "=Rate*2";
            workbook.Save(source);
        }

        string target = _fixture.CreateSalesWorkbook("names.xlsx");
        using (var workbook = new Workbook(target))
        {
            int index = workbook.Worksheets.Names.Add("Rate");
            workbook.Worksheets.Names[index].RefersTo = "=Data!$B$2";
            workbook.Save(target);
        }

        CliException error = Assert.Throws<CliException>(() => Apply(
            target,
            $$"""{ "op": "import_sheet", "sheet": "Totals", "path": {{Json(source)}} }"""));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains("'Rate'", error.Details!["reason"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{ "op": "import_range", "sheet": "Second", "path": "{source}", "from": "Linked!A1", "to": "Second!B2", "content": "all" }""", "Second", "B2")]
    [InlineData("""{ "op": "import_sheet", "sheet": "Linked", "path": "{source}" }""", "Linked", "A1")]
    public void Import_AReferenceToAThirdWorkbookKeepsItsLinkAndCachedResultWithoutReadingIt(
        string operation, string sheet, string cell)
    {
        string source = CreateThirdPartyLinkedSource(operation.Contains("import_sheet", StringComparison.Ordinal) ? "linked-sheet" : "linked-range");
        string target = _fixture.CreateSalesWorkbook(Path.GetFileName(Path.GetDirectoryName(source)) + "-target.xlsx");

        using Workbook result = Apply(target, operation.Replace("\"{source}\"", Json(source), StringComparison.Ordinal));

        Cell imported = result.Worksheets[sheet].Cells[cell];
        Assert.EndsWith("[rates.xlsx]Sheet1'!$A$1", imported.Formula, StringComparison.Ordinal);
        ExternalLink link = Assert.Single(result.Worksheets.ExternalLinks.Cast<ExternalLink>());
        Assert.EndsWith("rates.xlsx", link.DataSource, StringComparison.OrdinalIgnoreCase);
        // rates.xlsx now holds 99 and stays readable, so any read of it would show here.
        Assert.Equal(2d, imported.DoubleValue);
    }

    [Theory]
    [InlineData("""{ "op": "import_range", "sheet": "Second", "path": "{source}", "from": "Linked!A1:A2", "to": "Second!B2", "content": "all" }""", "Second", "B2")]
    [InlineData("""{ "op": "import_sheet", "sheet": "Linked", "path": "{source}", "name": "Copied" }""", "Copied", "A1")]
    public void Import_WarnsWhenALinkWithoutCachedValuesNoLongerEvaluatesToAnError(string operation, string sheet, string cell)
    {
        string source = CreateUncachedLinkSource(sheet + "-uncached-source.xlsx");

        EditResult result = ApplyResult(
            _fixture.CreateSalesWorkbook(sheet + "-uncached.xlsx"),
            operation.Replace("\"{source}\"", Json(source), StringComparison.Ordinal));

        Warning warning = Assert.Single(result.Warnings!, static warning => warning.Code == "EXTERNAL_LINK_CACHE_MISSING");
        Assert.Equal($"'{sheet}'!{cell}", warning.Location);
        Assert.Contains("missing-rates.xlsx", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_VerificationReportsALinkWithoutCachedValuesAsAnIssue()
    {
        string source = CreateUncachedLinkSource("verify-uncached-source.xlsx");

        EditResult result = ApplyResult(
            _fixture.CreateSalesWorkbook("verify-uncached.xlsx"),
            $$"""{ "op": "import_sheet", "sheet": "Linked", "path": {{Json(source)}} }""",
            verify: true);

        Assert.False(result.Verification!.Ok);
        VerificationIssue issue = Assert.Single(result.Verification.Issues, static issue => issue.Code == "EXTERNAL_LINK_CACHE_MISSING");
        Assert.Equal("'Linked'!A1", issue.Location);
        Assert.Contains(result.Warnings!, static warning => warning.Code == "EXTERNAL_LINK_CACHE_MISSING");
    }

    [Fact]
    public void Import_TheCacheCheckChangesNoStoredValueWithoutRecalculation()
    {
        string source = CreateUncachedLinkSource("no-recalc-uncached-source.xlsx");

        EditResult result = ApplyResult(
            _fixture.CreateSalesWorkbook("no-recalc-uncached.xlsx"),
            $$"""{ "op": "import_sheet", "sheet": "Linked", "path": {{Json(source)}} }""",
            recalculate: false);

        using var output = new Workbook(result.Output!.Path);
        Aspose.Cells.Cells cells = output.Worksheets["Linked"].Cells;
        Assert.Equal(("#REF!", "#REF!"), (cells["A1"].StringValue, cells["A2"].StringValue));
        Assert.Contains(result.Warnings!, static warning => warning.Code == "EXTERNAL_LINK_CACHE_MISSING");
    }

    [Fact]
    public void ImportRange_ChecksOnlyTheImportedCells()
    {
        string source = CreateUncachedLinkSource("outside-uncached-source.xlsx");

        EditResult result = ApplyResult(
            _fixture.CreateSalesWorkbook("outside-uncached.xlsx"),
            $$"""{ "op": "import_range", "sheet": "Second", "path": {{Json(source)}}, "from": "Linked!B1:B2", "to": "Second!B2", "content": "all" }""");

        Assert.DoesNotContain(result.Warnings ?? [], static warning => warning.Code == "EXTERNAL_LINK_CACHE_MISSING");
    }

    /// <summary>
    /// A source whose Linked!A1 reads a workbook that never existed, written as set_formula
    /// writes it, so the link caches nothing: A1 and A2 (=A1*2) show #REF!. B1:B2 hold values.
    /// </summary>
    private string CreateUncachedLinkSource(string fileName)
    {
        string source = _fixture.Temp.File(fileName);
        using var workbook = new Workbook();
        Worksheet linked = workbook.Worksheets[0];
        linked.Name = "Linked";
        linked.Cells["A1"].Formula = "='[missing-rates.xlsx]Rates'!$B$2";
        linked.Cells["A2"].Formula = "=A1*2";
        linked.Cells["B1"].PutValue("plain");
        linked.Cells["B2"].PutValue(3);
        workbook.CalculateFormula();
        workbook.Save(source, SaveFormat.Xlsx);
        return source;
    }
    [Fact]
    public void Import_ALinkWithCachedValuesAddsNoCacheWarning()
    {
        string source = CreateThirdPartyLinkedSource("linked-cached-warning");

        EditResult result = ApplyResult(
            _fixture.CreateSalesWorkbook("linked-cached-warning-target.xlsx"),
            $$"""{ "op": "import_sheet", "sheet": "Linked", "path": {{Json(source)}} }""");

        Assert.DoesNotContain(result.Warnings ?? [], static warning => warning.Code == "EXTERNAL_LINK_CACHE_MISSING");
    }

    [Theory]
    [InlineData(null, "PASSWORD_REQUIRED")]
    [InlineData("wrong", "PASSWORD_INVALID")]
    public void Import_AnEncryptedSourcesPasswordErrorPointsAtPasswordEnv(string? password, string code)
    {
        string source = CreateSource($"encrypted-{code}.xlsx");
        using (var workbook = new Workbook(source))
        {
            workbook.Settings.Password = "right";
            workbook.Save(source, SaveFormat.Xlsx);
        }

        string passwordEnv = password is null ? string.Empty : """, "passwordEnv": "SOURCE_PWD" """;
        CliException error = Assert.Throws<CliException>(() => _fixture.Engine.ApplyOps(
            _fixture.CreateSalesWorkbook($"encrypted-{code}-target.xlsx"),
            Parse($$"""{ "ops": [ { "op": "import_sheet", "sheet": "Totals", "path": {{Json(source)}}{{passwordEnv}} } ] }"""),
            new EditRequest
            {
                Output = TestOutput.At(_fixture.Temp.File($"encrypted-{code}.out.xlsx"), overwrite: true),
                OpSecrets = password is null ? null : new Dictionary<string,
                string> { ["SOURCE_PWD"] = password },
            }));

        Assert.Equal(code, error.Code.Name);
        Assert.Contains("\"passwordEnv\"", error.Hint, StringComparison.Ordinal);
        Assert.DoesNotContain("--password-env", error.Hint, StringComparison.Ordinal);
        Assert.Equal(source, error.Details!["path"]!.GetValue<string>());
    }

    [Fact]
    public void ImportRange_ValuesOfAThirdWorkbookReferenceAddNoLink()
    {
        string source = CreateThirdPartyLinkedSource("linked-values");

        using Workbook result = Apply(
            _fixture.CreateSalesWorkbook("linked-values-target.xlsx"),
            $$"""{ "op": "import_range", "sheet": "Second", "path": {{Json(source)}}, "from": "Linked!A1", "to": "B2" }""");

        Cell imported = result.Worksheets["Second"].Cells["B2"];
        Assert.False(imported.IsFormula);
        Assert.Equal(2d, imported.DoubleValue);
        Assert.Equal(0, result.Worksheets.ExternalLinks.Count);
    }

    [Fact]
    public void ImportSheet_ReferencesToTheSourceBecomeLocalWhenThisWorkbookAlreadyLinksToIt()
    {
        string source = CreateSource("linked-back-source.xlsx");
        string target = _fixture.CreateSalesWorkbook("linked-back.xlsx");
        using (var workbook = new Workbook(target))
        {
            workbook.Worksheets["Data"].Cells["E1"].Formula =
                $"='{Path.GetDirectoryName(source)}{Path.DirectorySeparatorChar}[{Path.GetFileName(source)}]Rates'!A1";
            workbook.Save(target);
        }

        using Workbook result = Apply(
            target,
            $$"""
            { "op": "import_sheet", "sheet": "Rates", "path": {{Json(source)}} },
            { "op": "import_sheet", "sheet": "Totals", "path": {{Json(source)}} }
            """);

        Assert.Equal("=B4*Rates!A1", result.Worksheets["Totals"].Cells["B5"].Formula);
        Assert.Equal(4001d, result.Worksheets["Totals"].Cells["B5"].DoubleValue);
        // Only this workbook's own link to the source remains.
        ExternalLink link = Assert.Single(result.Worksheets.ExternalLinks.Cast<ExternalLink>());
        Assert.EndsWith(Path.GetFileName(source), link.DataSource, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"[{Path.GetFileName(source)}]Rates'!A1", result.Worksheets["Data"].Cells["E1"].Formula, StringComparison.Ordinal);
    }

    private static readonly (byte R, byte G, byte B) FillColor = (0xFF, 0xF2, 0xCC);

    /// <summary>
    /// A folder with rates.xlsx (Sheet1!A1 = 2) and a source whose Linked!A1 is
    /// ='[rates.xlsx]Sheet1'!$A$1 with the cached result 2, as Excel saves it. rates.xlsx is then
    /// rewritten to hold 99 and left readable, so any read of it during the edit yields 99; a result
    /// of 2 shows the linked file was not read.
    /// </summary>
    private string CreateThirdPartyLinkedSource(string folder)
    {
        string directory = _fixture.Temp.File(folder);
        Directory.CreateDirectory(directory);
        string rates = Path.Combine(directory, "rates.xlsx");
        string source = Path.Combine(directory, "source.xlsx");
        SaveRates(2);
        using (var linked = new Workbook(rates))
        using (var workbook = new Workbook())
        {
            workbook.Worksheets[0].Name = "Linked";
            workbook.Worksheets[0].Cells["A1"].Formula = $"='{directory}{Path.DirectorySeparatorChar}[rates.xlsx]Sheet1'!$A$1";
            // Authoring only: fill the link's cached values from the linked workbook, as Excel does.
            workbook.UpdateLinkedDataSource([linked]);
            workbook.CalculateFormula();
            workbook.Save(source, SaveFormat.Xlsx);
        }

        SaveRates(99);
        return source;

        void SaveRates(int value)
        {
            using var workbook = new Workbook();
            workbook.Worksheets[0].Cells["A1"].PutValue(value);
            workbook.Save(rates, SaveFormat.Xlsx);
        }
    }

    private static (byte R, byte G, byte B) Rgb(Style style) =>
        (style.ForegroundColor.R, style.ForegroundColor.G, style.ForegroundColor.B);

    /// <summary>
    /// A source with a Notes first sheet, a Totals sheet (a formatted number column, a SUM and a
    /// formula that reads the Rates sheet) and a Rates sheet.
    /// </summary>
    private string CreateSource(string fileName)
    {
        using var workbook = new Workbook();
        workbook.Worksheets[0].Name = "Notes";
        workbook.Worksheets[0].Cells["A1"].PutValue("first-sheet");
        Worksheet totals = workbook.Worksheets.Add("Totals");
        // Rates exists before the formula that reads it; otherwise the formula links to an
        // external workbook named Rates instead of the sheet.
        workbook.Worksheets.Add("Rates").Cells["A1"].PutValue(2);
        totals.Cells["A1"].PutValue("Region");
        totals.Cells["B1"].PutValue("Sales");
        totals.Cells["A2"].PutValue("DE");
        totals.Cells["B2"].PutValue(1200.5);
        totals.Cells["A3"].PutValue("FR");
        totals.Cells["B3"].PutValue(800);
        totals.Cells["B4"].Formula = "=SUM(B2:B3)";
        totals.Cells["B5"].Formula = "=B4*Rates!A1";

        Style number = workbook.CreateStyle();
        number.Custom = "#,##0.00";
        number.ForegroundColor = System.Drawing.Color.FromArgb(FillColor.R, FillColor.G, FillColor.B);
        number.Pattern = BackgroundType.Solid;
        foreach (string cell in new[] { "B2", "B3", "B4" })
        {
            totals.Cells[cell].SetStyle(number);
        }

        workbook.CalculateFormula();
        string path = _fixture.Temp.File(fileName);
        workbook.Save(path, SaveFormat.Xlsx);
        return path;
    }

    private static string Json(string value) => JsonSerializer.Serialize(value);

    private static CellsOpsBatch Parse(string json) =>
        CellsOp.Catalog.Parse<CellsOpsBatch>(json, Aspose.Cli.Generated.ProductJsonContext.Definition);

    [Fact]
    public void ImportSheet_ChargesEachImportedSheetsObjectsToTheEditBudget()
    {
        string source = _fixture.Temp.File("shapes-source.xlsx");
        using (var created = new Workbook())
        {
            created.Worksheets[0].Shapes.AddRectangle(0, 0, 0, 0, 50, 50);
            created.Worksheets[0].Shapes.AddRectangle(4, 0, 0, 0, 50, 50);
            created.Save(source);
        }
        using var deadline = OperationDeadline.Start(null);
        var budgets = new ResourceBudgetLedger(deadline, new Dictionary<string, long>
        {
            [CellsBudgetDomains.Cells] = 1_000,
            [CellsBudgetDomains.Sheets] = 10,
            [CellsBudgetDomains.Objects] = 3,
        });
        using var sources = new CellsImportSources(new CellsWorkbookLoader(budgets), budgets, null);
        using var workbook = new Workbook();

        ImportOps.ImportSheet(workbook, new ImportSheetOp { Path = source, Name = "One" }, sources);
        CliException refused = Assert.Throws<CliException>(() =>
            ImportOps.ImportSheet(workbook, new ImportSheetOp { Path = source, Name = "Two" }, sources));

        Assert.Equal(ErrorCodes.InputBudgetExceeded, refused.Code);
        Assert.Null(workbook.Worksheets["Two"]);
    }

    private Workbook Apply(string path, string operations) => new(ApplyResult(path, operations).Output!.Path);

    private EditResult ApplyResult(string path, string operations, bool recalculate = true, bool verify = false)
    {
        EditResult result = _fixture.Engine.ApplyOps(
            path,
            Parse($$"""{ "ops": [ {{operations}} ] }"""),
            new EditRequest { Output = TestOutput.At(_fixture.Temp.File(Path.GetFileNameWithoutExtension(path) + ".out.xlsx"), overwrite: true), Recalculate = recalculate, Verify = verify });
        Assert.All(result.Applied, static outcome => Assert.Equal(OpStatuses.Ok, outcome.Status));
        return result;
    }
}
