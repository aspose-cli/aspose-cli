using Aspose.Cli.Sdk.Errors;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.Errors;

/// <summary>
/// A password error names where the password comes from: the command's option for the input,
/// an operation's field for a file the operation reads.
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

    [Fact]
    public void ForOperationSource_RestatesTheErrorForTheOperationField()
    {
        CliException required = CliErrors.ForOperationSource(CliErrors.PasswordRequired("source.xlsx"), "passwordEnv");
        CliException invalid = CliErrors.ForOperationSource(CliErrors.PasswordInvalid("source.xlsx"), "passwordEnv");

        Assert.Equal((ErrorCodes.PasswordRequired, ErrorCodes.PasswordInvalid), (required.Code, invalid.Code));
        Assert.All([required, invalid], error =>
        {
            Assert.Equal("source.xlsx", error.Details!["path"]!.GetValue<string>());
            Assert.Contains("operation's \"passwordEnv\" field", error.Hint, StringComparison.Ordinal);
            Assert.DoesNotContain("--password-env", error.Hint, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void ForOperationSource_RefusesAnotherError()
    {
        Assert.False(CliErrors.IsPasswordError(CliErrors.FileNotFound("source.xlsx")));
        Assert.Throws<ArgumentException>(() => CliErrors.ForOperationSource(CliErrors.FileNotFound("source.xlsx"), "passwordEnv"));
    }
}
