using System.Runtime.InteropServices;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.IO;

public sealed class VerifiedResourceReadTests
{
    [Theory]
    [InlineData("../root-other/image.bin")]
    [InlineData("%2e%2e/root-other/image.bin")]
    [InlineData("image.bin:secret")]
    [InlineData("image.bin%3Asecret")]
    [InlineData("image.bin.")]
    [InlineData("image.bin%20")]
    [InlineData("CON")]
    [InlineData(@"\\?\C:\Windows\win.ini")]
    [InlineData(@"\\.\C:\Windows\win.ini")]
    [InlineData("file://localhost/C$/image.bin")]
    [InlineData("data:image/png;base64,AA==")]
    public void UnsafeReferences_LeaveNoPartialResult(string reference)
    {
        using var temp = new TempDirectory();
        string root = temp.File("root");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(temp.File("root-other"));
        File.WriteAllBytes(Path.Combine(root, "image.bin"), [1, 2]);
        File.WriteAllBytes(temp.File("root-other/image.bin"), [3, 4]);
        using var deadline = OperationDeadline.Start(null);
        using var resources = new LocalDocumentResourceLoader(
            Path.Combine(root, "input.html"), new ResourceBudgetLedger(deadline));
        Assert.False(resources.TryRead(reference, out byte[] bytes));
        Assert.Empty(bytes);
        Assert.Equal(1, resources.OmittedCount);
    }

    [Fact]
    public void EncodedOrdinaryName_ReadsTheVerifiedFile()
    {
        using var temp = new TempDirectory();
        File.WriteAllBytes(temp.File("local image.bin"), [1, 2]);
        using var deadline = OperationDeadline.Start(null);
        using var resources = new LocalDocumentResourceLoader(
            temp.File("input.html"), new ResourceBudgetLedger(deadline));
        bool accepted = resources.TryRead("local%20image.bin", out byte[] bytes);
        Assert.Equal(OperatingSystem.IsWindows(), accepted);
        Assert.Equal(accepted ? [1, 2] : Array.Empty<byte>(), bytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReplacedRootOrAncestor_CannotReuseAuthorization(bool ancestor)
    {
        Requires.Windows();
        using var temp = new TempDirectory();
        string parent = temp.File("parent");
        string root = Path.Combine(parent, "root");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "data"), "old");
        var boundary = new VerifiedFileBoundary(root);
        string replaced = ancestor ? parent : root;
        Directory.Move(replaced, replaced + "-old");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "data"), "new");
        Assert.Null(boundary.TryOpenRead(Path.Combine(root, "data")));
    }

    [Fact]
    public void Lease_PinsDirectoriesAndFileUntilDisposed()
    {
        Requires.Windows();
        using var temp = new TempDirectory();
        string root = temp.File("root");
        Directory.CreateDirectory(root);
        string file = Path.Combine(root, "data");
        File.WriteAllBytes(file, [1, 2]);
        var boundary = new VerifiedFileBoundary(root);
        using (VerifiedReadLease lease = Assert.IsType<VerifiedReadLease>(boundary.TryOpenRead(file)))
        {
            Assert.Throws<IOException>(() => Directory.Move(root, root + "-moved"));
            Assert.Throws<IOException>(() => File.WriteAllBytes(file, new byte[1024]));
            Assert.Throws<IOException>(() => File.WriteAllBytes(file, []));
            Assert.Equal(1, lease.ReadByte());
            Assert.Equal(2, lease.ReadByte());
            Assert.Equal(-1, lease.ReadByte());
        }
        File.WriteAllText(file, "released");
        Directory.Move(root, root + "-moved");
    }

    [Fact]
    public void ExistingWriter_DeniesReadInsteadOfObservingUnstableBytes()
    {
        Requires.Windows();
        using var temp = new TempDirectory();
        string file = temp.File("data");
        File.WriteAllBytes(file, [1, 2]);
        var boundary = new VerifiedFileBoundary(Path.GetDirectoryName(file)!);
        using var writer = new FileStream(file, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
        Assert.Null(boundary.TryOpenRead(file));
    }

    [Fact]
    public void HardLink_IsRejectedEvenWhenBothNamesAreInsideRoot()
    {
        Requires.Windows();
        using var temp = new TempDirectory();
        string file = temp.File("data");
        File.WriteAllText(file, "content");
        Assert.True(CreateHardLink(temp.File("alias"), file, IntPtr.Zero),
            $"CreateHardLink failed with {Marshal.GetLastWin32Error()}.");
        var boundary = new VerifiedFileBoundary(Path.GetDirectoryName(file)!);
        Assert.Null(boundary.TryOpenRead(file));
        Assert.Null(boundary.TryOpenRead(temp.File("alias")));
    }

    [Fact]
    public void JunctionAtRootOrBelow_IsRejected()
    {
        Requires.Windows();
        using var temp = new TempDirectory();
        string root = temp.File("root");
        string outside = temp.File("outside");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "data"), "outside");
        string junction = Path.Combine(root, "linked");
        FileSystemLinks.CreateDirectoryLink(junction, outside);
        try
        {
            Assert.Null(new VerifiedFileBoundary(root).TryOpenRead(Path.Combine(junction, "data")));
            Assert.Null(new VerifiedFileBoundary(junction).TryOpenRead(Path.Combine(junction, "data")));
        }
        finally
        {
            Directory.Delete(junction);
        }
        Assert.Equal("outside", File.ReadAllText(Path.Combine(outside, "data")));
    }

    [Theory]
    [InlineData(ResourceBudgetKinds.InputBytes)]
    [InlineData(ResourceBudgetKinds.MemoryBufferBytes)]
    public void SharedBudgetFailure_PropagatesAndRemainsFatal(string kind)
    {
        Requires.Windows();
        using var temp = new TempDirectory();
        File.WriteAllBytes(temp.File("data"), [1, 2, 3]);
        using var deadline = OperationDeadline.Start(null);
        var budgets = new ResourceBudgetLedger(deadline, new Dictionary<string, long> { [kind] = 5 });
        using var first = new LocalDocumentResourceLoader(temp.File("one.html"), budgets);
        using var second = new LocalDocumentResourceLoader(temp.File("two.html"), budgets);
        Assert.True(first.TryRead("data", out _));
        byte[] bytes = [99];
        CliException error = Assert.Throws<CliException>(() => second.TryRead("data", out bytes));
        Assert.Equal(ErrorCodes.InputBudgetExceeded, error.Code);
        Assert.Empty(bytes);
        Assert.Same(error, Assert.Throws<CliException>(second.ThrowIfFailed));
        Assert.Equal(0, second.OmittedCount);
    }

    [Fact]
    public async Task ConcurrentRequests_RespectAggregateLimit()
    {
        Requires.Windows();
        using var temp = new TempDirectory();
        File.WriteAllBytes(temp.File("data"), [1, 2]);
        using var deadline = OperationDeadline.Start(null);
        using var resources = new LocalDocumentResourceLoader(temp.File("input.html"),
            new ResourceBudgetLedger(deadline), maximumTotalBytes: 6);
        bool[] results = await Task.WhenAll(Enumerable.Range(0, 12).Select(index =>
            Task.Run(() => resources.TryRead("data", out _))));
        Assert.Equal(3, results.Count(static accepted => accepted));
        Assert.Equal(9, resources.OmittedCount);
    }

    [Fact]
    public void CancellationAndExpiredDeadline_AreFailuresIncludingOnRejectedReferences()
    {
        using var temp = new TempDirectory();
        using var cancellation = new CancellationTokenSource();
        using var deadline = OperationDeadline.Start(null, cancellation.Token);
        using var resources = new LocalDocumentResourceLoader(
            temp.File("input.html"), new ResourceBudgetLedger(deadline));
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => resources.TryRead("https://example.invalid", out _));
        using var expired = OperationDeadline.FromAbsoluteTick(TimeSpan.FromSeconds(1), Environment.TickCount64 - 1);
        using var timed = new LocalDocumentResourceLoader(temp.File("input.html"), new ResourceBudgetLedger(expired));
        Assert.Equal(ErrorCodes.OperationTimeout,
            Assert.Throws<CliException>(() => timed.TryRead("missing", out _)).Code);
    }

    [Fact]
    public void Disposal_ClosesCallbackLifetime()
    {
        using var temp = new TempDirectory();
        using var deadline = OperationDeadline.Start(null);
        var resources = new LocalDocumentResourceLoader(temp.File("input.html"), new ResourceBudgetLedger(deadline));
        resources.Dispose();
        Assert.Throws<ObjectDisposedException>(() => resources.TryRead("missing", out _));
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string name, string existing, IntPtr security);
}
