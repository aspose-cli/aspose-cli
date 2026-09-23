using Aspose.Cli.Host.Updating;
using Aspose.Cli.Sdk.Errors;
using Xunit;

namespace Aspose.Cli.Host.Tests;

public sealed class UpdateClientSecurityTests
{
    private const string CurrentRevision = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string CandidateRevision = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Theory]
    [InlineData("1.0.1", "1.0.0", 1)]
    [InlineData("2.0.0", "10.0.0", -1)]
    [InlineData("1.0.0-alpha", "1.0.0", -1)]
    [InlineData("1.0.0", "1.0.0-rc.1", 1)]
    [InlineData("1.0.0-alpha.2", "1.0.0-alpha.10", -1)]
    [InlineData("1.0.0-alpha.1", "1.0.0-alpha.beta", -1)]
    [InlineData("1.0.0-beta.11", "1.0.0-rc.1", -1)]
    [InlineData("999999999999999999999.0.0", "2.0.0", 1)]
    public void SemanticVersionPrecedence_FollowsSemVer(
        string candidate,
        string current,
        int expected)
    {
        int actual = UpdateClient.CompareArtifactIdentity(
            candidate,
            CandidateRevision,
            current,
            CurrentRevision);

        Assert.Equal(expected, Math.Sign(actual));
    }

    [Theory]
    [InlineData("1.0.0+build.2", "1.0.0+build.1")]
    [InlineData("1.0.0", "1.0.0+build.1")]
    [InlineData("1.0.0+BUILD", "1.0.0+build")]
    public void EqualPrecedenceWithDifferentIdentity_IsRejected(
        string candidate,
        string current)
    {
        CliException error = Assert.Throws<CliException>(() =>
            UpdateClient.CompareArtifactIdentity(
                candidate,
                CandidateRevision,
                current,
                CurrentRevision));

        Assert.Equal("RELEASE_VERIFICATION_FAILED", error.Code.Name);
        Assert.Contains("equal semantic precedence", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateCandidate_RejectsDowngrade()
    {
        CliException error = Assert.Throws<CliException>(() =>
            UpdateClient.CompareUpdateCandidate(
                "1.0.0-rc.1",
                CandidateRevision,
                "1.0.0",
                CurrentRevision));

        Assert.Equal("RELEASE_VERIFICATION_FAILED", error.Code.Name);
        Assert.Contains("older", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("01.0.0")]
    [InlineData("1.0.0-01")]
    [InlineData("1.0.0+")]
    [InlineData("1.0.0+bad_value")]
    public void InvalidSemanticVersions_AreRejected(string version)
    {
        CliException error = Assert.Throws<CliException>(() =>
            UpdateClient.CompareArtifactIdentity(
                version,
                CandidateRevision,
                "1.0.0",
                CurrentRevision));

        Assert.Equal("RELEASE_VERIFICATION_FAILED", error.Code.Name);
    }

    [Fact]
    public void ExactArtifactIdentity_IsUpToDate()
    {
        Assert.Equal(0, UpdateClient.CompareArtifactIdentity(
            "1.2.3-rc.1+build.9",
            CurrentRevision.ToUpperInvariant(),
            "1.2.3-rc.1+build.9",
            CurrentRevision));
    }

    [Theory]
    [InlineData("https://user:secret@example.test/RELEASE-MANIFEST.json")]
    [InlineData("https://example.test/RELEASE-MANIFEST.json?token=secret")]
    [InlineData("https://example.test/RELEASE-MANIFEST.json#secret")]
    public void HttpsFeed_RejectsSecretBearingComponentsWithoutEchoingThem(string value)
    {
        CliException error = Assert.Throws<CliException>(() =>
            UpdateClient.ValidateHttpsFeedUri(new Uri(value)));

        Assert.Equal("OPTION_INVALID", error.Code.Name);
        Assert.DoesNotContain("secret", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HttpsFeed_AcceptsPlainManifestUrl()
    {
        UpdateClient.ValidateHttpsFeedUri(
            new Uri("https://downloads.example.test/releases/RELEASE-MANIFEST.json"));
    }

    [Fact]
    public void WindowsPowerShell_IsResolvedFromTheSystemDirectory()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string expected = Path.GetFullPath(Path.Combine(
            Environment.SystemDirectory,
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe"));

        Assert.Equal(expected, WindowsPowerShell.TryResolve());
    }

    [Fact]
    public void ExpiredHandoff_DoesNotStartAnInstallerAndCleansItsOwnedPackage()
    {
        if (!OperatingSystem.IsWindows()) { return; }
        string root = Aspose.Cli.Sdk.IO.PrivateUserStorage.CreateTemporaryDirectory("update-test");
        File.WriteAllText(Path.Combine(root, "install.ps1"), "throw 'This installer must never start.'");
        using var deadline = Aspose.Cli.Sdk.Execution.OperationDeadline.FromAbsoluteTick(
            TimeSpan.FromSeconds(1), Environment.TickCount64 - 1);
        CliException error = Assert.Throws<CliException>(() => UpdateClient.HandoffToInstaller(
            WindowsPowerShell.TryResolve()!, root, Path.Combine(root, "install"), Path.Combine(root, "status.json"), deadline));
        Assert.Equal(ErrorCodes.OperationTimeout, error.Code);
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void FailedInstallerStart_RemovesTheExtractedRoot()
    {
        string root = Aspose.Cli.Sdk.IO.PrivateUserStorage.CreateTemporaryDirectory("update-test");
        File.WriteAllText(Path.Combine(root, "install.ps1"), string.Empty);

        Assert.ThrowsAny<Exception>(() => UpdateClient.HandoffToInstaller(
            Path.Combine(root, "missing-powershell.exe"),
            root,
            Path.Combine(root, "install"),
            Path.Combine(root, "status.json")));
        Assert.False(Directory.Exists(root));
    }
}
