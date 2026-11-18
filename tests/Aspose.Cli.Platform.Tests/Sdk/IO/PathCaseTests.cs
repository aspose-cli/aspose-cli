using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Extraction;
using Aspose.Cli.Sdk.IO.Files;
using Aspose.Cli.Sdk.IO.Paths;
using Aspose.Cli.Sdk.IO.Publication;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

/// <summary>
/// Windows file names ignore case, so every place that compares local paths treats two
/// spellings of one path as the same path: the publication plan, the output set and the
/// directory boundaries. The tests spell the second path in upper case, which every test
/// directory under %TEMP% changes.
/// </summary>
public sealed class PathCaseTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private static string Upper(string path) => path.ToUpperInvariant();

    [Fact]
    public void PublicationPlan_TreatsACaseVariantOfItsDirectoryAndArtifactsAsItsOwn()
    {
        AtomicPublicationPlan plan = AtomicPublicationPlan.Create(_temp.Path, "case", NoPublicationFaultInjector.Instance, null);
        string target = _temp.File("report.txt");
        string staged = AtomicPublicationPlan.StagedPath(plan.StagingDirectory, target, index: 0);
        Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
        File.WriteAllText(staged, "staged");
        plan.Entries.Add(new PublicationEntry
        {
            Index = 0,
            Target = Upper(target),
            Staged = Upper(staged),
            Overwrite = false,
            Original = FilePublicationSnapshot.Capture(target),
            StagedSnapshot = FilePublicationSnapshot.Capture(staged),
        });

        plan.EnsureTarget(Upper(target));
        plan.EnsureNoUnknownArtifacts();

        File.WriteAllText(Path.Combine(Path.GetDirectoryName(staged)!, "unknown.txt"), "unknown");
        Assert.Throws<IOException>(plan.EnsureNoUnknownArtifacts);
    }

    [Fact]
    public void OutputSet_CaseVariantsShareOneRootAndOneDeclaredDirectory()
    {
        string lower = _temp.File("out");
        Assert.Equal(lower, OutputSetPaths.CommonDirectory([Path.Combine(lower, "x"), Upper(Path.Combine(lower, "y"))]));

        var directories = new OwnedOutputDirectories();
        directories.Ensure(Path.Combine(lower, "new"));
        directories.Ensure(Upper(Path.Combine(lower, "new")));

        Assert.Equal(2, directories.Declared.Count());
        Assert.True(Directory.Exists(Path.Combine(lower, "new")));
    }

    /// <summary>
    /// The boundary checks for a drive root, a trailing separator, a sibling that shares the
    /// prefix (<c>a</c> and <c>ab</c>), a case variant, <c>..</c>, a UNC path and a device path.
    /// </summary>
    [Theory]
    [InlineData("a", @"a\x", true)]
    [InlineData("a", @"A\x", true)]
    [InlineData("a", @"a\b\x", true)]
    [InlineData("a", @"ab\x", false)]
    [InlineData("a", @"x", false)]
    public void Boundaries_ForAPublicationTarget(string directory, string target, bool inside)
    {
        AtomicPublicationPlan plan = AtomicPublicationPlan.Create(_temp.File(directory), "boundary", NoPublicationFaultInjector.Instance, null);

        Exception? error = Record.Exception(() => plan.EnsureTarget(_temp.File(target)));

        Assert.Equal(inside, error is null);
    }

    /// <summary>
    /// The worker's private root and the license snapshot hold every path below them, also when
    /// the root is a drive root or ends in a separator.
    /// </summary>
    [Theory]
    [InlineData(@"C:\a", @"C:\a", true)]
    [InlineData(@"C:\a", @"C:\a\b", true)]
    [InlineData(@"C:\a", @"C:\A\B", true)]
    [InlineData(@"C:\a", @"c:\a", true)]
    [InlineData(@"C:\a", @"C:\ab", false)]
    [InlineData(@"C:\a", @"C:\ab\x", false)]
    [InlineData(@"C:\a", @"C:\AB\x", false)]
    [InlineData(@"C:\a\", @"C:\a\b", true)]
    [InlineData(@"C:\", @"C:\x", true)]
    [InlineData(@"C:\", @"C:\", true)]
    [InlineData(@"C:\", @"D:\x", false)]
    [InlineData(@"\\server\share\a", @"\\server\share\a\x", true)]
    [InlineData(@"\\server\share\a", @"\\server\share\ab", false)]
    public void Boundaries_ForTheLicenseSnapshot(string root, string path, bool overlaps) =>
        Assert.Equal(overlaps, Aspose.Cli.Sdk.Licensing.LicenseInstaller.IsInsideSnapshot(root, path));

    [Fact]
    public void Boundaries_ForASiblingThatSharesThePrefix()
    {
        Assert.Equal(_temp.Path, OutputSetPaths.CommonDirectory([_temp.File(@"a\x"), _temp.File(@"ab\y")]));
    }

    [Theory]
    [InlineData(@"C:\", @"C:\x", false, true)]
    [InlineData(@"C:\", @"C:\", true, true)]
    [InlineData(@"C:\", @"C:\", false, false)]
    [InlineData(@"C:\a", @"C:\a\b", false, true)]
    [InlineData(@"C:\a\", @"C:\a\b", false, true)]
    [InlineData(@"C:\a", @"c:\A\b", false, true)]
    [InlineData(@"C:\a", @"C:\A", true, true)]
    [InlineData(@"C:\a", @"C:\a", false, false)]
    [InlineData(@"C:\a", @"C:\ab", true, false)]
    [InlineData(@"C:\a", @"C:\ab\x", true, false)]
    [InlineData(@"C:\a", @"C:\", true, false)]
    [InlineData(@"C:\a\", @"C:\a", true, false)]
    public void FilePaths_IsWithin_TreatsTheRootTheSeparatorAndCaseAsTheBoundariesDo(string root, string path, bool allowEqual, bool within) =>
        Assert.Equal(within, FilePaths.IsWithin(root, path, allowEqual));

    [Theory]
    [InlineData(@"\\server\share\a.txt", true)]
    [InlineData("//server/share/a.txt", true)]
    [InlineData(@"\\?\C:\a.txt", true)]
    [InlineData(@"\\.\PhysicalDrive0", true)]
    [InlineData(@"C:\a.txt", false)]
    [InlineData(@"\a.txt", false)]
    [InlineData("a.txt", false)]
    public void FilePaths_IsRemoteOrDevice_RecognisesUncAndDevicePaths(string path, bool remote) =>
        Assert.Equal(remote, FilePaths.IsRemoteOrDevice(path));

    [Theory]
    [InlineData(@"\\?\C:\a.txt", true)]
    [InlineData(@"\\.\PhysicalDrive0", true)]
    [InlineData("//?/C:/a.txt", true)]
    [InlineData(@"\\?\UNC\server\share\a.txt", true)]
    [InlineData(@"\\server\share\a.txt", false)]
    [InlineData(@"\\?", false)]
    [InlineData(@"C:\a.txt", false)]
    public void FilePaths_IsDevice_RecognisesDevicePathsAlone(string path, bool device) =>
        Assert.Equal(device, FilePaths.IsDevice(path));

    [Theory]
    [InlineData(@"\\?\C:\a.txt", @"C:\a.txt")]
    [InlineData(@"\\.\C:\a.txt", @"C:\a.txt")]
    [InlineData("//?/C:/a.txt", "C:/a.txt")]
    [InlineData(@"\\?\UNC\server\share\a.txt", @"\\server\share\a.txt")]
    [InlineData(@"\\?\unc\server\share", @"\\server\share")]
    [InlineData(@"\\.\PhysicalDrive0", null)]
    [InlineData(@"\\?\C:", null)]
    [InlineData(@"\\server\share\a.txt", @"\\server\share\a.txt")]
    [InlineData(@"C:\a.txt", @"C:\a.txt")]
    [InlineData("a.txt", "a.txt")]
    public void FilePaths_Plain_IsThePlainPathADevicePathSpells(string path, string? plain) =>
        Assert.Equal(plain, FilePaths.Plain(path));

    [Fact]
    public void FilePaths_TwoSpellingsOfOnePath_AreTheSamePath()
    {
        Assert.True(FilePaths.Same(@"C:\Data\Book.xlsx", @"c:\data\BOOK.XLSX"));
        Assert.False(FilePaths.Same(@"C:\Data\Book.xlsx", @"C:\Data\Book.xls"));
        Assert.True(FilePaths.Comparer.Equals(@"C:\Data", @"c:\DATA"));
        Assert.Equal(StringComparison.OrdinalIgnoreCase, FilePaths.Comparison);
    }
}
