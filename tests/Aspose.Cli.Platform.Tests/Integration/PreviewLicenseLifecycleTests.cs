using System.Diagnostics;
using System.Text.Json.Nodes;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

[Collection("Local service lifecycle")]
public sealed class PreviewLicenseLifecycleTests
{
    [Fact]
    public void ChildIdentityMismatch_RejectsStartupBeforePublishingAMarker()
    {
        using var fixture = new PreviewFixture();
        CliResult rejected = fixture.RunChildWithMismatchedIdentity();

        Assert.NotEqual(0, rejected.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(rejected.StdOut));
        JsonNode error = JsonNode.Parse(rejected.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Contains("license changed", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Empty(fixture.SessionIds());
    }

    [Fact]
    public void InvalidExplicitLicense_CannotHideBehindAnExistingPreview()
    {
        using var fixture = new PreviewFixture();
        Session original = fixture.Start();
        Assert.Equal("evaluation", original.Mode);
        Session unchanged = fixture.Start();
        Assert.True(unchanged.Reused);
        Assert.Equal(original.Id, unchanged.Id);
        Assert.Equal(original.Pid, unchanged.Pid);

        string invalid = fixture.File("invalid.lic");
        File.WriteAllText(invalid, "<not-an-aspose-license />");
        foreach (string path in new[] { invalid, fixture.File("missing.lic") })
        {
            CliResult rejected = fixture.Run(null, "preview", fixture.Source, "--license", path);
            Assert.NotEqual(0, rejected.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(rejected.StdOut));
            string code = JsonNode.Parse(rejected.StdErr)!["error"]!["code"]!.GetValue<string>();
            Assert.StartsWith("LICENSE_", code, StringComparison.Ordinal);
            Assert.False(fixture.Process(original).HasExited);
            Assert.Equal([original.Id], fixture.SessionIds());
        }
    }

    [Fact]
    public void UnidentifiedMarker_RestartsOnlyItsOwnedConfigAndKeepsTheUrl()
    {
        using var first = new PreviewFixture();
        using var other = new PreviewFixture();
        Session original = first.Start();
        Session unrelated = other.Start();
        JsonNode marker = first.Marker(original);
        marker.AsObject().Remove("licenseIdentity");
        File.WriteAllText(first.MarkerPath(original), marker.ToJsonString());

        Session replacement = first.Start();

        first.AssertReplaced(original, replacement);
        Assert.Equal("evaluation", replacement.Mode);
        Assert.False(other.Process(unrelated).HasExited);
        Assert.Equal([unrelated.Id], other.SessionIds());
    }

    [LicensedFact]
    public void LicensedIdentity_ChangesWithSamePathBytesAndInvalidInputPreservesIt()
    {
        using var license = new PrivateLicense();
        using var fixture = new PreviewFixture();
        fixture.AssertSdkLicensed(license.Path);
        Session evaluation = fixture.Start();
        Session licensed = fixture.Start(null, "--license", license.Path);
        fixture.AssertReplaced(evaluation, licensed);
        Assert.Equal("licensed", licensed.Mode);
        string initialIdentity = fixture.LicenseIdentity(licensed);

        Session reused = fixture.Start(null, "--license", license.Path);
        Assert.True(reused.Reused);
        Assert.Equal(licensed.Id, reused.Id);
        Assert.Equal(licensed.Pid, reused.Pid);

        license.AppendWhitespace();
        fixture.AssertSdkLicensed(license.Path);
        Session changed = fixture.Start(null, "--license", license.Path);
        fixture.AssertReplaced(licensed, changed);
        Assert.Equal("licensed", changed.Mode);
        Assert.NotEqual(initialIdentity, fixture.LicenseIdentity(changed));

        string invalid = fixture.File("invalid.lic");
        File.WriteAllText(invalid, "<invalid />");
        CliResult rejected = fixture.Run(
            new Dictionary<string, string?> { ["ASPOSE_LICENSE_PATH"] = license.Path },
            "preview", fixture.Source, "--license", invalid);
        Assert.NotEqual(0, rejected.ExitCode);
        Assert.Equal("LICENSE_INVALID", JsonNode.Parse(rejected.StdErr)!["error"]!["code"]!.GetValue<string>());
        Assert.False(fixture.Process(changed).HasExited);
        Assert.Equal([changed.Id], fixture.SessionIds());
    }

    [LicensedFact]
    public void EnvironmentAndUserConfigChanges_RestartRatherThanReuseStaleLicensing()
    {
        using var license = new PrivateLicense();
        using var fixture = new PreviewFixture();
        fixture.AssertSdkLicensed(license.Path);
        Session evaluation = fixture.Start();
        CliResult installed = fixture.Run(null, "license", "install", license.Path, "--product", "cells");
        Assert.True(installed.ExitCode == 0, installed.StdErr);
        Session configured = fixture.Start();
        fixture.AssertReplaced(evaluation, configured);
        Assert.Equal("licensed", configured.Mode);

        var global = new Dictionary<string, string?> { ["ASPOSE_LICENSE_PATH"] = license.Path };
        Session environment = fixture.Start(global);
        fixture.AssertReplaced(configured, environment);
        Assert.Equal("licensed", environment.Mode);
        Session sameEnvironment = fixture.Start(global);
        Assert.True(sameEnvironment.Reused);
        Assert.Equal(environment.Id, sameEnvironment.Id);

        string invalid = fixture.File("ignored-global.lic");
        File.WriteAllText(invalid, "invalid lower-precedence source");
        var productSpecific = new Dictionary<string, string?>
        {
            ["ASPOSE_LICENSE_PATH"] = invalid,
            ["ASPOSE_CELLS_LICENSE_PATH"] = license.Path,
        };
        Session specific = fixture.Start(productSpecific);
        fixture.AssertReplaced(environment, specific);
        Assert.Equal("licensed", specific.Mode);

        CliResult removed = fixture.Run(null, "license", "remove", "--product", "cells");
        Assert.True(removed.ExitCode == 0, removed.StdErr);
        Session unlicensed = fixture.Start();
        fixture.AssertReplaced(specific, unlicensed);
        Assert.Equal("evaluation", unlicensed.Mode);
    }

    private sealed record Session(string Id, int Pid, string Url, string Mode, bool Reused);

    private sealed class PreviewFixture : IDisposable
    {
        private readonly TempWorkspace _workspace = new();
        private readonly Dictionary<string, Session> _sessions = new(StringComparer.Ordinal);
        private readonly Dictionary<int, Process> _processes = [];

        public PreviewFixture()
        {
            Source = File("preview.csv");
            System.IO.File.WriteAllText(Source, "Label,Value\nREADY,42\n");
        }

        public string Source { get; }
        public string File(string name) => _workspace.File(name);
        public string MarkerPath(Session session) => System.IO.Path.Combine(_workspace.ConfigDirectory, "previews", session.Id + ".json");
        public JsonNode Marker(Session session) => JsonNode.Parse(System.IO.File.ReadAllText(MarkerPath(session)))!;
        public string LicenseIdentity(Session session) => Marker(session)["licenseIdentity"]!.GetValue<string>();
        public Process Process(Session session) => _processes[session.Pid];

        public Session Start(IReadOnlyDictionary<string, string?>? variables = null, params string[] options)
        {
            CliResult result = Run(variables, ["preview", Source, .. options]);
            Assert.True(result.ExitCode == 0, result.StdErr);
            JsonNode value = JsonNode.Parse(result.StdOut)!;
            var session = new Session(value["id"]!.GetValue<string>(), value["pid"]!.GetValue<int>(),
                value["url"]!.GetValue<string>(), value["license"]!["mode"]!.GetValue<string>(), value["reused"]!.GetValue<bool>());
            _sessions[session.Id] = session;
            if (!_processes.ContainsKey(session.Pid))
            {
                Process process = System.Diagnostics.Process.GetProcessById(session.Pid);
                _ = process.Handle;
                _processes.Add(session.Pid, process);
            }
            return session;
        }

        public void AssertSdkLicensed(string path)
        {
            CliResult inspected = Run(null, "cells", "inspect", Source, "--license", path);
            Assert.True(inspected.ExitCode == 0, inspected.StdErr);
            Assert.Equal("licensed", JsonNode.Parse(inspected.StdOut)!["license"]!["mode"]!.GetValue<string>());
        }

        public void AssertReplaced(Session previous, Session next)
        {
            Assert.False(next.Reused);
            Assert.NotEqual(previous.Id, next.Id);
            Assert.NotEqual(previous.Pid, next.Pid);
            Assert.Equal(previous.Url, next.Url);
            Assert.True(Process(previous).WaitForExit(10_000));
            Assert.False(System.IO.File.Exists(MarkerPath(previous)));
            Assert.False(System.IO.File.Exists(System.IO.Path.ChangeExtension(MarkerPath(previous), ".secret")));
            Assert.Equal([next.Id], SessionIds());
            Assert.False(string.IsNullOrWhiteSpace(LicenseIdentity(next)));
            using var http = new HttpClient(new HttpClientHandler { UseProxy = false });
            using HttpResponseMessage response = http.GetAsync(next.Url).GetAwaiter().GetResult();
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        }

        public string[] SessionIds()
        {
            CliResult result = Run(null, "preview", "status");
            Assert.True(result.ExitCode == 0, result.StdErr);
            return JsonNode.Parse(result.StdOut)!["sessions"]!.AsArray()
                .Select(static item => item!["id"]!.GetValue<string>()).ToArray();
        }

        public CliResult Run(IReadOnlyDictionary<string, string?>? variables, params string[] arguments)
        {
            ProcessStartInfo start = StartInfo();
            // Deliberately selected license sources are applied after normal test isolation.
            if (variables is not null)
            {
                foreach ((string key, string? value) in variables) { start.Environment[key] = value; }
            }
            foreach (string argument in arguments) { start.ArgumentList.Add(argument); }
            start.ArgumentList.Add("--output");
            start.ArgumentList.Add("json");
            using Process process = System.Diagnostics.Process.Start(start)!;
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(120_000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("Isolated preview licensing command did not finish.");
            }
            return new CliResult(process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
        }

        public CliResult RunChildWithMismatchedIdentity()
        {
            ProcessStartInfo start = StartInfo();
            foreach (string argument in new[]
            {
                "preview", "__host", Source, "--product", "cells", "--view", "workbook",
                "--port", "0", "--preview-service-id", Guid.NewGuid().ToString("N"), "--output", "json",
            }) { start.ArgumentList.Add(argument); }
            using Process process = ServiceStartSecretChannel.Start(start, new ServiceStartSecrets
            {
                WorkDirectory = _workspace.Path,
                ExpectedLicenseIdentity = new string('a', 64),
                ServiceToken = Guid.NewGuid().ToString("N"),
                ServiceNonce = Guid.NewGuid().ToString("N"),
            });
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            try
            {
                Assert.True(process.WaitForExit(15_000), "A child with a mismatched license identity must reject startup.");
                return new CliResult(process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5_000);
                }
            }
        }

        private ProcessStartInfo StartInfo()
        {
            var start = new ProcessStartInfo(CliRunner.ExecutablePath)
            {
                WorkingDirectory = _workspace.Path,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            CliEnvironment.Evaluation(System.IO.Path.GetDirectoryName(_workspace.ConfigDirectory)!).Apply(start.Environment);
            return start;
        }

        public void Dispose()
        {
            try
            {
                foreach (Session session in _sessions.Values) { _ = Run(null, "preview", "stop", session.Id); }
                foreach (Process process in _processes.Values)
                {
                    if (!process.HasExited && !process.WaitForExit(5_000))
                    {
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(5_000);
                    }
                }
            }
            finally
            {
                foreach (Process process in _processes.Values) { process.Dispose(); }
                string ownedConfigDirectory = System.IO.Path.GetFullPath(_workspace.ConfigDirectory);
                _workspace.Dispose();
                Assert.True(LocalFileCleanup.DeleteDirectory(ownedConfigDirectory),
                    "The isolated configuration, including installed test license copies, could not be removed.");
            }
        }
    }

    private sealed class PrivateLicense : IDisposable
    {
        private readonly string _directory = PrivateUserStorage.CreateTemporaryDirectory("preview-license-tests");
        public PrivateLicense()
        {
            Path = System.IO.Path.Combine(_directory, "test.lic");
            string original = Environment.GetEnvironmentVariable("ASPOSE_CLI_TEST_LICENSE_PATH")!;
            try
            {
                using FileStream input = System.IO.File.OpenRead(original);
                using FileStream output = PrivateUserStorage.CreateFile(Path);
                input.CopyTo(output);
            }
            catch
            {
                Dispose();
                throw;
            }
        }
        public string Path { get; }
        public void AppendWhitespace()
        {
            using var file = new FileStream(Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Span<byte> prefix = stackalloc byte[4];
            int count = file.Read(prefix);
            byte[] whitespace = count >= 4 && prefix[..4].SequenceEqual(new byte[] { 0xff, 0xfe, 0, 0 }) ? [0x20, 0, 0, 0]
                : count >= 4 && prefix[..4].SequenceEqual(new byte[] { 0, 0, 0xfe, 0xff }) ? [0, 0, 0, 0x20]
                : count >= 2 && prefix[..2].SequenceEqual(new byte[] { 0xff, 0xfe }) ? [0x20, 0]
                : count >= 2 && prefix[..2].SequenceEqual(new byte[] { 0xfe, 0xff }) ? [0, 0x20]
                : [0x20];
            file.Seek(0, SeekOrigin.End);
            file.Write(whitespace);
            file.Flush(flushToDisk: true);
        }

        public void Dispose()
        {
            string expectedRoot = System.IO.Path.GetFullPath(PrivateUserStorage.TemporaryRoot()) + System.IO.Path.DirectorySeparatorChar;
            string actual = System.IO.Path.GetFullPath(_directory);
            if (!actual.StartsWith(expectedRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Refusing cleanup outside the owned private test root.");
            }
            Assert.True(LocalFileCleanup.DeleteDirectory(actual), "Private test license copy could not be removed.");
        }
    }
}
