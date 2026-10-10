using Aspose.Cli.Sdk;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

public sealed class PublishedSchemaCacheTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void ABuild_IsReadOnceAndARebuildIsReadAgain()
    {
        string executable = Build("build", "first");
        string cache = _temp.File("cache");
        int reads = 0;

        Assert.Equal("first", Get(executable, cache, "first", ref reads));
        Assert.Equal("first", Get(executable, cache, "second", ref reads));
        Assert.Equal(1, reads);

        // A rebuild rewrites an assembly: new content and a later write time.
        string assembly = Path.Combine(Path.GetDirectoryName(executable)!, "Aspose.Cli.Product.Words.dll");
        File.WriteAllText(assembly, "rebuilt with another schema");
        File.SetLastWriteTimeUtc(assembly, DateTime.UtcNow.AddMinutes(1));
        Assert.Equal("second", Get(executable, cache, "second", ref reads));
        Assert.Equal("second", Get(executable, cache, "third", ref reads));
        Assert.Equal(2, reads);

        // Same content, only rewritten later: still another build.
        File.SetLastWriteTimeUtc(assembly, DateTime.UtcNow.AddMinutes(2));
        Assert.Equal("third", Get(executable, cache, "third", ref reads));
        Assert.Equal(3, reads);
    }

    [Fact]
    public void AnotherOutputDirectory_NeverSharesAnEntry()
    {
        string first = Build("one", "same");
        string second = Build("two", "same");
        foreach (string file in Directory.EnumerateFiles(Path.GetDirectoryName(first)!))
        {
            File.SetLastWriteTimeUtc(Path.Combine(Path.GetDirectoryName(second)!, Path.GetFileName(file)), File.GetLastWriteTimeUtc(file));
        }

        string cache = _temp.File("cache");
        int reads = 0;
        Assert.Equal("one", Get(first, cache, "one", ref reads));
        Assert.Equal("two", Get(second, cache, "two", ref reads));
        Assert.Equal("one", Get(first, cache, "never", ref reads));
        Assert.Equal(2, reads);
    }

    [Fact]
    public async Task ConcurrentFirstLoads_ReadTheBuildOnce()
    {
        string executable = Build("build", "content");
        string cache = _temp.File("cache");
        int reads = 0;
        string[] seen = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(() =>
            PublishedSchemaCache.GetOrAdd(executable, cache, () =>
            {
                Interlocked.Increment(ref reads);
                Thread.Sleep(200);
                return Snapshot("only");
            }).Documents["v2/common/x"])));

        Assert.All(seen, static document => Assert.Equal("only", document));
        Assert.Equal(1, reads);
    }

    [Fact]
    public void AnUnreadableEntry_IsReadAgain()
    {
        string executable = Build("build", "content");
        string cache = _temp.File("cache");
        int reads = 0;
        _ = Get(executable, cache, "first", ref reads);
        foreach (string entry in Directory.EnumerateFiles(cache, "*.json"))
        {
            File.WriteAllText(entry, "{ \"fingerprint\": ");
        }

        Assert.Equal("second", Get(executable, cache, "second", ref reads));
        Assert.Equal(2, reads);
    }

    private string Build(string name, string content)
    {
        string output = _temp.File(Path.Combine(name, "net10.0"));
        Directory.CreateDirectory(output);
        string executable = Path.Combine(output, DistributionInfo.CommandName + (OperatingSystem.IsWindows() ? ".exe" : ""));
        File.WriteAllText(executable, content);
        File.WriteAllText(Path.Combine(output, "Aspose.Cli.Product.Words.dll"), content);
        return executable;
    }

    private static string Get(string executable, string cache, string document, ref int reads)
    {
        int counted = reads;
        string result = PublishedSchemaCache.GetOrAdd(executable, cache, () =>
        {
            counted++;
            return Snapshot(document);
        }).Documents["v2/common/x"];
        reads = counted;
        return result;
    }

    private static PublishedSchemaSnapshot Snapshot(string document) =>
        new(["v2/common/x"], new Dictionary<string, string>(StringComparer.Ordinal) { ["v2/common/x"] = document });
}
