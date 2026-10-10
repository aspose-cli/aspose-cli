using System.Text.Json;
using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Host.LocalServices;

/// <summary>
/// A secret the Host's own processes hand each other: a document password on its way to the
/// viewer service and its render worker, or a local-service token in a control frame, a start
/// message or the service's secret file. Those carriers must hold the value, so unlike the SDK
/// <see cref="Secret"/> it writes its value to JSON as a plain string; its text form stays
/// redacted, so a printed record never shows it, and it compares in fixed time.
/// </summary>
[JsonConverter(typeof(Converter))]
internal sealed class WireSecret : IEquatable<WireSecret>
{
    private readonly string _value;

    /// <summary>Wraps a non-empty secret value.</summary>
    /// <exception cref="ArgumentException">The value is null or empty.</exception>
    public WireSecret(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        _value = value;
    }

    /// <summary>Carries a resolved secret across the local frames, or null for none.</summary>
    public static WireSecret? From(Secret? secret) => secret is null ? null : new WireSecret(secret.Reveal());

    /// <summary>The value, for the Host code that hands it to its peer or compares it.</summary>
    public string Reveal() => _value;

    /// <summary>The secret again, for the product that opens the document.</summary>
    public Secret ToSecret() => new(_value);

    /// <summary>Returns the redaction marker, never the value.</summary>
    public override string ToString() => Secret.Redacted;

    public bool Equals(WireSecret? other) => other is not null && SecretText.FixedEquals(other._value, _value);

    public override bool Equals(object? obj) => Equals(obj as WireSecret);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>Writes the value as a JSON string; an empty string reads as no secret.</summary>
    internal sealed class Converter : JsonConverter<WireSecret>
    {
        public override WireSecret Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.GetString() is { Length: > 0 } value ? new WireSecret(value) : null!;

        public override void Write(Utf8JsonWriter writer, WireSecret value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value._value);
    }
}
