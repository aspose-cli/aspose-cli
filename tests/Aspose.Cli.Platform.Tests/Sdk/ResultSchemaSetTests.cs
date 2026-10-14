using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Serialization;
using Json.Schema;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

/// <summary>
/// The result schemas written from the records the contract generator describes, shown on a
/// block this test assembly declares and the generator compiles as a product's.
/// </summary>
public sealed class ResultSchemaSetTests
{
    private static readonly ResultSchemaSet Sample = new("test", ((IResultSchemaSource)SampleSchemaJsonContext.Default).ResultRecords, common: null);

    [Fact]
    public void Block_IsWrittenFromItsRecordsAndDocumentation()
    {
        Assert.Equal(["v2/test/sample"], Sample.Ids);
        Assert.True(Sample.TryRead("v2/test/sample", out string? document));
        Assert.Equal(
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "https://schemas.aspose.com/aspose-cli/v2/test/sample.schema.json",
              "description": "A sample block that shows every shape a result record can take.",
              "type": "object",
              "required": [
                "schema",
                "schemaVersion",
                "name",
                "pages",
                "style",
                "when"
              ],
              "additionalProperties": false,
              "properties": {
                "schema": {
                  "description": "The block's schema.",
                  "const": "https://schemas.aspose.com/aspose-cli/v2/test/sample.schema.json"
                },
                "schemaVersion": {
                  "description": "The block's version.",
                  "const": 2
                },
                "name": {
                  "description": "The block's name.",
                  "type": "string",
                  "minLength": 1
                },
                "pages": {
                  "description": "The pages, in order.",
                  "type": "array",
                  "maxItems": 3,
                  "items": {
                    "$ref": "#/$defs/samplePage"
                  }
                },
                "labels": {
                  "description": "Labels by name; a label is null when it is empty.",
                  "type": "object",
                  "additionalProperties": {
                    "type": [
                      "string",
                      "null"
                    ]
                  }
                },
                "style": {
                  "description": "The style, or an engine style outside the list.",
                  "type": "string",
                  "anyOf": [
                    {
                      "enum": [
                        "plain",
                        "bold"
                      ]
                    },
                    {
                      "pattern": "^[a-z]+$"
                    }
                  ]
                },
                "cell": {
                  "$ref": "#/$defs/sampleCell"
                },
                "tags": {
                  "description": "Tags, each starting with #.",
                  "type": "array",
                  "items": {
                    "type": "string",
                    "pattern": "^#"
                  }
                },
                "when": {
                  "description": "When the block was written.",
                  "type": "string",
                  "format": "date-time"
                },
                "details": {
                  "description": "Free-form details.",
                  "type": "object"
                }
              },
              "$defs": {
                "sampleCell": {
                  "description": "A cell whose type decides its value.",
                  "type": "object",
                  "required": [
                    "t"
                  ],
                  "additionalProperties": false,
                  "properties": {
                    "t": {
                      "description": "The stored type.",
                      "type": "string",
                      "enum": [
                        "empty",
                        "number",
                        "string"
                      ]
                    },
                    "v": {
                      "description": "The stored value."
                    }
                  },
                  "oneOf": [
                    {
                      "properties": {
                        "t": {
                          "const": "empty"
                        },
                        "v": {
                          "type": "null"
                        }
                      }
                    },
                    {
                      "required": [
                        "v"
                      ],
                      "properties": {
                        "t": {
                          "const": "number"
                        },
                        "v": {
                          "type": "number"
                        }
                      }
                    },
                    {
                      "required": [
                        "v"
                      ],
                      "properties": {
                        "t": {
                          "const": "string"
                        },
                        "v": {
                          "type": "string"
                        }
                      }
                    }
                  ]
                },
                "samplePage": {
                  "description": "One page of a sample block.",
                  "type": "object",
                  "required": [
                    "number"
                  ],
                  "additionalProperties": false,
                  "properties": {
                    "number": {
                      "description": "The 1-based page number.",
                      "type": "integer",
                      "minimum": 0
                    },
                    "hidden": {
                      "description": "Whether the page is hidden; omitted when it is not.",
                      "type": "boolean"
                    }
                  }
                }
              }
            }

            """.ReplaceLineEndings("\n"),
            document);
    }

    [Fact]
    public void SerializedBlock_ConformsToItsSchema_AndUnknownMembersAreRefused()
    {
        Assert.True(Sample.TryRead("v2/test/sample", out string? document));
        JsonSchema schema = JsonSchema.FromText(document);
        var block = new SampleBlock
        {
            Name = "a",
            Pages = [new SamplePage { Number = 1 }, new SamplePage { Number = 2, Hidden = true }],
            Labels = new Dictionary<string, string?> { ["x"] = null },
            Style = "custom",
            Cell = new SampleCell { T = "number", V = 2.5 },
            Tags = ["#a"],
            When = DateTimeOffset.UnixEpoch,
        };
        JsonObject json = JsonSerializer.SerializeToNode(block, SampleSchemaJsonContext.Default.SampleBlock)!.AsObject();

        Assert.True(schema.Evaluate(JsonDocument.Parse(json.ToJsonString()).RootElement).IsValid, json.ToJsonString());
        foreach (Action<JsonObject> breaks in new Action<JsonObject>[]
        {
            static value => value["extra"] = 1,
            static value => value["pages"]![0]!["extra"] = 1,
            static value => value["cell"] = new JsonObject { ["t"] = "number", ["v"] = "text" },
            static value => value["cell"] = new JsonObject { ["t"] = "empty", ["v"] = 1 },
            static value => value["style"] = "Not-Matching",
            static value => value["schemaVersion"] = 3,
        })
        {
            JsonObject broken = json.DeepClone().AsObject();
            breaks(broken);
            Assert.False(schema.Evaluate(JsonDocument.Parse(broken.ToJsonString()).RootElement).IsValid, broken.ToJsonString());
        }
    }

    [Fact]
    public void PublishedRecordOfAnotherVersion_IsRefused()
    {
        ResultRecord current = new() { Type = typeof(SamplePage), SchemaId = "current", SchemaVersion = 2 };
        ResultRecord block = new() { Type = typeof(SampleBlock), SchemaId = "block" };
        ResultRecord older = new() { Type = typeof(SampleCell), SchemaId = "older", SchemaVersion = 1 };

        Assert.Equal(["v2/test/block", "v2/test/current"], new ResultSchemaSet("test", [current, block], common: null).Ids);
        Assert.Contains(
            "states schemaVersion 1, but its schema is published under v2",
            Assert.Throws<InvalidOperationException>(() => new ResultSchemaSet("test", [current, older], common: null)).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RecordsThatClaimOneSchemaOrDefinition_AreRefused()
    {
        ResultRecord first = new() { Type = typeof(SamplePage), SchemaId = "twice" };
        ResultRecord second = new() { Type = typeof(SampleCell), SchemaId = "twice" };
        Assert.Contains("published by more than one", Assert.Throws<InvalidOperationException>(() => new ResultSchemaSet("test", [first, second], common: null)).Message, StringComparison.Ordinal);

        ResultRecord root = new()
        {
            Type = typeof(SampleBlock),
            SchemaId = "root",
            Properties =
            [
                new() { Name = "a", Description = "A.", Value = new() { Kind = ResultValueKind.Record, Record = typeof(Nested.Twin) } },
                new() { Name = "b", Description = "B.", Value = new() { Kind = ResultValueKind.Record, Record = typeof(Other.Twin) } },
            ],
        };
        var set = new ResultSchemaSet("test", [root, new() { Type = typeof(Nested.Twin) }, new() { Type = typeof(Other.Twin) }], common: null);
        Assert.Contains("claimed by Twin and Twin", Assert.Throws<InvalidOperationException>(() => set.TryRead("v2/test/root", out _)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnOutputItem_InheritsTheFlatFileFactsOfTheSdk()
    {
        ResultRecord item = new()
        {
            Type = typeof(SamplePageFile),
            Description = "A page's file.",
            Base = typeof(OutputItem),
            Properties =
            [
                new() { Name = "page", Description = "P.", Value = new() { Kind = ResultValueKind.Integer }, Required = true, Order = -1 },
            ],
        };
        ResultRecord root = new()
        {
            Type = typeof(SampleBlock),
            SchemaId = "files",
            Properties =
            [
                new()
                {
                    Name = "outputs",
                    Description = "Os.",
                    Value = new() { Kind = ResultValueKind.Array, Items = new() { Kind = ResultValueKind.Record, Record = typeof(SamplePageFile) } },
                    Required = true,
                },
            ],
        };

        Assert.True(new ResultSchemaSet("test", [root, item], SdkSchemaCatalog.Schemas).TryRead("v2/test/files", out string? document));
        JsonObject page = JsonNode.Parse(document)!["$defs"]!["samplePageFile"]!.AsObject();
        Assert.Equal(["page", "path", "format", "sizeBytes", "fingerprint"], page["required"]!.AsArray().Select(static name => name!.GetValue<string>()));
        Assert.Equal(["page", "path", "format", "sizeBytes", "fingerprint", "encrypted"], page["properties"]!.AsObject().Select(static pair => pair.Key));
    }

    [Fact]
    public void SchemaIdsTheSdkStates_AreServed()
    {
        Assert.True(SdkSchemaCatalog.TryRead(DiagnosticDetails.CatalogId, out _));
        Assert.True(SdkSchemaCatalog.TryRead(NotFoundDetails.CatalogId, out _));
        Assert.True(SdkSchemaCatalog.TryRead(ResultSchemaSet.Id("common", Aspose.Cli.Sdk.Views.ViewManifest.Id), out _));
        Assert.True(SdkSchemaCatalog.TryRead(ResultSchemaSet.Id("common", ErrorEnvelope.Id), out string? error));
        Assert.Equal(ErrorEnvelope.SchemaUri, JsonNode.Parse(error)!["$id"]!.GetValue<string>());
    }

    [Fact]
    public void ProductAssemblyWithoutAProductModule_HasNoSchemaOwner()
    {
        static System.Reflection.Assembly Named(string name) =>
            System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(new System.Reflection.AssemblyName(name), System.Reflection.Emit.AssemblyBuilderAccess.Run);

        Assert.Equal("common", ResultEnvelope.OwnerOf(typeof(ResultEnvelope).Assembly));
        Assert.Equal("common", ResultEnvelope.OwnerOf(Named("Aspose.Cli.Product.Sample.Tests")));
        Assert.Contains(
            "'Aspose.Cli.Product.Sample' declares no product module",
            Assert.Throws<InvalidOperationException>(() => ResultEnvelope.OwnerOf(Named("Aspose.Cli.Product.Sample"))).Message,
            StringComparison.Ordinal);
    }

    private static class Nested
    {
        public sealed record Twin;
    }

    private static class Other
    {
        public sealed record Twin;
    }
}

/// <summary>A sample block that shows every shape a result record can take.</summary>
[SchemaId("sample")]
public sealed record SampleBlock
{
    /// <summary>The block's schema.</summary>
    [JsonPropertyOrder(-100)]
    public string Schema => "https://schemas.aspose.com/aspose-cli/v2/test/sample.schema.json";

    /// <summary>The block's version.</summary>
    [JsonPropertyOrder(-99)]
    public int SchemaVersion => 2;

    /// <summary>The block's name.</summary>
    [MinLength(1)]
    public required string Name { get; init; }

    /// <summary>The pages, in order.</summary>
    [MaxItems(3)]
    public required IReadOnlyList<SamplePage> Pages { get; init; }

    /// <summary>Labels by name; a label is null when it is empty.</summary>
    public IReadOnlyDictionary<string, string?>? Labels { get; init; }

    /// <summary>The style, or an engine style outside the list.</summary>
    [AllowedValues("plain", "bold")]
    [OpenEnum("^[a-z]+$")]
    public required string Style { get; init; }

    public SampleCell? Cell { get; init; }

    /// <summary>Tags, each starting with #.</summary>
    [Pattern("^#")]
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>When the block was written.</summary>
    public required DateTimeOffset When { get; init; }

    /// <summary>Free-form details.</summary>
    public JsonObject? Details { get; init; }
}

/// <summary>The file of one page, a sample item of a flat outputs list.</summary>
public sealed record SamplePageFile : OutputItem
{
    /// <summary>Describes the page's file.</summary>
    public SamplePageFile(OutputInfo file)
        : base(file)
    {
    }

    /// <summary>The 1-based page number.</summary>
    public required int Page { get; init; }
}

/// <summary>One page of a sample block.</summary>
public sealed record SamplePage
{
    /// <summary>The 1-based page number.</summary>
    public required uint Number { get; init; }

    /// <summary>Whether the page is hidden; omitted when it is not.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Hidden { get; init; }
}

/// <summary>A cell whose type decides its value.</summary>
public sealed record SampleCell
{
    /// <summary>The stored type.</summary>
    [AllowedValues("empty", "number", "string")]
    public required string T { get; init; }

    /// <summary>The stored value.</summary>
    [OneOfBy("t", "empty")]
    [OneOfBy("t", "number", Type = "number")]
    [OneOfBy("t", "string", Type = "string")]
    public object? V { get; init; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SampleBlock))]
[JsonSerializable(typeof(double))]
internal sealed partial class SampleSchemaJsonContext : JsonSerializerContext;
