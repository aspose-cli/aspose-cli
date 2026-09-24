using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;
using static Aspose.Cli.Platform.Tests.Integration.AppLicenseChecks;

namespace Aspose.Cli.Platform.Tests.Integration;

[Collection("Local service lifecycle")]
public sealed class AppLicenseUploadTests
{
    /// <summary>The same App and uploaded document survive a licensed worker refresh.</summary>
    [LicensedFact]
    public async Task LicenseInstallation_KeepsTheUploadedDocumentOnScreen()
    {
        await using var app = await AppTestSession.Start();
        int pid = RunCli(app, "app", "status")["pid"]!.GetValue<int>();
        await UploadCsv(app, "uploaded.csv", "Label,Value\nLICENSE_TRANSFER_42,42\n");
        JsonNode before = await app.Status();
        Assert.True(before["uploadedCopy"]!.GetValue<bool>());
        PreviewSnapshot original = await ReadPreview(app);
        Assert.Equal("evaluation", original.License);
        Assert.Contains("LICENSE_TRANSFER_42", original.Html, StringComparison.Ordinal);

        using var events = await PreviewEvents.Open(app, original.Url);
        _ = await events.Next("hello");
        await SaveLicense(app);
        JsonNode update = await events.Next("update");
        Assert.Equal("licensed", update["license"]!.GetValue<string>());
        Assert.True(update["revision"]!.GetValue<int>() > original.Revision);

        JsonNode after = await app.Status();
        Assert.True(after["uploadedCopy"]!.GetValue<bool>());
        Assert.Equal("uploaded.csv", after["file"]!.GetValue<string>());
        Assert.Equal(original.Url, after["previewUrl"]!.GetValue<string>());
        Assert.Equal("licensed", Mode(after["license"]!));
        PreviewSnapshot refreshed = await ReadPreview(app);
        Assert.Equal("licensed", refreshed.License);
        Assert.True(refreshed.Revision > original.Revision);
        Assert.Contains("LICENSE_TRANSFER_42", refreshed.Html, StringComparison.Ordinal);
        Assert.Equal(pid, RunCli(app, "app", "status")["pid"]!.GetValue<int>());
    }
}
