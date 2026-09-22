namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// Evidence that a published output was reopened and verified. The receipt is
/// present exactly when publication happened, so its presence is the signal;
/// a dry run carries none.
/// </summary>
public sealed record MutationReceipt
{
    /// <summary>Verification performed before the output was published.</summary>
    public required string Verification { get; init; }
}
