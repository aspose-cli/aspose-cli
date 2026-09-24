using System.Text.Json.Nodes;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.IO;
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
        _workspace.Run("cells", "create", "book.xlsx", "--sheets", "Data", "--output", "json").Succeeded();

    public void Dispose()
    {
        _workspace.Run("preview", "stop", "--all", "--output", "json");
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
                "preview", "book.xlsx", "--license", license, "--output", "json");

            Assert.NotEqual(0, refused.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(refused.StdOut));
            Assert.StartsWith(
                "LICENSE_",
                JsonNode.Parse(refused.StdErr)!["error"]!["code"]!.GetValue<string>(),
                StringComparison.Ordinal);
            Assert.Equal([open["id"]!.GetValue<string>()], DocumentIds());
        }
    }

    [LicensedFact]
    public void ExplicitLicense_RendersItsDocumentLicensedBesideAnEvaluationOne()
    {
        using var license = new PrivateLicense();
        JsonNode evaluation = Start();
        _workspace.Run("cells", "create", "second.xlsx", "--sheets", "Data", "--output", "json").Succeeded();

        JsonNode licensed = _workspace.Run(
            "preview", "second.xlsx", "--license", license.Path, "--output", "json").Json();

        Assert.Equal("licensed", licensed["license"]!["mode"]!.GetValue<string>());
        Assert.Equal("evaluation", evaluation["license"]!["mode"]!.GetValue<string>());
        Assert.NotEqual(evaluation["id"]!.GetValue<string>(), licensed["id"]!.GetValue<string>());
        // One service holds both, because the renderer behind it is recycled.
        Assert.Equal(evaluation["pid"]!.GetValue<int>(), licensed["pid"]!.GetValue<int>());
        Assert.Equal(2, DocumentIds().Length);
    }

    private JsonNode Start() =>
        _workspace.Run("preview", "book.xlsx", "--output", "json").Json();

    private string[] DocumentIds() =>
        _workspace.Run("preview", "status", "--output", "json").Json()["sessions"]!
            .AsArray()
            .Select(static session => session!["id"]!.GetValue<string>())
            .ToArray();

    /// <summary>A private copy of the test license, removed with the test.</summary>
    private sealed class PrivateLicense : IDisposable
    {
        private readonly string _directory = PrivateUserStorage.CreateTemporaryDirectory("preview-license-tests");

        public PrivateLicense()
        {
            Path = System.IO.Path.Combine(_directory, "test.lic");
            string original = TestLicense.Path!;
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

        public void Dispose()
        {
            string root = System.IO.Path.GetFullPath(PrivateUserStorage.TemporaryRoot())
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
