using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Results;

/// <summary>
/// What publishing an edited document produced (<see cref="OutputPipeline{TDocument}.PublishEdit"/>):
/// the output, the backup an in-place edit kept or created, and the product's read-back of the
/// staged output. A dry run publishes nothing, so all three are null.
/// </summary>
/// <typeparam name="TVerification">The product's verification of the staged output.</typeparam>
/// <param name="Output">The published output, or null for a dry run.</param>
/// <param name="Backup">The requested backup, or null when none was requested or the output replaced no file.</param>
/// <param name="Verification">The read-back of the staged output, or null when it was not verified.</param>
public sealed record EditPublication<TVerification>(
    OutputInfo? Output,
    BackupInfo? Backup,
    TVerification? Verification)
    where TVerification : class
{
    /// <summary>The warning that a kept backup holds an earlier version than the file the edit replaced, or null.</summary>
    public IReadOnlyList<Warning>? BackupWarnings => EnvelopeParts.BackupWarnings(Backup);
}
