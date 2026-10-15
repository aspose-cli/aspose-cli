using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// A file an operation reads that does not exist fails that operation, numbered as the runner
/// numbers every failure, so <c>--best-effort</c> keeps the others.
/// </summary>
public sealed class OperationInputFileTests : IDisposable
{
    private const string Ops = """{"ops":[{"op":"set_properties","title":"T"},{"op":"insert_image","at":{"block":1},"position":"after","path":"missing.png"}]}""";

    private readonly TempWorkspace _workspace = new();

    public OperationInputFileTests()
    {
        CliResult created = _workspace.Run("words", "create", "a.docx", "--output-format", "json");
        Assert.True(created.ExitCode == 0, created.StdErr);
        File.WriteAllText(_workspace.File("ops.json"), Ops);
    }

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void AMissingImage_FailsItsOperationByNumber()
    {
        CliResult result = _workspace.Run("words", "edit", "a.docx", "--ops", "ops.json", "--out", "b.docx", "--output-format", "json");

        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("FILE_NOT_FOUND", error["code"]!.GetValue<string>());
        Assert.Equal("op-0002", error["details"]!["id"]!.GetValue<string>());
        Assert.Equal("insert_image", error["details"]!["op"]!.GetValue<string>());
        Assert.False(File.Exists(_workspace.File("b.docx")));
    }

    [Fact]
    public void AMissingImage_WithBestEffort_KeepsTheOtherOperations()
    {
        CliResult result = _workspace.Run("words", "edit", "a.docx", "--ops", "ops.json", "--out", "b.docx", "--best-effort", "--output-format", "json");

        Assert.Equal(8, result.ExitCode);
        JsonArray applied = JsonNode.Parse(result.StdOut)!["applied"]!.AsArray();
        Assert.Equal("ok", applied[0]!["status"]!.GetValue<string>());
        Assert.Equal("failed", applied[1]!["status"]!.GetValue<string>());
        Assert.Equal("FILE_NOT_FOUND", applied[1]!["error"]!["code"]!.GetValue<string>());
        Assert.True(File.Exists(_workspace.File("b.docx")));
    }
}
