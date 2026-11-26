using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Input;
using Xunit;

namespace Aspose.Cli.Platform.Tests.IO;

public sealed class LocalDocumentResourceLoaderTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "aspose-cli-resource-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void TryRead_AllowsOnlyGuardedLocalFiles()
    {
        Directory.CreateDirectory(_root);
        string document = Path.Combine(_root, "input.html");
        string local = Path.Combine(_root, "image.bin");
        File.WriteAllText(document, "document");
        File.WriteAllBytes(local, [1, 2, 3]);
        var budgets = new ResourceBudgetLedger();
        using var loader = new LocalDocumentResourceLoader(document, budgets);

        Assert.True(loader.TryRead("image.bin", out byte[] bytes));
        Assert.Equal([1, 2, 3], bytes);
        Assert.True(loader.TryRead(new Uri(local).AbsoluteUri, out bytes));
        Assert.Equal([1, 2, 3], bytes);
        Assert.False(loader.TryRead("../outside.bin", out _));
        Assert.False(loader.TryRead("https://example.invalid/image.bin", out _));
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
        using var loader = new LocalDocumentResourceLoader(document, new ResourceBudgetLedger());

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
        using var loader = new LocalDocumentResourceLoader(
            document, new ResourceBudgetLedger(),
            maximumItems: 1,
            maximumItemBytes: 2,
            maximumTotalBytes: 2);

        Assert.True(loader.TryRead("one.bin", out _));
        Assert.False(loader.TryRead("two.bin", out _));
    }

    /// <summary>
    /// An engine asks for resources on several threads, so the loader lists what it left out by
    /// reference, whatever order it was asked in, and its warning names the first ten of them.
    /// </summary>
    [Fact]
    public void Omitted_AreListedByReference()
    {
        Directory.CreateDirectory(_root);
        string document = Path.Combine(_root, "input.html");
        File.WriteAllText(document, "document");
        using var loader = new LocalDocumentResourceLoader(document, new ResourceBudgetLedger());

        foreach (int index in Enumerable.Range(0, 12).Reverse())
        {
            Assert.False(loader.TryRead($"{index:D2}.png", out _));
        }

        Assert.Equal(Enumerable.Range(0, 12).Select(static index => $"{index:D2}.png"), loader.Omitted.Select(static item => item.Reference));
        string listed = string.Join(", ", Enumerable.Range(0, 10).Select(static index => $"{index:D2}.png (not found)"));
        Assert.Equal($"12 resource(s) the document references were omitted: {listed}, and 2 more.", Assert.IsType<Warning>(loader.Warning).Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
