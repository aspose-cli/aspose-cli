using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Input;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.Errors;

/// <summary>
/// A password error (<see cref="PasswordSource"/>) is <c>PASSWORD_REQUIRED</c> without a password
/// and <c>PASSWORD_INVALID</c> with one, and names the file.
/// </summary>
public sealed class PasswordErrorTests
{
    [Fact]
    public void APasswordSource_RefusesWithTheCodeOfItsPassword()
    {
        CliException required = PasswordSource.Option(null).Refused("source.xlsx");
        CliException invalid = PasswordSource.Option(new Secret("wrong")).Refused("source.xlsx");

        Assert.Equal((ErrorCodes.PasswordRequired, ErrorCodes.PasswordInvalid), (required.Code, invalid.Code));
        Assert.All([required, invalid], static error => Assert.Equal("source.xlsx", error.Details!["path"]!.GetValue<string>()));
        Assert.Equal(ErrorCodes.PasswordRequired, default(PasswordSource).Refused("source.xlsx").Code);
    }
}
