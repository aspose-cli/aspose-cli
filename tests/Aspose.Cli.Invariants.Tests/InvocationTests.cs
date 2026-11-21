using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>
/// The smallest valid invocation follows the command tree: an option it implies is found by what
/// the tree declares, so a renamed option is still filled, and a rule that names an option by
/// verb fails when no command has that option any more.
/// </summary>
public sealed class InvocationTests
{
    [Fact]
    public void ImpliedRules_MatchTheCatalog()
    {
        string[] problems = [.. Invocation.ImpliedRulesMatchTheCatalog(CliCatalog.Current.Commands)];
        Assert.True(problems.Length == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void ARenamedOperationsOption_IsStillFilled()
    {
        CliCommand edit = Command("cells edit", Option("--operations", inputKind: "json-source"));

        Invocation invocation = Invocation.For(edit);

        Assert.Equal(["cells", "edit", "input.xlsx", "--operations", "ops.json"], invocation.Args);
        Assert.Equal("cells.ops", invocation.Files["ops.json"].Fixture);
    }

    [Fact]
    public void ARenamedCertificatePassword_IsStillFilled()
    {
        CliCommand sign = Command(
            "pdf sign",
            Option("--key", required: true, inputKind: "file"),
            Option("--key-password-env", valueSource: "environment-variable-name"));

        Invocation invocation = Invocation.For(sign, omit: "--key");

        Assert.Equal(["pdf", "sign", "input.pdf", "--key-password-env", Invocation.CertificateVariable], invocation.Args);
    }

    [Fact]
    public void AVerbRuleWhoseOptionWasRenamed_Fails()
    {
        CliCommand[] commands =
        [
            Command("cells convert", Option("--format", allowedValues: ["xlsx"])),
            Command("cells create", Option("--from-html", inputKind: "file")) with { Arguments = [new CliArgument(null, 1, true)] },
        ];

        string[] problems = [.. Invocation.ImpliedRulesMatchTheCatalog(commands)];

        Assert.Contains("no convert command has --to, which the invocation rules imply", problems);
        Assert.Contains("cells create creates its argument from file options none of the invocation rules names", problems);
    }

    private static CliCommand Command(string words, params CliOption[] options) =>
        new(words, words.Split(' ')[0], options, [new CliArgument("file", 1, true)], []);

    private static CliOption Option(
        string name, bool required = false, string? inputKind = null, string valueSource = "command-line", string[]? allowedValues = null) =>
        new(name, "string", required, valueSource != "command-line", valueSource, allowedValues ?? [], false, MaximumArity: 1, Aliases: [], InputKind: inputKind);
}
