using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.Preview;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Host.Tests.PreviewInfrastructure;

public sealed class PreviewSessionStorageTests
{
    [Fact]
    public void Create_RemovesOnlyRecognizedRootsWithoutALiveOwner()
    {
        using var temp = new TempDirectory();
        string category = temp.File("preview");
        Directory.CreateDirectory(category);
        string stale = CreateOwnedRoot(category, 101, 201, 'a');
        string live = CreateOwnedRoot(category, 102, 202, 'b');
        string unknown = Path.Combine(category, "customer-files");
        Directory.CreateDirectory(unknown);

        using PreviewSessionStorage storage = PreviewSessionStorage.Create(
            category,
            (processId, processStart) =>
                processId == 102 && processStart == 202,
            static path => LocalFileCleanup.DeleteDirectory(path));

        Assert.False(Directory.Exists(stale));
        Assert.True(Directory.Exists(live));
        Assert.True(Directory.Exists(unknown));
        Assert.True(Directory.Exists(storage.Root));
    }

    [Fact]
    public void Create_RetriesARecognizedRootLeftByFailedDisposal()
    {
        using var temp = new TempDirectory();
        string category = temp.File("preview");
        Directory.CreateDirectory(category);
        string? failedRoot = null;
        int failuresRemaining = 4;
        bool Delete(string path)
        {
            if (string.Equals(
                    path,
                    failedRoot,
                    StringComparison.OrdinalIgnoreCase)
                && failuresRemaining-- > 0)
            {
                return false;
            }

            return LocalFileCleanup.DeleteDirectory(path);
        }

        PreviewSessionStorage first = PreviewSessionStorage.Create(
            category,
            static (_, _) => false,
            Delete);
        failedRoot = first.Root;
        first.Dispose();
        Assert.True(Directory.Exists(failedRoot));

        using PreviewSessionStorage second = PreviewSessionStorage.Create(
            category,
            static (_, _) => false,
            Delete);

        Assert.False(Directory.Exists(failedRoot));
        Assert.True(Directory.Exists(second.Root));
    }

    [Fact]
    public void Create_PreservesARecognizedRootWithUnknownChildren()
    {
        using var temp = new TempDirectory();
        string category = temp.File("preview");
        Directory.CreateDirectory(category);
        string stale = CreateOwnedRoot(category, 101, 201, 'a');
        Directory.CreateDirectory(Path.Combine(stale, "customer-files"));

        using PreviewSessionStorage storage = PreviewSessionStorage.Create(
            category,
            static (_, _) => false,
            static path => LocalFileCleanup.DeleteDirectory(path));

        Assert.True(Directory.Exists(stale));
        Assert.True(Directory.Exists(storage.Root));
    }

    [Fact]
    public void Dispose_PreservesTheSessionRootWhenOwnershipDrifts()
    {
        using var temp = new TempDirectory();
        string category = temp.File("preview");
        PreviewSessionStorage storage = PreviewSessionStorage.Create(
            category,
            static (_, _) => false,
            static path => LocalFileCleanup.DeleteDirectory(path));
        string unknown = Path.Combine(storage.Root, "customer-files");
        Directory.CreateDirectory(unknown);

        storage.Dispose();

        Assert.True(Directory.Exists(unknown));
    }

    [Fact]
    public void Dispose_PreservesAnUnknownDirectoryInsideTheViewsRoot()
    {
        using var temp = new TempDirectory();
        string category = temp.File("preview");
        PreviewSessionStorage storage = PreviewSessionStorage.Create(
            category,
            static (_, _) => false,
            static path => LocalFileCleanup.DeleteDirectory(path));
        string unknown = Path.Combine(
            storage.Root,
            "views",
            "customer-files");
        Directory.CreateDirectory(unknown);

        storage.Dispose();

        Assert.True(Directory.Exists(unknown));
    }

    private static string CreateOwnedRoot(
        string category,
        int processId,
        long processStart,
        char tokenCharacter)
    {
        string path = Path.Combine(
            category,
            $"{processId}-{processStart}-{new string(tokenCharacter, 32)}");
        Directory.CreateDirectory(path);
        return path;
    }
}
