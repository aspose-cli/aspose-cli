using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Host.Tests;
using Aspose.Cli.Sdk;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

public sealed class UpdateHandoffTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InstallerSurvivesPreparationAndParentExitWithoutHoldingResultPipes(bool timed)
    {
        Requires.Windows();
        using var workspace = new TempWorkspace();
        string ready = workspace.File("ready.json");
        string release = workspace.File("release.txt");
        string complete = workspace.File("complete.txt");
        string scratch = workspace.File("temp");
        Directory.CreateDirectory(scratch);
        string script = $$"""
            param($PackageRoot, $InstallDirectory, [switch]$Update, [int]$WaitForProcessId, $CleanupRoot, $StatusPath)
            $ErrorActionPreference = 'Stop'
            try { Wait-Process -Id $WaitForProcessId -ErrorAction SilentlyContinue } catch { }
            $handoff = [ordered]@{ parent = $WaitForProcessId; installer = $PID; update = [bool]$Update; installDirectory = $InstallDirectory; status = $StatusPath }
            [IO.File]::WriteAllText('{{Quote(ready)}}', ($handoff | ConvertTo-Json -Compress))
            $stop = [DateTime]::UtcNow.AddSeconds(30)
            while (-not [IO.File]::Exists('{{Quote(release)}}') -and [DateTime]::UtcNow -lt $stop) { Start-Sleep -Milliseconds 25 }
            [IO.File]::WriteAllText('{{Quote(complete)}}', 'completed')
            $full = [IO.Path]::GetFullPath($CleanupRoot)
            $prefix = [IO.Path]::GetFullPath('{{Quote(scratch)}}').TrimEnd('\') + '\'
            if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($full).StartsWith('aspose-cli-update-')) { throw 'Unexpected cleanup target.' }
            Remove-Item -LiteralPath $full -Recurse -Force
            """;
        string feed = Feed(workspace, script);
        var start = StartInfo(workspace, scratch,
            ["update", "install", feed, "--output", "json", .. timed ? new[] { "--timeout", "20" } : Array.Empty<string>()]);
        using Process cli = Process.Start(start)!;
        Task<string> stdout = cli.StandardOutput.ReadToEndAsync();
        Task<string> stderr = cli.StandardError.ReadToEndAsync();
        try
        {
            await cli.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(25));
            Assert.Equal(0, cli.ExitCode);
            // The installer deliberately stays alive until release; it must not retain CLI result pipes.
            string output = await stdout.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(string.IsNullOrWhiteSpace(await stderr.WaitAsync(TimeSpan.FromSeconds(5))));
            JsonNode result = JsonNode.Parse(output)!;
            Assert.Equal("pending", result["status"]!.GetValue<string>());
            await WaitForFile(ready);
            JsonNode handoff = JsonNode.Parse(File.ReadAllText(ready))!;
            Assert.Equal(cli.Id, handoff["parent"]!.GetValue<int>());
            Assert.Equal(result["processId"]!.GetValue<int>(), handoff["installer"]!.GetValue<int>());
            // The installer replays the recorded choices and receives one spelling of the directory.
            Assert.True(handoff["update"]!.GetValue<bool>());
            string installDirectory = handoff["installDirectory"]!.GetValue<string>();
            Assert.False(installDirectory.EndsWith(Path.DirectorySeparatorChar), installDirectory);
            JsonNode status = JsonNode.Parse(File.ReadAllText(handoff["status"]!.GetValue<string>()))!;
            Assert.Equal("pending", status["state"]!.GetValue<string>());
            Assert.Equal(handoff["installer"]!.GetValue<int>(), status["installerProcessId"]!.GetValue<int>());
            Assert.StartsWith(Path.GetFullPath(scratch), handoff["status"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(complete));
        }
        finally
        {
            File.WriteAllText(release, "continue");
            if (!cli.HasExited) { cli.Kill(entireProcessTree: true); }
        }
        await WaitForFile(complete);
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (Directory.EnumerateDirectories(scratch, "aspose-cli-update-*", SearchOption.AllDirectories).Any())
        { await Task.Delay(25, cleanup.Token); }
    }

    [Fact]
    public async Task StalledHttpsPreparation_TimesOutAndReclaimsWorkerScratch()
    {
        Requires.Windows();
        using var workspace = new TempWorkspace();
        string scratch = workspace.File("temp");
        Directory.CreateDirectory(scratch);
        using var server = new TcpListener(IPAddress.Loopback, 0);
        server.Start();
        int port = ((IPEndPoint)server.LocalEndpoint).Port;
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        Task<TcpClient> accepted = server.AcceptTcpClientAsync(stop.Token).AsTask();
        var start = StartInfo(workspace, scratch,
            ["update", "install", $"https://127.0.0.1:{port}/RELEASE-MANIFEST.json", "--timeout", "3", "--output", "json"]);
        using Process cli = Process.Start(start)!;
        Task<string> stdout = cli.StandardOutput.ReadToEndAsync();
        Task<string> stderr = cli.StandardError.ReadToEndAsync();
        try
        {
            using TcpClient connection = await accepted;
            await cli.WaitForExitAsync(stop.Token);
            Assert.Equal(9, cli.ExitCode);
            Assert.Equal(string.Empty, await stdout);
            Assert.Equal("OPERATION_TIMEOUT", JsonNode.Parse(await stderr)!["error"]!["code"]!.GetValue<string>());
            Assert.Empty(Directory.EnumerateFiles(scratch, "*", SearchOption.AllDirectories));
            Assert.Empty(Directory.EnumerateDirectories(scratch, "aspose-cli-update-*", SearchOption.AllDirectories));
        }
        finally { if (!cli.HasExited) { cli.Kill(entireProcessTree: true); } }
    }

    [Fact]
    public void Preparation_IsNotAPublicOrMcpExecutionEntry()
    {
        using var workspace = new TempWorkspace();
        CliResult result = workspace.Run("update", "_prepare", "feed.json", workspace.File("package"), "--output", "json");
        Assert.Equal(2, result.ExitCode);
        Assert.False(Directory.Exists(workspace.File("package")));
        var runner = new Aspose.Cli.Host.Mcp.McpCommandRunner(ActualCommandTree.Host);
        Assert.Throws<Aspose.Cli.Host.Mcp.McpCommandException>(() => runner.EnsureAllowed(
            ["update", "_prepare", "feed.json", "package"]));
    }

    private static async Task WaitForFile(string path)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!File.Exists(path)) { await Task.Delay(25, timeout.Token); }
    }

    private static ProcessStartInfo StartInfo(TempWorkspace workspace, string scratch, string[] args)
    {
        ProcessStartInfo start = workspace.StartInfo(new Dictionary<string, string?>
        {
            ["TEMP"] = scratch, ["TMP"] = scratch,
        });
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        foreach (string argument in args) { start.ArgumentList.Add(argument); }
        return start;
    }

    private static string Feed(TempWorkspace workspace, string script)
    {
        string archive = workspace.File("release.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            Write("install.ps1", script);
            Write("aspose-cli.exe", "synthetic payload; never executed");
            Write("SHA256SUMS", "synthetic installer fixture");
            void Write(string name, string content)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
        }
        string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archive))).ToLowerInvariant();
        string manifest = workspace.File("RELEASE-MANIFEST.json");
        File.WriteAllText(manifest, JsonSerializer.Serialize(new
        {
            schemaVersion = 1, productId = DistributionInfo.Id, edition = DistributionInfo.Edition,
            runtimeIdentifier = "win-x64", artifactVersion = "99.0.0", sourceRevision = new string('a', 40),
            archive = new { path = "release.zip", size = new FileInfo(archive).Length, sha256 = hash },
        }));
        return manifest;
    }

    private static string Quote(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
