using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// Results by the JSON names they are published under, so a test reads a field by its contract
/// name rather than by the record member that carries it.
/// </summary>
internal static class ResultJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>The value as published, with camel-case names.</summary>
    public static JsonNode Of(object value) => JsonSerializer.SerializeToNode(value, value.GetType(), Options)!;

    /// <summary>A record read from its published JSON; a name the record does not declare is ignored.</summary>
    public static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Options)!;

    /// <summary>The signature's <c>intact</c>, or null when it has none.</summary>
    public static bool? Intact(PdfSignatureInfo signature) => Of(signature)["intact"]?.GetValue<bool>();
}
