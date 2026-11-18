using System.Security.Cryptography;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.IO.Publication;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

/// <summary>
/// Agents run several CLI processes over one file. A process that is still reading an input
/// must not stop another from replacing it in place: the reader keeps the bytes it opened and
/// its own fingerprint check later reports the change.
/// </summary>
public sealed class InPlaceReadSharingTests
{
    [Fact]
    public void InPlaceReplace_IsPublishedWhileAnotherInvocationReadsTheInput()
    {
        using var temp = new TempDirectory();
        string target = temp.File("book.bin");
        File.WriteAllText(target, "original");
        ResourceBudgetLedger reader = TestBudgets.Create();
        ResourceBudgetLedger editor = TestBudgets.Create();
        FileWritePrecondition precondition = FileWritePrecondition.Capture(target);

        using Stream open = reader.Inputs.OpenFile(target);
        using (var set = new AtomicOutputSetWriter(new SafeFileWriter(editor), temp.Path, "edit"))
        {
            set.Stage(target, overwrite: true, precondition,
                staged => File.WriteAllText(staged, "edited"));
            set.Commit();
        }

        Assert.Equal("edited", File.ReadAllText(target));
        Assert.Equal("original", new StreamReader(open).ReadToEnd());
    }

    [Fact]
    public void InPlaceReplace_WhileAnotherHandleReadsTheInput_LeavesNoStaging()
    {
        using var temp = new TempDirectory();
        string target = temp.File("book.bin");
        File.WriteAllText(target, "original");
        FileWritePrecondition precondition = FileWritePrecondition.Capture(target);

        using var open = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        using (var set = new AtomicOutputSetWriter(TestBudgets.Writer(), temp.Path, "edit"))
        {
            set.Stage(target, overwrite: true, precondition,
                staged => File.WriteAllText(staged, "edited"));
            set.Commit();
        }

        Assert.Equal("edited", File.ReadAllText(target));
        Assert.Equal("original", new StreamReader(open).ReadToEnd());
        Assert.Empty(Directory.EnumerateDirectories(temp.Path, ".aspose-publication-*"));
    }

    [Fact]
    public void Precondition_IsCapturedWhileAnotherProcessReplacesTheInput()
    {
        using var temp = new TempDirectory();
        string target = temp.File("book.bin");
        File.WriteAllText(target, "original");

        // A concurrent in-place replace holds the input open with delete access.
        using (new FileStream(target, FileMode.Open, FileAccess.Read,
                   FileShare.ReadWrite | FileShare.Delete, 1, FileOptions.DeleteOnClose))
        {
            FileWritePrecondition precondition = FileWritePrecondition.Capture(target);

            Assert.Equal(
                Convert.ToHexStringLower(SHA256.HashData("original"u8)),
                precondition.Fingerprint.Sha256);
        }
    }
}
