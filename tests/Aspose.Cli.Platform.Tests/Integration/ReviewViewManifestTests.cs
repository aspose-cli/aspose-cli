using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.TestKit;
using Json.Schema;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// Every product publishes its review evidence through the one view contract:
/// a schema-valid view.json whose parts are the evidence, in document order,
/// each stamped with the digest of its bytes.
/// </summary>
[Category(TestCategory.Slow)]
public sealed class ReviewViewManifestTests
{
    [Theory]
    [InlineData("book.xlsx", "cells", "sheets")]
    [InlineData("doc.docx", "words", "pages")]
    [InlineData("deck.pptx", "slides", "slides")]
    [InlineData("doc.pdf", "pdf", "pages")]
    public void Review_PublishesTheProductViewAsEvidence(string file, string product, string view)
    {
        using var workspace = new TempWorkspace();
        CreateSample(workspace, file);

        CliResult reviewed = workspace.Run("review", file, "--out", "evidence", "--output", "json");

        Assert.True(reviewed.ExitCode == 0, reviewed.StdErr);
        JsonNode result = JsonNode.Parse(reviewed.StdOut)!;
        Assert.Equal(product, result["product"]!.GetValue<string>());
        Assert.Equal(view, result["view"]!.GetValue<string>());
        string manifestPath = workspace.File(Path.Combine("evidence", "artifacts", "view.json"));
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonSchema schema = SchemaTestRegistry.CreateOptions().SchemaRegistry
            .Get(new Uri(CommonSchemaIds.View)) as JsonSchema
            ?? throw new InvalidOperationException("The view schema is not registered.");
        EvaluationResults evaluation = schema.Evaluate(manifest.RootElement);
        Assert.True(evaluation.IsValid, JsonSerializer.Serialize(evaluation));

        JsonElement[] parts = manifest.RootElement.GetProperty("parts").EnumerateArray().ToArray();
        Assert.NotEmpty(parts);
        string[] evidence = result["artifacts"]!.AsArray()
            .Where(static artifact => artifact!["role"]!.GetValue<string>() == "evidence")
            .Select(static artifact => artifact!["path"]!.GetValue<string>())
            .ToArray();
        Assert.Equal(
            parts.Select(static part => "artifacts/" + part.GetProperty("file").GetString()),
            evidence);
        foreach (JsonElement part in parts)
        {
            string path = workspace.File(Path.Combine(
                "evidence",
                "artifacts",
                part.GetProperty("file").GetString()!.Replace('/', Path.DirectorySeparatorChar)));
            string digest = "sha256:" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
            Assert.Equal(digest, part.GetProperty("digest").GetString());
        }
    }

    private static void CreateSample(TempWorkspace workspace, string file)
    {
        switch (Path.GetExtension(file))
        {
            case ".xlsx":
                workspace.Run("cells", "create", file, "--sheets", "Data", "--output", "json").Succeeded();
                workspace.Run("cells", "edit", file, "--in-place", "--set", "Data!A1=Hello", "--output", "json").Succeeded();
                break;
            case ".pptx":
                workspace.Run("slides", "create", file, "--output", "json").Succeeded();
                break;
            default:
                File.WriteAllText(workspace.File("doc.md"), "# Title\n\nFirst paragraph.\n");
                workspace.Run("words", "create", "doc.docx", "--markdown", "doc.md", "--output", "json").Succeeded();
                if (file.EndsWith(".pdf", StringComparison.Ordinal))
                {
                    workspace.Run("words", "convert", "doc.docx", "--to", "pdf", "--out", file, "--output", "json").Succeeded();
                }
                break;
        }
    }
}
