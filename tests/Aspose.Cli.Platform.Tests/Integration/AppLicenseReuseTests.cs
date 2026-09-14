using System.Diagnostics;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

[Collection("Local service lifecycle")]
public sealed class AppLicenseReuseTests
{
    [Fact]
    public void UnchangedNativeLicenseIdentity_ReusesTheRunningApp()
    {
        using var workspace = new TempWorkspace();
        try
        {
            JsonNode first = Start(workspace);
            JsonNode second = Start(workspace);

            Assert.False(first["reused"]!.GetValue<bool>());
            Assert.True(second["reused"]!.GetValue<bool>());
            Assert.Equal(first["pid"]!.GetValue<int>(), second["pid"]!.GetValue<int>());
            JsonNode marker = ReadMarker(workspace);
            Assert.Matches("^[a-f0-9]{64}$", marker["licenseIdentity"]!.GetValue<string>());
        }
        finally { Stop(workspace); }
    }

    [Fact]
    public async Task ChangedAndRepairedLicenseConfiguration_ReplacesTheSdkProcess()
    {
        using var workspace = new TempWorkspace();
        try
        {
            JsonNode first = Start(workspace);
            using Process initialProcess = Process.GetProcessById(first["pid"]!.GetValue<int>());
            _ = initialProcess.Handle;
            string source = workspace.File(".aspose/licenses/pdf.lic");
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            File.WriteAllText(source, "<License>invalid native license fixture</License>");

            JsonNode invalid = Start(workspace);
            AssertReplaced(first, invalid, initialProcess);
            Assert.Null(ReadMarker(workspace)["licenseIdentity"]);
            Assert.Equal("invalid", await ProductMode(invalid, "pdf"));
            Assert.Equal("evaluation", await ProductMode(invalid, "words"));

            using Process invalidProcess = Process.GetProcessById(invalid["pid"]!.GetValue<int>());
            _ = invalidProcess.Handle;
            JsonNode invalidAgain = Start(workspace);
            AssertReplaced(invalid, invalidAgain, invalidProcess);
            Assert.Equal("invalid", await ProductMode(invalidAgain, "pdf"));

            using Process unreusableProcess = Process.GetProcessById(invalidAgain["pid"]!.GetValue<int>());
            _ = unreusableProcess.Handle;
            File.Delete(source);
            JsonNode repaired = Start(workspace);
            AssertReplaced(invalidAgain, repaired, unreusableProcess);
            Assert.Equal("evaluation", await ProductMode(repaired, "pdf"));
            Assert.NotNull(ReadMarker(workspace)["licenseIdentity"]);
            Assert.True(Start(workspace)["reused"]!.GetValue<bool>());
        }
        finally { Stop(workspace); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnmatchedOrAbsentReadyIdentity_PreventsReuse(bool missing)
    {
        using var workspace = new TempWorkspace();
        try
        {
            JsonNode first = Start(workspace);
            using Process initialProcess = Process.GetProcessById(first["pid"]!.GetValue<int>());
            _ = initialProcess.Handle;
            JsonNode marker = ReadMarker(workspace);
            if (missing) { marker.AsObject().Remove("licenseIdentity"); }
            else { marker["licenseIdentity"] = new string('0', 64); }
            File.WriteAllText(MarkerPath(workspace), marker.ToJsonString());

            JsonNode replacement = Start(workspace);
            AssertReplaced(first, replacement, initialProcess);
            Assert.Matches("^[a-f0-9]{64}$", ReadMarker(workspace)["licenseIdentity"]!.GetValue<string>());
            Assert.True(Start(workspace)["reused"]!.GetValue<bool>());
        }
        finally { Stop(workspace); }
    }

    private static JsonNode Start(TempWorkspace workspace)
    {
        CliResult result = workspace.Run("app", "--no-open", "--output", "json");
        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.Empty(result.StdErr);
        return JsonNode.Parse(result.StdOut)!;
    }

    private static void Stop(TempWorkspace workspace)
    {
        CliResult stopped = workspace.Run("app", "stop", "--output", "json");
        Assert.True(stopped.ExitCode == 0, stopped.StdErr);
    }

    private static void AssertReplaced(JsonNode before, JsonNode after, Process previous)
    {
        Assert.False(after["reused"]!.GetValue<bool>());
        Assert.NotEqual(before["pid"]!.GetValue<int>(), after["pid"]!.GetValue<int>());
        Assert.True(previous.WaitForExit(10000), "The replaced SDK process must be confirmed stopped.");
    }

    private static async Task<string> ProductMode(JsonNode app, string product)
    {
        var url = new Uri(app["url"]!.GetValue<string>());
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        JsonNode status = JsonNode.Parse(await client.GetStringAsync(new Uri(url, "/api/status")))!;
        return status["license"]!["products"]!.AsArray()
            .Single(item => item!["product"]!.GetValue<string>() == product)!["mode"]!.GetValue<string>();
    }

    private static JsonNode ReadMarker(TempWorkspace workspace) =>
        JsonNode.Parse(File.ReadAllText(MarkerPath(workspace)))!;

    private static string MarkerPath(TempWorkspace workspace) =>
        Path.Combine(workspace.ConfigDirectory, "app-instance.json");
}
