using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Execution;
using Xunit;

namespace Aspose.Cli.Platform.Tests.IO;

public sealed class LocalDocumentResourceLoaderTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "aspose-cli-resource-tests",
        Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TryRead_AllowsOnlyGuardedLocalFiles(bool snapshot)
    {
        Directory.CreateDirectory(_root);
        string document = Path.Combine(_root, "input.html");
        string local = Path.Combine(_root, "image.bin");
        File.WriteAllText(document, "document");
        File.WriteAllBytes(local, [1, 2, 3]);
        using var deadline = OperationDeadline.Start(null);
        var budgets = new ResourceBudgetLedger(deadline);
        string loaded = document;
        if (snapshot)
        {
            string directory = Path.Combine(_root, "private");
            Directory.CreateDirectory(directory);
            loaded = Path.Combine(directory, "input.html");
            File.Copy(document, loaded);
            budgets.Inputs.SetSnapshotOrigin(loaded, document);
        }
        using var loader = new LocalDocumentResourceLoader(loaded, budgets);

        Assert.True(loader.TryRead("image.bin", out byte[] bytes));
        Assert.Equal([1, 2, 3], bytes);
        Assert.True(loader.TryRead(new Uri(local).AbsoluteUri, out bytes));
        Assert.Equal([1, 2, 3], bytes);
        Assert.False(loader.TryRead("../outside.bin", out _));
        Assert.False(loader.TryRead("https://example.invalid/image.bin", out _));
        if (snapshot)
        {
            using var unrelated = new LocalDocumentResourceLoader(Path.Combine(_root, "private", "other.html"), budgets);
            Assert.False(unrelated.TryRead("image.bin", out _));
        }
    }

    [Theory]
    [InlineData("https://example.invalid/image.png")]
    [InlineData("http://example.invalid/image.png")]
    [InlineData("//example.invalid/image.png")]
    [InlineData("data:image/png;base64,AA==")]
    [InlineData("file:///outside/image.png")]
    [InlineData("\\\\server\\share\\image.png")]
    [InlineData("../outside.png")]
    public void TryRead_BlocksExternalAndEscapingReferences(string reference)
    {
        Directory.CreateDirectory(_root);
        string document = Path.Combine(_root, "input.html");
        File.WriteAllText(document, "document");
        using var deadline = OperationDeadline.Start(null);
        using var loader = new LocalDocumentResourceLoader(document, new ResourceBudgetLedger(deadline));

        Assert.False(loader.TryRead(reference, out byte[] bytes));
        Assert.Empty(bytes);
    }

    [Fact]
    public void TryRead_EnforcesItemAndAggregateBudgets()
    {
        Directory.CreateDirectory(_root);
        string document = Path.Combine(_root, "input.html");
        File.WriteAllText(document, "document");
        File.WriteAllBytes(Path.Combine(_root, "one.bin"), [1, 2]);
        File.WriteAllBytes(Path.Combine(_root, "two.bin"), [3, 4]);
        using var deadline = OperationDeadline.Start(null);
        using var loader = new LocalDocumentResourceLoader(
            document, new ResourceBudgetLedger(deadline),
            maximumItems: 1,
            maximumItemBytes: 2,
            maximumTotalBytes: 2);

        Assert.True(loader.TryRead("one.bin", out _));
        Assert.False(loader.TryRead("two.bin", out _));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
