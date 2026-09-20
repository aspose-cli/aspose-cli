using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

[Collection("Local service lifecycle")]
public sealed class AppControlRecoveryTests
{
    [Fact]
    public async Task RejectedOpen_PreservesTheSessionAndTheControlEndpoint()
    {
        await using var app = await AppTestSession.Start();
        string originalUrl = (await app.Status())["previewUrl"]!.GetValue<string>();
        // Generic routing cannot distinguish this one-column text from a Words text document.
        File.WriteAllText(app.Workspace.File("ambiguous.csv"), "Marker\nambiguous text\n");
        var refused = app.Workspace.Run("app", app.Workspace.File("ambiguous.csv"), "--no-open", "--output", "json");
        Assert.NotEqual(0, refused.ExitCode);
        Assert.Equal(originalUrl, (await app.Status())["previewUrl"]!.GetValue<string>());
        Assert.Equal("first.csv", (await app.Status())["file"]!.GetValue<string>());
        app.Open("second.csv");
        System.Text.Json.Nodes.JsonNode status = await app.Status();
        Assert.Equal("second.csv", status["file"]!.GetValue<string>());
        // The viewer serves the new document at its own address.
        Assert.NotEqual(originalUrl, status["previewUrl"]!.GetValue<string>());
        using var viewer = new System.Net.Http.HttpClient();
        Assert.Contains(
            "definePresenter('cells'",
            await viewer.GetStringAsync(status["previewUrl"]!.GetValue<string>()),
            StringComparison.Ordinal);
    }
}
