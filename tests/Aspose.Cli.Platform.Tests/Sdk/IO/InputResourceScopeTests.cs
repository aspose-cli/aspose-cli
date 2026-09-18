using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

public sealed class InputResourceScopeTests
{
    [Fact]
    public void DeferredConsumersShareTheInputBudgetAndAllStreamsAreReleased()
    {
        using var temp = new TempDirectory();
        File.WriteAllBytes(temp.File("first.bin"), [1, 2, 3, 4, 5]);
        File.WriteAllBytes(temp.File("second.bin"), [6, 7, 8, 9, 10]);
        using var deadline = OperationDeadline.Start(null);
        var budgets = new ResourceBudgetLedger(deadline, new Dictionary<string, long> { [ResourceBudgetKinds.InputBytes] = 9 });
        var scope = budgets.Inputs.CreateScope();
        Stream first = scope.OpenFile(temp.File("first.bin"));
        Stream second = scope.OpenFile(temp.File("second.bin"));
        Assert.Equal(5, first.Read(new byte[5]));
        CliException failure = Assert.Throws<CliException>(() => second.Read(new byte[5]));
        Assert.Equal(ErrorCodes.InputBudgetExceeded, failure.Code);
        Assert.True(failure.IsInvocationFailure);
        Assert.Same(failure, Assert.Throws<CliException>(() => scope.ThrowIfFailed()));
        Assert.Throws<CliException>(() => new SafeFileWriter(budgets).Write(temp.File("output.bin"), false,
            path => File.WriteAllBytes(path, [1])));
        Assert.False(File.Exists(temp.File("output.bin")));
        scope.Dispose();
        Assert.Throws<ObjectDisposedException>(() => first.ReadByte());
        Assert.Throws<ObjectDisposedException>(() => second.ReadByte());
        scope.Dispose();
        Assert.Throws<ObjectDisposedException>(() => scope.OpenFile(temp.File("first.bin")));
    }
}
