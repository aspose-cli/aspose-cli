using System.Globalization;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Host.Tests;

/// <summary>
/// The local services read only the limits their guides name: the preview snapshot limits in
/// <c>docs preview</c> and the App upload limits in <c>docs app</c>, each with its default. Every
/// other ceiling is fixed, so no undocumented variable changes them.
/// </summary>
public sealed class LocalServiceResourceLimitsTests : IDisposable
{
    private static readonly string SourceRoot = Path.Combine(RepositoryPaths.Root, "src");
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    public static TheoryData<string, string, long> DocumentedLimits => new()
    {
        { "preview", "PREVIEW_MAX_FILES", 7 },
        { "preview", "PREVIEW_MAX_FILE_BYTES", 7 },
        { "preview", "PREVIEW_MAX_SNAPSHOT_BYTES", 7 },
        { "app", "APP_MAX_UPLOAD_FILES", 7 },
        { "app", "APP_MAX_UPLOAD_SESSION_BYTES", 7 },
    };

    [Theory]
    [MemberData(nameof(DocumentedLimits))]
    public void ADocumentedLimit_IsNamedWithItsDefaultAndApplies(string topic, string name, long value)
    {
        string variable = DistributionInfo.EnvironmentVariablePrefix + name;
        long defaultValue = Limit(LocalServiceResourceLimits.Resolve(static _ => null), name);
        CliResult guide = _workspace.Run("docs", topic);
        Assert.True(guide.ExitCode == 0, guide.StdErr);

        string line = Assert.Single(
            guide.StdOut.ReplaceLineEndings("\n").Split('\n'),
            text => text.Contains($"`{variable}`", StringComparison.Ordinal));
        Assert.Contains($"`{defaultValue.ToString(CultureInfo.InvariantCulture)}`", line, StringComparison.Ordinal);
        Assert.Equal(value, Limit(LocalServiceResourceLimits.Resolve(
            key => key == variable ? value.ToString(CultureInfo.InvariantCulture) : null), name));
    }

    [Theory]
    [InlineData("LOCAL_HTTP_MAX_REQUESTS", "2")]
    [InlineData("LOCAL_SSE_MAX_CLIENTS", "2")]
    [InlineData("LOCAL_SSE_QUEUE_CAPACITY", "2")]
    [InlineData("LOCAL_SSE_WRITE_TIMEOUT_MS", "200")]
    [InlineData("VIEWER_RENDER_TIMEOUT_MS", "2000")]
    public void AnUndocumentedVariable_ChangesNoLimit(string name, string value)
    {
        string variable = DistributionInfo.EnvironmentVariablePrefix + name;

        Assert.Equal(
            LocalServiceResourceLimits.Resolve(static _ => null),
            LocalServiceResourceLimits.Resolve(key => key == variable ? value : null));
    }

    [Fact]
    public void TheBrowserLauncher_ReadsNoTestSwitch()
    {
        string[] readers = [.. Directory.EnumerateFiles(SourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(static path => File.ReadAllText(path).Contains("NO_OPEN", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(SourceRoot, path))];

        Assert.Empty(readers);
    }

    private static long Limit(LocalServiceResourceLimits limits, string name) => name switch
    {
        "PREVIEW_MAX_FILES" => limits.MaximumSnapshotFiles,
        "PREVIEW_MAX_FILE_BYTES" => limits.MaximumSnapshotFileBytes,
        "PREVIEW_MAX_SNAPSHOT_BYTES" => limits.MaximumSnapshotBytes,
        "APP_MAX_UPLOAD_FILES" => limits.MaximumUploadFiles,
        "APP_MAX_UPLOAD_SESSION_BYTES" => limits.MaximumUploadSessionBytes,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };
}
