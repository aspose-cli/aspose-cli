namespace Aspose.Cli.Sdk.Results;

/// <summary>
/// What an invocation's unencrypted copies lost, for the hint of <c>ENCRYPTION_REMOVED</c>.
/// </summary>
/// <param name="Formats">The format ids of the unencrypted copies.</param>
/// <param name="OpenPassword">
/// Whether an encrypted input needed a password to open; when not, its encryption only restricted
/// what may be done with it.
/// </param>
/// <param name="Requested">Whether the command removed the encryption because it was asked to.</param>
internal sealed record EncryptionLoss(IReadOnlyList<string> Formats, bool OpenPassword, bool Requested);
