using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

[Collection("Local service lifecycle")]
public sealed class AppLicenseUploadTests
{
    /// <summary>
    /// Installing a licence from the App changes the licence and nothing
    /// else: the same process answers, the uploaded copy is still open, and
    /// the next render of it is licensed.
    /// </summary>
    [LicensedFact]
    public async Task LicenseInstallation_KeepsTheUploadedDocumentOnScreen()
    {
        await using var app = await AppTestSession.Start();
        using (var upload = new HttpRequestMessage(HttpMethod.Post, "/api/files/upload"))
        {
            upload.Headers.Add("X-File-Name", "uploaded.csv");
            upload.Content = new StringContent(
                "Label,Value\nLICENSE_TRANSFER_42,42\n", Encoding.UTF8, "application/octet-stream");
            using HttpResponseMessage uploaded = await app.Client.SendAsync(upload);
            Assert.True(uploaded.IsSuccessStatusCode, await uploaded.Content.ReadAsStringAsync());
        }
        JsonNode before = await app.Status();
        Assert.True(before["uploadedCopy"]!.GetValue<bool>());
        string document = before["previewUrl"]!.GetValue<string>();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/license");
        request.Headers.Add("X-Product", "cells");
        request.Content = new StreamContent(
            File.OpenRead(Environment.GetEnvironmentVariable("ASPOSE_CLI_TEST_LICENSE_PATH")!));
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using HttpResponseMessage response = await app.Client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        Assert.EndsWith("/settings", JsonNode.Parse(body)!["continueUrl"]!.GetValue<string>(), StringComparison.Ordinal);

        JsonNode after = await app.Status();
        Assert.True(after["uploadedCopy"]!.GetValue<bool>());
        Assert.Equal("uploaded.csv", after["file"]!.GetValue<string>());
        Assert.Equal(document, after["previewUrl"]!.GetValue<string>());
        JsonNode cells = Assert.Single(
            after["license"]!["products"]!.AsArray(),
            product => product!["product"]!.GetValue<string>() == "cells")!;
        Assert.Equal("licensed", cells["mode"]!.GetValue<string>());
        using HttpResponseMessage page = await app.Client.GetAsync(document);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
    }
}
