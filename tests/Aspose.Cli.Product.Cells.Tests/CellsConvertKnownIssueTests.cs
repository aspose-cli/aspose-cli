using System.Text;
using System.Text.Json;
using Aspose.Cells;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// The Aspose.Cells conversion defects in KNOWN-ISSUES.md, reproduced with the SDK alone. Each
/// passes while the pinned SDK still has its defect.
/// </summary>
public sealed class CellsConvertKnownIssueTests
{
    [LicensedFact]
    public void JsonSave_OfASheetWithOnlyAHeaderRow_WritesInvalidJson()
    {
        using var fixture = new CellsFixture();
        using var workbook = new Workbook();
        workbook.Worksheets[0].Cells["A1"].PutValue("Name");
        workbook.Worksheets[0].Cells["B1"].PutValue("Qty");
        using var stream = new MemoryStream();
        workbook.Save(stream, SaveFormat.Json);
        string json = Encoding.UTF8.GetString(stream.ToArray());

        bool invalid;
        try
        {
            using JsonDocument _ = JsonDocument.Parse(json);
            invalid = false;
        }
        catch (JsonException)
        {
            invalid = true;
        }

        KnownIssue.Reproduces("CELLS-JSON-HEADER-ONLY", invalid, $"the JSON of a header-only sheet parses: {json}");
    }
}
