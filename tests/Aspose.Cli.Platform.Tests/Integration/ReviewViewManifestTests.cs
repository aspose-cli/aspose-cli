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

        CliResult reviewed = workspace.Run("review", file, "--out", "evidence", "--output-format", "json");

        Assert.True(reviewed.ExitCode == 0, reviewed.StdErr);
        JsonNode result = JsonNode.Parse(reviewed.StdOut)!;
        Assert.Equal(product, result["product"]!.GetValue<string>());
        Assert.Equal(view, result["view"]!.GetValue<string>());
        string manifestPath = workspace.File(Path.Combine("evidence", "artifacts", "view.json"));
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonSchema schema = SchemaTestRegistry.CreateOptions().SchemaRegistry
            .Get(new Uri(ResultEnvelope.SchemaUri("common", Aspose.Cli.Sdk.Views.ViewManifest.Id))) as JsonSchema
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

    [Fact]
    public void Review_OfAWordDocumentNamedPdf_NamesWordsAndHonorsTheExplicitProduct()
    {
        using var workspace = new TempWorkspace();
        CreateSample(workspace, "doc.docx");
        File.Move(workspace.File("doc.docx"), workspace.File("renamed.pdf"));

        CliResult generic = workspace.Run("review", "renamed.pdf", "--out", "generic", "--output-format", "json");
        Assert.Equal(6, generic.ExitCode);
        JsonNode error = JsonNode.Parse(generic.StdErr)!["error"]!;
        Assert.Equal("FORMAT_MISMATCH", error["code"]!.GetValue<string>());
        Assert.Equal("words", Assert.Single(error["details"]!["detected"]!.AsArray())!.GetValue<string>());
        Assert.Contains("--product words", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);

        CliResult selected = workspace.Run(
            "review", "renamed.pdf", "--product", "words", "--out", "selected", "--output-format", "json");
        Assert.True(selected.ExitCode == 0, selected.StdErr);
        Assert.Equal("words", JsonNode.Parse(selected.StdOut)!["product"]!.GetValue<string>());
    }

    [Fact]
    public void Review_ReportsTheReviewedFileAsAnInputObject_AndTheIndexPath()
    {
        using var workspace = new TempWorkspace();
        CreateSample(workspace, "book.xlsx");

        CliResult reviewed = workspace.Run("review", "book.xlsx", "--out", "evidence", "--output-format", "json");

        Assert.True(reviewed.ExitCode == 0, reviewed.StdErr);
        JsonObject result = JsonNode.Parse(reviewed.StdOut)!.AsObject();
        JsonObject input = result["input"]!.AsObject();
        string book = workspace.File("book.xlsx");
        Assert.Equal(["path", "format", "sizeBytes", "fingerprint", "encrypted"], input.Select(static pair => pair.Key));
        Assert.Equal(book, input["path"]!.GetValue<string>());
        Assert.Equal("xlsx", input["format"]!.GetValue<string>());
        Assert.Equal(new FileInfo(book).Length, input["sizeBytes"]!.GetValue<long>());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(book))).ToLowerInvariant(), input["fingerprint"]!["sha256"]!.GetValue<string>());
        Assert.False(input["encrypted"]!.GetValue<bool>());
        Assert.Equal(workspace.File(Path.Combine("evidence", "index.html")), result["indexPath"]!.GetValue<string>());
        Assert.False(result.ContainsKey("index"));
        Assert.False(result.ContainsKey("sourceFormat"));
    }

    [Theory]
    [InlineData("flatopc")]
    [InlineData("wordml")]
    public void Review_ReadsTheWordXmlThisCliWrites(string format)
    {
        using var workspace = new TempWorkspace();
        CreateSample(workspace, "doc.docx");
        workspace.Run("words", "convert", "doc.docx", "--to", format, "--out", "doc.xml", "--output-format", "json")
            .Succeeded();

        CliResult reviewed = workspace.Run("review", "doc.xml", "--out", "evidence", "--output-format", "json");

        Assert.True(reviewed.ExitCode == 0, reviewed.StdErr);
        Assert.Equal("words", JsonNode.Parse(reviewed.StdOut)!["product"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("data.records", "<?xml version=\"1.0\"?><records><record/></records>")]
    [InlineData("data.xml", "<?xml version=\"1.0\"?><records><record/></records>")]
    [InlineData("book.xml", "<?xml version=\"1.0\"?><?mso-application progid=\"Excel.Sheet\"?><Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"/>")]
    public void Review_OfContentNoProductRecognizes_PointsAtTheProductOption(string file, string content)
    {
        // .xml is shared by many kinds of file: XML no product recognizes is not a damaged document.
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File(file), content);

        CliResult reviewed = workspace.Run("review", file, "--output-format", "json");

        JsonNode error = JsonNode.Parse(reviewed.StdErr)!["error"]!;
        Assert.Equal("FORMAT_MISMATCH", error["code"]!.GetValue<string>());
        Assert.Contains("--product <id>", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void Review_RefusesAViewWithoutImages_AndPointsAtOneWithImages()
    {
        using var workspace = new TempWorkspace();
        CreateSample(workspace, "book.xlsx");
        CreateSample(workspace, "doc.pdf");

        CliResult workbook = workspace.Run("review", "book.xlsx", "--view", "workbook", "--out", "evidence", "--output-format", "json");
        CliResult foreign = workspace.Run("review", "doc.pdf", "--view", "workbook", "--out", "other", "--output-format", "json");

        JsonNode error = JsonNode.Parse(workbook.StdErr)!["error"]!;
        Assert.Equal("FEATURE_UNSUPPORTED", error["code"]!.GetValue<string>());
        Assert.Contains("--view sheets", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(["sheets"], error["details"]!["available"]!.AsArray().Select(static item => item!.GetValue<string>()));
        Assert.False(Directory.Exists(workspace.File("evidence")));
        Assert.Equal("OPTION_INVALID", JsonNode.Parse(foreign.StdErr)!["error"]!["code"]!.GetValue<string>());
    }

    private static void CreateSample(TempWorkspace workspace, string file)
    {
        switch (Path.GetExtension(file))
        {
            case ".xlsx":
                workspace.Run("cells", "create", file, "--sheets", "Data", "--output-format", "json").Succeeded();
                workspace.Run("cells", "edit", file, "--in-place", "--set", "Data!A1=Hello", "--output-format", "json").Succeeded();
                break;
            case ".pptx":
                workspace.Run("slides", "create", file, "--output-format", "json").Succeeded();
                break;
            default:
                File.WriteAllText(workspace.File("doc.md"), "# Title\n\nFirst paragraph.\n");
                workspace.Run("words", "create", "doc.docx", "--from-markdown", "doc.md", "--output-format", "json").Succeeded();
                if (file.EndsWith(".pdf", StringComparison.Ordinal))
                {
                    workspace.Run("words", "convert", "doc.docx", "--to", "pdf", "--out", file, "--output-format", "json").Succeeded();
                }
                break;
        }
    }
}
