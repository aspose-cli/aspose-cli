using Aspose.Cli.Sdk;

namespace Aspose.Cli.TestKit;

/// <summary>
/// The per-user lock that runs this account's test runs, from any checkout and any runner, one
/// at a time, because App tests share the per-user App endpoint. scripts/test.ps1 holds it for
/// its whole run and sets <see cref="HeldVariable"/> for the test processes it starts; a test
/// process started any other way, such as by <c>dotnet test</c>, takes it in its assembly
/// fixture and sets the variable for the processes it starts, so none of them waits for its own run.
/// </summary>
/// <remarks>
/// A mutex belongs to the thread that acquired it, and the fixture is created and disposed on
/// different threads, so a dedicated thread holds it. The operating system releases it when the
/// holding process dies.
/// </remarks>
internal sealed class TestRunLock : IDisposable
{
    public const string HeldVariable = DistributionInfo.EnvironmentVariablePrefix + "TEST_RUN_LOCK_HELD";

    /// <summary>The name scripts/test.ps1 uses too.</summary>
    public static string Name => $@"Global\{DistributionInfo.Id}-test-run-{Environment.UserName}";

    private readonly ManualResetEventSlim _release = new();
    private readonly Thread _holder;

    private TestRunLock(Thread holder) => _holder = holder;

    /// <summary>Waits for the lock unless a process this one descends from holds it.</summary>
    public static TestRunLock? AcquireUnlessHeld()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(HeldVariable)))
        {
            return null;
        }
        TestRunLock? held = TryAcquire(Name, TimeSpan.Zero);
        if (held is null)
        {
            Console.Error.WriteLine("Waiting for another test run of this account to finish...");
            held = TryAcquire(Name, Timeout.InfiniteTimeSpan)!;
        }
        Environment.SetEnvironmentVariable(HeldVariable, "1");
        return held;
    }

    /// <summary>
    /// The lock <paramref name="name"/>, or null when another holder keeps it past
    /// <paramref name="timeout"/>. Throws what creating or waiting on the mutex throws.
    /// </summary>
    internal static TestRunLock? TryAcquire(string name, TimeSpan timeout)
    {
        var acquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        TestRunLock? held = null;
        var holder = new Thread(() =>
        {
            try
            {
                Hold(name, timeout, acquired, held!._release);
            }
            catch (Exception exception) when (acquired.TrySetException(exception))
            {
                // The caller rethrows it; the lock was never held.
            }
        })
        { IsBackground = true, Name = "Test run lock" };
        held = new TestRunLock(holder);
        holder.Start();
        if (acquired.Task.GetAwaiter().GetResult())
        {
            return held;
        }
        holder.Join();
        held._release.Dispose();
        return null;
    }

    private static void Hold(string name, TimeSpan timeout, TaskCompletionSource<bool> acquired, ManualResetEventSlim release)
    {
        using var mutex = new Mutex(false, name);
        bool owned;
        try
        {
            owned = mutex.WaitOne(timeout);
        }
        catch (AbandonedMutexException)
        {
            // A run that ended without releasing it; this thread now holds the mutex.
            owned = true;
        }
        acquired.SetResult(owned);
        if (owned)
        {
            release.Wait();
            mutex.ReleaseMutex();
        }
    }

    public void Dispose()
    {
        _release.Set();
        _holder.Join();
        _release.Dispose();
    }
}
