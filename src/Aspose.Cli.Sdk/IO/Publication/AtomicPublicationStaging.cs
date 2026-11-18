using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Files;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.IO.Paths;

namespace Aspose.Cli.Sdk.IO.Publication;

/// <summary>Validates and stages unique publication targets.</summary>
internal sealed class AtomicPublicationStaging(
    AtomicPublicationPlan plan,
    SafeFileWriter writer)
{
    public StagedOutput StagePrepared(
        string target,
        bool overwrite,
        FilePublicationSnapshot original,
        Action<string> write,
        FileWritePrecondition? inputPrecondition = null,
        Action<string>? verify = null)
    {
        ArgumentNullException.ThrowIfNull(write);
        if (plan.State != PublicationTransactionState.Staging) { throw new InvalidOperationException("The output set is sealed."); }
        plan.EnsureTarget(target);
        target = OutputPathValidator.NormalizeFile(target);
        EnsureUnique(target);
        plan.EnsureCapacityForEntry();
        // Only an in-place edit, whose precondition names the target, replaces an input.
        if (inputPrecondition?.Targets(target) is not true)
        {
            writer.ResourceBudgets.EnsureNotInput(target, original.PhysicalIdentity);
        }
        if (!overwrite && original.Exists)
        {
            throw CliErrors.OutputExists(target);
        }
        plan.EnsureOutputDirectory(Path.GetDirectoryName(target)!);
        FilePhysicalIdentity? targetParentIdentity = Directory.Exists(Path.GetDirectoryName(target))
            ? OutputPathValidator.CaptureParentIdentity(target) : null;

        int index = plan.Entries.Count;
        string staged = plan.StagedPath(target, index);
        plan.CreatedStagingDirectories.Add(Path.GetDirectoryName(staged)!);
        Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
        using var temporary = OwnedTemporaryFile.Create(staged);
        write(staged);
        temporary.BindAndVerify(verify);
        original.Metadata?.Apply(staged);
        temporary.BindProducedFile();
        temporary.FlushBound();
        FilePublicationSnapshot stagedSnapshot = temporary.CaptureBoundSnapshot();
        writer.ConsumeOutput(stagedSnapshot.Length, "output-set-stage");

        long size = stagedSnapshot.Length;
        OutputPathValidator.EnsureParentUnchanged(
            target,
            targetParentIdentity);
        plan.Entries.Add(new PublicationEntry
        {
            Index = index,
            Target = target,
            Staged = staged,
            Overwrite = overwrite,
            InputPath = inputPrecondition?.Path,
            InputSnapshot = inputPrecondition?.Snapshot,
            Original = original,
            StagedSnapshot = stagedSnapshot,
            TargetParentIdentity = targetParentIdentity,
            Displaced = original.Exists
                ? plan.DisplacedPath(index)
                : null,
            Size = size,
        });
        temporary.MarkPublished();
        return new StagedOutput(plan.Entries[^1]);
    }

    public void StageDeletionPrepared(
        string target,
        FilePublicationSnapshot original)
    {
        if (plan.State != PublicationTransactionState.Staging)
        {
            throw new InvalidOperationException(
                $"Cannot stage a deletion while the transaction is {plan.State}.");
        }
        if (!original.Exists)
        {
            throw new InvalidOperationException(
                $"Cannot stage deletion of missing target '{target}'.");
        }

        plan.EnsureTarget(target);
        target = OutputPathValidator.NormalizeFile(target);
        EnsureUnique(target);
        plan.EnsureCapacityForEntry();
        plan.EnsureOutputDirectory(Path.GetDirectoryName(target)!);
        FilePhysicalIdentity? targetParentIdentity = Directory.Exists(Path.GetDirectoryName(target))
            ? OutputPathValidator.CaptureParentIdentity(target) : null;
        int index = plan.Entries.Count;
        string staged = plan.StagedPath(target, index);
        plan.CreatedStagingDirectories.Add(Path.GetDirectoryName(staged)!);
        Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
        using var temporary = OwnedTemporaryFile.Create(staged);
        temporary.BindAndVerify(verify: null);
        temporary.FlushBound();
        FilePublicationSnapshot stagedSnapshot = temporary.CaptureBoundSnapshot();
        OutputPathValidator.EnsureParentUnchanged(
            target,
            targetParentIdentity);

        plan.Entries.Add(new PublicationEntry
        {
            Index = index,
            Target = target,
            Staged = staged,
            Overwrite = true,
            Original = original,
            StagedSnapshot = stagedSnapshot,
            TargetParentIdentity = targetParentIdentity,
            Displaced = plan.DisplacedPath(index),
            DeleteTarget = true,
            Size = 0,
        });
        temporary.MarkPublished();
    }

    private void EnsureUnique(string target)
    {
        if (plan.Entries.Any(item => FilePaths.Same(item.Target, target)))
        {
            throw CliErrors.DuplicateOutput(target);
        }
    }
}
