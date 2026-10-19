using System.Text.Json.Nodes;

namespace Aspose.Cli.Sdk.IO;

/// <summary>
/// How an opened input document is protected, as the write pipeline learns it: whether every
/// copy a command publishes without encryption removes a protection.
/// </summary>
/// <param name="Encrypted">Whether the document is encrypted, including a PDF with only an owner password.</param>
/// <param name="OpenPassword">Whether an encrypted document needs a password to open, not only to change it.</param>
/// <param name="Restrictions">
/// For an encrypted document, the fields of the operation that encrypts
/// (<c>[EncryptsOutput]</c>) that restrict a copy as the document is
/// restricted, such as the permissions it grants readers without the owner password.
/// </param>
public sealed record InputProtection(bool Encrypted, bool OpenPassword = true, JsonObject? Restrictions = null);

/// <summary>
/// An input document a product loader opened, which tells the write pipeline how it is
/// protected (<see cref="InputProtection"/>).
/// </summary>
public interface IOpenedInput
{
    /// <summary>How the document is protected, as it was opened; a later change does not alter it.</summary>
    InputProtection Protection { get; }
}
