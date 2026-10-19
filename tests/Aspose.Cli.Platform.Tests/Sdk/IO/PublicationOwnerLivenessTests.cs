using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

/// <summary>
/// Recovery rolls back an interrupted publication only when the process that wrote its journal
/// has ended: a live owner is still publishing, and a reused process id is not the owner.
/// </summary>
[Collection("Publication timing")]
public sealed class PublicationOwnerLivenessTests
{
    [Fact]
    public void AJournalOfALiveOwner_IsNotRecovered()
    {
        using var temp = new TempDirectory();
        string transaction = InterruptedPublication(temp, Environment.ProcessId, ProcessIdentity.CurrentStartUtcTicks);

        Assert.Equal(0, AtomicOutputSetWriter.RecoverPending(temp.Path));
        Assert.Equal("candidate", File.ReadAllText(temp.File("document.txt")));
        Assert.True(Directory.Exists(transaction));
    }

    [Fact]
    public void AJournalWhoseOwnerPidWasReused_IsRecovered()
    {
        using var temp = new TempDirectory();
        string transaction = InterruptedPublication(temp, Environment.ProcessId, ownerStartTicks: 1);

        Assert.Equal(1, AtomicOutputSetWriter.RecoverPending(temp.Path));
        Assert.Equal("original", File.ReadAllText(temp.File("document.txt")));
        Assert.False(Directory.Exists(transaction));
    }

    [Fact]
    public void AJournalWithoutAnOwnerStartTime_IsNotRecovered()
    {
        using var temp = new TempDirectory();
        string transaction = InterruptedPublication(temp, Environment.ProcessId, ownerStartTicks: 0);

        Assert.Equal(0, AtomicOutputSetWriter.RecoverPending(temp.Path));
        Assert.Equal("candidate", File.ReadAllText(temp.File("document.txt")));
        Assert.True(Directory.Exists(transaction));
    }

    /// <summary>A publication that swapped its candidate into place and stopped before committing.</summary>
    private static string InterruptedPublication(TempDirectory temp, int ownerProcessId, long ownerStartTicks)
    {
        string transaction = Directory.CreateDirectory(temp.File(".aspose-publication-owner-liveness")).FullName;
        string backups = Directory.CreateDirectory(Path.Combine(transaction, "backups")).FullName;
        string target = temp.File("document.txt");
        File.WriteAllText(target, "original");
        FilePublicationSnapshot original = FilePublicationSnapshot.Capture(target);
        string staged = AtomicPublicationPlan.StagedPath(transaction, target, 0);
        Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
        File.WriteAllText(staged, "candidate");
        FilePublicationSnapshot candidate = FilePublicationSnapshot.Capture(staged);
        string displaced = Path.Combine(backups, "000001.displaced");
        File.Replace(staged, target, displaced);
        new PublicationJournal
        {
            Operation = "owner-liveness", OwnerProcessId = ownerProcessId, OwnerProcessStartUtcTicks = ownerStartTicks,
            State = PublicationTransactionState.Publishing,
            Entries = [new PublicationJournalEntry
            {
                Index = 0, Target = target, Staged = staged, Overwrite = true,
                Original = original, StagedSnapshot = candidate, Size = candidate.Length,
                Backup = Path.Combine(backups, "000001.backup"), Displaced = displaced,
                TargetParentIdentity = OutputPathValidator.CaptureParentIdentity(target),
                State = PublicationEntryState.Publishing,
            }],
        }.Write(Path.Combine(transaction, AtomicPublicationPlan.JournalName));
        return transaction;
    }
}
