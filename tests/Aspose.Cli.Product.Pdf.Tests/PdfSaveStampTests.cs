using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// What the PDF engine writes into every document it saves. The engine leaves its modification
/// date out of a document saved while other documents of the process are saved, so these tests
/// run alone, after the parallel ones.
/// </summary>
[Collection(EngineSaveStampsCollection.Name)]
public sealed class PdfSaveStampTests
{
    /// <summary>
    /// Aspose.PDF writes the creator, producer and modification date into every PDF it saves, so
    /// clear_properties names only the entries the output no longer holds, and says the others stay.
    /// </summary>
    [Fact]
    public void ClearProperties_NamesOnlyTheEntriesTheOutputNoLongerHolds()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("described.pdf");
        using (var document = new Aspose.Pdf.Document(fixture.CreateDocument(pages: 1)))
        {
            document.Info.Title = "Quarterly";
            document.Info.Author = "Ann";
            document.Save(input);
        }

        PdfEditResult result = Edit(fixture, input, new ClearPropertiesOp());

        IReadOnlyDictionary<string, string?> before = Metadata(fixture, input);
        IReadOnlyDictionary<string, string?> after = Metadata(fixture, result.Output!.Path);
        BoundedOperationOutcome outcome = Assert.Single(result.Applied);
        Assert.Contains("property/title", outcome.Targets);
        Assert.Contains("property/author", outcome.Targets);
        Assert.Equal(outcome.Targets.Count, outcome.ItemsAffected);
        Assert.All(outcome.Targets, target =>
        {
            string key = target["property/".Length..];
            Assert.NotNull(before[key]);
            Assert.Null(after.GetValueOrDefault(key));
        });
        Assert.All(["creator", "producer", "modificationDate"], key => Assert.NotNull(after[key]));
        Warning warning = Assert.Single(result.Warnings!, static warning => warning.Code.Name == "METADATA_REWRITTEN");
        Assert.Contains($"'{outcome.Id}'", warning.Message, StringComparison.Ordinal);
    }

    private static PdfEditResult Edit(PdfEngineFixture fixture, string input, params PdfOp[] ops) =>
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = ops },
            Output = TestOutput.At(fixture.File(Guid.NewGuid().ToString("N") + ".pdf")),
        });

    private static IReadOnlyDictionary<string, string?> Metadata(PdfEngineFixture fixture, string path) =>
        PdfInfo.Run(fixture.Session, new PdfInspectRequest { Input = path, Details = ["properties"] }).Properties!;
}
