using System.Text.Json;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Words;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// A mail merge discloses each template field that a record leaves null or omits, with the
/// records that have no value for it; the merge itself is unchanged.
/// </summary>
public sealed class WordsMergeGapTests
{
    [Theory]
    [InlineData("null.json", """[{"Name":"Ava","Salary":"100"},{"Name":"Noah","Salary":null}]""")]
    [InlineData("missing.json", """[{"Name":"Ava","Salary":"100"},{"Name":"Noah"}]""")]
    [InlineData("short.csv", "Name,Salary\nAva,100\nNoah\n")]
    [InlineData("blank.csv", "Name,Salary\nAva,100\nNoah,\n")]
    [InlineData("quoted.csv", "Name,Salary\nAva,100\nNoah,\"\"\n")]
    public void MailMerge_WarnsAboutEachTemplateFieldARecordLeavesBlank(string dataName, string data)
    {
        using var fixture = new WordsFixture();
        string input = Template(fixture, "Name", "Salary");
        string path = fixture.Temp.File(dataName);
        File.WriteAllText(path, data);
        string output = fixture.Temp.File("merged.docx");

        WordsEditResult result = fixture.Engine.ApplyOps(
            input,
            new WordsOpsBatch { Ops = [new MailMergeOp { Path = path }] },
            new WordsEditRequest { OutputPath = output, Verify = true });

        Warning warning = Assert.Single(result.Warnings ?? [], static w => w.Code == WordsDiagnostics.MergeValueMissing);
        Assert.Contains("Salary: record 2.", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Name", warning.Message, StringComparison.Ordinal);
        Assert.True(result.Verification!.Ok);
        // Record 2 is the second merged copy.
        var merged = new Document(output);
        Assert.Contains("Ava 100", merged.Sections[0].Body.GetText(), StringComparison.Ordinal);
        Assert.Contains("Noah", merged.Sections[1].Body.GetText(), StringComparison.Ordinal);
        Assert.DoesNotContain("100", merged.Sections[1].Body.GetText(), StringComparison.Ordinal);
        // A missing key merges as blank text, as a null value does.
        Assert.DoesNotContain("«", merged.GetText(), StringComparison.Ordinal);
    }

    [Fact]
    public void MailMerge_TakesNumbersAndBooleansInlineAsInADataFile()
    {
        using var fixture = new WordsFixture();
        string input = Template(fixture, "Name", "Salary", "Active");
        const string Row = """{"Name":"Ava","Salary":28000.50,"Active":true}""";
        string path = fixture.Temp.File("typed.json");
        File.WriteAllText(path, $"[{Row}]");

        string Merge(string data, string name)
        {
            string output = fixture.Temp.File(name);
            WordsOpsBatch batch = WordsOp.Catalog.Parse<WordsOpsBatch>(
                $$"""{"ops":[{"op":"mail_merge",{{data}}}]}""", Aspose.Cli.Generated.ProductJsonContext.Definition);
            fixture.Engine.ApplyOps(input, batch, new WordsEditRequest { OutputPath = output });
            return new Document(output).Sections[0].Body.GetText();
        }

        string inline = Merge($"\"inline\":[{Row}]", "inline.docx");

        Assert.Contains("Ava 28000.50 true", inline, StringComparison.Ordinal);
        Assert.Equal(inline, Merge($"\"path\":{JsonSerializer.Serialize(path)}", "file.docx"));
    }

    [Theory]
    [InlineData(null, "Photo: record 1.")]
    [InlineData("Ref:No", "Ref:No: record 1.")]
    public void MailMerge_NamesTheDataFieldAsTheEngineReadsIt(string? quoted, string expected)
    {
        using var fixture = new WordsFixture();
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.InsertField("MERGEFIELD NAME");
        builder.InsertField(quoted is null ? "MERGEFIELD Image:Photo" : $"MERGEFIELD \"{quoted}\"");
        string input = fixture.Temp.File("named.docx");
        document.Save(input);
        string output = fixture.Temp.File("merged.docx");

        WordsEditResult result = fixture.Engine.ApplyOps(
            input,
            new WordsOpsBatch
            {
                Ops = [new MailMergeOp { Inline = [new Dictionary<string, object?> { ["name"] = "Ava", [quoted ?? "Photo"] = null }] }],
            },
            new WordsEditRequest { OutputPath = output });

        Warning warning = Assert.Single(result.Warnings ?? [], static w => w.Code == WordsDiagnostics.MergeValueMissing);
        Assert.EndsWith(": " + expected, warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MailMerge_ListsEveryBlankFieldWithItsRecords()
    {
        using var fixture = new WordsFixture();
        string input = Template(fixture, "Name", "Salary", "Bonus");
        string output = fixture.Temp.File("merged.docx");

        WordsEditResult result = fixture.Engine.ApplyOps(
            input,
            new WordsOpsBatch
            {
                Ops =
                [
                    new MailMergeOp
                    {
                        Inline =
                        [
                            new Dictionary<string, object?> { ["Name"] = "A", ["Salary"] = "1" },
                            new Dictionary<string, object?> { ["Name"] = "B", ["Salary"] = null, ["Bonus"] = "2" },
                            new Dictionary<string, object?> { ["Name"] = "C", ["Salary"] = "3" },
                        ],
                    },
                ],
            },
            new WordsEditRequest { OutputPath = output });

        Warning warning = Assert.Single(result.Warnings ?? [], static w => w.Code == WordsDiagnostics.MergeValueMissing);
        Assert.Contains("Salary: record 2; Bonus: records 1, 3.", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MailMerge_SuggestsAnUnusedDataFieldCloseToABlankTemplateField()
    {
        using var fixture = new WordsFixture();
        string input = Template(fixture, "Name", "Salary");
        string path = fixture.Temp.File("typo.csv");
        File.WriteAllText(path, "Name,Salery\nAva,100\nNoah,200\n");

        WordsEditResult result = fixture.Engine.ApplyOps(
            input,
            new WordsOpsBatch { Ops = [new MailMergeOp { Path = path }] },
            new WordsEditRequest { OutputPath = fixture.Temp.File("merged.docx") });

        Warning warning = Assert.Single(result.Warnings ?? [], static w => w.Code == WordsDiagnostics.MergeValueMissing);
        Assert.Contains("Salary: records 1, 2 (did you mean the unused data field 'Salery'?).", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MailMerge_CutsALongRecordList()
    {
        using var fixture = new WordsFixture();
        string input = Template(fixture, "Name");
        string output = fixture.Temp.File("merged.docx");
        IReadOnlyDictionary<string, object?>[] rows = Enumerable.Range(1, 13)
            .Select(static _ => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?> { ["Name"] = null })
            .ToArray();

        WordsEditResult result = fixture.Engine.ApplyOps(
            input,
            new WordsOpsBatch { Ops = [new MailMergeOp { Inline = rows }] },
            new WordsEditRequest { OutputPath = output });

        Warning warning = Assert.Single(result.Warnings ?? [], static w => w.Code == WordsDiagnostics.MergeValueMissing);
        Assert.Contains("Name: records 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 and 3 more.", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MailMerge_DoesNotWarnAboutEmptyValuesOrFieldsTheTemplateDoesNotUse()
    {
        using var fixture = new WordsFixture();
        string input = Template(fixture, "Name", "Salary");
        string output = fixture.Temp.File("merged.docx");

        WordsEditResult result = fixture.Engine.ApplyOps(
            input,
            new WordsOpsBatch
            {
                Ops =
                [
                    new MailMergeOp
                    {
                        Inline = [new Dictionary<string, object?> { ["name"] = "Ava", ["Salary"] = "", ["Unused"] = null }],
                    },
                ],
            },
            new WordsEditRequest { OutputPath = output });

        Assert.DoesNotContain(result.Warnings ?? [], static w => w.Code == WordsDiagnostics.MergeValueMissing);
    }

    [Theory]
    [InlineData("null", "Qty: record 2.")]
    [InlineData("missing", "Qty: record 2.")]
    [InlineData("missingEverywhere", "Qty: records 1, 2.")]
    public void RegionMerge_WarnsOnlyAboutTheRegionFields(string gap, string expected)
    {
        using var fixture = new WordsFixture();
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.InsertField("MERGEFIELD Title");
        builder.Writeln();
        builder.InsertField("MERGEFIELD TableStart:Items");
        builder.InsertField("MERGEFIELD Item");
        builder.Write(" ");
        builder.InsertField("MERGEFIELD Qty");
        builder.InsertField("MERGEFIELD TableEnd:Items");
        string input = fixture.Temp.File("region.docx");
        document.Save(input);
        string output = fixture.Temp.File("merged.docx");
        var first = new Dictionary<string, object?> { ["Item"] = "Widget" };
        var second = new Dictionary<string, object?> { ["Item"] = "Gadget" };
        if (gap != "missingEverywhere")
        {
            first["Qty"] = "2";
        }

        if (gap == "null")
        {
            second["Qty"] = null;
        }

        WordsEditResult result = fixture.Engine.ApplyOps(
            input,
            new WordsOpsBatch { Ops = [new MailMergeOp { Regions = true, Inline = [first, second] }] },
            new WordsEditRequest { OutputPath = output });

        Warning warning = Assert.Single(result.Warnings ?? [], static w => w.Code == WordsDiagnostics.MergeValueMissing);
        Assert.EndsWith(": " + expected, warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Title", warning.Message, StringComparison.Ordinal);
        // A missing key merges blank even when no record has it; fields outside the region stay.
        string text = new Document(output).GetText();
        Assert.Contains("Gadget ", text, StringComparison.Ordinal);
        Assert.DoesNotContain("«Qty»", text, StringComparison.Ordinal);
        Assert.Contains("«Title»", text, StringComparison.Ordinal);
    }

    // One paragraph holding the fields, separated by spaces.
    private static string Template(WordsFixture fixture, params string[] fields)
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        for (int index = 0; index < fields.Length; index++)
        {
            if (index > 0)
            {
                builder.Write(" ");
            }

            builder.InsertField("MERGEFIELD " + fields[index]);
        }

        string path = fixture.Temp.File("template.docx");
        document.Save(path);
        return path;
    }
}
