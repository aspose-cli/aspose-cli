using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// A declared warning code. A <see cref="Warning"/> takes its code only as one of these, and each
/// is declared once, as a static member of a diagnostics class whose <c>All</c> list hands it to the
/// diagnostic catalog, so <c>capabilities</c> lists every code the CLI can emit. On the wire it
/// is the plain code string.
/// </summary>
[JsonConverter(typeof(WarningCodeJsonConverter))]
public sealed class WarningCode : IEquatable<WarningCode>
{
    /// <summary>Declares a code.</summary>
    /// <param name="name">Stable SCREAMING_SNAKE_CASE identifier, e.g. <c>EVAL_MODE</c>.</param>
    public WarningCode(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>The stable SCREAMING_SNAKE_CASE identifier.</summary>
    public string Name { get; }

    /// <summary>
    /// Whether only licensing produces this warning, such as the evaluation disclosures; a build
    /// whose products need no license leaves it out of <c>capabilities</c>.
    /// </summary>
    public bool LicenseSurface { get; init; }

    /// <summary>
    /// A code read back from a result another CLI process wrote, such as a worker's or a child
    /// service's; it names a code that process declared.
    /// </summary>
    internal static WarningCode Received(string name) => new(name);

    /// <summary>Whether both name the same code.</summary>
    public bool Equals(WarningCode? other) =>
        other is not null && string.Equals(Name, other.Name, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as WarningCode);

    /// <inheritdoc/>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Name);

    /// <summary>The code.</summary>
    public override string ToString() => Name;

    /// <summary>Whether both name the same code.</summary>
    public static bool operator ==(WarningCode? left, WarningCode? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Whether the two name different codes.</summary>
    public static bool operator !=(WarningCode? left, WarningCode? right) => !(left == right);
}

/// <summary>Writes a <see cref="WarningCode"/> as its code string and reads it back.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class WarningCodeJsonConverter : JsonConverter<WarningCode>
{
    /// <inheritdoc/>
    public override WarningCode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String && reader.GetString() is { Length: > 0 } name
            ? WarningCode.Received(name)
            : throw new JsonException("A warning code is a non-empty string.");

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, WarningCode value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStringValue(value.Name);
    }
}
