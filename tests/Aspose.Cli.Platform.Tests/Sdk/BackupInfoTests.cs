using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Resources;
using Aspose.Cli.Sdk.Serialization;
using Json.Schema;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

public sealed class BackupInfoTests
{
    [Fact]
    public void LastWriteUtc_IsWrittenInUtcWithTheZDesignator()
    {
        BackupInfo backup = new()
        {
            Path = "in.backup.docx",
            Created = false,
            SizeBytes = 10,
            LastWriteUtc = new DateTimeOffset(2026, 9, 28, 8, 30, 0, 125, TimeSpan.FromHours(-7)),
            HoldsReplacedVersion = false,
        };

        JsonObject json = JsonNode.Parse(JsonSerializer.Serialize(backup, SdkJsonContext.Default.BackupInfo))!.AsObject();

        Assert.Equal("2026-09-28T15:30:00.125Z", json["lastWriteUtc"]!.GetValue<string>());
        BackupInfo read = JsonSerializer.Deserialize(json.ToJsonString(), SdkJsonContext.Default.BackupInfo)!;
        Assert.Equal(backup.LastWriteUtc, read.LastWriteUtc);
        Assert.Equal(TimeSpan.Zero, read.LastWriteUtc.Offset);
    }

    [Theory]
    [InlineData("2026-09-28T15:30:00Z", true)]
    [InlineData("2026-09-28T15:30:00.1234567Z", true)]
    [InlineData("2026-09-28T15:30:00+00:00", false)]
    [InlineData("2026-09-28T08:30:00-07:00", false)]
    public void BackupSchema_AcceptsOnlyUtcTimes(string lastWriteUtc, bool valid)
    {
        JsonSchema schema = JsonSchema.FromText(
            SdkSchemaCatalog.Read("v2/common/backup"),
            new BuildOptions { SchemaRegistry = new SchemaRegistry() });
        JsonObject instance = new()
        {
            ["path"] = "in.backup.docx",
            ["created"] = true,
            ["sizeBytes"] = 10,
            ["lastWriteUtc"] = lastWriteUtc,
            ["holdsReplacedVersion"] = true,
        };

        using JsonDocument document = JsonDocument.Parse(instance.ToJsonString());
        Assert.Equal(valid, schema.Evaluate(document.RootElement).IsValid);
    }
}
