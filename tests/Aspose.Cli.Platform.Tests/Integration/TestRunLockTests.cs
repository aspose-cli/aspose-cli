using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

public sealed class TestRunLockTests
{
    [Fact]
    public void ASecondRunWaitsUntilTheFirstReleasesTheLock()
    {
        string name = $"{nameof(TestRunLockTests)}-{Guid.NewGuid():N}";
        using (TestRunLock? first = TestRunLock.TryAcquire(name, TimeSpan.Zero))
        {
            Assert.NotNull(first);
            Assert.False(TryAcquireAndRelease(name));
        }
        Assert.True(TryAcquireAndRelease(name));
    }

    [Fact]
    public void ThisRunHoldsTheLockSoTheProcessesItStartsDoNotWaitForIt()
    {
        // The test assembly fixture or scripts/test.ps1 holds the lock under the name both use.
        Assert.Null(TestRunLock.TryAcquire(TestRunLock.Name, TimeSpan.Zero));
        Assert.False(string.IsNullOrEmpty(Environment.GetEnvironmentVariable(TestRunLock.HeldVariable)));
        Assert.Null(TestRunLock.AcquireUnlessHeld());
    }

    [Fact]
    public void ALockThatCannotBeCreatedFailsTheCaller()
    {
        Assert.ThrowsAny<IOException>(() => TestRunLock.TryAcquire($@"NoSuchNamespace\{Guid.NewGuid():N}", TimeSpan.Zero));
    }

    private static bool TryAcquireAndRelease(string name)
    {
        using TestRunLock? second = TestRunLock.TryAcquire(name, TimeSpan.Zero);
        return second is not null;
    }
}
