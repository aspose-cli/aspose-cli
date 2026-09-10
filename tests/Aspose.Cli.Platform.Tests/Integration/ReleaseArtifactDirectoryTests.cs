using System.Text;
using System.Text.Json;
using Aspose.Cli.Architecture.Tests;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>Real PowerShell coverage for release output ownership and filesystem boundaries.</summary>
public sealed class ReleaseArtifactDirectoryTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void FreshNestedParent_CreatesAnOwnedEmptyOutput(bool legacy, bool trailingSeparator)
    {
        if (!OperatingSystem.IsWindows()) { return; }
        using var directory = new TempDirectory();
        string allowed = directory.File("workspace/artifacts/publish/free");
        string target = Path.Combine(allowed, "portable");

        AssertSuccess(Initialize(legacy, directory,
            trailingSeparator ? target + Path.DirectorySeparatorChar : target, allowed));

        Assert.True(Directory.Exists(target));
        Assert.Empty(Directory.EnumerateFileSystemEntries(target));
        using JsonDocument marker = JsonDocument.Parse(File.ReadAllText(Marker(target)));
        Assert.Equal(1, marker.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("aspose-cli-build-output", marker.RootElement.GetProperty("productId").GetString());
        Assert.Equal("publish", marker.RootElement.GetProperty("kind").GetString());
        Assert.Equal(Path.GetFullPath(target), marker.RootElement.GetProperty("target").GetString());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void OwnedReinitialization_DeletesOnlyTheOwnedContents(bool legacy, bool trailingSeparator)
    {
        if (!OperatingSystem.IsWindows()) { return; }
        using var directory = new TempDirectory();
        string allowed = directory.File("publish");
        string target = Path.Combine(allowed, "portable");
        Directory.CreateDirectory(allowed);
        AssertSuccess(Initialize(legacy, directory, target, allowed));
        string nested = Path.Combine(target, "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "old.dll"), "generated output");
        string sibling = Path.Combine(allowed, "unrelated.txt");
        File.WriteAllText(sibling, "preserve sibling");
        byte[] marker = File.ReadAllBytes(Marker(target));

        AssertSuccess(Initialize(legacy, directory,
            trailingSeparator ? target + Path.DirectorySeparatorChar : target, allowed));

        Assert.Empty(Directory.EnumerateFileSystemEntries(target));
        Assert.Equal("preserve sibling", File.ReadAllText(sibling));
        Assert.Equal(marker, File.ReadAllBytes(Marker(target)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnownedExistingOutput_IsPreservedWithoutClaimingOwnership(bool legacy)
    {
        if (!OperatingSystem.IsWindows()) { return; }
        using var directory = new TempDirectory();
        string allowed = directory.File("publish");
        string target = Path.Combine(allowed, "portable");
        Directory.CreateDirectory(target);
        string sentinel = Path.Combine(target, "user.txt");
        File.WriteAllText(sentinel, "preserve user output");

        CliResult result = Initialize(legacy, directory, target, allowed);

        AssertRefused(result, "exists without its ownership marker");
        Assert.Equal("preserve user output", File.ReadAllText(sentinel));
        Assert.False(File.Exists(Marker(target)));
    }

    [Theory]
    [InlineData(false, "root")]
    [InlineData(true, "root")]
    [InlineData(false, "root-trailing")]
    [InlineData(true, "root-trailing")]
    [InlineData(false, "sibling-prefix")]
    [InlineData(true, "sibling-prefix")]
    [InlineData(false, "parent-traversal")]
    [InlineData(true, "parent-traversal")]
    public void BoundaryEscape_IsRefusedBeforeWriting(bool legacy, string kind)
    {
        if (!OperatingSystem.IsWindows()) { return; }
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

        CliResult result = Initialize(legacy, directory, target, allowed);

        AssertRefused(result, "escaped its allowed root");
        Assert.Equal("preserve allowed root", File.ReadAllText(sentinel));
        Assert.False(File.Exists(Marker(Path.GetFullPath(target))));
        Assert.False(Directory.Exists(directory.File("publish-other")));
        Assert.False(Directory.Exists(directory.File("outside")));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void AncestorJunction_IsRefusedBeforeCreatingOrDeletingOutput(bool legacy, bool ownedOutput)
    {
        if (!OperatingSystem.IsWindows()) { return; }
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
        AssertSuccess(Run(legacy, directory,
            $"New-Item -ItemType Junction -Path {Literal(junction)} -Target {Literal(outside)} | Out-Null"));
        try
        {
            Assert.True((File.GetAttributes(junction) & FileAttributes.ReparsePoint) != 0);

            CliResult result = Initialize(legacy, directory, target, allowed);

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

    private static CliResult Initialize(bool legacy, TempDirectory directory, string target, string allowed) =>
        Run(legacy, directory,
            $"Initialize-OwnedArtifactDirectory -Path {Literal(target)} -AllowedRoot {Literal(allowed)} -Kind publish");

    private static CliResult Run(bool legacy, TempDirectory directory, string command)
    {
        string shell = legacy
            ? Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe")
            : (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .Select(static path => Path.Combine(path, "pwsh.exe")).First(File.Exists);
        string helper = Path.Combine(RepositoryPaths.Root, "scripts", "release-common.ps1");
        string script = "[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false); $ErrorActionPreference = 'Stop'; "
            + $"try {{ . {Literal(helper)}; {command} }} catch {{ [Console]::Error.WriteLine($_.Exception.Message); exit 17 }}";
        return new CliProcess(shell, CliEnvironment.Evaluation(directory.File("config")), TimeSpan.FromSeconds(30))
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

    private static void AssertSuccess(CliResult result) =>
        Assert.True(result.ExitCode == 0, result.StdOut + result.StdErr);

    private static void AssertRefused(CliResult result, string message)
    {
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(message, result.StdErr, StringComparison.OrdinalIgnoreCase);
    }

    private static string Marker(string path) => path + ".aspose-owner.json";
    private static string Literal(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
}
