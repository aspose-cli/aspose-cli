using Aspose.Cells;
using Aspose.Cli.Sdk.Configuration;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// Shared real-engine setup. Fixtures are saved through the same license gate
/// as the engine so every test remains valid in licensed and evaluation modes.
/// </summary>
public sealed class CellsFixture : IDisposable
{
    public CellsFixture()
    {
        LicenseResolution resolution = LicenseResolver.Resolve(
            flagPath: null,
            productId: "cells",
            Environment.GetEnvironmentVariable,
            Directory.GetCurrentDirectory(),
            ConfigurationPaths.UserDirectory());
        Gate = new CellsLicenseGate(resolution, Environment.GetEnvironmentVariable);
    }

    public ILicenseGate Gate { get; }

    internal CellsWorkbookEngine Engine =>
        ProductTestBudgets.StartEngine<CellsModule, CellsWorkbookEngine>(
            (budgets, writer) => new CellsWorkbookEngine(Gate, budgets, writer));

    public TempDirectory Temp { get; } = new();

    public LicenseState LicenseState => Gate.EnsureApplied();

    public string CreateSalesWorkbook(string fileName = "sales.xlsx")
    {
        Gate.EnsureApplied();

        using var workbook = new Workbook();
        Worksheet data = workbook.Worksheets[0];
        data.Name = "Data";
        data.Cells["A1"].PutValue("Region");
        data.Cells["B1"].PutValue("Q1");
        data.Cells["C1"].PutValue("Q2");
        data.Cells["A2"].PutValue("East");
        data.Cells["B2"].PutValue(1200);
        data.Cells["C2"].PutValue(1500);
        data.Cells["A3"].PutValue("Total");
        data.Cells["B3"].Formula = "=SUM(B2:B2)";
        data.Cells["C3"].Formula = "=SUM(C2:C2)";
        workbook.CalculateFormula();

        Style headerStyle = workbook.CreateStyle();
        headerStyle.Font.IsBold = true;
        for (int column = 0; column <= 2; column++)
        {
            data.Cells[0, column].SetStyle(headerStyle);
        }

        Worksheet second = workbook.Worksheets.Add("Second");
        second.Cells["A1"].PutValue("second-sheet-marker");

        Worksheet hidden = workbook.Worksheets.Add("Backstage");
        hidden.Cells["A1"].PutValue("hidden");
        hidden.IsVisible = false;

        string path = Temp.File(fileName);
        workbook.Save(path, SaveFormat.Xlsx);
        return path;
    }

    public string CreateWorkbookWithDetails(string fileName = "details.xlsx")
    {
        Gate.EnsureApplied();

        using var workbook = new Workbook();
        Worksheet sheet = workbook.Worksheets[0];
        sheet.Name = "Data";
        sheet.Cells["A1"].PutValue(10);
        sheet.Cells["A2"].PutValue(0);
        sheet.Cells["A3"].Formula = "=A1/A2";

        int index = workbook.Worksheets.Names.Add("Threshold");
        workbook.Worksheets.Names[index].RefersTo = "=Data!$A$1";
        workbook.CalculateFormula();

        string path = Temp.File(fileName);
        workbook.Save(path, SaveFormat.Xlsx);
        return path;
    }

    public string CreateEncryptedWorkbook(string password, string fileName = "secret.xlsx")
    {
        Gate.EnsureApplied();

        using var workbook = new Workbook();
        workbook.Worksheets[0].Cells["A1"].PutValue("classified");
        workbook.Settings.Password = password;

        string path = Temp.File(fileName);
        workbook.Save(path, SaveFormat.Xlsx);
        return path;
    }

    public string CreateCsv(string fileName = "plain.csv")
    {
        string path = Temp.File(fileName);
        File.WriteAllText(path, "Region,Q1\nEast,1200\n");
        return path;
    }

    public void Dispose() => Temp.Dispose();
}
