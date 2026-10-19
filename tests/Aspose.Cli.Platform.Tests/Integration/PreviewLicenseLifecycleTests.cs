using System.Text.Json.Nodes;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.IO.Paths;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// Previewing a document says which license rendered it, and a license the
/// engine refuses fails that document alone: what is already open keeps
/// rendering.
/// </summary>
[Collection("Local service lifecycle")]
public sealed class PreviewLicenseLifecycleTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public PreviewLicenseLifecycleTests() =>
        _workspace.Run("cells", "create", "book.xlsx", "--sheets", "Data", "--output-format", "json").Succeeded();

    public void Dispose()
    {
        _workspace.Run("preview", "stop", "--all", "--output-format", "json");
        _workspace.Dispose();
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void RefusedLicense_FailsItsOwnDocumentAndLeavesTheOpenOneRendering()
    {
        JsonNode open = Start();
        Assert.Equal("evaluation", open["license"]!["mode"]!.GetValue<string>());
        File.WriteAllText(_workspace.File("invalid.lic"), "<not-an-aspose-license />");

        foreach (string license in new[] { "invalid.lic", "missing.lic" })
        {
            CliResult refused = _workspace.Run(
                "preview", "open", "book.xlsx", "--no-open", "--license", license, "--output-format", "json");

            Assert.NotEqual(0, refused.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(refused.StdOut));
            Assert.StartsWith(
                "LICENSE_",
                JsonNode.Parse(refused.StdErr)!["error"]!["code"]!.GetValue<string>(),
                StringComparison.Ordinal);
            Assert.Equal([open["id"]!.GetValue<string>()], DocumentIds());
        }
    }

    [Category(TestCategory.Slow)]
    [LicensedFact]
    public void ExplicitLicense_RendersItsDocumentLicensedBesideAnEvaluationOne()
    {
        using var license = new PrivateLicense();
        JsonNode evaluation = Start();
        _workspace.Run("cells", "create", "second.xlsx", "--sheets", "Data", "--output-format", "json").Succeeded();

        JsonNode licensed = _workspace.Run(
            "preview", "open", "second.xlsx", "--no-open", "--license", license.Path, "--output-format", "json").Json();

        // A licensed result omits its license block.
        Assert.Null(licensed["license"]);
        Assert.Equal("evaluation", evaluation["license"]!["mode"]!.GetValue<string>());
        Assert.NotEqual(evaluation["id"]!.GetValue<string>(), licensed["id"]!.GetValue<string>());
        // One service holds both, because the renderer behind it is recycled.
        Assert.Equal(evaluation["pid"]!.GetValue<int>(), licensed["pid"]!.GetValue<int>());
        Assert.Equal(2, DocumentIds().Length);
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void EvaluationRequest_OpensItsOwnDocumentAndIsReusedOnlyByTheSameRequest()
    {
        JsonNode configured = Start();

        JsonNode requested = _workspace.Run(
            "preview", "open", "book.xlsx", "--no-open", "--license-mode", "evaluation", "--output-format", "json").Json();
        JsonNode again = _workspace.Run(
            "preview", "open", "book.xlsx", "--no-open", "--license-mode", "evaluation", "--output-format", "json").Json();

        Assert.Equal("evaluation", requested["license"]!["mode"]!.GetValue<string>());
        Assert.False(requested["reused"]!.GetValue<bool>());
        Assert.NotEqual(configured["id"]!.GetValue<string>(), requested["id"]!.GetValue<string>());
        Assert.True(again["reused"]!.GetValue<bool>());
        Assert.Equal(requested["id"]!.GetValue<string>(), again["id"]!.GetValue<string>());
        Assert.True(Start()["reused"]!.GetValue<bool>());
        Assert.Equal(2, DocumentIds().Length);
    }

    [Category(TestCategory.Slow)]
    [LicensedFact]
    public void EvaluationRequest_RendersInEvaluationBesideTheLicensedDocument()
    {
        using var license = new PrivateLicense();
        JsonNode licensed = _workspace.Run(
            "preview", "open", "book.xlsx", "--no-open", "--license", license.Path, "--output-format", "json").Json();

        JsonNode requested = _workspace.Run(
            "preview", "open", "book.xlsx", "--no-open", "--license-mode", "evaluation", "--output-format", "json").Json();
        JsonNode relicensed = _workspace.Run(
            "preview", "open", "book.xlsx", "--no-open", "--license", license.Path, "--output-format", "json").Json();

        // A licensed result omits its license block.
        Assert.Null(licensed["license"]);
        Assert.Equal("evaluation", requested["license"]!["mode"]!.GetValue<string>());
        Assert.NotEqual(licensed["id"]!.GetValue<string>(), requested["id"]!.GetValue<string>());
        Assert.Equal(licensed["pid"]!.GetValue<int>(), requested["pid"]!.GetValue<int>());
        Assert.True(relicensed["reused"]!.GetValue<bool>());
        Assert.Null(relicensed["license"]);
    }

    // The service outlives the shell that started it; a document is rendered under the license
    // environment of the shell that opens it, not of that first shell.
    [Category(TestCategory.Slow)]
    [LicensedFact]
    public void EnvironmentLicense_OfTheRequestingShellRendersItsDocument()
    {
        using var license = new PrivateLicense();
        JsonNode evaluation = Start();
        _workspace.Run("cells", "create", "second.xlsx", "--sheets", "Data", "--output-format", "json").Succeeded();

        JsonNode requested = RunInLicensedShell(license.Path, "preview", "open", "second.xlsx", "--no-open", "--output-format", "json");

        Assert.Equal("evaluation", evaluation["license"]!["mode"]!.GetValue<string>());
        Assert.Equal(evaluation["pid"]!.GetValue<int>(), requested["pid"]!.GetValue<int>());
        // A licensed result omits its license block.
        Assert.Null(requested["license"]);
    }

    [Category(TestCategory.Slow)]
    [LicensedFact]
    public void ShellWithoutALicense_RendersInEvaluationInAServiceALicensedShellStarted()
    {
        using var license = new PrivateLicense();
        JsonNode started = RunInLicensedShell(license.Path, "preview", "open", "book.xlsx", "--no-open", "--output-format", "json");
        _workspace.Run("cells", "create", "second.xlsx", "--sheets", "Data", "--output-format", "json").Succeeded();

        JsonNode requested = _workspace.Run(
            "preview", "open", "second.xlsx", "--no-open", "--output-format", "json").Json();

        Assert.Null(started["license"]);
        Assert.Equal(started["pid"]!.GetValue<int>(), requested["pid"]!.GetValue<int>());
        Assert.Equal("evaluation", requested["license"]!["mode"]!.GetValue<string>());
    }

    [Category(TestCategory.Slow)]
    [LicensedFact]
    public void ProjectLicense_OfTheRequestingDirectoryRendersItsDocument()
    {
        JsonNode evaluation = Start();
        string project = Directory.CreateDirectory(_workspace.File(Path.Combine("project", ".aspose"))).Parent!.FullName;
        File.Copy(TestLicense.Path!, Path.Combine(project, ".aspose", "license.lic"));
        _workspace.Run("cells", "create", "second.xlsx", "--sheets", "Data", "--output-format", "json").Succeeded();

        // The service started where no project license is; this command runs where one is.
        JsonNode requested = RunInShell(new Dictionary<string, string>(), project,
            "preview", "open", _workspace.File("second.xlsx"), "--no-open", "--output-format", "json");

        Assert.Equal("evaluation", evaluation["license"]!["mode"]!.GetValue<string>());
        Assert.Equal(evaluation["pid"]!.GetValue<int>(), requested["pid"]!.GetValue<int>());
        Assert.Null(requested["license"]);
    }

    [Category(TestCategory.Slow)]
    [LicensedFact]
    public void LicenseContent_FromTheRequestingShellIsWrittenNowhere()
    {
        string content = Convert.ToBase64String(File.ReadAllBytes(TestLicense.Path!));
        JsonNode licensed = RunInShell(new Dictionary<string, string> { ["ASPOSE_LICENSE_B64"] = content }, null,
            "preview", "open", "book.xlsx", "--no-open", "--output-format", "json");
        JsonNode status = _workspace.Run("preview", "status", "--output-format", "json").Json();

        Assert.Null(licensed["license"]);
        Assert.DoesNotContain(content, status.ToJsonString(), StringComparison.Ordinal);
        string probe = content[..Math.Min(64, content.Length)];
        string temporary = UserStorage.TemporaryRoot();
        string[] written = new[]
            {
                _workspace.ConfigDirectory,
                Path.Combine(temporary, "preview"),
                Path.Combine(temporary, "services"),
            }
            .Where(Directory.Exists)
            .SelectMany(static root => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            .Where(file => Contains(file, probe))
            .ToArray();
        Assert.Empty(written);

        static bool Contains(string file, string text)
        {
            try { return System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(file)).Contains(text, StringComparison.Ordinal); }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
    }

    // A shell whose environment names the license file; the workspace runs strip license sources.
    private JsonNode RunInLicensedShell(string license, params string[] args) =>
        RunInShell(new Dictionary<string, string> { ["ASPOSE_LICENSE_PATH"] = license }, null, args);

    private JsonNode RunInShell(IReadOnlyDictionary<string, string> variables, string? workingDirectory, params string[] args)
    {
        System.Diagnostics.ProcessStartInfo start = _workspace.StartInfo();
        foreach ((string name, string value) in variables) { start.Environment[name] = value; }
        if (workingDirectory is not null) { start.WorkingDirectory = workingDirectory; }
        start.RedirectStandardOutput = start.RedirectStandardError = true;
        start.StandardOutputEncoding = start.StandardErrorEncoding = System.Text.Encoding.UTF8;
        foreach (string arg in args) { start.ArgumentList.Add(arg); }
        using var process = System.Diagnostics.Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromSeconds(120)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"aspose-cli {string.Join(' ', args)} did not finish.");
        }
        Assert.True(process.ExitCode == 0, error.Result);
        return JsonNode.Parse(output.Result)!;
    }

    private JsonNode Start() =>
        _workspace.Run("preview", "open", "book.xlsx", "--no-open", "--output-format", "json").Json();

    private string[] DocumentIds() =>
        _workspace.Run("preview", "status", "--output-format", "json").Json()["sessions"]!
            .AsArray()
            .Select(static session => session!["id"]!.GetValue<string>())
            .ToArray();

    /// <summary>A private copy of the test license, removed with the test.</summary>
    private sealed class PrivateLicense : IDisposable
    {
        private readonly string _directory = UserStorage.CreateTemporaryDirectory("preview-license-tests");

        public PrivateLicense()
        {
            Path = System.IO.Path.Combine(_directory, "test.lic");
            string original = TestLicense.Path!;
            try
            {
                using FileStream input = System.IO.File.OpenRead(original);
                using FileStream output = new(Path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                input.CopyTo(output);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public string Path { get; }

        public void Dispose()
        {
            string root = System.IO.Path.GetFullPath(UserStorage.TemporaryRoot())
                + System.IO.Path.DirectorySeparatorChar;
            string directory = System.IO.Path.GetFullPath(_directory);
            if (!directory.StartsWith(
                    root,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Refusing cleanup outside the owned private test root.");
            }
            Assert.True(
                LocalFileCleanup.DeleteDirectory(directory),
                "Private test license copy could not be removed.");
        }
    }
}
