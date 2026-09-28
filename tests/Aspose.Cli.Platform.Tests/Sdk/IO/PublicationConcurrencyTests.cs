using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

[CollectionDefinition("Publication timing", DisableParallelization = true)]
public sealed class PublicationTimingCollection;

// Each test exercises concurrent operations itself; unrelated workloads must not consume its deadline.
[Collection("Publication timing")]
public sealed class PublicationConcurrencyTests
{
    [Fact]
    public void ConcurrentDirectoryPublicationsRemainIsolated()
    {
        Requires.Windows();

        using var temp = new TempDirectory();
        const int count = 20;

        Parallel.For(0, count, index =>
        {
            string target = temp.File($"output-{index:00}.txt");
            using var set = new AtomicOutputSetWriter(
                TestBudgets.Writer(),
                temp.Path,
                $"concurrent-{index:00}");
            set.Stage(
                target,
                overwrite: false,
                staged => File.WriteAllText(staged, index.ToString()));
            set.Commit();
        });

        for (int index = 0; index < count; index++)
        {
            Assert.Equal(
                index.ToString(),
                File.ReadAllText(temp.File($"output-{index:00}.txt")));
        }
        Assert.Empty(Directory.EnumerateDirectories(temp.Path, ".aspose-*"));
    }

    [Fact]
    public async Task AnIndependentCommitCompletesWhileAnotherDirectoryIsPaused()
    {
        TimeSpan watchdog = TimeSpan.FromSeconds(60);
        using var temp = new TempDirectory();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var resume = new ManualResetEventSlim();
        string first = temp.File(Path.Combine("a", "one.txt"));
        string second = temp.File(Path.Combine("b", "two.txt"));
        using var transaction = new AtomicOutputSetWriter(TestBudgets.Writer(), Path.GetDirectoryName(first)!, "paused",
            new Callback(point =>
            {
                if (point.Kind == PublicationFaultKind.Publish)
                {
                    entered.SetResult();
                    resume.Wait();
                }
            }));
        transaction.Stage(first, false, path => File.WriteAllText(path, "first"));
        Task commit = Task.Factory.StartNew(() => transaction.Commit(), CancellationToken.None,
            TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Task independent = Task.CompletedTask;
        try
        {
            if (await Task.WhenAny(entered.Task, commit).WaitAsync(watchdog) == commit)
            {
                await commit;
                Assert.Fail("Publication completed before reaching its pause.");
            }
            independent = Task.Run(() => TestBudgets.Writer().Write(second, false, path => File.WriteAllText(path, "second")));
            await independent.WaitAsync(watchdog);
            Assert.Equal("second", File.ReadAllText(second));
            Assert.False(File.Exists(first));
        }
        finally
        {
            resume.Set();
            await Task.WhenAll(commit, independent).WaitAsync(watchdog);
        }
    }

    [Fact]
    public void ExpirationBetweenFilesRestoresEarlierPublications()
    {
        using var temp = new TempDirectory();
        using var cancelled = new CancellationTokenSource();
        using var deadline = OperationDeadline.Start(null, cancelled.Token);
        var writer = new SafeFileWriter(new ResourceBudgetLedger(deadline));
        string first = temp.File("one.txt");
        string second = temp.File("two.txt");
        File.WriteAllText(first, "original");
        using var transaction = new AtomicOutputSetWriter(writer, temp.Path, "cancelled",
            new Callback(point =>
            {
                if (point.Kind == PublicationFaultKind.Publish && point.EntryIndex == 0) { cancelled.Cancel(); }
            }));
        transaction.Stage(first, true, path => File.WriteAllText(path, "replacement"));
        transaction.Stage(second, false, path => File.WriteAllText(path, "second"));
        Assert.ThrowsAny<OperationCanceledException>(() => transaction.Commit());
        Assert.Equal("original", File.ReadAllText(first));
        Assert.False(File.Exists(second));
    }

    [Fact]
    public void LockWaitUsesTheOriginalDeadlineWithoutPublishing()
    {
        using var temp = new TempDirectory();
        using PublicationDirectoryLease held = PublicationDirectoryLease.Acquire(temp.Path);
        using var deadline = OperationDeadline.Start(TimeSpan.FromSeconds(2));
        var writer = new SafeFileWriter(new ResourceBudgetLedger(deadline));
        string target = temp.File("late.txt");
        using var transaction = new AtomicOutputSetWriter(writer, temp.Path, "deadline");
        transaction.Stage(target, false, path => File.WriteAllText(path, "late"));
        CliException error = Assert.Throws<CliException>(() => transaction.Commit());
        Assert.Equal(ErrorCodes.OperationTimeout, error.Code);
        Assert.False(File.Exists(target));
    }

    [Fact]
    public void CancellationBeforeTheDurableCommitRecordRestoresPublishedData()
    {
        using var temp = new TempDirectory();
        using var cancelled = new CancellationTokenSource();
        using var deadline = OperationDeadline.Start(null, cancelled.Token);
        var budgets = new ResourceBudgetLedger(deadline);
        string target = temp.File("report.txt");
        File.WriteAllText(target, "original");
        using var transaction = new AtomicOutputSetWriter(new SafeFileWriter(budgets), temp.Path, "commit-deadline");
        transaction.Stage(target, true, path => File.WriteAllText(path, "replacement"));
        bool reachedCommitRecord = false;
        Assert.ThrowsAny<OperationCanceledException>(() => transaction.Commit(() =>
        {
            reachedCommitRecord = true;
            Assert.Equal("replacement", File.ReadAllText(target));
            cancelled.Cancel();
        }));
        Assert.True(reachedCommitRecord);
        Assert.False(budgets.HasCommittedOutputs);
        Assert.Equal("original", File.ReadAllText(target));
    }

    private sealed class Callback(Action<PublicationFaultPoint> action) : IPublicationFaultInjector
    {
        public void Hit(PublicationFaultPoint point) => action(point);
    }
}
