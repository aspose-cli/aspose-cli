using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Host.Tests;

public sealed class RetainedResourceTests
{
    [Fact]
    public async Task RetiringAnUploadWaitsForEveryBorrowerAndRejectsNewBorrowers()
    {
        using var temp = new TempDirectory();
        string path = temp.File("upload.bin");
        OwnedTemporaryFile file = OwnedTemporaryFile.Create(path);
        File.WriteAllText(path, "uploaded content");
        file.BindProducedFile();
        using var owner = new RetainedResource<OwnedTemporaryFile>(file);
        Assert.True(owner.TryAcquire(out var first));
        Assert.True(owner.TryAcquire(out var second));
        owner.Dispose();
        Assert.False(owner.TryAcquire(out _));
        Assert.True(File.Exists(path));
        Assert.False(owner.Completion.IsCompleted);
        first!.Dispose();
        first.Dispose();
        Assert.True(File.Exists(path));
        second!.Dispose();
        await owner.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(File.Exists(path));
        Assert.Throws<ObjectDisposedException>(() => first.Value);
    }
}
