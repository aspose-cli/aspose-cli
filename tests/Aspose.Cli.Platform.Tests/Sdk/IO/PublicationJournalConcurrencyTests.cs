using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

public sealed class PublicationJournalConcurrencyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AtomicReplacementOrDeletionWaitsUntilTheLiveReaderClosesItsHandle(bool delete)
    {
        using var temp = new TempDirectory();
        PrivateUserStorage.EnsureDirectory(temp.Path);
        string path = temp.File(AtomicPublicationPlan.JournalName);
        new PublicationJournal { Operation = "initial", State = PublicationTransactionState.Staging }.Write(path);
        using var releaseReader = new ManualResetEventSlim();
        var readerOpened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writerBlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TimeSpan watchdog = TimeSpan.FromSeconds(30);
        Task<PublicationJournal> reader = Task.Factory.StartNew(() => PublicationJournal.Read(path, new Faults(point =>
        {
            if (point.Kind != PublicationFaultKind.JournalRead) { return; }
            readerOpened.SetResult();
            Assert.True(releaseReader.Wait(watchdog));
        })), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Task writer = Task.CompletedTask;
        try
        {
            await readerOpened.Task.WaitAsync(watchdog);
            writer = Task.Factory.StartNew(() =>
            {
                var faults = new Faults(point =>
                {
                    if (point.Kind == PublicationFaultKind.JournalLockWait) { writerBlocked.TrySetResult(); }
                });
                if (delete) { PublicationJournal.Delete(path, faults); return; }
                for (int index = 0; index < 32; index++)
                {
                    new PublicationJournal { Operation = "update-" + index, State = PublicationTransactionState.Prepared }
                        .Write(path, faults);
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            Task first = await Task.WhenAny(writerBlocked.Task, writer).WaitAsync(watchdog);
            Assert.Same(writerBlocked.Task, first);
            Assert.False(writer.IsCompleted);
            releaseReader.Set();
            Assert.Equal("initial", (await reader.WaitAsync(watchdog)).Operation);
            await writer.WaitAsync(watchdog);
            if (delete) { Assert.Empty(Directory.EnumerateFileSystemEntries(temp.Path)); }
            else
            {
                Assert.Equal("update-31", PublicationJournal.Read(path).Operation);
                Assert.Single(Directory.EnumerateFileSystemEntries(temp.Path));
            }
        }
        finally
        {
            releaseReader.Set();
            await Task.WhenAll(reader, writer).WaitAsync(watchdog);
        }
    }

    [Fact]
    public async Task WaitingWriterHonorsCallerCancellationWithoutReplacingTheJournal()
    {
        using var temp = new TempDirectory();
        PrivateUserStorage.EnsureDirectory(temp.Path);
        string path = temp.File(AtomicPublicationPlan.JournalName);
        new PublicationJournal { Operation = "original", State = PublicationTransactionState.Staging }.Write(path);
        using var releaseReader = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        using OperationDeadline deadline = OperationDeadline.Start(null, cancellation.Token);
        var readerOpened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writerBlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TimeSpan watchdog = TimeSpan.FromSeconds(30);
        Task reader = Task.Factory.StartNew(() => PublicationJournal.Read(path, new Faults(point =>
        {
            if (point.Kind != PublicationFaultKind.JournalRead) { return; }
            readerOpened.SetResult();
            Assert.True(releaseReader.Wait(watchdog));
        })), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Task writer = Task.CompletedTask;
        try
        {
            await readerOpened.Task.WaitAsync(watchdog);
            writer = Task.Factory.StartNew(() => Assert.ThrowsAny<OperationCanceledException>(() =>
                new PublicationJournal { Operation = "replacement", State = PublicationTransactionState.Prepared }.Write(path,
                    new Faults(point =>
                    {
                        if (point.Kind == PublicationFaultKind.JournalLockWait) { writerBlocked.TrySetResult(); }
                    }), deadline)), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            Task first = await Task.WhenAny(writerBlocked.Task, writer).WaitAsync(watchdog);
            Assert.Same(writerBlocked.Task, first);
            cancellation.Cancel();
            await writer.WaitAsync(watchdog);
        }
        finally
        {
            releaseReader.Set();
            await Task.WhenAll(reader, writer).WaitAsync(watchdog);
        }
        Assert.Equal("original", PublicationJournal.Read(path).Operation);
    }

    [Fact]
    public void RecoveryJournalTimeoutDoesNotStopRestoringRemainingTargets()
    {
        using var temp = new TempDirectory();
        string first = temp.File("first.txt");
        string second = temp.File("second.txt");
        File.WriteAllText(first, "first original");
        File.WriteAllText(second, "second original");
        bool recovery = false;
        var faults = new Faults(point =>
        {
            if (point.Kind == PublicationFaultKind.Publish && point.EntryIndex == 1)
            {
                recovery = true;
                throw new IOException("stop after first publication");
            }
            if (point.Kind == PublicationFaultKind.JournalWrite && recovery)
            { throw CliErrors.OperationTimeout(1, "publication-journal-lock"); }
        });
        using var output = new AtomicOutputSetWriter(TestBudgets.Writer(), temp.Path, "recovery-timeout", faults);
        output.Stage(first, true, path => File.WriteAllText(path, "first replacement"));
        output.Stage(second, true, path => File.WriteAllText(path, "second replacement"));
        CliException failure = Assert.Throws<CliException>(() => output.Commit());
        Assert.Equal("first original", File.ReadAllText(first));
        Assert.Equal("second original", File.ReadAllText(second));
        Assert.False(failure.Details!["recoveryComplete"]!.GetValue<bool>());
        Assert.All(failure.Details["targets"]!.AsArray(), target =>
        {
            Assert.True(target!["contentVerified"]!.GetValue<bool>());
            Assert.True(target["metadataVerified"]!.GetValue<bool>());
        });
    }
    private sealed class Faults(Action<PublicationFaultPoint> action) : IPublicationFaultInjector
    {
        public void Hit(PublicationFaultPoint point) => action(point);
    }
}