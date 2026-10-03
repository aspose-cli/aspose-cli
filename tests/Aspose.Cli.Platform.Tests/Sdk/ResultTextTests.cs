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

    private static string Render(Action<TableSurface> render, TableFormat format = TableFormat.Plain)
    {
        using var writer = new StringWriter();
        render(new TableSurface(writer, format));
        return writer.ToString();
    }

    private static string[] Lines(string text) =>
        text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
}
