using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// Every field a product's ops schema publishes, on an operation or on a record an operation
/// nests, carries a description, since an agent writes the operation from the schema alone. The
/// <c>op</c> discriminator and the shared <c>id</c>, described by the definition it references,
/// are the envelope's and not a field of the operation.
/// </summary>
public sealed class OperationDescriptionTests
{
    private static readonly string[] EnvelopeFields = ["id", "op"];

    [Fact]
    public void EveryOperationField_HasADescription()
    {
        var missing = new List<string>();
        string[] opsSchemas = [.. PublishedSchemas.Ids.Where(static id => id.EndsWith("/ops", StringComparison.Ordinal))];
        Assert.NotEmpty(opsSchemas);
        int fields = 0;
        foreach (string id in opsSchemas)
        {
            JsonObject definitions = PublishedSchemas.Document(id)["$defs"]!.AsObject();
            foreach ((string definition, JsonNode? schema) in definitions)
            {
                if (schema?["properties"] is not JsonObject properties)
                {
                    continue;
                }

                bool isOperation = properties["op"]?["const"] is not null;
                foreach ((string field, JsonNode? property) in properties)
                {
                    if (isOperation && EnvelopeFields.Contains(field))
                    {
                        continue;
                    }

                    fields++;
                    if (property?["description"]?.GetValue<string>() is not { } description || string.IsNullOrWhiteSpace(description))
                    {
                        missing.Add($"{id} {definition}.{field}");
                    }
                }
            }
        }

        Assert.True(fields > 0, "No ops schema publishes an operation field.");
        Assert.True(missing.Count == 0, $"{missing.Count} of {fields} operation fields have no description:{Environment.NewLine}{string.Join(Environment.NewLine, missing)}");
    }
}
