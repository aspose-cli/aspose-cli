using System.CommandLine;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Architecture;

public sealed class BoundedEditOptionsTests
{
    [Fact]
    public void Read_CombinesExecutionFlagsAndRejectsAConflictingFingerprint()
    {
        var options = new BoundedEditOptions();
        var command = new Command("edit");
        options.AddTo(command);

        EditCommandOptions values = options.Read(
            command.Parse(["--if-match", "ABCDEF", "--dry-run", "--best-effort"]),
            "abcdef");

        Assert.Equal("ABCDEF", values.IfMatch);
        Assert.True(values.DryRun);
        Assert.True(values.BestEffort);

        CliException conflict = Assert.Throws<CliException>(() => options.Read(
            command.Parse(["--if-match", "current"]),
            "stale"));
        Assert.Equal(ErrorCodes.OptionInvalid, conflict.Code);
    }
}
