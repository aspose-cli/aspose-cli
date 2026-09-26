using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Contracts.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// Tests against the real Aspose.Cells engine (never mocked, per the team
/// rule). The in-process engine runs licensed only (see <see cref="CellsFixture"/>);
/// CellsCliTests covers what evaluation mode discloses.
/// </summary>
public sealed class CellsEngineTests : IClassFixture<CellsFixture>
{
    private readonly CellsFixture _fixture;

    public CellsEngineTests(CellsFixture fixture) => _fixture = fixture;

    private static SheetInfo FindSheet(WorkbookInfoResult result, string name) =>
        Assert.Single(result.Workbook.Sheets, sheet => sheet.Name == name);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EditOwnsDocumentBackupAndEvidenceAcrossSiblingDirectories(bool verify)
    {
        using var temp = new Aspose.Cli.TestKit.TempDirectory();
        string documents = Path.Combine(temp.Path, "documents");
        Directory.CreateDirectory(documents);
        string source = Path.Combine(documents, "book.xlsx");
        File.Copy(_fixture.CreateSalesWorkbook($"backup-root-{verify}.xlsx"), source);
        byte[] original = File.ReadAllBytes(source);
        string backup = Path.Combine(temp.Path, "backups", "original.xlsx");
        EditResult result = _fixture.Engine.ApplyOps(source,
            ParseOps("""{"ops":[{"op":"set_values","sheet":"Data","range":"B2","values":[[7]]}]}"""),
            new EditRequest
            {
                OutputPath = source, Overwrite = true, BackupPath = backup,
                Verify = verify,
            });
        Assert.Equal(original, File.ReadAllBytes(backup));
        Assert.Equal(backup, result.Backup!.Path);
        Assert.Equal(source, result.Output!.Path);
        using var reopened = new Aspose.Cells.Workbook(source);
        Assert.Equal(7, reopened.Worksheets["Data"].Cells["B2"].IntValue);
        if (verify)
        {
            Assert.Equal("B2", Assert.Single(result.Verification!.DirectChanges).Cell);
        }
    }

    [Fact]
    public void TextCreationReportsTheSameSheetLossAsConversionAndEditing()
    {
        CreateResult result = _fixture.Engine.Create(new NewWorkbookRequest
        {
            OutputPath = _fixture.Temp.File("multiple.csv"), SheetNames = ["One", "Two"],
        });
        Assert.Contains(result.Warnings ?? [], warning => warning.Code == "SHEETS_DROPPED" && warning.AffectsCompleteness);
    }

    [Fact]
    public void TextEditCommitsButDoesNotCertifyAnImplicitlyDroppedWorksheet()
    {
        string source = _fixture.CreateSalesWorkbook("text-loss.xlsx");
        EditResult result = _fixture.Engine.ApplyOps(source,
            ParseOps("""{"ops":[{"op":"set_values","sheet":"Data","range":"B2","values":[[7]]}]}"""),
            new EditRequest { OutputPath = _fixture.Temp.File("text-loss.csv"), Verify = true });
        Assert.True(File.Exists(result.Output!.Path));
        Assert.Contains(result.Warnings ?? [], warning => warning.Code == "SHEETS_DROPPED");
        Assert.False(result.Verification!.Ok);
        Assert.Contains(result.Verification.Issues!, issue => issue.Code == "SHEETS_DROPPED");
    }

    [Fact]
    public void GetInfo_ReportsSheetStructure()
    {
        string path = _fixture.CreateSalesWorkbook("info.xlsx");

        WorkbookInfoResult result = _fixture.Engine.GetInfo(path, new InfoRequest());

        Assert.Equal("workbook", result.Kind);
        Assert.Equal("xlsx", result.Source.Format);
        Assert.True(result.Source.SizeBytes > 0);

        SheetInfo data = FindSheet(result, "Data");
        Assert.Equal("A1:C3", data.UsedRange);
        Assert.Equal(3, data.RowCount);
        Assert.Equal(3, data.ColumnCount);
        Assert.False(data.Hidden);
        Assert.Null(data.Preview);

        Assert.True(FindSheet(result, "Backstage").Hidden);
        Assert.False(result.Workbook.HasVba);
    }

    [Fact]
    public void GetInfo_WithPreview_ReturnsDisplayValues()
    {
        string path = _fixture.CreateSalesWorkbook("preview.xlsx");

        WorkbookInfoResult result = _fixture.Engine.GetInfo(
            path, new InfoRequest { IncludePreview = true, PreviewRows = 2 });

        SheetInfo data = FindSheet(result, "Data");
        Assert.NotNull(data.Preview);
        Assert.Equal(2, data.Preview.Count);
        Assert.Equal(["Region", "Q1", "Q2"], data.Preview[0]);
        Assert.Equal(["East", "1200", "1500"], data.Preview[1]);
    }

    [Fact]
    public void GetInfo_WithDetails_ReturnsNamesFormulaErrorsAndFonts()
    {
        string path = _fixture.CreateWorkbookWithDetails("details.xlsx");

        WorkbookInfoResult result = _fixture.Engine.GetInfo(
            path, new InfoRequest { Details = [InfoDetails.Names, InfoDetails.Errors, InfoDetails.Fonts] });

        Assert.NotNull(result.Workbook.DefinedNames);
        Assert.Contains(result.Workbook.DefinedNames, n => n.Name == "Threshold");

        Assert.NotNull(result.Workbook.FormulaErrors);
        CellError error = Assert.Single(result.Workbook.FormulaErrors);
        Assert.Equal("Data", error.Sheet);
        Assert.Equal("A3", error.Cell);
        Assert.Equal("#DIV/0!", error.Error);

        // Every workbook uses at least its default font.
        Assert.NotNull(result.Workbook.Fonts);
        Assert.NotEmpty(result.Workbook.Fonts);
    }

    // A real corpus xlsb held 19,318 formula errors; the old scan returned exactly
    // 1,000 from the first sheet with no signal, so later sheets looked clean. The
    // list stays capped, but the honest total now rides a LIST_TRUNCATED warning.
    [Fact]
    public void GetInfo_WithErrorsDetail_PastTheCap_WarnsWithTheHonestTotal()
    {
        string path = _fixture.Temp.File("many-errors.xlsx");
        using (var workbook = new Aspose.Cells.Workbook())
        {
            Aspose.Cells.Cells cells = workbook.Worksheets[0].Cells;
            for (int i = 0; i < 1001; i++)
            {
                cells[i, 0].Formula = "=1/0"; // #DIV/0!
            }

            workbook.CalculateFormula();
            workbook.Save(path, Aspose.Cells.SaveFormat.Xlsx);
        }

        WorkbookInfoResult result = _fixture.Engine.GetInfo(
            path, new InfoRequest { Details = [InfoDetails.Errors] });

        // The list is capped, but the warning reports the true count.
        Assert.Equal(1000, result.Workbook.FormulaErrors!.Count);
        Warning? warning = result.Warnings?.FirstOrDefault(w => w.Code == WarningCodes.ListTruncated);
        Assert.NotNull(warning);
        Assert.Contains("1001", warning!.Message, StringComparison.Ordinal);
        Assert.NotNull(warning.Hint);
    }

    [Fact]
    public void GetInfo_WithoutDetails_OmitsDetailSections()
    {
        string path = _fixture.CreateWorkbookWithDetails("no-details.xlsx");

        WorkbookInfoResult result = _fixture.Engine.GetInfo(path, new InfoRequest());

        Assert.Null(result.Workbook.DefinedNames);
        Assert.Null(result.Workbook.FormulaErrors);
    }

    [Fact]
    public void GetInfo_ReportsTheLicensedMode()
    {
        string path = _fixture.CreateSalesWorkbook("license.xlsx");

        WorkbookInfoResult result = _fixture.Engine.GetInfo(path, new InfoRequest());

        Assert.Equal(LicenseModes.Licensed, result.License?.Mode);
        // Read-only operations never carry the evaluation warning.
        Assert.Null(result.Warnings);
    }

    [Fact]
    public void Convert_ToPdf_ProducesAPdfDocument()
    {
        string path = _fixture.CreateSalesWorkbook("to-pdf.xlsx");
        string output = _fixture.Temp.File("out.pdf");

        ConvertResult result = _fixture.Engine.Convert(path, new ConvertRequest
        {
            TargetFormatId = "pdf",
            OutputPath = output,
        });

        Assert.Equal(output, result.Output.Path);
        Assert.True(HasHeader(output, [0x25, 0x50, 0x44, 0x46]));
        Assert.Equal(new FileInfo(output).Length, result.Output.SizeBytes);
    }

    [Fact]
    public void Convert_PreservesInputFormat_WhenSourceIsCsv()
    {
        // Regression: Workbook.FileFormat mutates on save; the reported input
        // format must reflect the file that was read, not the one written.
        string path = _fixture.CreateCsv("input-format.csv");
        string output = _fixture.Temp.File("from-csv.xlsx");

        ConvertResult result = _fixture.Engine.Convert(path, new ConvertRequest
        {
            TargetFormatId = "xlsx",
            OutputPath = output,
        });

        Assert.Equal("csv", result.Input.Format);
        Assert.Equal("xlsx", result.Output.Format);
    }

    [Fact]
    public void Convert_LicensedOutput_CarriesNoEvaluationWarning()
    {
        string path = _fixture.CreateSalesWorkbook("warnings.xlsx");
        string output = _fixture.Temp.File("warnings.csv");

        ConvertResult result = _fixture.Engine.Convert(path, new ConvertRequest
        {
            TargetFormatId = "csv",
            OutputPath = output,
        });

        // The sales fixture is multi-sheet, so a csv export also carries a
        // SHEETS_DROPPED warning; only the evaluation warning must be absent.
        Assert.DoesNotContain(result.Warnings ?? [], static warning => warning.Code == WarningCodes.EvalMode);
    }

    [Fact]
    public void Convert_UnknownSheet_ThrowsSheetNotFoundWithAlternatives()
    {
        string path = _fixture.CreateSalesWorkbook("bad-sheet.xlsx");

        CliException exception = Assert.Throws<CliException>(() => _fixture.Engine.Convert(path, new ConvertRequest
        {
            TargetFormatId = "csv",
            OutputPath = _fixture.Temp.File("never.csv"),
            SheetName = "Dat",
        }));

        Assert.Equal("SHEET_NOT_FOUND", exception.Code.Name);
        Assert.Equal("Dat", exception.Details!["requested"]!.GetValue<string>());
        Assert.Contains("Data", exception.Details["available"]!.AsArray().Select(static name => name!.GetValue<string>()));
        Assert.Equal("Data", exception.Details["suggestions"]![0]!.GetValue<string>());
    }

    // The legacy xls grid is 65,536 rows × 256 columns. A modern source that
    // overflows it loses everything past the limit on save — the engine does it
    // silently, so the CLI must WARN (a real 200,001-row corpus file lost 134,465
    // rows to an exit-0 convert with no warning at all).
    [Fact]
    public void Convert_ToXls_RowsBeyondTheGrid_WarnsDataTruncated()
    {
        string src = _fixture.Temp.File("tall.xlsx");
        using (var workbook = new Aspose.Cells.Workbook())
        {
            workbook.Worksheets[0].Cells["A1"].PutValue("keep");
            workbook.Worksheets[0].Cells["A65537"].PutValue("lost"); // one row past the xls grid
            workbook.Save(src, Aspose.Cells.SaveFormat.Xlsx);
        }

        ConvertResult result = _fixture.Engine.Convert(src, new ConvertRequest
        {
            TargetFormatId = "xls",
            OutputPath = _fixture.Temp.File("tall.xls"),
            Overwrite = true,
        });

        Warning? warning = result.Warnings?.FirstOrDefault(w => w.Code == "DATA_TRUNCATED");
        Assert.NotNull(warning);
        Assert.Contains("row", warning!.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(warning.Hint);
    }

    [Fact]
    public void Convert_ToXls_ColumnsBeyondTheGrid_WarnsDataTruncated()
    {
        string src = _fixture.Temp.File("wide.xlsx");
        using (var workbook = new Aspose.Cells.Workbook())
        {
            workbook.Worksheets[0].Cells[0, 0].PutValue("keep");
            workbook.Worksheets[0].Cells[0, 256].PutValue("lost"); // column 257, one past the 256-col grid
            workbook.Save(src, Aspose.Cells.SaveFormat.Xlsx);
        }

        ConvertResult result = _fixture.Engine.Convert(src, new ConvertRequest
        {
            TargetFormatId = "xls",
            OutputPath = _fixture.Temp.File("wide.xls"),
            Overwrite = true,
        });

        Warning? warning = result.Warnings?.FirstOrDefault(w => w.Code == "DATA_TRUNCATED");
        Assert.NotNull(warning);
        Assert.Contains("column", warning!.Message, StringComparison.OrdinalIgnoreCase);
    }

    // A sheet that fits (and a modern target that never truncates) must NOT warn.
    [Fact]
    public void Convert_ToXls_WithinTheGrid_DoesNotWarnTruncated()
    {
        string src = _fixture.CreateSalesWorkbook("small.xlsx");

        ConvertResult result = _fixture.Engine.Convert(src, new ConvertRequest
        {
            TargetFormatId = "xls",
            OutputPath = _fixture.Temp.File("small.xls"),
            Overwrite = true,
        });

        Assert.DoesNotContain(result.Warnings ?? [], w => w.Code == "DATA_TRUNCATED");
    }

    [Fact]
    public void Convert_ToXlsb_RowsBeyondTheXlsGrid_DoesNotWarn()
    {
        // xlsb keeps the modern 1,048,576-row grid, so a 65,537-row sheet fits.
        string src = _fixture.Temp.File("tall2.xlsx");
        using (var workbook = new Aspose.Cells.Workbook())
        {
            workbook.Worksheets[0].Cells["A65537"].PutValue("fits in xlsb");
            workbook.Save(src, Aspose.Cells.SaveFormat.Xlsx);
        }

        ConvertResult result = _fixture.Engine.Convert(src, new ConvertRequest
        {
            TargetFormatId = "xlsb",
            OutputPath = _fixture.Temp.File("tall2.xlsb"),
            Overwrite = true,
        });

        Assert.DoesNotContain(result.Warnings ?? [], w => w.Code == "DATA_TRUNCATED");
    }

    // The same silent truncation lurks in every write path, not just convert:
    // editing or recalculating a modern-sized workbook out to .xls drops the
    // overflow too. The shared Save helper now warns for all of them.
    [Fact]
    public void Edit_ToXls_RowsBeyondTheGrid_WarnsDataTruncated()
    {
        string src = _fixture.Temp.File("edit-tall.xlsx");
        using (var workbook = new Aspose.Cells.Workbook())
        {
            workbook.Worksheets[0].Cells["A65537"].PutValue("lost");
            workbook.Save(src, Aspose.Cells.SaveFormat.Xlsx);
        }

        EditResult result = _fixture.Engine.ApplyOps(
            src,
            ParseOps("""{ "ops": [ { "op": "set_values", "sheet": "Sheet1", "range": "A1", "values": [["x"]] } ] }"""),
            new EditRequest { OutputPath = _fixture.Temp.File("edit-tall.xls"), Overwrite = true });

        Assert.Contains(result.Warnings ?? [], w => w.Code == "DATA_TRUNCATED");
    }

    [Fact]
    public void FormulaOnlyEdit_ToXls_RowsBeyondTheGrid_WarnsDataTruncated()
    {
        string src = _fixture.Temp.File("calc-tall.xlsx");
        using (var workbook = new Aspose.Cells.Workbook())
        {
            workbook.Worksheets[0].Cells["A65537"].PutValue("lost");
            workbook.Save(src, Aspose.Cells.SaveFormat.Xlsx);
        }

        EditResult result = _fixture.Engine.ApplyOps(
            src,
            ParseOps("""{ "ops": [ { "op": "set_formula", "range": "B1", "formula": "=1" } ] }"""),
            new EditRequest { OutputPath = _fixture.Temp.File("calc-tall.xls"), Overwrite = true });

        Assert.Contains(result.Warnings ?? [], w => w.Code == "DATA_TRUNCATED");
    }

    // A whole-column total (=SUM(A5:A1048576)) references rows past the xls grid.
    // The engine's save to xls rewrites it to =SUM(#REF!) — silent formula
    // corruption on a real GST-return template (29 such totals). The convert path
    // now measures the rise in #REF! formulas the save produced and warns.
    [Fact]
    public void Convert_ToXls_FormulaReferencingBeyondTheGrid_WarnsFormulasBroken()
    {
        string src = _fixture.Temp.File("wholecol.xlsx");
        using (var workbook = new Aspose.Cells.Workbook())
        {
            Aspose.Cells.Cells cells = workbook.Worksheets[0].Cells;
            cells["A5"].PutValue(5);
            cells["B1"].Formula = "=SUM(A5:A1048576)"; // endpoint past the 65,536-row xls grid
            workbook.CalculateFormula();
            workbook.Save(src, Aspose.Cells.SaveFormat.Xlsx);
        }

        ConvertResult result = _fixture.Engine.Convert(src, new ConvertRequest
        {
            TargetFormatId = "xls",
            OutputPath = _fixture.Temp.File("wholecol.xls"),
            Overwrite = true,
        });

        Warning? warning = result.Warnings?.FirstOrDefault(w => w.Code == "FORMULAS_BROKEN");
        Assert.NotNull(warning);
        Assert.Contains("#REF!", warning!.Message, StringComparison.Ordinal);
        Assert.NotNull(warning.Hint);
    }

    // The same formula converted to a modern grid stays intact — no false warning.
    [Fact]
    public void Convert_ToXlsb_FormulaReferencingBeyondTheXlsGrid_DoesNotWarnBroken()
    {
        string src = _fixture.Temp.File("wholecol2.xlsx");
        using (var workbook = new Aspose.Cells.Workbook())
        {
            Aspose.Cells.Cells cells = workbook.Worksheets[0].Cells;
            cells["A5"].PutValue(5);
            cells["B1"].Formula = "=SUM(A5:A1048576)";
            workbook.CalculateFormula();
            workbook.Save(src, Aspose.Cells.SaveFormat.Xlsx);
        }

        ConvertResult result = _fixture.Engine.Convert(src, new ConvertRequest
        {
            TargetFormatId = "xlsb",
            OutputPath = _fixture.Temp.File("wholecol2.xlsb"),
            Overwrite = true,
        });

        Assert.DoesNotContain(result.Warnings ?? [], w => w.Code == "FORMULAS_BROKEN");
    }

    [Fact]
    public void Render_ToPng_ProducesAPngImage()
    {
        string path = _fixture.CreateSalesWorkbook("render.xlsx");
        string output = _fixture.Temp.File("sheet.png");

        RenderResult result = _fixture.Engine.Render(path, new RenderRequest
        {
            TargetFormatId = "png",
            OutputPath = output,
            SheetName = "Data",
            Range = new RangeRef(new CellRef(0, 0), new CellRef(2, 2)),
            Dpi = 96,
        });

        Assert.Equal("Data", result.Sheet);
        Assert.Equal("A1:C3", result.Range);
        Assert.Equal(96, result.Dpi);
        Assert.True(HasHeader(output, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]));
    }

    [Fact]
    public void Render_ToSvg_OmitsDpi()
    {
        string path = _fixture.CreateSalesWorkbook("render-svg.xlsx");
        string output = _fixture.Temp.File("sheet.svg");

        RenderResult result = _fixture.Engine.Render(path, new RenderRequest
        {
            TargetFormatId = "svg",
            OutputPath = output,
            SheetName = "Data",
        });

        Assert.Null(result.Dpi);
        Assert.True(new FileInfo(output).Length > 0);
    }

    [Fact]
    public void Open_EncryptedWithoutPassword_ReportsPasswordRequired()
    {
        string path = _fixture.CreateEncryptedWorkbook("secret", "locked1.xlsx");

        CliException exception = Assert.Throws<CliException>(
            () => _fixture.Engine.GetInfo(path, new InfoRequest()));

        Assert.Equal(ErrorCodes.PasswordRequired, exception.Code);
    }

    [Fact]
    public void Open_EncryptedWithWrongPassword_ReportsPasswordInvalid()
    {
        string path = _fixture.CreateEncryptedWorkbook("secret", "locked2.xlsx");

        CliException exception = Assert.Throws<CliException>(
            () => _fixture.Engine.GetInfo(path, new InfoRequest { Password = "wrong" }));

        Assert.Equal(ErrorCodes.PasswordInvalid, exception.Code);
    }

    [Fact]
    public void Open_EncryptedWithCorrectPassword_Succeeds()
    {
        string path = _fixture.CreateEncryptedWorkbook("secret", "locked3.xlsx");

        WorkbookInfoResult result = _fixture.Engine.GetInfo(path, new InfoRequest { Password = "secret" });

        Assert.True(result.Workbook.SheetCount >= 1);
    }

    [Fact]
    public void Open_GarbageBytes_ReportsFileCorrupt()
    {
        string path = _fixture.Temp.File("garbage.xlsx");
        File.WriteAllBytes(path, [0x00, 0x01, 0x02, 0x03, 0xFF, 0xFE]);

        CliException exception = Assert.Throws<CliException>(
            () => _fixture.Engine.GetInfo(path, new InfoRequest()));

        Assert.Equal(ErrorCodes.FileCorrupt, exception.Code);
    }

    private static bool HasHeader(string path, byte[] expected)
    {
        byte[] actual = new byte[expected.Length];
        using FileStream stream = File.OpenRead(path);
        return stream.Read(actual, 0, actual.Length) == actual.Length
            && actual.AsSpan().SequenceEqual(expected);
    }

    private static CellsOpsBatch ParseOps(string json) =>
        CellsOp.Catalog.Parse<CellsOpsBatch>(json, Aspose.Cli.Generated.ProductJsonContext.Definition);
}
