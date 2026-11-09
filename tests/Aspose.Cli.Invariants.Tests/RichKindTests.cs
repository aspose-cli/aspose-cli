using System.Text.Json.Nodes;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>
/// The tokens by which C-R42 and C-R43 expect a warning to name each kind of a rich fixture
/// (<see cref="ScenarioFixtures.Kinds"/>) are tokens the CLI can send: the error-payload schema
/// publishes each <see cref="RichKind.Content"/> among the <c>details.content</c> tokens and each
/// <see cref="RichKind.Part"/> among the <c>details.part</c> tokens. A token outside them would
/// make a rule demand a warning no product can give.
/// </summary>
public sealed class RichKindTests
{
    private const string ErrorPayload = "v2/common/error-payload";

    [Fact]
    public void EveryContentToken_IsOneTheErrorPayloadPublishes()
    {
        string[] published = Tokens(Details()["content"]?["items"]?["oneOf"]);
        Assert.All(ScenarioFixtures.Kinds.Where(static kind => kind.Content is not null), kind =>
            Assert.True(published.Contains(kind.Content), $"The {kind.Name} kind expects details.content '{kind.Content}', which {ErrorPayload} does not publish: [{string.Join(", ", published)}]."));
    }

    [Fact]
    public void EveryPartToken_IsOneTheErrorPayloadPublishes()
    {
        string[] published = Tokens(Details()["part"]?["oneOf"]);
        Assert.All(ScenarioFixtures.Kinds.Where(static kind => kind.Part is not null), kind =>
            Assert.True(published.Contains(kind.Part), $"The {kind.Name} kind expects details.part '{kind.Part}', which {ErrorPayload} does not publish: [{string.Join(", ", published)}]."));
    }

    private static JsonNode Details() =>
        JsonNode.Parse(CliCatalog.Current.SchemaText(ErrorPayload))!["properties"]!["details"]!["properties"]!;

    private static string[] Tokens(JsonNode? oneOf) =>
        [.. (oneOf as JsonArray ?? []).Select(static entry => entry?["const"]?.GetValue<string>()).OfType<string>()];
}
