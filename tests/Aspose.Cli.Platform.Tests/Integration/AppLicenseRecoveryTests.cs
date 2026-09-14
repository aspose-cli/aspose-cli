using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

[Collection("Local service lifecycle")]
public sealed class AppLicenseRecoveryTests
{
    [LicensedFact]
    public async Task SavedLicenseWithMissingDocument_RestartFailureKeepsTheOldAppControllable()
    {
        await using var app = await AppSession.Start();
        int originalPid = app.Pid;
        string sourceHash = Hash(LicensePath);
        File.Delete(app.Workspace.File("first.csv"));

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/license");
        request.Headers.Add("X-Product", "cells");
        request.Content = new StreamContent(File.OpenRead(LicensePath));
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using HttpResponseMessage response = await app.Client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        JsonNode failure = JsonNode.Parse(body)!;
        Assert.False(failure["ok"]!.GetValue<bool>());
        Assert.Equal("APP_STARTUP_FAILED", failure["code"]!.GetValue<string>());
        string message = failure["message"]!.GetValue<string>();
        Assert.Contains("was saved", message, StringComparison.Ordinal);
        Assert.Contains("could not restart", message, StringComparison.Ordinal);
        Assert.Contains("current App is still running", message, StringComparison.Ordinal);

        string installed = Path.Combine(app.Workspace.ConfigDirectory, "licenses", "cells.lic");
        Assert.True(File.Exists(installed));
        Assert.Equal(sourceHash, Hash(installed));
        Assert.Equal(sourceHash, Hash(LicensePath));
        Assert.Equal("licensed", Mode(app.Cli("license", "status"), "products"));

        JsonNode control = app.Cli("app", "status");
        Assert.True(control["running"]!.GetValue<bool>());
        Assert.Equal(originalPid, control["pid"]!.GetValue<int>());
        Assert.Equal("evaluation", Mode((await app.Status())["license"]!, "products"));
        Assert.Contains("FIRST_DOCUMENT", await app.Client.GetStringAsync("/document"), StringComparison.Ordinal);
        app.AssertOwnedProcesses(originalPid);

        await app.UploadCsv("recovered.csv", "Label,Value\nRECOVERED_UPLOAD,7\n");
        await app.AssertDocumentMode("evaluation", "RECOVERED_UPLOAD");
        Assert.Equal(originalPid, app.Cli("app", "status")["pid"]!.GetValue<int>());
        await app.StopAndAssertNoProcesses();
    }

    [LicensedFact]
    public async Task ExternalLicenseInstall_DoesNotChangeTheRunningAppsDocumentLicenseUntilRestart()
    {
        await using var app = await AppSession.Start();
        int originalPid = app.Pid;
        Process original = app.CurrentProcess;
        await app.AssertDocumentMode("evaluation", "FIRST_DOCUMENT");

        JsonNode installed = app.Cli("license", "install", LicensePath, "--product", "cells");
        Assert.Equal("licensed", Mode(installed, "products"));
        Assert.Equal(originalPid, app.Cli("app", "status")["pid"]!.GetValue<int>());

        // Upload directly into the existing App: no activation/restart command occurs here.
        await app.UploadCsv("pinned.csv", "Label,Value\nPINNED_EVALUATION_UPLOAD,99\n");
        JsonNode beforeRestart = await app.Status();
        Assert.True(beforeRestart["uploadedCopy"]!.GetValue<bool>());
        Assert.Equal("pinned.csv", beforeRestart["file"]!.GetValue<string>());
        await app.AssertDocumentMode("evaluation", "PINNED_EVALUATION_UPLOAD");
        Assert.Equal(originalPid, app.Cli("app", "status")["pid"]!.GetValue<int>());
        app.AssertOwnedProcesses(originalPid);

        JsonNode restarted = await app.Open("first.csv");
        Assert.False(restarted["reused"]!.GetValue<bool>());
        Assert.NotEqual(originalPid, app.Pid);
        await original.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await app.AssertDocumentMode("licensed", "FIRST_DOCUMENT");
        app.AssertOwnedProcesses(app.Pid);
        await app.StopAndAssertNoProcesses();
    }

    private static string LicensePath => Environment.GetEnvironmentVariable("ASPOSE_CLI_TEST_LICENSE_PATH")!;

    private static string Mode(JsonNode status, string collection) =>
        Assert.Single(status[collection]!.AsArray(), product => product!["product"]!.GetValue<string>() == "cells")!
            ["mode"]!.GetValue<string>();

    private static string Hash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private sealed class AppSession : IAsyncDisposable
    {
        private readonly List<Process> _processes = [];
        internal TempWorkspace Workspace { get; } = new();
        internal HttpClient Client { get; private set; } = null!;
        internal int Pid { get; private set; }
        internal Process CurrentProcess => _processes[^1];

        internal static async Task<AppSession> Start()
        {
            var session = new AppSession();
            try
            {
                File.WriteAllText(session.Workspace.File("first.csv"), "Label,Value\nFIRST_DOCUMENT,42\n");
                await session.Open("first.csv");
                return session;
            }
            catch
            {
                await session.DisposeAsync();
                throw;
            }
        }

        internal JsonNode Cli(params string[] arguments)
        {
            CliResult result = Workspace.Run([.. arguments, "--output", "json"]);
            Assert.True(result.ExitCode == 0, result.StdErr);
            return JsonNode.Parse(result.StdOut)!;
        }

        internal async Task<JsonNode> Open(string file)
        {
            JsonNode result = Cli("app", Workspace.File(file), "--no-open");
            Pid = result["pid"]!.GetValue<int>();
            if (!_processes.Any(process => process.Id == Pid))
            {
                Process process = Process.GetProcessById(Pid);
                _ = process.Handle;
                _processes.Add(process);
            }
            var url = new Uri(result["url"]!.GetValue<string>());
            Client?.Dispose();
            Client = new HttpClient(new HttpClientHandler { UseCookies = false })
            {
                BaseAddress = new Uri(url.GetLeftPart(UriPartial.Authority)),
                Timeout = TimeSpan.FromSeconds(60),
            };
            string shell = await Client.GetStringAsync("/");
            string csrf = Regex.Match(shell, @"name=""aspose-csrf"" content=""([^""]+)""").Groups[1].Value;
            Assert.NotEmpty(csrf);
            Client.DefaultRequestHeaders.Add("Origin", Client.BaseAddress.GetLeftPart(UriPartial.Authority));
            Client.DefaultRequestHeaders.Add("X-CSRF-Token", csrf);
            return result;
        }

        internal async Task<JsonNode> Status() =>
            JsonNode.Parse(await Client.GetStringAsync("/api/status"))!;

        internal async Task UploadCsv(string fileName, string content)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/files/upload");
            request.Headers.Add("X-File-Name", fileName);
            request.Content = new StringContent(content, Encoding.UTF8, "application/octet-stream");
            using HttpResponseMessage response = await Client.SendAsync(request);
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        }

        internal async Task AssertDocumentMode(string expectedMode, string expectedText)
        {
            JsonNode status = await Status();
            Assert.Equal(expectedMode, Mode(status["license"]!, "products"));
            string html = await Client.GetStringAsync("/document");
            Assert.Contains(expectedText, html, StringComparison.Ordinal);
            Match bootstrap = Regex.Match(html, @"window\.__asposePreview=(\{.*?\});", RegexOptions.Singleline);
            Assert.True(bootstrap.Success, "The served document must expose its effective preview license mode.");
            JsonNode metadata = JsonNode.Parse(bootstrap.Groups[1].Value)!;
            Assert.Equal(expectedMode == "evaluation", metadata["eval"]!.GetValue<bool>());
        }

        internal void AssertOwnedProcesses(params int[] running)
        {
            foreach (Process process in _processes)
            {
                Assert.Equal(!running.Contains(process.Id), process.HasExited);
            }
        }

        internal async Task StopAndAssertNoProcesses()
        {
            Assert.False(Cli("app", "stop")["running"]!.GetValue<bool>());
            foreach (Process process in _processes)
            {
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
            Assert.False(Cli("app", "status")["running"]!.GetValue<bool>());
            AssertOwnedProcesses();
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (Pid != 0)
                {
                    try { _ = Workspace.Run("app", "stop", "--output", "json"); }
                    catch (Exception exception) when (exception is IOException or TimeoutException or InvalidOperationException) { }
                }
                foreach (Process process in _processes)
                {
                    try
                    {
                        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
                        catch (TimeoutException)
                        {
                            process.Kill(entireProcessTree: true);
                            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                        }
                    }
                    finally { process.Dispose(); }
                }
            }
            finally
            {
                try { _ = Workspace.Run("license", "remove", "--product", "cells", "--output", "json"); }
                finally
                {
                    Client?.Dispose();
                    Workspace.Dispose();
                }
            }
        }
    }
}
