using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.IO.Input;

namespace Aspose.Cli.Sdk.IO.Output;

/// <summary>
/// The password a written copy of a document carries, decided once from the command line and
/// the primary input: <c>--encrypt</c> replaces the input's password; without it, a format that
/// can carry a password keeps the password of an encrypted primary input; every other output is
/// written without one, which the write pipeline discloses as <c>ENCRYPTION_REMOVED</c>.
/// A product's engine adapter carries the decision out: an engine that encrypts through save
/// options sets <see cref="Password"/>, and one that keeps encryption on the document keeps it
/// for <see cref="Keep"/> and removes it for <see cref="None"/>.
/// </summary>
public abstract record OutputProtection
{
    private OutputProtection()
    {
    }

    /// <summary>The output is written without a password.</summary>
    public static OutputProtection None { get; } = new Unprotected();

    /// <summary>Whether the output is encrypted, as the write pipeline and the result report it.</summary>
    public bool Encrypted => this is not Unprotected;

    /// <summary>
    /// The password the output is encrypted with and opens with: an engine that encrypts through
    /// save options sets it, and a read-back reopens the output with it; null for <see cref="None"/>.
    /// </summary>
    public abstract Secret? Password { get; }

    /// <summary>
    /// Decides the protection of an output written in <paramref name="written"/>, from the
    /// <c>--encrypt</c> password, how the primary input was protected when it was opened, and
    /// the password it was opened with. Only the primary input counts: a document an operation
    /// imported passes no password on, and a plaintext input opened with an unused password
    /// stays plaintext.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="encrypt"/> is given for a format that cannot carry a password, which the
    /// command line refuses before the product runs, naming the option the caller passed; or the
    /// output would keep the encryption of a primary input opened without a password, which a
    /// loader refuses, so the output would be reported encrypted without a password to write.
    /// </exception>
    public static OutputProtection For(FormatDescriptor written, Secret? encrypt, InputProtection primary, Secret? inputPassword)
    {
        ArgumentNullException.ThrowIfNull(written);
        ArgumentNullException.ThrowIfNull(primary);
        if (encrypt is not null)
        {
            return written.Protectable
                ? new Replace(encrypt)
                : throw new InvalidOperationException($"An output password reached the '{written.Id}' format, which cannot carry one.");
        }

        if (!written.Protectable || !primary.Encrypted)
        {
            return None;
        }

        return inputPassword is not null
            ? new Keep(inputPassword)
            : throw new InvalidOperationException($"An encrypted input reached the '{written.Id}' output without the password it opened with.");
    }

    /// <summary>The output is encrypted with a new password, the one <c>--encrypt</c> gave.</summary>
    public sealed record Replace(Secret NewPassword) : OutputProtection
    {
        /// <inheritdoc />
        public override Secret? Password => NewPassword;
    }

    /// <summary>
    /// The output keeps the encryption of the primary input, which opened with
    /// <paramref name="InputPassword"/>; an engine that keeps encryption on the document keeps it as it opened.
    /// </summary>
    public sealed record Keep(Secret InputPassword) : OutputProtection
    {
        /// <inheritdoc />
        public override Secret? Password => InputPassword;
    }

    private sealed record Unprotected : OutputProtection
    {
        public override Secret? Password => null;
    }
}
