using System.Text.Json;
using Aspose.Cli.Sdk.Contracts;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

public sealed class BoundedOperationValidationTests
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Operations =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["set"] = new HashSet<string>(["id", "op", "value"], StringComparer.Ordinal),
        };

    [Fact]
    public void ValidateJsonShape_EnforcesSharedAndProductOwnedFields()
    {
        Validate("""{"schemaVersion":2,"ifMatch":"abc","ops":[{"id":"one","op":"set","value":1}]}""");

        InvalidOperationException root = Assert.Throws<InvalidOperationException>(
            () => Validate("""{"unexpected":true,"ops":[{"op":"set","value":1}]}"""));
        InvalidOperationException operation = Assert.Throws<InvalidOperationException>(
            () => Validate("""{"ops":[{"op":"set","unexpected":1}]}"""));
        InvalidOperationException count = Assert.Throws<InvalidOperationException>(
            () => Validate("""{"ops":[{"op":"set"},{"op":"set"}]}"""));

        Assert.Contains("root property 'unexpected'", root.Message, StringComparison.Ordinal);
        Assert.Contains("property 'unexpected'", operation.Message, StringComparison.Ordinal);
        Assert.Contains("ops must contain 1-1", count.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\"ops\":[{\"op\":\"set\"}],\"ops\":[{\"op\":\"set\"}]}", "root property 'ops' is duplicated")]
    [InlineData("{\"ops\":[{\"op\":\"set\",\"value\":1,\"value\":2}]}", "ops[0] property 'value' is duplicated")]
    [InlineData("{\"ops\":[{\"op\":\"set\",\"op\":\"set\"}]}", "ops[0] property 'op' is duplicated")]
    [InlineData("{\"ops\":[{\"op\":\"set\",\"value\":{\"style\":{\"bold\":true,\"bold\":false}}}]}", "ops[0].value.style property 'bold' is duplicated")]
    public void ValidateJsonShape_RejectsDuplicateProperties(string json, string expected)
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => Validate(json));

        Assert.Contains(expected, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateEnvelope_AcceptsOnlyTheCurrentVersionAndSchema()
    {
        var valid = new TestBatch { Schema = "v2/test/ops", Ops = [new(null)] };
        BoundedOperationValidation.ValidateEnvelope(
            valid,
            "v2/test/ops",
            static reason => new InvalidOperationException(reason));
        BoundedOperationValidation.ValidateEnvelope(
            valid with { SchemaVersion = null },
            "v2/test/ops",
            static reason => new InvalidOperationException(reason));

        InvalidOperationException version = Assert.Throws<InvalidOperationException>(() =>
            BoundedOperationValidation.ValidateEnvelope(
                valid with { SchemaVersion = 1 },
                "v2/test/ops",
                static reason => new InvalidOperationException(reason)));
        InvalidOperationException schema = Assert.Throws<InvalidOperationException>(() =>
            BoundedOperationValidation.ValidateEnvelope(
                valid with { Schema = "v2/test/other" },
                "v2/test/ops",
                static reason => new InvalidOperationException(reason)));

        Assert.Contains("supports version 2", version.Message, StringComparison.Ordinal);
        Assert.Contains("schema must be 'v2/test/ops'", schema.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OperationIds_AreDeterministicBoundedAndCollisionFree()
    {
        var used = new HashSet<string>(["op-0001"], StringComparer.Ordinal);

        Assert.Equal("op-0001", BoundedOperationIds.CreateDefault(0));
        Assert.Equal("op-0002", BoundedOperationIds.Allocate(0, used));
        Assert.True(BoundedOperationIds.IsValid("A.b_1-2"));
        Assert.False(BoundedOperationIds.IsValid("1-invalid"));
        Assert.False(BoundedOperationIds.IsValid(new string('a', 65)));
    }

    [Fact]
    public void Assign_RejectsInvalidAndDuplicateExplicitIdsAndFillsMissingIds()
    {
        IReadOnlyList<TestOperation> assigned = BoundedOperationIds.Assign<TestOperation>(
            [new(null), new("explicit"), new(null)],
            static operation => operation.Id,
            static (operation, id) => operation with { Id = id },
            static reason => new InvalidOperationException(reason));

        Assert.Equal(["op-0001", "explicit", "op-0003"], assigned.Select(static operation => operation.Id));

        InvalidOperationException duplicate = Assert.Throws<InvalidOperationException>(
            () => BoundedOperationIds.Assign<TestOperation>(
                [new TestOperation("same"), new TestOperation("same")],
                static operation => operation.Id,
                static (operation, id) => operation with { Id = id },
                static reason => new InvalidOperationException(reason)));
        InvalidOperationException invalid = Assert.Throws<InvalidOperationException>(
            () => BoundedOperationIds.Assign<TestOperation>(
                [new TestOperation("1-invalid")],
                static operation => operation.Id,
                static (operation, id) => operation with { Id = id },
                static reason => new InvalidOperationException(reason)));

        Assert.Contains("duplicated", duplicate.Message, StringComparison.Ordinal);
        Assert.Contains("must start with a letter", invalid.Message, StringComparison.Ordinal);
    }

    private static void Validate(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        BoundedOperationValidation.ValidateJsonShape(
            document.RootElement,
            Operations,
            maximumOperations: 1,
            static reason => new InvalidOperationException(reason));
    }

    private sealed record TestOperation(string? Id);

    private sealed record TestBatch : BoundedOperationEnvelope<TestOperation>;
}
