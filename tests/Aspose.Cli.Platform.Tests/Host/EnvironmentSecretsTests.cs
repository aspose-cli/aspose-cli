using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.IO;
using Xunit;

namespace Aspose.Cli.Host.Tests;

public sealed class EnvironmentSecretsTests
{
    [Fact]
    public void RepeatedConcurrentReads_ShareOneValueAndOneBudgetCharge()
    {
        using var deadline = OperationDeadline.Start(TimeSpan.FromSeconds(5));
        var budgets = new ResourceBudgetLedger(deadline);
        int reads = 0;
        var secrets = new EnvironmentSecrets(_ => { Interlocked.Increment(ref reads); return "value"; }, budgets);
        Parallel.For(0, 20, _ => Assert.Equal("value", secrets.Read("secret.name")));
        Assert.Equal(1, reads);
        Assert.Equal(budgets.Limit(ResourceBudgetKinds.SecretCharacters) - 5,
            budgets.Remaining(ResourceBudgetKinds.SecretCharacters));
    }

    [Fact]
    public void DifferentSecrets_ShareTheInvocationBudget()
    {
        using var deadline = OperationDeadline.Start(null);
        var budgets = new ResourceBudgetLedger(deadline,
            new Dictionary<string, long> { [ResourceBudgetKinds.SecretCharacters] = 5 });
        var secrets = new EnvironmentSecrets(_ => "abc", budgets);
        Assert.Equal("abc", secrets.Read("first"));
        CliException error = Assert.Throws<CliException>(() => secrets.Read("second"));
        Assert.Equal(ErrorCodes.InputBudgetExceeded, error.Code);
        Assert.Equal("abc", secrets.Read("first"));
        Assert.Equal(2, budgets.Remaining(ResourceBudgetKinds.SecretCharacters));
    }

    [Fact]
    public void CancellationAndMissingValues_KeepTheirMeaning()
    {
        using var cancellation = new CancellationTokenSource();
        using var deadline = OperationDeadline.Start(null, cancellation.Token);
        var budgets = new ResourceBudgetLedger(deadline);
        var secrets = new EnvironmentSecrets(name => name == "missing" ? null : "", budgets);
        Assert.Null(secrets.Read("missing"));
        Assert.Equal("", secrets.Read("empty"));
        cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => secrets.Read("missing"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad=name")]
    [InlineData("bad\0name")]
    public void InvalidNames_FailBeforeReadingTheSource(string name)
    {
        using var deadline = OperationDeadline.Start(null);
        var secrets = new EnvironmentSecrets(_ => throw new InvalidOperationException("Source must not run."),
            new ResourceBudgetLedger(deadline));
        CliException error = Assert.Throws<CliException>(() => secrets.Read(name));
        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
    }
}
