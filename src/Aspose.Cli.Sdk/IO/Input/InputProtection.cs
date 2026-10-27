namespace Aspose.Cli.Sdk.IO.Input;

/// <summary>
/// How an opened input document is protected, as the write pipeline learns it: whether every
/// copy a command publishes without encryption removes a protection.
/// </summary>
/// <param name="Encrypted">Whether the document is encrypted, including a PDF with only an owner password.</param>
/// <param name="OpenPassword">Whether an encrypted document needs a password to open, not only to change it.</param>
public sealed record InputProtection(bool Encrypted, bool OpenPassword = true);

/// <summary>
/// An input document a product loader opened, which tells the write pipeline how it is
/// protected (<see cref="InputProtection"/>).
/// </summary>
public interface IOpenedInput
{
    /// <summary>How the document is protected, as it was opened; a later change does not alter it.</summary>
    InputProtection Protection { get; }
}
