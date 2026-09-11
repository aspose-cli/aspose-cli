using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

[Collection("Local service lifecycle")]
public sealed class AppPreviewStreamTests
{
    [Fact]
    public async Task MountedEvents_StayOpenDeliverUpdatesAndCloseWithTheirSession()
    {
        await using var app = await AppTestSession.Start();
        using HttpResponseMessage response = await app.Client.GetAsync(
            "/live/events", HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(), Encoding.UTF8);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        string hello = await ReadFrame(reader, deadline.Token);
        Assert.Contains("event: hello", hello, StringComparison.Ordinal);
        int initial = Payload(hello)["revision"]!.GetValue<int>();

        File.WriteAllText(app.Workspace.File("first.csv"), "Label,Value\nUPDATED_DOCUMENT,84\n");
        string update;
        do { update = await ReadFrame(reader, deadline.Token); }
        while (!update.StartsWith("event: update\n", StringComparison.Ordinal));
        Assert.True(Payload(update)["revision"]!.GetValue<int>() > initial);
        Assert.Contains("UPDATED_DOCUMENT", await app.Client.GetStringAsync("/document"), StringComparison.Ordinal);
        Assert.NotNull((await app.Status())["previewUrl"]);

        app.Open("second.csv");
        await reader.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Contains("SECOND_DOCUMENT", await app.Client.GetStringAsync("/document"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreviewRejectionsAndAppResponses_CompleteNormally()
    {
        await using var app = await AppTestSession.Start();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/live/refresh");
        request.Headers.Add("Origin", "http://untrusted.invalid");
        using HttpResponseMessage refused = await app.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        await refused.Content.ReadAsStringAsync();
        using HttpResponseMessage missing = await app.Client.GetAsync("/live/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        await missing.Content.ReadAsStringAsync();
        Assert.Equal("first.csv", (await app.Status())["file"]!.GetValue<string>());
    }

    private static JsonNode Payload(string frame) =>
        JsonNode.Parse(frame.Split('\n').Single(line => line.StartsWith("data: ", StringComparison.Ordinal))[6..])!;

    private static async Task<string> ReadFrame(StreamReader reader, CancellationToken cancellationToken)
    {
        var frame = new StringBuilder();
        while (true)
        {
            string? line = await reader.ReadLineAsync(cancellationToken);
            Assert.NotNull(line);
            if (line.Length == 0) { return frame.ToString(); }
            frame.Append(line).Append('\n');
        }
    }
}
