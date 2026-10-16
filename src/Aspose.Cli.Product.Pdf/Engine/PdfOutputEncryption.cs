using System.Security.Cryptography;
using Aspose.Pdf;
using Aspose.Pdf.Facades;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>
/// How a PDF command writes the encryption of its output, and the one judgment of whether the
/// written file is encrypted, which its <c>output.encrypted</c> and the write pipeline's
/// <c>ENCRYPTION_REMOVED</c> comparison both state.
/// </summary>
internal static class PdfOutputEncryption
{
    /// <summary>
    /// Whether the file saved from <paramref name="document"/> with <see cref="Save"/> is
    /// encrypted: it is when it is saved with passwords, or when the document carries
    /// encryption, which a file opened with only an owner password keeps as well. Aspose.PDF
    /// reports <see cref="Document.IsEncrypted"/> for the encryption a save writes, after
    /// <c>Encrypt</c> and <c>Decrypt</c> alike.
    /// </summary>
    internal static bool Encrypted(Document document, PdfOutputPasswords? passwords) =>
        passwords is not null || document.IsEncrypted;

    /// <summary>The passwords <c>--encrypt</c> puts on an output: its password opens it and grants every permission.</summary>
    internal static PdfOutputPasswords? Requested(Secret? encrypt) =>
        encrypt is null ? null : new PdfOutputPasswords(encrypt, encrypt, Permissions: null);

    /// <summary>
    /// The passwords of a new document assembled from inputs, such as a merge or the parts of a
    /// split, and what they keep of the inputs' encryption. <c>--encrypt</c> sets them as asked.
    /// Otherwise the output keeps the open password the inputs were opened with, as other
    /// products keep a source's password; an input with only an owner password has none to keep,
    /// so the output is not encrypted. The owner password and the permissions cannot both be
    /// kept, since only one of an input's passwords is known, so the output discloses what
    /// becomes of them with <c>PROTECTION_NOT_ENFORCED</c>, unless the open password of every
    /// such input is also its owner password, which the output keeps as both and so loses nothing:
    /// <list type="bullet">
    /// <item>opened with the open (user) password, the output opens with it and keeps the
    /// permissions all those inputs grant, under a new owner password that no one knows;</item>
    /// <item>opened with the owner password, which is all the run knows, the output opens with
    /// it, it is also the owner password, and so it grants every permission.</item>
    /// </list>
    /// </summary>
    /// <param name="encrypt">The <c>--encrypt</c> password, or null.</param>
    /// <param name="password">The password the inputs were opened with, or null.</param>
    /// <param name="inputs">The security of every input as it was opened.</param>
    internal static (PdfOutputPasswords? Passwords, Warning? Disclosure) Kept(
        Secret? encrypt, Secret? password, IReadOnlyCollection<PdfInputSecurity> inputs)
    {
        if (Requested(encrypt) is { } requested)
        {
            return (requested, null);
        }

        PdfInputSecurity[] protectedInputs = [.. inputs.Where(static input => input.OpenPassword)];
        if (password is null || protectedInputs.Length == 0)
        {
            return (null, null);
        }

        if (protectedInputs.All(static input => input.OpenPasswordIsOwner))
        {
            return (new PdfOutputPasswords(password, password, Permissions: null), null);
        }

        if (protectedInputs.Any(static input => input.OpenedAsOwner && !input.OpenPasswordIsOwner))
        {
            return (new PdfOutputPasswords(password, password, Permissions: null), new Warning(WarningCodes.ProtectionNotEnforced,
                "The input was opened with its owner password, so the output opens with that password, which is also its owner password: "
                + "the output grants every permission to whoever opens it, and the input's open password does not open it.")
            {
                Hint = "Protect the output with the intended open password through --encrypt-env, or run the command with the input's open password to keep its permissions.",
            });
        }

        Permissions granted = protectedInputs.Where(static input => !input.OpenPasswordIsOwner)
            .Aggregate((Permissions)~0, static (all, input) => all & input.Permissions);
        var owner = new Secret(Convert.ToHexString(RandomNumberGenerator.GetBytes(24)));
        return (new PdfOutputPasswords(password, owner, granted), new Warning(WarningCodes.ProtectionNotEnforced,
            "The input was opened with its user password, so the output opens with that password and keeps the permissions the input grants, "
            + "under a new owner password that no one knows: the input's owner password does not lift them on the output.")
        {
            Hint = "Confirm that the document's owner accepts this; replace the protection with --encrypt-env, or have the owner protect the output again.",
        });
    }

    /// <summary>
    /// Saves <paramref name="document"/> to <paramref name="path"/>, encrypted with
    /// <paramref name="passwords"/> when given, in place of any encryption the document carries.
    /// PDF-ENCRYPTED-INFO-TEXT: the engine garbles document information beyond Latin-1 that it
    /// writes into an encrypted file, so when the information holds such text the passwords are
    /// put on a copy reopened from an unencrypted save, which keeps the text. Otherwise the
    /// document is encrypted and saved once, which in evaluation mode prints one notice.
    /// </summary>
    /// <param name="document">The document to save; with passwords its own encryption is replaced.</param>
    /// <param name="path">The file to write.</param>
    /// <param name="passwords">The passwords and permissions of the file; null to save the document as it is.</param>
    internal static void Save(Document document, string path, PdfOutputPasswords? passwords)
    {
        if (passwords is null)
        {
            document.Save(path);
            return;
        }

        if (document.IsEncrypted)
        {
            document.Decrypt();
        }

        if (!InformationBeyondLatin1(document))
        {
            Encrypt(document, passwords);
            document.Save(path);
            return;
        }

        using var plain = new MemoryStream();
        document.Save(plain);
        using var copy = new Document(plain);
        Encrypt(copy, passwords);
        copy.Save(path);
    }

    /// <summary>
    /// Whether the document information holds a character beyond Latin-1, which the engine
    /// writes garbled into an encrypted file (PDF-ENCRYPTED-INFO-TEXT).
    /// </summary>
    internal static bool InformationBeyondLatin1(Document document) =>
        document.Info.Any(static entry => entry.Value is { } value && value.Any(static character => character > 'ÿ'));

    private static void Encrypt(Document document, PdfOutputPasswords passwords)
    {
        string user = passwords.User.Reveal();
        string owner = passwords.Owner.Reveal();
        if (passwords.Permissions is { } permissions)
        {
            document.Encrypt(user, owner, permissions, CryptoAlgorithm.AESx256);
        }
        else
        {
            document.Encrypt(user, owner, DocumentPrivilege.AllowAll, CryptoAlgorithm.AESx256, usePdf20: false);
        }
    }
}

/// <summary>The passwords an output PDF is encrypted with, and the permissions it grants without the owner password; null grants every one.</summary>
internal sealed record PdfOutputPasswords(Secret User, Secret Owner, Permissions? Permissions);

/// <summary>The encryption of one input as a run opened it.</summary>
/// <param name="OpenPassword">Whether the input needs a password to open.</param>
/// <param name="OpenedAsOwner">Whether the run opened it with its owner password.</param>
/// <param name="OpenPasswordIsOwner">Whether its open password is also its owner password.</param>
/// <param name="Permissions">What the input grants without its owner password.</param>
internal sealed record PdfInputSecurity(bool OpenPassword, bool OpenedAsOwner, bool OpenPasswordIsOwner, Permissions Permissions)
{
    internal static PdfInputSecurity Of(LoadedPdf loaded) => new(
        loaded.HasOpenPassword,
        loaded.PasswordType == PasswordType.Owner,
        loaded.OpenPasswordIsOwner,
        loaded.Document.IsEncrypted ? (Permissions)loaded.Document.Permissions : (Permissions)~0);
}
