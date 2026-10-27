using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.IO.Output;
using Xunit;

namespace Aspose.Cli.Sdk.Tests;

/// <summary>
/// <see cref="OutputProtection.For"/> over every combination of <c>--encrypt</c>, whether the
/// primary input was encrypted, whether it was opened with a password, and whether the written
/// format can carry one: <c>--encrypt</c> replaces, a format that can carry a password keeps an
/// encrypted input's, and every other output is written without one.
/// </summary>
public sealed class OutputProtectionTests
{
    private static readonly Secret Encrypt = new("new-secret");
    private static readonly Secret InputPassword = new("input-secret");
    private static readonly FormatDescriptor Protectable =
        FormatDescriptor.Declare("locked", FormatUse.Convert, true, ".locked") with { Protectable = true };
    private static readonly FormatDescriptor Open = FormatDescriptor.Declare("open", FormatUse.Convert, true, ".open");

    public enum Expected
    {
        Replace,
        Keep,
        None,
        Refused,
    }

    [Theory]
    // --encrypt replaces whatever the input carried, where the format can carry a password.
    [InlineData(true, true, true, true, Expected.Replace)]
    [InlineData(true, true, false, true, Expected.Replace)]
    [InlineData(true, false, true, true, Expected.Replace)]
    [InlineData(true, false, false, true, Expected.Replace)]
    // --encrypt for a format that cannot carry a password never reaches a product.
    [InlineData(true, true, true, false, Expected.Refused)]
    [InlineData(true, true, false, false, Expected.Refused)]
    [InlineData(true, false, true, false, Expected.Refused)]
    [InlineData(true, false, false, false, Expected.Refused)]
    // Without --encrypt, a format that can carry a password keeps an encrypted input's; a loader
    // opens no encrypted input without one, so an output never reports a password it lacks.
    [InlineData(false, true, true, true, Expected.Keep)]
    [InlineData(false, true, false, true, Expected.Refused)]
    // A plaintext input stays plaintext, even when it was opened with an unused password.
    [InlineData(false, false, true, true, Expected.None)]
    [InlineData(false, false, false, true, Expected.None)]
    // A format that cannot carry a password is written without one.
    [InlineData(false, true, true, false, Expected.None)]
    [InlineData(false, true, false, false, Expected.None)]
    [InlineData(false, false, true, false, Expected.None)]
    [InlineData(false, false, false, false, Expected.None)]
    public void For_DecidesTheOutputPassword(bool encrypt, bool inputEncrypted, bool inputPassword, bool protectable, Expected expected)
    {
        Secret? given = inputPassword ? InputPassword : null;
        OutputProtection Decide() => OutputProtection.For(
            protectable ? Protectable : Open, encrypt ? Encrypt : null, new InputProtection(inputEncrypted), given);

        if (expected == Expected.Refused)
        {
            Assert.Throws<InvalidOperationException>(Decide);
            return;
        }

        OutputProtection protection = Decide();
        switch (expected)
        {
            case Expected.Replace:
                Assert.Same(Encrypt, Assert.IsType<OutputProtection.Replace>(protection).NewPassword);
                Assert.True(protection.Encrypted);
                Assert.Same(Encrypt, protection.Password);
                break;
            case Expected.Keep:
                Assert.Same(given, Assert.IsType<OutputProtection.Keep>(protection).InputPassword);
                Assert.True(protection.Encrypted);
                Assert.Same(given, protection.Password);
                break;
            default:
                Assert.Same(OutputProtection.None, protection);
                Assert.False(protection.Encrypted);
                Assert.Null(protection.Password);
                break;
        }
    }

    /// <summary>
    /// An encrypted input that opens without a password, such as a PDF with only an owner
    /// password, has no password to open that a copy could keep, whatever password it was
    /// opened with.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void For_KeepsNoPasswordOfAnInputThatOpensWithoutOne(bool inputPassword)
    {
        OutputProtection protection = OutputProtection.For(
            Protectable, null, new InputProtection(Encrypted: true, OpenPassword: false), inputPassword ? InputPassword : null);

        Assert.Same(OutputProtection.None, protection);
    }

    [Fact]
    public void Printing_ShowsNoPassword()
    {
        OutputProtection replaced = OutputProtection.For(Protectable, Encrypt, new InputProtection(false), null);
        OutputProtection kept = OutputProtection.For(Protectable, null, new InputProtection(true), InputPassword);

        Assert.DoesNotContain(Encrypt.Reveal(), replaced.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(InputPassword.Reveal(), kept.ToString(), StringComparison.Ordinal);
    }
}
