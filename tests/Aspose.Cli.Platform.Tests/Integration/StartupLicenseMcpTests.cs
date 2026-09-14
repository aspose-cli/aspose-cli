using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

public sealed partial class McpProtocolTests
{
    [Fact]
    public async Task StartupLicenseNotice_IsAbsentFromMcpAndItsHumanFormatChildren()
    {
        using var temp = new TempDirectory();
        string work = temp.File("work");
        Directory.CreateDirectory(work);
        File.WriteAllText(Path.Combine(work, "source.md"), "# Client project\n\nConfirmed delivery scope.\n");
        Server server = await Server.Start(temp.Path, work, options: ["--output", "table"]);
        try
        {
            JsonNode schema = await server.Execute(["schema", "v2/common/license-status", "--output", "table"]);
            AssertSuccess(schema);
            JsonNode schemaExecution = schema["result"]!["structuredContent"]!;
            Assert.Empty(schemaExecution["stderr"]!.GetValue<string>());
            Assert.NotNull(JsonNode.Parse(schemaExecution["stdout"]!.GetValue<string>())!["$schema"]);

            JsonNode created = await server.Execute(["words", "create", "brief.docx", "--markdown", "source.md",
                "--timeout", "15", "--output", "table"]);
            AssertSuccess(created);
            Assert.True(File.Exists(Path.Combine(work, "brief.docx")));
            JsonNode createdExecution = created["result"]!["structuredContent"]!;
            Assert.DoesNotContain("license:", createdExecution["stdout"]!.GetValue<string>());
            Assert.DoesNotContain("license:", createdExecution["stderr"]!.GetValue<string>());
        }
        finally
        {
            await server.DisposeAsync();
        }
        Assert.Empty(await server.StandardError);
    }
}
