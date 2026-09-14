using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

[Collection("Local service lifecycle")]
public sealed class AppLicenseUploadTests
{
    [LicensedFact]
    public async Task LicenseInstallation_RestartsAndPreservesUploadedDocument()
    {
        await using var app = await AppTestSession.Start();
        using (var upload = new HttpRequestMessage(HttpMethod.Post, "/api/files/upload"))
        {
            upload.Headers.Add("X-File-Name", "uploaded.csv");
            upload.Content = new StringContent("Label,Value\nLICENSE_TRANSFER_42,42\n", Encoding.UTF8, "application/octet-stream");
            using HttpResponseMessage uploaded = await app.Client.SendAsync(upload);
            Assert.True(uploaded.IsSuccessStatusCode, await uploaded.Content.ReadAsStringAsync());
        }
        Assert.True((await app.Status())["uploadedCopy"]!.GetValue<bool>());
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/license");
        request.Headers.Add("X-Product", "cells");
        request.Content = new StreamContent(File.OpenRead(Environment.GetEnvironmentVariable("ASPOSE_CLI_TEST_LICENSE_PATH")!));
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using HttpResponseMessage response = await app.Client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        string url = JsonNode.Parse(body)!["restartUrl"]!.GetValue<string>();
        await app.FollowRestart(url);
        JsonNode status = await app.Status();
        Assert.True(status["uploadedCopy"]!.GetValue<bool>());
        Assert.Equal("uploaded.csv", status["file"]!.GetValue<string>());
        JsonNode cells = Assert.Single(status["license"]!["products"]!.AsArray(), product => product!["product"]!.GetValue<string>() == "cells")!;
        Assert.Equal("licensed", cells["mode"]!.GetValue<string>());
        Assert.Contains("LICENSE_TRANSFER_42", await app.Client.GetStringAsync("/document"), StringComparison.Ordinal);
    }
}
