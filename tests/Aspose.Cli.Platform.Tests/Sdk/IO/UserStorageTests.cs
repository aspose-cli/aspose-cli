using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

public sealed class UserStorageTests
{
    [Fact]
    public void TemporaryRoot_IsAPerUserDirectoryBelowTheTemporaryPath()
    {
        Requires.Windows();
        string root = UserStorage.TemporaryRoot();

        Assert.True(Directory.Exists(root));
        Assert.Equal(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())),
            Path.GetDirectoryName(root),
            StringComparer.OrdinalIgnoreCase);
        Assert.StartsWith(DistributionInfo.Id + "-", Path.GetFileName(root), StringComparison.Ordinal);
        Assert.Equal(root, UserStorage.TemporaryRoot());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData(@"a\b")]
    [InlineData("a.b")]
    public void CreateTemporaryDirectory_RejectsCategoriesThatAreNotOneSimpleName(string category)
    {
        Assert.Throws<ArgumentException>(() => UserStorage.CreateTemporaryDirectory(category));
    }

    [Fact]
    public void CreateTemporaryDirectory_ReturnsAFreshDirectoryInItsCategory()
    {
        Requires.Windows();
        string first = UserStorage.CreateTemporaryDirectory("storage-test", "probe");
        string second = UserStorage.CreateTemporaryDirectory("storage-test", "probe");
        try
        {
            Assert.NotEqual(first, second, StringComparer.OrdinalIgnoreCase);
            foreach (string directory in new[] { first, second })
            {
                Assert.True(Directory.Exists(directory));
                Assert.Empty(Directory.EnumerateFileSystemEntries(directory));
                Assert.Equal(
                    Path.Combine(UserStorage.TemporaryRoot(), "storage-test"),
                    Path.GetDirectoryName(directory),
                    StringComparer.OrdinalIgnoreCase);
                Assert.StartsWith("probe-", Path.GetFileName(directory), StringComparison.Ordinal);
            }
        }
        finally
        {
            UserStorage.TryDeleteTree(first);
            UserStorage.TryDeleteTree(second);
        }
    }

    [Fact]
    public void TryDeleteTree_RemovesATree()
    {
        Requires.Windows();
        string root = UserStorage.CreateTemporaryDirectory("storage-test");
        string child = Directory.CreateDirectory(Path.Combine(root, "child")).FullName;
        File.WriteAllText(Path.Combine(child, "owned.txt"), "owned");
        File.WriteAllText(Path.Combine(root, "top.txt"), "owned");

        Assert.True(UserStorage.TryDeleteTree(root));
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void TryDeleteTree_ReportsAMissingRootAsDeleted()
    {
        using var temp = new TempDirectory();

        Assert.True(UserStorage.TryDeleteTree(temp.File("missing")));
    }

    [Fact]
    public void TryDeleteTree_PreservesADanglingRootLink()
    {
        Requires.Windows();
        using var temp = new TempDirectory();
        string link = temp.File("dangling-root");
        FileSystemLinks.CreateDirectoryLink(link, temp.File("missing-root"));

        try
        {
            Assert.False(UserStorage.TryDeleteTree(link));
            Assert.True(
                FilePublicationOwnedDelete.TryGetAttributesNoFollow(link)
                    ?.HasFlag(FileAttributes.ReparsePoint));
        }
        finally
        {
            Directory.Delete(link, recursive: false);
        }
    }

    [Fact]
    public void TryDeleteTree_KeepsATreeThatContainsALink()
    {
        Requires.Windows();
        using var temp = new TempDirectory();
        string outside = Directory.CreateDirectory(temp.File("outside")).FullName;
        File.WriteAllText(Path.Combine(outside, "kept.txt"), "kept");
        string root = UserStorage.CreateTemporaryDirectory("storage-test");
        string link = Path.Combine(root, "link");
        FileSystemLinks.CreateDirectoryLink(link, outside);

        try
        {
            Assert.False(UserStorage.TryDeleteTree(root));
            Assert.True(File.Exists(Path.Combine(outside, "kept.txt")));
        }
        finally
        {
            Directory.Delete(link, recursive: false);
            UserStorage.TryDeleteTree(root);
        }
    }
}
