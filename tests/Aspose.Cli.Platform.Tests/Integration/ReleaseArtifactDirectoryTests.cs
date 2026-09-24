using System.Text;
using System.Text.Json;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>Real PowerShell coverage for release output ownership and filesystem boundaries.</summary>
public sealed class ReleaseArtifactDirectoryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FreshNestedParent_CreatesAnOwnedEmptyOutput(bool trailingSeparator)
    {
        Requires.Windows();
        using var directory = new TempDirectory();
        string allowed = directory.File("workspace/artifacts/publish/free");
        string target = Path.Combine(allowed, "portable");

        Initialize(directory,
            trailingSeparator ? target + Path.DirectorySeparatorChar : target, allowed).Succeeded();

        Assert.True(Directory.Exists(target));
        Assert.Empty(Directory.EnumerateFileSystemEntries(target));
        using JsonDocument marker = JsonDocument.Parse(File.ReadAllText(Marker(target)));
        Assert.Equal(1, marker.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("aspose-cli-build-output", marker.RootElement.GetProperty("productId").GetString());
        Assert.Equal("publish", marker.RootElement.GetProperty("kind").GetString());
        Assert.Equal(Path.GetFullPath(target), marker.RootElement.GetProperty("target").GetString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OwnedReinitialization_DeletesOnlyTheOwnedContents(bool trailingSeparator)
    {
        Requires.Windows();
        using var directory = new TempDirectory();
        string allowed = directory.File("publish");
        string target = Path.Combine(allowed, "portable");
        Directory.CreateDirectory(allowed);
        Initialize(directory, target, allowed).Succeeded();
        string nested = Path.Combine(target, "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "old.dll"), "generated output");
        string sibling = Path.Combine(allowed, "unrelated.txt");
        File.WriteAllText(sibling, "preserve sibling");
        byte[] marker = File.ReadAllBytes(Marker(target));

        Initialize(directory,
            trailingSeparator ? target + Path.DirectorySeparatorChar : target, allowed).Succeeded();

        Assert.Empty(Directory.EnumerateFileSystemEntries(target));
        Assert.Equal("preserve sibling", File.ReadAllText(sibling));
        Assert.Equal(marker, File.ReadAllBytes(Marker(target)));
    }

    [Fact]
    public void UnownedExistingOutput_IsPreservedWithoutClaimingOwnership()
    {
        Requires.Windows();
        using var directory = new TempDirectory();
        string allowed = directory.File("publish");
        string target = Path.Combine(allowed, "portable");
        Directory.CreateDirectory(target);
        string sentinel = Path.Combine(target, "user.txt");
        File.WriteAllText(sentinel, "preserve user output");

        CliResult result = Initialize(directory, target, allowed);

        AssertRefused(result, "exists without its ownership marker");
        Assert.Equal("preserve user output", File.ReadAllText(sentinel));
        Assert.False(File.Exists(Marker(target)));
    }

    [Theory]
    [InlineData("root")]
    [InlineData("root-trailing")]
    [InlineData("sibling-prefix")]
    [InlineData("parent-traversal")]
    public void BoundaryEscape_IsRefusedBeforeWriting(string kind)
    {
        Requires.Windows();
        using var directory = new TempDirectory();
        string allowed = directory.File("publish");
        Directory.CreateDirectory(allowed);
        string sentinel = Path.Combine(allowed, "keep.txt");
        File.WriteAllText(sentinel, "preserve allowed root");
        string target = kind switch
        {
            "root" => allowed,
            "root-trailing" => allowed + Path.DirectorySeparatorChar,
            "sibling-prefix" => directory.File("publish-other/portable"),
            _ => Path.Combine(allowed, "..", "outside", "portable"),
        };

        CliResult result = Initialize(directory, target, allowed);

        AssertRefused(result, "escaped its allowed root");
        Assert.Equal("preserve allowed root", File.ReadAllText(sentinel));
        Assert.False(File.Exists(Marker(Path.GetFullPath(target))));
        Assert.False(Directory.Exists(directory.File("publish-other")));
        Assert.False(Directory.Exists(directory.File("outside")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AncestorJunction_IsRefusedBeforeCreatingOrDeletingOutput(bool ownedOutput)
    {
        Requires.Windows();
        using var directory = new TempDirectory();
        string outside = directory.File("outside");
        string junction = directory.File("workspace");
        Directory.CreateDirectory(outside);
        string unrelated = Path.Combine(outside, "keep.txt");
        File.WriteAllText(unrelated, "preserve external tree");
        string allowed = Path.Combine(junction, "artifacts", "publish", "free");
        string target = Path.Combine(allowed, "portable");
        string actualTarget = Path.Combine(outside, "artifacts", "publish", "free", "portable");
        if (ownedOutput)
        {
            Directory.CreateDirectory(actualTarget);
            File.WriteAllText(Path.Combine(actualTarget, "keep.dll"), "preserve owned tree through link");
            WriteMarker(Marker(actualTarget), target);
        }
        FileSystemLinks.CreateDirectoryLink(junction, outside);
        try
        {
            Assert.True((File.GetAttributes(junction) & FileAttributes.ReparsePoint) != 0);

            CliResult result = Initialize(directory, target, allowed);

            AssertRefused(result, "reparse point");
            Assert.Equal("preserve external tree", File.ReadAllText(unrelated));
            if (ownedOutput)
            {
                Assert.Equal("preserve owned tree through link", File.ReadAllText(Path.Combine(actualTarget, "keep.dll")));
                Assert.True(File.Exists(Marker(actualTarget)));
            }
            else
            {
                Assert.False(Directory.Exists(Path.Combine(outside, "artifacts")));
            }
        }
        finally { Directory.Delete(junction); }
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void Publish_RefusesToDeleteAnUnownedOutputTree()
    {
        Requires.Windows();
        string parent = Path.Combine(
            RepositoryPaths.Root,
            "artifacts",
            "publish",
            "unowned-" + Guid.NewGuid().ToString("N"));
        string output = Path.Combine(parent, "win-x64");
        string sentinel = Path.Combine(output, "customer-owned.txt");
        Directory.CreateDirectory(output);
        File.WriteAllText(sentinel, "must survive", Encoding.UTF8);
        string[] before = Directory.GetFileSystemEntries(parent, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();

        try
        {
            using var workspace = new TempWorkspace();
            CliResult result = new CliProcess(
                ToolPath.Require("pwsh"),
                CliEnvironment.Evaluation(workspace.Path),
                TimeSpan.FromSeconds(120)).Run(
                workspace.Path,
                "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                "-File", Path.Combine(RepositoryPaths.Root, "scripts", "publish.ps1"),
                "-Configuration", "Release",
                "-RuntimeIdentifier", "win-x64",
                "-OutputRoot", output);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("exists without its ownership marker", result.StdErr, StringComparison.Ordinal);
            Assert.DoesNotContain("NamedParameterNotFound", result.StdErr, StringComparison.Ordinal);
            Assert.False(File.Exists(output + ".aspose-owner.json"));
            Assert.Equal(before, Directory.GetFileSystemEntries(parent, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
            Assert.True(File.Exists(sentinel), result.StdOut + result.StdErr);
            Assert.Equal("must survive", File.ReadAllText(sentinel));
        }
        finally
        {
            if (Directory.Exists(parent)) { Directory.Delete(parent, recursive: true); }
        }
    }

    private static CliResult Initialize(TempDirectory directory, string target, string allowed) =>
        Run(directory,
            $"Initialize-OwnedArtifactDirectory -Path {Literal(target)} -AllowedRoot {Literal(allowed)} -Kind publish");

    private static CliResult Run(TempDirectory directory, string command)
    {
        string helper = Path.Combine(RepositoryPaths.Root, "scripts", "release-common.ps1");
        string script = "[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false); $ErrorActionPreference = 'Stop'; "
            + $"try {{ . {Literal(helper)}; {command} }} catch {{ [Console]::Error.WriteLine($_.Exception.Message); exit 17 }}";
        return new CliProcess(ToolPath.Require("pwsh"), CliEnvironment.Evaluation(directory.File("config")), TimeSpan.FromSeconds(30))
            .Run(directory.Path, "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));
    }

    private static void WriteMarker(string path, string target) => File.WriteAllText(path, JsonSerializer.Serialize(new
    {
        schemaVersion = 1,
        productId = "aspose-cli-build-output",
        kind = "publish",
        target = Path.GetFullPath(target),
    }));

    private static void AssertRefused(CliResult result, string message)
    {
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(message, result.StdErr, StringComparison.OrdinalIgnoreCase);
    }

    private static string Marker(string path) => path + ".aspose-owner.json";
    private static string Literal(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
}
