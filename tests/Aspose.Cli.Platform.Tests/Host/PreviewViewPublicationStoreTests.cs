using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.Preview;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Preview;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Host.Tests.PreviewInfrastructure;

public sealed class PreviewViewPublicationStoreTests
{
    [Fact]
    public void Publish_EvictsTheInactiveLeastRecentlyUsedEntry()
    {
        using var temp = new TempDirectory();
        using var store = Store(temp, maximumEntries: 2);
        PreviewViewPublication first = PublishView(store, "first");
        PreviewViewPublication second = PublishView(store, "second");
        Assert.True(store.TryAcquire(first.Token, out var touched));
        touched!.Dispose();

        PreviewViewPublication third = PublishView(store, "third");

        Assert.True(store.TryAcquire(first.Token, out var firstLease));
        firstLease!.Dispose();
        Assert.False(store.TryAcquire(second.Token, out _));
        Assert.False(Directory.Exists(second.OwnedDirectory));
        Assert.True(store.TryAcquire(third.Token, out var thirdLease));
        thirdLease!.Dispose();
        Assert.Equal(2, store.Count);
    }

    [Fact]
    public void Publish_WhenEveryEntryIsActive_RejectsInsteadOfBreakingAResponse()
    {
        using var temp = new TempDirectory();
        using var store = Store(temp, maximumEntries: 1);
        PreviewViewPublication first = PublishView(store, "first");
        Assert.True(store.TryAcquire(first.Token, out var active));

        CliException error = Assert.Throws<CliException>(() =>
            PublishView(store, "second"));

        Assert.Equal(ErrorCodes.PreviewBudgetExceeded, error.Code);
        Assert.Equal("first", ReadEntry(active!.Snapshot));
        Assert.Equal(1, store.Count);
        active.Dispose();
    }

    [Fact]
    public void Acquire_AfterTheFixedLifetime_RetiresThePublication()
    {
        using var temp = new TempDirectory();
        DateTimeOffset now = new(2026, 8, 12, 0, 0, 0, TimeSpan.Zero);
        using var store = new PreviewViewPublicationStore(
            temp.File("views"),
            maximumEntries: 2,
            maximumBytes: 1024,
            lifetime: TimeSpan.FromMinutes(5),
            utcNow: () => now);
        PreviewViewPublication publication = PublishView(store, "view");

        now = now.AddMinutes(6);

        Assert.False(store.TryAcquire(publication.Token, out _));
        Assert.Equal(0, store.Count);
        Assert.False(Directory.Exists(publication.OwnedDirectory));
    }

    [Fact]
    public void Publish_WhenOneViewExceedsTheByteBound_PreservesExistingViews()
    {
        using var temp = new TempDirectory();
        using var store = new PreviewViewPublicationStore(
            temp.File("views"),
            maximumEntries: 2,
            maximumBytes: 16,
            lifetime: TimeSpan.FromMinutes(5),
            utcNow: static () => DateTimeOffset.UtcNow);
        PreviewViewPublication existing = PublishView(store, "existing");

        CliException error = Assert.Throws<CliException>(() =>
            store.Publish(1, sink =>
            {
                sink.WriteText("index.html", new string('x', 17));
                return Outcome();
            }));

        Assert.Equal(ErrorCodes.PreviewBudgetExceeded, error.Code);
        Assert.Equal(1, store.Count);
        Assert.Single(Directory.EnumerateDirectories(temp.File("views")));
        Assert.True(store.TryAcquire(existing.Token, out var lease));
        Assert.Equal("existing", ReadEntry(lease!.Snapshot));
        lease.Dispose();
    }

    [Fact]
    public void Publish_WhenArtifactWriteFails_DeletesTheOwnedTokenDirectory()
    {
        using var temp = new TempDirectory();
        using var store = new PreviewViewPublicationStore(
            temp.File("views"),
            maximumEntries: 2,
            maximumBytes: 4,
            lifetime: TimeSpan.FromMinutes(5),
            utcNow: static () => DateTimeOffset.UtcNow);
        Assert.Throws<CliException>(() => store.Publish(1, sink =>
        {
            sink.WriteText("index.html", "too-large");
            return Outcome();
        }));

        Assert.Empty(Directory.EnumerateDirectories(temp.File("views")));
    }

    [Fact]
    public void Publish_WhenRendererCatchesASinkFailure_StillFailsClosed()
    {
        using var temp = new TempDirectory();
        using var store = new PreviewViewPublicationStore(
            temp.File("views"),
            maximumEntries: 2,
            maximumBytes: 4,
            lifetime: TimeSpan.FromMinutes(5),
            utcNow: static () => DateTimeOffset.UtcNow);

        CliException error = Assert.Throws<CliException>(() =>
            store.Publish(1, sink =>
            {
                try
                {
                    sink.Write("index.html", stream =>
                        stream.Write(new byte[5]));
                }
                catch (CliException)
                {
                    // A renderer may add diagnostics, but the store retains
                    // the breached budget as the publication outcome.
                }

                return Outcome();
            }));

        Assert.Equal(ErrorCodes.PreviewBudgetExceeded, error.Code);
        Assert.Empty(Directory.EnumerateDirectories(temp.File("views")));
    }

    [Fact]
    public void Publish_WhenRendererStartsAnAsyncWrite_FailsClosed()
    {
        using var temp = new TempDirectory();
        using var store = Store(temp, maximumEntries: 2);

        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            store.Publish(1, sink =>
            {
                try
                {
                    sink.Write("index.html", stream =>
                    {
                        _ = stream.WriteAsync(new byte[] { 1 });
                    });
                }
                catch (NotSupportedException)
                {
                    // The sink retains the failure even if product code tries
                    // to continue the publication.
                }

                return Outcome();
            }));

        Assert.Contains("synchronously", error.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateDirectories(temp.File("views")));
    }

    [Fact]
    public void Publish_RejectsDuplicateArtifactPathsDeterministically()
    {
        using var temp = new TempDirectory();
        using var store = Store(temp, maximumEntries: 2);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            store.Publish(1, sink =>
            {
                sink.WriteText("index.html", "first");
                sink.WriteText(
                    OperatingSystem.IsWindows()
                        ? "INDEX.HTML"
                        : "index.html",
                    "second");
                return Outcome();
            }));

        Assert.Contains("more than once", error.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateDirectories(temp.File("views")));
    }

    [Fact]
    public async Task Publish_SerializesMaterializationToBoundTransientDiskUsage()
    {
        using var temp = new TempDirectory();
        using var store = new PreviewViewPublicationStore(
            temp.File("views"),
            maximumEntries: 16,
            maximumBytes: 4096,
            lifetime: TimeSpan.FromMinutes(5),
            utcNow: static () => DateTimeOffset.UtcNow);
        int active = 0;
        int maximumActive = 0;
        var concurrencyGate = new object();

        Task[] publishers = Enumerable.Range(0, 8)
            .Select(index => Task.Run(() =>
            {
                using PreviewViewPublicationStore.PreviewViewLease publication =
                    store.Publish(1, sink =>
                    {
                        lock (concurrencyGate)
                        {
                            active++;
                            maximumActive = Math.Max(maximumActive, active);
                        }
                        Thread.Sleep(10);
                        lock (concurrencyGate)
                        {
                            active--;
                        }
                        sink.WriteText("index.html", index.ToString());
                        return Outcome();
                    });
            }))
            .ToArray();

        await Task.WhenAll(publishers);

        Assert.Equal(1, maximumActive);
        Assert.Equal(8, store.Count);
    }

    [Fact]
    public void Publish_RejectsTraversalWithoutTouchingAnOutsideFile()
    {
        using var temp = new TempDirectory();
        using var store = Store(temp, maximumEntries: 2);
        string outside = temp.File("renderer-owned");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "sentinel.txt"), "preserve");
        Assert.Throws<InvalidDataException>(() =>
            store.Publish(1, sink =>
            {
                sink.WriteText("../renderer-owned/sentinel.txt", "replace");
                return Outcome();
            }));

        Assert.Equal(
            "preserve",
            File.ReadAllText(Path.Combine(outside, "sentinel.txt")));
    }

    [Fact]
    public async Task Dispose_DuringMaterialization_RemovesARecreatedTokenDirectory()
    {
        using var temp = new TempDirectory();
        string root = temp.File("views");
        var store = new PreviewViewPublicationStore(
            root,
            maximumEntries: 2,
            maximumBytes: 4096,
            lifetime: TimeSpan.FromMinutes(5),
            utcNow: static () => DateTimeOffset.UtcNow);
        using var entered = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        string? ownedDirectory = null;
        Task<PreviewViewPublicationStore.PreviewViewLease> publishing = Task.Run(() =>
            store.Publish(1, sink =>
            {
                sink.WriteText("index.html", "late");
                ownedDirectory = Assert.Single(
                    Directory.EnumerateDirectories(root));
                entered.Set();
                Assert.True(resume.Wait(TimeSpan.FromSeconds(10)));
                return Outcome();
            }));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));

        store.Dispose();
        resume.Set();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await publishing);
        Assert.NotNull(ownedDirectory);
        Assert.False(Directory.Exists(ownedDirectory));
        Assert.True(Directory.Exists(root));
    }

    [Fact]
    public void Dispose_DefersDirectoryDeletionUntilAnActiveLeaseEnds()
    {
        using var temp = new TempDirectory();
        var store = Store(temp, maximumEntries: 1);
        PreviewViewPublication publication = PublishView(store, "active");
        string directory = publication.Snapshot.DirectoryPath!;
        Assert.True(store.TryAcquire(publication.Token, out var active));

        store.Dispose();

        Assert.True(Directory.Exists(directory));
        active!.Dispose();
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public void Publish_ReturnsAResponseLeaseThatSurvivesConcurrentDisposal()
    {
        using var temp = new TempDirectory();
        var store = Store(temp, maximumEntries: 1);
        using PreviewViewPublicationStore.PreviewViewLease response =
            store.Publish(1, sink =>
            {
                sink.WriteText("index.html", "response");
                return Outcome();
            });

        store.Dispose();

        Assert.True(Directory.Exists(response.OwnedDirectory));
        Assert.Equal("response", ReadEntry(response.Snapshot));
        response.Dispose();
        Assert.False(Directory.Exists(response.OwnedDirectory));
    }

    [Fact]
    public void Dispose_DeletesAnOwnedPublicationDirectory()
    {
        using var temp = new TempDirectory();
        var store = Store(temp, maximumEntries: 1);
        PreviewViewPublication publication = PublishView(store, "view");
        Assert.True(Directory.Exists(publication.OwnedDirectory));

        store.Dispose();

        Assert.False(Directory.Exists(publication.OwnedDirectory));
    }

    private static PreviewViewPublicationStore Store(
        TempDirectory temp,
        int maximumEntries) => new(
            temp.File("views"),
            maximumEntries,
            maximumBytes: 4096,
            lifetime: TimeSpan.FromMinutes(5),
            utcNow: static () => DateTimeOffset.UtcNow);

    private static PreviewViewPublication PublishView(
        PreviewViewPublicationStore store,
        string content)
    {
        using PreviewViewPublicationStore.PreviewViewLease lease =
            store.Publish(1, sink =>
            {
                sink.WriteText("index.html", content);
                return Outcome();
            });
        return lease.Publication;
    }

    private static PreviewRenderOutcome Outcome() =>
        new("index.html", "test", 1);

    private static string ReadEntry(PreviewSnapshot snapshot)
    {
        using Stream stream = snapshot.ArtifactManifest!
            .TryOpenRead(snapshot.EntryFileName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

}
