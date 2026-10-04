using System.Text.Json;
using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Operations;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Platform.Tests.Sdk;

// The operation vocabulary of the SDK tests, declared and generated exactly as a product's.

public static class Shades
{
    public const string Light = "light";
    public const string Dark = "dark";
}

/// <summary>Test operations.</summary>
[OperationVocabulary("https://schemas.aspose.com/aspose-cli/v2/test/ops.schema.json", MaximumOperations = 8, JsonContext = typeof(TestOpsJsonContext))]
[JsonConverter(typeof(OperationJsonConverter<TestOp>))]
public abstract partial record TestOp : BoundedOperation;

[ExactlyOneOf("path", "all")]
public abstract record TargetOp : TestOp
{
    [MinLength(1)] public string? Path { get; init; }

    public bool All { get; init; }
}

public sealed record Box
{
    [ExclusiveMinimum(0)] public double Width { get; init; } = 1;
}

[MinProperties(1), DependentRequired("size", "font")]
public sealed record Style
{
    [MistakenFor("typeface")] public string? Font { get; init; }

    public double? Size { get; init; }

    [MistakenFor("fontWeight", "strong")] public bool? Bold { get; init; }
}

/// <summary>Places boxes.</summary>
[Operation("place")]
public sealed record PlaceOp : TargetOp
{
    [PageRange] public required string Pages { get; init; }

    public Box Box { get; init; } = new();

    public Style? Style { get; init; }

    [MinItems(1), HexColor] public IReadOnlyList<string>? Colors { get; init; }

    [MaxItems(3), MinItems(1, Depth = 1), MaxItems(2, Depth = 1)] public IReadOnlyList<IReadOnlyList<string>>? Rows { get; init; }

    [JsonScalar] public IReadOnlyList<object?>? Cells { get; init; }

    [Pattern("^[a-z]+$", Meaning = "must be a lowercase tag such as \"draft\"")] public IReadOnlyList<string>? Tags { get; init; }

    public IReadOnlyDictionary<string, string>? Labels { get; init; }

    [AllowedValues(typeof(Shades))] public string Shade { get; init; } = Shades.Light;

    public uint Count { get; init; }

    // The SDK grants these tests its internals, so the override keeps both modifiers.
    protected internal override BoundedOperation Validated() => this with { Pages = PageRange.Parse(Pages).Text };
}

[Operation("note")]
[AtLeastOneOf("text", "pinned")]
public sealed record NoteOp : TestOp
{
    [MinLength(1)] public string? Text { get; init; }

    public bool? Pinned { get; init; }
}

/// <summary>Moves by an offset or to a position.</summary>
[Operation("shift")]
[PresentWhen("by", "mode", "relative")]
[PresentWhen("to", "mode", "absolute")]
public sealed record ShiftOp : TestOp
{
    [AllowedValues("absolute", "relative")] public string Mode { get; init; } = "absolute";

    public int? By { get; init; }

    public int? To { get; init; }
}

[Operation("set")]
public sealed record SetOp : TestOp
{
    [Minimum(0)] public required int Value { get; init; }
}

/// <summary>Labels the target; a null text removes the label.</summary>
[Operation("label")]
public sealed record LabelOp : TestOp
{
    [MinLength(1)] public required string? Text { get; init; }
}

[Operation("link")]
public sealed record LinkOp : TestOp
{
    [InputPath] public required string Path { get; init; }
}

[Operation("paint")]
public sealed record PaintOp : TestOp
{
    [MinItems(1), AllowedValues(typeof(Shades))] public required IReadOnlyList<string> Shades { get; init; }
}

[Operation("secret")]
public sealed record SecretOp : TestOp
{
    [SecretEnv] public string? PasswordEnv { get; init; }
}

/// <summary>Reads a mode whose converter cannot read the value "unsupported".</summary>
[Operation("probe")]
public sealed record ProbeOp : TestOp
{
    [JsonConverter(typeof(RefusingConverter))] public string? Mode { get; init; }
}

/// <summary>A converter that fails outside the JSON exceptions, as a serializer limit does.</summary>
public sealed class RefusingConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() is "unsupported" ? throw new NotSupportedException("unsupported mode") : reader.GetString()!;

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteStringValue(value);
}

/// <summary>The test batch, declared as a product declares its batch, with no strictness of its own.</summary>
public sealed record TestBatch : BoundedOperationEnvelope<TestOp>;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(TestBatch))]
[JsonSerializable(typeof(LabelOp))]
[JsonSerializable(typeof(LinkOp))]
[JsonSerializable(typeof(NoteOp))]
[JsonSerializable(typeof(PaintOp))]
[JsonSerializable(typeof(PlaceOp))]
[JsonSerializable(typeof(ProbeOp))]
[JsonSerializable(typeof(SecretOp))]
[JsonSerializable(typeof(SetOp))]
[JsonSerializable(typeof(ShiftOp))]
public sealed partial class TestOpsJsonContext : JsonSerializerContext;

public static class TestContracts
{
    /// <summary>The vocabulary's serializer, as a product declares its own.</summary>
    public static ProductJsonDefinition Json { get; } = new("test", TestOpsJsonContext.Default);
}
