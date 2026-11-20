using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// An image an operation reads that does not exist, or is no image, fails the edit before the
/// document is opened, naming the file, and nothing is written.
/// </summary>
public sealed class OperationInputFileTests : IDisposable
{
    private const string Ops = """{"ops":[{"set":"document","props":{"builtInDocumentProperties.title":"T"}},{"add":"image","in":"section/0/body/paragraph/0","args":{"image":"missing.png"}}]}""";

    private readonly TempWorkspace _workspace = new();

    public OperationInputFileTests()
    {
        CliResult created = _workspace.Run("words", "create", "a.docx", "--output-format", "json");
        Assert.True(created.ExitCode == 0, created.StdErr);
        File.WriteAllText(_workspace.File("ops.json"), Ops);
    }

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void AMissingImage_IsFileNotFoundNamingIt()
    {
        CliResult result = _workspace.Run("words", "edit", "a.docx", "--ops", "ops.json", "--out", "b.docx", "--output-format", "json");

        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("FILE_NOT_FOUND", error["code"]!.GetValue<string>());
        Assert.Equal(_workspace.File("missing.png"), error["details"]!["path"]!.GetValue<string>());
        Assert.False(File.Exists(_workspace.File("b.docx")));
    }

    /// <summary>A file that is not an intact image names its path, as a missing one does.</summary>
    [Fact]
    public void ABrokenImage_NamesItsPath()
    {
        File.WriteAllText(_workspace.File("missing.png"), "not an image");

        CliResult result = _workspace.Run("words", "edit", "a.docx", "--ops", "ops.json", "--out", "b.docx", "--output-format", "json");

        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("FILE_CORRUPT", error["code"]!.GetValue<string>());
        Assert.Equal(_workspace.File("missing.png"), error["details"]!["path"]!.GetValue<string>());
        Assert.False(File.Exists(_workspace.File("b.docx")));
    }
}
