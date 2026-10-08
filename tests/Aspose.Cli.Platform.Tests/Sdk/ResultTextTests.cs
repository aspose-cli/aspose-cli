using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Extensibility.Output;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

public sealed class ResultTextTests
{
    [Fact]
    public void Edit_PrintsEveryBestEffortFailureWithItsHintAndTheBackup()
    {
        var output = new OutputInfo { Path = "out.docx", Format = "docx", SizeBytes = 1_024 };
        BoundedOperationOutcome[] applied =
        [
            new() { Id = "op-0001", Index = 0, Op = "set_text", Status = OpStatuses.Ok, ItemsAffected = 2 },
            new()
            {
                Id = "title",
                Index = 1,
                Op = "delete_block",
                Status = OpStatuses.Failed,
                ItemsAffected = 0,
                Error = new OpError { Code = "OPS_INVALID", Message = "block 9 does not exist", Hint = "Read the blocks first." },
            },
        ];

        string text = Render(surface => ResultText.Edit(
            surface,
            dryRun: false,
            output,
            applied,
            new BackupInfo
            {
                Path = "in.backup.docx", Created = true, SizeBytes = 10,
                LastWriteUtc = DateTimeOffset.UnixEpoch, HoldsReplacedVersion = true,
            }));

        Assert.Equal(
            [
                "wrote out.docx (docx, 1,024 bytes, 1 of 2 op(s) applied, 1 failed)",
                "  [op-0001/0] set_text: ok (2 item(s))",
                "  [title/1] delete_block: failed (0 item(s))",
                "      OPS_INVALID: block 9 does not exist",
                "      hint: Read the blocks first.",
                "backup: in.backup.docx (created)",
            ],
            Lines(text));
    }

    [Fact]
    public void Produced_NamesACompanionFileWithoutAFormat()
    {
        string text = Render(surface => ResultText.Produced(surface, new OutputInfo { Path = "pres.css", Format = null, SizeBytes = 10 }));

        Assert.Equal("wrote pres.css (companion, 10 bytes)", Lines(text)[0]);
    }

    [Fact]
    public void Backup_SaysAKeptBackupHoldsAnEarlierVersion()
    {
        string text = Render(surface => ResultText.Backup(surface, new BackupInfo
        {
            Path = "in.backup.docx", Created = false, SizeBytes = 10,
            LastWriteUtc = new DateTimeOffset(2026, 9, 28, 8, 30, 0, TimeSpan.Zero), HoldsReplacedVersion = false,
        }));

        Assert.Equal(
            "backup: in.backup.docx (kept existing, an earlier version last written 2026-09-28 08:30:00 UTC)",
            Lines(text)[0]);
    }

    [Fact]
    public void Edit_SaysADryRunWroteNothing()
    {
        string text = Render(surface => ResultText.Edit(
            surface,
            dryRun: true,
            output: null,
            [new() { Id = "op-0001", Index = 0, Op = "set_text", Status = OpStatuses.Ok, ItemsAffected = 1 }],
            backup: null));

        Assert.Equal("dry run: 1 of 1 op(s) applied; nothing was written", Lines(text)[0]);
    }

    [Theory]
    [InlineData(TableFormat.Plain, false, "\nfonts:\n")]
    [InlineData(TableFormat.Plain, true, "\nfonts:\nnone\n")]
    [InlineData(TableFormat.Markdown, false, "\n### fonts\n\n")]
    [InlineData(TableFormat.Markdown, true, "\n### fonts\n\nnone\n")]
    public void Section_WritesTheHeadingForTheFormatAndNoneWhenEmpty(TableFormat format, bool empty, string expected)
    {
        bool writeContent = true;
        string text = Render(surface => writeContent = ResultText.Section(surface, "fonts", empty), format);

        Assert.Equal(expected.Replace("\n", Environment.NewLine, StringComparison.Ordinal), text);
        Assert.Equal(!empty, writeContent);
    }

    [Fact]
    public void Table_WritesNothingForAnUnrequestedList()
    {
        string text = Render(surface => ResultText.Table<string>(surface, "fonts", null, ["font"], static font => [font]));

        Assert.Empty(text);
    }

    [Fact]
    public void Table_WritesOneRowPerItemUnderTheHeadingAndNoneWhenEmpty()
    {
        string rows = Render(surface => ResultText.Table(
            surface, "names", new[] { ("Total", "=B9"), ("Rate", "=0.2") }, ["name", "refers to"], static name => [name.Item1, name.Item2]));
        string empty = Render(surface => ResultText.Table(
            surface, "names", Array.Empty<(string, string)>(), ["name", "refers to"], static name => [name.Item1, name.Item2]));

        Assert.Equal(["names:", "name   refers to", "Total  =B9", "Rate   =0.2"], Lines(rows));
        Assert.Equal(["names:", "none"], Lines(empty));
    }

    [Fact]
    public void List_JoinsTheValuesOnOneLineAndSkipsAnUnrequestedList()
    {
        Assert.Equal(["fonts:", "Arial, Calibri"], Lines(Render(surface => ResultText.List(surface, "fonts", ["Arial", "Calibri"]))));
        Assert.Empty(Render(surface => ResultText.List(surface, "fonts", null)));
    }

    [Fact]
    public void Properties_KeepsTheMapOrderOrSortsByNameAndSpellsMissingValues()
    {
        var properties = new Dictionary<string, string?> { ["Title"] = "Plan", ["Author"] = null };

        string kept = Render(surface => ResultText.Properties(surface, "metadata", properties, nameColumn: "property"));
        string sorted = Render(surface => ResultText.Properties(surface, "properties", properties, missing: "-", sortByName: true));

        Assert.Equal(["metadata:", "property  value", "Title     Plan", "Author"], Lines(kept).Select(static line => line.TrimEnd()));
        Assert.Equal(["properties:", "name    value", "Author  -", "Title   Plan"], Lines(sorted).Select(static line => line.TrimEnd()));
        Assert.Empty(Render(surface => ResultText.Properties(surface, "properties", null)));
    }

    [Fact]
    public void Source_WritesThePathFormatAndSize()
    {
        string text = Render(surface => ResultText.Source(surface, new SourceInfo { Path = "in.pdf", Format = "pdf", SizeBytes = 20_000 }));

        Assert.Equal(["in.pdf (pdf, 20,000 bytes)"], Lines(text));
    }

    [Theory]
    [InlineData(612.0, "612")]
    [InlineData(595.2756, "595.28")]
    [InlineData(0.5, "0.5")]
    public void Points_KeepsAtMostTwoDecimals(double value, string expected) =>
        Assert.Equal(expected, TableText.Points(value));

    [Theory]
    [InlineData(true, false, "  FIELD_STALE [block 4]: 1 field needs an update")]
    [InlineData(false, false, "  FIELD_STALE: 1 field needs an update")]
    [InlineData(true, true, "  FIELD_STALE [block 4]: 1 field needs an update|    hint: Update the fields.")]
    public void Verification_WritesTheOutcomeEvidenceAndTheRequestedIssueParts(bool locations, bool hints, string expected)
    {
        VerificationIssue[] issues =
        [
            new() { Code = "FIELD_STALE", Message = "1 field needs an update", Location = "block 4", Hint = "Update the fields." },
        ];

        string text = Render(surface => ResultText.Verification(surface, ok: false, issues, "; 1 direct", locations, hints));

        Assert.Equal(["verification: needs attention; 1 direct", .. expected.Split('|')], Lines(text));
    }

    private static string Render(Action<TableSurface> render, TableFormat format = TableFormat.Plain)
    {
        using var writer = new StringWriter();
        render(new TableSurface(writer, format));
        return writer.ToString();
    }

    private static string[] Lines(string text) =>
        text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
}
