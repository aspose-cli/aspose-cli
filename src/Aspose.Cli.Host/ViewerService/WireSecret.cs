using System.Text.Json;
using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Host.ViewerService;

/// <summary>
/// A document password on its way from the CLI to the viewer service and on to the render
/// worker. Those local frames must carry the value, so unlike the SDK <see cref="Secret"/> it
/// writes its value to JSON; its text form stays redacted, so a printed request never shows it.
/// The render worker turns it back into a <see cref="Secret"/> for the product.
/// </summary>
[JsonConverter(typeof(Converter))]
internal sealed class WireSecret : IEquatable<WireSecret>
{
    private readonly string _value;

    private WireSecret(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        _value = value;
    }

    /// <summary>Carries a resolved secret across the local frames, or null for none.</summary>
    public static WireSecret? From(Secret? secret) => secret is null ? null : new WireSecret(secret.Reveal());

    /// <summary>The secret again, for the product that opens the document.</summary>
    public Secret ToSecret() => new(_value);

    /// <summary>Returns the redaction marker, never the value.</summary>
    public override string ToString() => Secret.Redacted;

    public bool Equals(WireSecret? other) => other is not null && string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as WireSecret);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(_value);

    internal sealed class Converter : JsonConverter<WireSecret>
    {
        public override WireSecret Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.GetString() is { Length: > 0 } value
                ? new WireSecret(value)
                : throw new JsonException("A password is never empty.");

        public override void Write(Utf8JsonWriter writer, WireSecret value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value._value);
    }
}
