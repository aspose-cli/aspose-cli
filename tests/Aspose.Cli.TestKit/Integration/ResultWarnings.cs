using System.Text.Json.Nodes;

namespace Aspose.Cli.TestKit;

/// <summary>The warnings of a result document.</summary>
public static class ResultWarnings
{
    /// <summary>The code of each warning of <paramref name="result"/>, in order; none when it has no warnings.</summary>
    public static string[] Codes(JsonNode result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return [.. (result["warnings"] as JsonArray ?? []).Select(static warning => warning!["code"]!.GetValue<string>())];
    }
}
