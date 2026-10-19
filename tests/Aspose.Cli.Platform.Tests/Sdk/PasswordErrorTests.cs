using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Input;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.Errors;

/// <summary>
/// A password error names where the password comes from (<see cref="PasswordSource"/>): the
/// command's option for the input, an operation's field for a file the operation reads, or what
/// to do instead for a file the command opens without a password.
/// </summary>
public sealed class PasswordErrorTests
{
    [Fact]
    public void AnInputsPasswordError_NamesTheCommandOption()
    {
        Assert.Contains("--password-env", CliErrors.PasswordRequired("in.pdf").Hint, StringComparison.Ordinal);
        Assert.Contains("--password-env", CliErrors.PasswordInvalid("in.pdf").Hint, StringComparison.Ordinal);
    }

    /// <summary>A file another option supplies the password of, such as a signing certificate, names that option.</summary>
    [Fact]
    public void APasswordErrorOfAnotherFile_NamesItsOwnOption()
    {
        CliException invalid = CliErrors.PasswordInvalid("cert.pfx", passwordEnvironmentOption: "--certificate-password-env");
        CliException required = CliErrors.PasswordRequired("cert.pfx", passwordEnvironmentOption: "--certificate-password-env");

        Assert.Equal((ErrorCodes.PasswordInvalid, ErrorCodes.PasswordRequired), (invalid.Code, required.Code));
        Assert.All([invalid, required], error =>
        {
            Assert.Equal("cert.pfx", error.Details!["path"]!.GetValue<string>());
            Assert.Contains("retry with --certificate-password-env <NAME>.", error.Hint, StringComparison.Ordinal);
            Assert.DoesNotContain(" --password-env", error.Hint, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// A command option supplies the password: <c>--password-env</c> by default, or another
    /// option such as <c>--certificate-password-env</c>.
    /// </summary>
    [Theory]
    [InlineData(null, "--password-env")]
    [InlineData("--certificate-password-env", "--certificate-password-env")]
    public void APasswordSourceOption_NamesTheOption(string? option, string named)
    {
        PasswordSource without = option is null ? PasswordSource.Option(null) : PasswordSource.Option(null, option);
        PasswordSource with = option is null ? PasswordSource.Option(new Secret("wrong")) : PasswordSource.Option(new Secret("wrong"), option);

        AssertRefused(without, with, $"retry with {named} <NAME>.", "\"");
        Assert.Equal(PasswordSource.Option(null).Refused("in.pdf").Hint, default(PasswordSource).Refused("in.pdf").Hint);
    }

    /// <summary>An operation field supplies the password of a file the operation reads.</summary>
    [Fact]
    public void APasswordSourceOperationField_NamesTheField()
    {
        AssertRefused(
            PasswordSource.OperationField(null, "sourcePasswordEnv"),
            PasswordSource.OperationField(new Secret("wrong"), "sourcePasswordEnv"),
            "name that variable in the operation's \"sourcePasswordEnv\" field.",
            "--password-env");
    }

    /// <summary>A file the command opens without a password, such as a template, is refused with what to do instead.</summary>
    [Fact]
    public void APasswordSourceNone_GivesWhatToDoInstead()
    {
        CliException error = PasswordSource.None("Use an unencrypted copy.").Refused("template.pptx");

        Assert.Equal(ErrorCodes.PasswordRequired, error.Code);
        Assert.Equal("template.pptx", error.Details!["path"]!.GetValue<string>());
        Assert.Equal("Use an unencrypted copy.", error.Hint);
    }

    private static void AssertRefused(PasswordSource without, PasswordSource with, string hint, string absent)
    {
        CliException required = without.Refused("source.xlsx");
        CliException invalid = with.Refused("source.xlsx");

        Assert.Equal((ErrorCodes.PasswordRequired, ErrorCodes.PasswordInvalid), (required.Code, invalid.Code));
        Assert.All([required, invalid], error =>
        {
            Assert.Equal("source.xlsx", error.Details!["path"]!.GetValue<string>());
            Assert.EndsWith(hint, error.Hint, StringComparison.Ordinal);
            Assert.DoesNotContain(absent, error.Hint, StringComparison.Ordinal);
        });
    }
}
