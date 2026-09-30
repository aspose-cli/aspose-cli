using Aspose.Cli.Product.Words.Engine.Editing;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.IO;
using Aspose.Words;
using Aspose.Words.Saving;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>Operations that multiply content are charged to the node budget before they allocate.</summary>
public sealed class WordsAllocationBudgetTests
{
    [Fact]
    public void InsertTable_BeyondTheNodeBudget_IsRejectedBeforeAllocating()
    {
        using var fixture = new WordsFixture();
        string input = fixture.CreateReport();
        string output = fixture.Temp.File("table.docx");

        CliException error = Assert.Throws<CliException>(() => Engine(fixture, nodes: 500).ApplyOps(
            input,
            new WordsOpsBatch
            {
                Ops = [new InsertTableOp { At = new WordsTarget { Block = 1 }, Position = "after", RowCount = 100, ColumnCount = 20 }],
            },
            new WordsEditRequest { OutputPath = output }));

        Assert.Equal(ErrorCodes.InputBudgetExceeded, error.Code);
        Assert.Contains("pre-allocation", error.Message + error.Details, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void MailMerge_BeyondTheNodeBudget_IsRejectedBeforeCloningTheTemplate()
    {
        using var fixture = new WordsFixture();
        string input = fixture.CreateReport();
        string output = fixture.Temp.File("merged.docx");
        IReadOnlyDictionary<string, string?>[] rows = Enumerable.Range(0, 50)
            .Select(static index => (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?> { ["Name"] = $"N{index}" })
            .ToArray();

        CliException error = Assert.Throws<CliException>(() => Engine(fixture, nodes: 500).ApplyOps(
            input,
            new WordsOpsBatch { Ops = [new MailMergeOp { Inline = rows }] },
            new WordsEditRequest { OutputPath = output }));

        Assert.Equal(ErrorCodes.InputBudgetExceeded, error.Code);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void RegionMerge_WithSeveralRegions_IsRejectedInsteadOfHalfMerged()
    {
        using var fixture = new WordsFixture();
        var template = new Document();
        var builder = new DocumentBuilder(template);
        foreach (string region in new[] { "People", "Orders" })
        {
            builder.InsertField($" MERGEFIELD TableStart:{region} ");
            builder.InsertField(" MERGEFIELD Name ");
            builder.InsertField($" MERGEFIELD TableEnd:{region} ");
            builder.Writeln();
        }

        string input = fixture.Temp.File("regions.docx");
        template.Save(input, SaveFormat.Docx);
        string output = fixture.Temp.File("regions.out.docx");

        CliException error = Assert.Throws<CliException>(() => fixture.Engine.ApplyOps(
            input,
            new WordsOpsBatch
            {
                Ops = [new MailMergeOp { Regions = true, Inline = [new Dictionary<string, string?> { ["Name"] = "Ava" }] }],
            },
            new WordsEditRequest { OutputPath = output }));

        Assert.Equal(WordsDiagnostics.MergeDataInvalid, error.Code);
        Assert.Contains("People", error.Message, StringComparison.Ordinal);
        // The template, not the data, is at fault; the hint must not send the caller to the data.
        Assert.Contains("one region", error.Hint!, StringComparison.Ordinal);
        Assert.DoesNotContain("JSON", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Watermark_FromAMissingImage_ReportsFileNotFound()
    {
        using var fixture = new WordsFixture();
        string input = fixture.CreateReport();
        string missing = fixture.Temp.File("missing.png");

        CliException error = Assert.Throws<CliException>(() => fixture.Engine.ApplyOps(
            input,
            new WordsOpsBatch { Ops = [new AddWatermarkOp { ImagePath = missing }] },
            new WordsEditRequest { OutputPath = fixture.Temp.File("watermarked.docx") }));

        Assert.Equal(ErrorCodes.FileNotFound, error.Code);
    }

    [Fact]
    public void Watermark_WhoseDecodedPixelsExceedTheMemoryBudget_IsRejectedBeforeDecoding()
    {
        using var fixture = new WordsFixture();
        string input = fixture.CreateReport();
        string image = fixture.Temp.File("wide.png");
        using (var bitmap = new SkiaSharp.SKBitmap(2000, 2000))
        using (SkiaSharp.SKData data = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100))
        {
            File.WriteAllBytes(image, data.ToArray());
        }

        CliException error = Assert.Throws<CliException>(() => Engine(fixture, memoryBytes: 4L * 1024 * 1024).ApplyOps(
            input,
            new WordsOpsBatch { Ops = [new AddWatermarkOp { ImagePath = image }] },
            new WordsEditRequest { OutputPath = fixture.Temp.File("watermarked.docx") }));

        Assert.Equal(ErrorCodes.InputBudgetExceeded, error.Code);
    }

    [Fact]
    public void Csv_FollowsRfc4180Quoting()
    {
        IReadOnlyList<string[]> records = WordsMutationHandlers.ReadCsv(
            "Name, City\r\n\"Doe, Jane\",\"He said \"\"hi\"\"\"\n\n\"Two\nlines\", Oslo \r\n");

        Assert.Equal(3, records.Count);
        Assert.Equal(["Name", "City"], records[0]);
        Assert.Equal(["Doe, Jane", "He said \"hi\""], records[1]);
        Assert.Equal(["Two\nlines", "Oslo"], records[2]);
        Assert.Throws<CliException>(() => WordsMutationHandlers.ReadCsv("\"open"));
    }

    private static WordsEngine Engine(WordsFixture fixture, long? nodes = null, long? memoryBytes = null)
    {
        var limits = WordsModule.Manifest.ResourceBudgets.ToDictionary(static item => item.Kind, static item => item.Default);
        if (nodes is long nodeLimit)
        {
            limits[WordsBudgetDomains.Nodes] = nodeLimit;
        }

        if (memoryBytes is long memoryLimit)
        {
            limits[ResourceBudgetKinds.MemoryBufferBytes] = memoryLimit;
        }

        var budgets = new ResourceBudgetLedger(OperationDeadline.Start(null), limits);
        return new WordsEngine(fixture.Gate, budgets, new SafeFileWriter(budgets));
    }
}
