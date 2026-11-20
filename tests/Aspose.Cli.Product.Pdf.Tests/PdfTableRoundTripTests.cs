using System.Text.Json.Nodes;
using Aspose.Cli.TestKit.Tables;
using Aspose.Pdf;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// The PDF table's round trip, one case per row: a settable row is set through an operation
/// document to a value other than the one it holds, the edit saves and reopens the file, and the
/// value its report reads back equals the one written; a read-only row is read. The document is
/// made by the engine with one object of every kind.
/// </summary>
public sealed class PdfTableRoundTripTests
{
    public static TheoryData<string, string> Settable => TableRoundTrip.Cases(PdfTable.Definition, settable: true);

    public static TheoryData<string, string> ReadOnly => TableRoundTrip.Cases(PdfTable.Definition, settable: false);

    /// <summary>The rows whose sample is the member's, not the value kind's.</summary>
    private static readonly Dictionary<(string Kind, string Row), JsonNode> Samples = new()
    {
        [("page", "mediaBox")] = "0,0,500,700",
        [("page", "cropBox")] = "10,10,400,600",
        [("annotation", "rect")] = "60,410,210,430",
        [("annotation", "color")] = "#FF0000",
        [("annotation", "action.uri")] = "https://example.org/trip",
        [("bookmark", "color")] = "#12AB34",
        [("field", "value")] = "Trip",
        [("attachment", "mimeType")] = "text/plain",
        [("pageLabel", "startingValue")] = 7,
    };

    /// <summary>The objects a row is written on or read from when the first object that has it is not one read lists.</summary>
    private static readonly Dictionary<string, string> Addresses = new()
    {
        ["area"] = "page/1/area/0,0,595,842",
        ["taggedContent"] = "taggedContent",
    };

    /// <summary>The rows the engine does not keep as written, each with its known issue.</summary>
    private static readonly Dictionary<(string Kind, string Row), string> EngineDifferences = new()
    {
        [("attachment", "description")] = "PDF-ATTACHMENT-DESCRIPTION-NOT-SAVED: the engine does not save a description set on an attachment it read",
    };

    [Theory]
    [MemberData(nameof(Settable))]
    public void SettableRow_ReadsBackFromTheSavedFile(string kind, string row)
    {
        PdfEngineFixture.RequireLicense();
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateRich();
        string at;
        JsonNode? current;
        using (var document = new Document(input))
        {
            at = Address(document, kind, row);
            current = TableRoundTrip.Read(PdfTable.Definition, document, at)[row]?.DeepClone();
        }

        JsonNode write = Samples.TryGetValue((kind, row), out JsonNode? special)
            ? special.DeepClone()
            : TableRoundTrip.Sample(TableRoundTrip.ValueOf(PdfTable.Definition, kind, row), current);
        string ops = new JsonObject { ["ops"] = new JsonArray(new JsonObject { ["set"] = at, ["props"] = new JsonObject { [row] = write.DeepClone() } }) }.ToJsonString();

        PdfEditResult result = PdfEdit.Run(fixture.Session, PdfEngineFixture.Edit(input, fixture.File("out.pdf"), ops));
        JsonNode? now = Assert.Single(result.Ops).Now![row];
        if (EngineDifferences.TryGetValue((kind, row), out string? issue))
        {
            Assert.False(JsonNode.DeepEquals(now, write), $"{kind}.{row} now reads back as written; it was listed because {issue}.");
            return;
        }

        Assert.True(JsonNode.DeepEquals(now, write), $"{kind}.{row} at {at}: wrote {write.ToJsonString()}, read back {now?.ToJsonString() ?? "null"} (was {current?.ToJsonString() ?? "null"}).");
    }

    [Theory]
    [MemberData(nameof(ReadOnly))]
    public void ReadOnlyRow_IsRead(string kind, string row)
    {
        PdfEngineFixture.RequireLicense();
        using var fixture = new PdfEngineFixture();
        using var document = new Document(fixture.CreateRich());

        Assert.True(TableRoundTrip.Read(PdfTable.Definition, document, Address(document, kind, row)).ContainsKey(row));
    }

    /// <summary>The address of an object of the kind that has the row.</summary>
    private static string Address(Document document, string kind, string row) =>
        Addresses.TryGetValue(kind, out string? at)
            ? at
            : TableRoundTrip.AddressOf(PdfTable.Definition, document, kind, row)
                ?? throw new InvalidOperationException($"The round-trip document has no {kind} with the row {row}.");
}
