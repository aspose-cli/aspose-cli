using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// A password or other secret the command template or an operation resolved. Its text form is
/// a redaction marker and it refuses JSON serialization, so a secret carried in a request never
/// reaches a log, a result or an error by accident; only a product's engine adapter calls
/// <see cref="Reveal"/> to pass the value to its document engine.
/// </summary>
[JsonConverter(typeof(SecretJsonConverter))]
public sealed class Secret
{
    /// <summary>The text that stands for a secret wherever it is printed.</summary>
    public const string Redacted = "[redacted]";

    private readonly string _value;

    /// <summary>Wraps a non-empty secret value.</summary>
    /// <exception cref="ArgumentException">The value is null or empty.</exception>
    public Secret(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        _value = value;
    }

    /// <summary>The secret value, for an engine adapter to pass to its document engine.</summary>
    public string Reveal() => _value;

    /// <summary>Returns the redaction marker, never the value.</summary>
    public override string ToString() => Redacted;

    private sealed class SecretJsonConverter : JsonConverter<Secret>
    {
        public override Secret Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            throw new NotSupportedException("A secret is never read from JSON.");

        public override void Write(Utf8JsonWriter writer, Secret value, JsonSerializerOptions options) =>
            throw new NotSupportedException("A secret is never written to JSON.");
    }
}
