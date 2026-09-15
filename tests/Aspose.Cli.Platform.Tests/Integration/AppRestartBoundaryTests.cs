using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

[Collection("Local service lifecycle")]
public sealed class AppRestartBoundaryTests
{
    [Fact]
    public async Task RemovalRestartTransfersUploadBeforeTheOldAppStops()
    {
        await using var app = await AppTestSession.Start();
        using (var upload = new HttpRequestMessage(HttpMethod.Post, "/api/files/upload"))
        {
            upload.Headers.Add("X-File-Name", "retained.csv");
            upload.Content = new StringContent("Heading,Value\nRETAINED_UPLOAD,42\n", Encoding.UTF8, "application/octet-stream");
            using var uploaded = await app.Client.SendAsync(upload);
            Assert.True(uploaded.IsSuccessStatusCode, await uploaded.Content.ReadAsStringAsync());
        }
        using var removal = new HttpRequestMessage(HttpMethod.Delete, "/api/license");
        removal.Headers.Add("X-Product", "cells");
        using HttpResponseMessage response = await app.Client.SendAsync(removal);
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        await app.FollowRestart(JsonNode.Parse(body)!["restartUrl"]!.GetValue<string>());
        Assert.Equal("retained.csv", (await app.Status())["file"]!.GetValue<string>());
        Assert.Contains("RETAINED_UPLOAD", await app.Client.GetStringAsync("/document"));
    }

    [Fact]
    public async Task FailedReplacementRestoresControlAndKeepsTheExistingSessionUsable()
    {
        await using var app = await AppTestSession.Start();
        File.Delete(app.Workspace.File("first.csv"));
        using var removal = new HttpRequestMessage(HttpMethod.Delete, "/api/license");
        removal.Headers.Add("X-Product", "cells");
        using HttpResponseMessage response = await app.Client.SendAsync(removal);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        JsonNode error = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal("APP_STARTUP_FAILED", error["code"]!.GetValue<string>());
        Assert.Contains("was saved", error["message"]!.GetValue<string>());
        Assert.Contains("FIRST_DOCUMENT", await app.Client.GetStringAsync("/document"));
        Assert.Equal(0, app.Open("second.csv").ExitCode);
        Assert.Equal("second.csv", (await app.Status())["file"]!.GetValue<string>());
    }
}
