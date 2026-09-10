using System.CommandLine;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Host.Tests;

public sealed class ProductInputAdmissionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Admission_ChecksAbsoluteAndRelativeInputsBeforeRuntime(bool absolute)
    {
        using var directory = new TempDirectory();
        string input = directory.File("large.bin");
        File.WriteAllBytes(input, new byte[128]);
        var root = new RootCommand();
        var inspect = new Command("inspect");
        inspect.Arguments.Add(new Argument<string>("file"));
        root.Subcommands.Add(inspect);
        var globals = new GlobalOptions(licensingApplicable: false);
        globals.AddTo(root);
        var parse = root.Parse(["--workdir", directory.Path, "inspect", absolute ? input : "large.bin"]);
        Assert.Empty(parse.Errors);
        using OperationDeadline deadline = OperationDeadline.Start(null);
        var budgets = new ResourceBudgetLedger(deadline, new Dictionary<string, long> { [ResourceBudgetKinds.InputBytes] = 64 });

        CliException error = Assert.Throws<CliException>(() =>
            ProductInputAdmission.Admit(parse, globals.Resolve(parse), budgets));

        Assert.Equal(ErrorCodes.FileTooLarge, error.Code);
    }

    [Theory]
    [InlineData("--target", false)]
    [InlineData("--target", true)]
    [InlineData("--out", false)]
    [InlineData("--out", true)]
    public void Admission_UsesTheParsedOutputOptionIdentity(string name, bool equals)
    {
        using var directory = new TempDirectory();
        string output = directory.File("large.bin");
        File.WriteAllBytes(output, new byte[128]);
        var root = new RootCommand();
        var command = new Command("convert");
        command.Options.Add(new Option<string>("--out", "--target"));
        root.Subcommands.Add(command);
        var globals = new GlobalOptions(licensingApplicable: false);
        globals.AddTo(root);
        string[] option = equals ? [name + "=" + output] : [name, output];
        var parse = root.Parse(["convert", .. option]);
        Assert.Empty(parse.Errors);
        using OperationDeadline deadline = OperationDeadline.Start(null);
        var budgets = new ResourceBudgetLedger(deadline, new Dictionary<string, long> { [ResourceBudgetKinds.InputBytes] = 64 });

        ProductInputAdmission.Admit(parse, globals.Resolve(parse), budgets);
    }
}
