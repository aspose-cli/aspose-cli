using Aspose.Cells;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.IO.Publication;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// Shared real-engine setup. The test license state is applied before any test authors a
/// workbook, so inputs are written in the state the engine reads them in.
/// </summary>
/// <remarks>
/// Aspose.Cells evaluation mode refuses to open more files once one process has opened 100, so
/// the in-process engine suite runs licensed only. The CLI suites cover evaluation mode, where
/// every command is a fresh process.
/// </remarks>
public sealed class CellsFixture : IDisposable
{
    private const string EvaluationLimit =
        "Aspose.Cells evaluation mode opens at most 100 files per process, too few for the in-process engine suite.";

    public ILicenseGate Gate { get; } = TestLicense.Apply(CellsActivation.ApplyLicense);

    /// <summary>The session of one invocation, as activation builds it.</summary>
    internal CellsSession Session
    {
        get
        {
            TestLicense.Require(EvaluationLimit);
            return ProductTestBudgets.StartEngine<CellsModule, CellsSession>(
                (budgets, writer) =>
                {
                    OutputPipeline outputs = new(Gate, new CellsEvaluationProfile(), writer);
                    return new CellsSession(outputs, budgets, new Engine.Mapping.CellsWorkbookLoader(budgets, outputs));
                });
        }
    }

    public LicenseState LicenseState => Gate.EnsureApplied();

    /// <summary>
    /// Skips a test that opens workbooks in this process without a license, where evaluation mode
    /// opens at most 100 files per process; call it before the test opens one.
    /// </summary>
    public static void RequireLicense() => TestLicense.Require(EvaluationLimit);

    public TempDirectory Temp { get; } = new();

    public string File(string name) => Temp.File(name);

    /// <summary>
    /// The workbook of the common tasks: a sheet whose name has a space and Chinese, a 6 by 6 table
    /// of four regions by four quarters with sums in its last column and row, and two empty cells.
    /// </summary>
    public string CreateSales(string name = "sales.xlsx")
    {
        using var workbook = new Workbook();
        Worksheet sheet = workbook.Worksheets[0];
        sheet.Name = "销售 Sales";
        string[] header = ["区域 Region", "Q1", "Q2", "Q3", "Q4", "合计 Total"];
        for (int column = 0; column < header.Length; column++)
        {
            sheet.Cells[0, column].PutValue(header[column]);
        }

        object?[][] rows =
        [
            ["华东 East", 1250.0, 1380.0, 1420.0, 1465.0],
            ["华南 South", 1100.0, 1180.0, null, null],
            ["华北 North", 980.0, 1010.0, 1065.0, 1120.0],
            ["海外 Overseas", 760.0, 815.0, null, null],
        ];
        for (int row = 0; row < rows.Length; row++)
        {
            for (int column = 0; column < rows[row].Length; column++)
            {
                if (rows[row][column] is { } value)
                {
                    sheet.Cells[row + 1, column].PutValue(value);
                }
            }

            sheet.Cells[row + 1, 5].Formula = $"=SUM(B{row + 2}:E{row + 2})";
        }

        sheet.Cells["A6"].PutValue("合计 Total");
        foreach (string column in new[] { "B", "C", "D", "E", "F" })
        {
            sheet.Cells[$"{column}6"].Formula = $"=SUM({column}2:{column}5)";
        }

        Worksheet summary = workbook.Worksheets.Add("汇总 Summary");
        summary.Cells["A1"].PutValue("Revenue");
        summary.Cells["B1"].Formula = "='销售 Sales'!F6";
        workbook.CalculateFormula();
        string path = File(name);
        workbook.Save(path, SaveFormat.Xlsx);
        return path;
    }

    /// <summary>A request of <c>cells edit</c> over a parsed operation document.</summary>
    internal static TableEditRequest Edit(string input, string output, string ops) => new()
    {
        Input = input,
        Batch = TableBatch.Parse(ops, Engine.Mapping.CellsTable.Definition, new Inputs()),
        Output = new EditOutput(output),
    };

    /// <summary>The files and secrets of a batch, as the command line reads them.</summary>
    internal sealed class Inputs : ITableInputs
    {
        public string ResolvePath(string path) => Path.GetFullPath(path);

        public byte[] ReadImage(string path) => System.IO.File.ReadAllBytes(path);


        public byte[] ReadFile(string path) => System.IO.File.ReadAllBytes(path);

        public Secret? ReadSecret(string variable) => Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value ? new Secret(value) : null;
    }

    public void Dispose() => Temp.Dispose();
}

/// <summary>Outputs for engine calls in the product's declared formats.</summary>
internal static class CellsOutput
{
    /// <inheritdoc cref="ResolvedOutputs.At"/>
    public static ResolvedOutput At(string path, string? format = null, bool overwrite = false, bool inPlace = false) =>
        ResolvedOutputs.At(CellsFormats.Definitions, path, format, overwrite, inPlace);
}
