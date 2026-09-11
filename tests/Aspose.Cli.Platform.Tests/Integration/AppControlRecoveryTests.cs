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
        Assert.Equal("second.csv", (await app.Status())["file"]!.GetValue<string>());
        Assert.Contains("SECOND_DOCUMENT", await app.Client.GetStringAsync("/document"), StringComparison.Ordinal);
    }
}
