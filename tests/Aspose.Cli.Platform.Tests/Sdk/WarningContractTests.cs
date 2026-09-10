using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Serialization;
using Xunit;

namespace Aspose.Cli.Sdk.Tests;

public sealed class WarningContractTests
{
    [Fact]
    public void OptionalVisualFields_AreOmittedByDefault()
    {
        var warning = new Warning
        {
            Code = "TEST_WARNING",
            Message = "A bounded warning.",
        };

        JsonObject json = Serialize(warning);

        Assert.Equal(["code", "message"], json.Select(static item => item.Key));
        Assert.DoesNotContain("location", json);
        Assert.DoesNotContain("affectsCompleteness", json);
    }

    [Fact]
    public void VisualFields_SerializeAfterTheExistingContract()
    {
        var warning = new Warning
        {
            Code = "TEST_WARNING",
            Message = "A bounded warning.",
            Hint = "Inspect the affected item.",
            Docs = "test/verification",
            Location = "page:2",
            AffectsCompleteness = true,
        };

        JsonObject json = Serialize(warning);

        Assert.Equal(
            ["code", "message", "hint", "docs", "location", "affectsCompleteness"],
            json.Select(static item => item.Key));
        Assert.Equal("page:2", json["location"]!.GetValue<string>());
        Assert.True(json["affectsCompleteness"]!.GetValue<bool>());
    }

    [Fact]
    public void ExplicitFalse_RoundTripsAsTheOmittedDefault()
    {
        Warning warning = JsonSerializer.Deserialize(
            """{"code":"TEST_WARNING","message":"A bounded warning.","affectsCompleteness":false}""",
            SdkJsonContext.Default.Warning)!;

        Assert.False(warning.AffectsCompleteness);
        Assert.DoesNotContain("affectsCompleteness", Serialize(warning));
    }

    private static JsonObject Serialize(Warning warning) =>
        JsonNode.Parse(JsonSerializer.Serialize(
            warning,
            SdkJsonContext.Default.Warning))!.AsObject();
}
