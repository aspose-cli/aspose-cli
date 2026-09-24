using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Xunit;
using static Aspose.Cli.Platform.Tests.Integration.AppLicenseChecks;

namespace Aspose.Cli.Platform.Tests.Integration;

[Collection("Local service lifecycle")]
public sealed class AppLicenseRecoveryTests
{
    [LicensedFact]
    public async Task SavedLicenseWithMissingDocument_KeepsTheLastRevisionAndReportsRefreshFailure()
    {
        await using var app = await AppTestSession.Start();
        int pid = RunCli(app, "app", "status")["pid"]!.GetValue<int>();
        using Process process = Process.GetProcessById(pid);
        _ = process.Handle;
        PreviewSnapshot before = await ReadPreview(app);
        Assert.Equal("evaluation", before.License);
        Assert.Contains("FIRST_DOCUMENT", before.Html, StringComparison.Ordinal);
        string sourceHash = FileHashes.Sha256(LicensePath);

        using var events = await PreviewEvents.Open(app, before.Url);
        _ = await events.Next("hello");
        File.Delete(app.Workspace.File("first.csv"));
        JsonNode missing = await events.Next("error");
        Assert.Equal("FILE_NOT_FOUND", missing["code"]!.GetValue<string>());

        await SaveLicense(app);
        JsonNode refresh = await events.Next("error");
        Assert.Equal("FILE_NOT_FOUND", refresh["code"]!.GetValue<string>());
        Assert.True(refresh["revision"]!.GetValue<int>() > missing["revision"]!.GetValue<int>());
        Assert.Equal(before, await ReadPreview(app));
        Assert.Equal("licensed", Mode((await app.Status())["license"]!));
        string installed = Path.Combine(app.Workspace.ConfigDirectory, "licenses", "cells.lic");
        Assert.Equal(sourceHash, FileHashes.Sha256(installed));
        Assert.Equal(sourceHash, FileHashes.Sha256(LicensePath));
        Assert.Equal("licensed", Mode(RunCli(app, "license", "status")));
        Assert.Equal(pid, RunCli(app, "app", "status")["pid"]!.GetValue<int>());
        Assert.False(process.HasExited);

        // A missing source affects that document alone. A new upload renders
        // with the saved license through the recycled worker in the same App.
        await UploadCsv(app, "recovered.csv", "Label,Value\nRECOVERED_UPLOAD,7\n");
        PreviewSnapshot recovered = await ReadPreview(app);
        Assert.Equal("licensed", recovered.License);
        Assert.Contains("RECOVERED_UPLOAD", recovered.Html, StringComparison.Ordinal);
        Assert.Equal(pid, RunCli(app, "app", "status")["pid"]!.GetValue<int>());
        await Stop(app, process);
    }

    [LicensedFact]
    public async Task ExternalLicenseInstall_AppliesToTheNextRenderWithoutRestartingTheApp()
    {
        await using var app = await AppTestSession.Start();
        int pid = RunCli(app, "app", "status")["pid"]!.GetValue<int>();
        using Process process = Process.GetProcessById(pid);
        _ = process.Handle;
        PreviewSnapshot before = await ReadPreview(app);
        Assert.Equal("evaluation", before.License);
        Assert.Contains("FIRST_DOCUMENT", before.Html, StringComparison.Ordinal);

        Assert.Equal("licensed", Mode(RunCli(app, "license", "install", LicensePath, "--product", "cells")));
        // Configuration has changed; the immutable published revision has not.
        Assert.Equal(before, await ReadPreview(app));
        await UploadCsv(app, "licensed.csv", "Label,Value\nLICENSED_UPLOAD,99\n");
        JsonNode status = await app.Status();
        Assert.True(status["uploadedCopy"]!.GetValue<bool>());
        Assert.Equal("licensed.csv", status["file"]!.GetValue<string>());
        PreviewSnapshot uploaded = await ReadPreview(app);
        Assert.Equal("licensed", uploaded.License);
        Assert.Contains("LICENSED_UPLOAD", uploaded.Html, StringComparison.Ordinal);
        Assert.Equal(before, await ReadPreview(app, before.Url));
        Assert.Equal(pid, RunCli(app, "app", "status")["pid"]!.GetValue<int>());
        Assert.False(process.HasExited);
        await Stop(app, process);
    }

    private static async Task Stop(AppTestSession app, Process process)
    {
        Assert.False(RunCli(app, "app", "stop")["running"]!.GetValue<bool>());
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(process.HasExited);
        Assert.False(RunCli(app, "app", "status")["running"]!.GetValue<bool>());
    }
}

/// <summary>License assertions over the shared App fixture and the current live-view contract.</summary>
internal static class AppLicenseChecks
{
    internal static string LicensePath => TestLicense.Path!;

    internal static JsonNode RunCli(AppTestSession app, params string[] arguments)
    {
        CliResult result = app.Workspace.Run([.. arguments, "--output", "json"]);
        Assert.True(result.ExitCode == 0, result.StdErr);
        return JsonNode.Parse(result.StdOut)!;
    }

    internal static string Mode(JsonNode status) =>
        Assert.Single(status["products"]!.AsArray(), product => product!["product"]!.GetValue<string>() == "cells")!
            ["mode"]!.GetValue<string>();

    internal static async Task SaveLicense(AppTestSession app)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/license");
        request.Headers.Add("X-Product", "cells");
        request.Content = new StreamContent(File.OpenRead(LicensePath));
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using HttpResponseMessage response = await app.Client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonNode saved = JsonNode.Parse(body)!;
        Assert.True(saved["ok"]!.GetValue<bool>());
        Assert.Equal(new Uri(app.Client.BaseAddress!, "/settings").AbsoluteUri,
            saved["continueUrl"]!.GetValue<string>());
    }

    internal static async Task UploadCsv(AppTestSession app, string name, string content)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/files/upload");
        request.Headers.Add("X-File-Name", name);
        request.Content = new StringContent(content, Encoding.UTF8, "application/octet-stream");
        using HttpResponseMessage response = await app.Client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    internal static async Task<PreviewSnapshot> ReadPreview(AppTestSession app, string? url = null)
    {
        url ??= (await app.Status())["previewUrl"]!.GetValue<string>();
        string page = await app.Client.GetStringAsync(url);
        Match bootstrap = Regex.Match(page,
            """<script type="application/json" id="aspose-viewer-data">(.*?)</script>""", RegexOptions.Singleline);
        Assert.True(bootstrap.Success, "The viewer must expose its published revision and effective render license.");
        JsonNode live = JsonNode.Parse(bootstrap.Groups[1].Value)!["live"]!;
        int revision = live["revision"]!.GetValue<int>();
        var root = new Uri(url);
        JsonNode manifest = JsonNode.Parse(await app.Client.GetStringAsync(new Uri(root, $"r/{revision}/view.json")))!;
        JsonNode part = Assert.Single(manifest["parts"]!.AsArray())!;
        string html = await app.Client.GetStringAsync(new Uri(root, $"r/{revision}/{part["file"]!.GetValue<string>()}"));
        return new PreviewSnapshot(url, revision, live["license"]!.GetValue<string>(), html);
    }

    internal sealed record PreviewSnapshot(string Url, int Revision, string License, string Html);

    internal sealed class PreviewEvents(HttpResponseMessage response, Stream stream) : IDisposable
    {
        private readonly StreamReader _reader = new(stream);

        internal static async Task<PreviewEvents> Open(AppTestSession app, string url)
        {
            HttpResponseMessage response = await app.Client.GetAsync(new Uri(new Uri(url), "events"),
                HttpCompletionOption.ResponseHeadersRead);
            try
            {
                response.EnsureSuccessStatusCode();
                return new PreviewEvents(response, await response.Content.ReadAsStreamAsync());
            }
            catch { response.Dispose(); throw; }
        }

        internal async Task<JsonNode> Next(string name)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            string? current = null;
            while (await _reader.ReadLineAsync(deadline.Token) is { } line)
            {
                if (line.StartsWith("event:", StringComparison.Ordinal)) { current = line["event:".Length..].Trim(); }
                else if (line.StartsWith("data:", StringComparison.Ordinal) && current == name)
                { return JsonNode.Parse(line["data:".Length..].Trim())!; }
            }
            throw new EndOfStreamException($"The preview ended before its '{name}' event.");
        }

        public void Dispose()
        {
            _reader.Dispose();
            response.Dispose();
        }
    }
}
