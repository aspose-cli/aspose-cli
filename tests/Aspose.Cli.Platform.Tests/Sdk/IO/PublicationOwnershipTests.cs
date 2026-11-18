using System.Runtime.InteropServices;
using System.Text;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Extraction;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.IO.Publication;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

/// <summary>
/// Publication replaces or removes only what it can prove is not an input of the invocation:
/// a target is compared with every admitted input by file identity, so no other spelling of
/// an input (a case variant, a hard link, a junction or a short 8.3 name) lets a command write
/// over it, and only an in-place edit replaces its own input.
/// </summary>
public sealed class PublicationOwnershipTests
{
    /// <summary>The ways one file can be named twice: the input as admitted, the target as written.</summary>
    public static TheoryData<string> Aliases => ["case", "hard link", "junction", "short name"];

    [Theory]
    [MemberData(nameof(Aliases))]
    public void AFileTargetThatIsAnInput_IsRefusedAndTheInputKept(string alias)
    {
        Requires.Windows();
        using var temp = new TempDirectory();
        (string input, string target) = Spell(temp, alias);
        ResourceBudgetLedger budgets = TestBudgets.Create();
        budgets.AdmitFile(input);

        CliException error = Assert.Throws<CliException>(() => new SafeFileWriter(budgets).Write(
            target, overwrite: true, path => File.WriteAllText(path, "replaced")));

        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
        Assert.Equal("input", File.ReadAllText(input));
        Assert.Empty(Directory.EnumerateDirectories(temp.File("real"), ".aspose-publication-*"));
    }

    [Fact]
    public void AnInPlaceEdit_ReplacesItsOwnInput()
    {
        using var temp = new TempDirectory();
        string input = temp.File("book.xlsx");
        File.WriteAllText(input, "input");
        ResourceBudgetLedger budgets = TestBudgets.Create();
        budgets.AdmitFile(input);

        new SafeFileWriter(budgets).Write(input, overwrite: true,
            FileWritePrecondition.Capture(input), path => File.WriteAllText(path, "edited"));

        Assert.Equal("edited", File.ReadAllText(input));
    }

    [Fact]
    public void AnInPlaceEdit_StillRefusesAnotherInputAsItsTarget()
    {
        Requires.Windows();
        using var temp = new TempDirectory();
        string edited = temp.File("book.xlsx");
        string source = temp.File("source.xlsx");
        File.WriteAllText(edited, "edited input");
        File.WriteAllText(source, "input");
        string alias = temp.File("alias.xlsx");
        HardLink(alias, source);
        ResourceBudgetLedger budgets = TestBudgets.Create();
        budgets.AdmitFile(edited);
        budgets.AdmitFile(source);

        CliException error = Assert.Throws<CliException>(() => new SafeFileWriter(budgets).Write(alias, overwrite: true,
            FileWritePrecondition.Capture(edited), path => File.WriteAllText(path, "replaced")));

        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
        Assert.Equal("input", File.ReadAllText(source));
    }

    [Fact]
    public void AnExtractedFileNamedLikeItsInput_IsRefusedAndTheInputKept()
    {
        using var temp = new TempDirectory();
        string input = temp.File("attachment-0001.pdf");
        File.WriteAllText(input, "input");
        ResourceBudgetLedger budgets = TestBudgets.Create();
        budgets.AdmitFile(input);

        using var extraction = new ExtractionGuard(budgets, temp.Path, overwrite: true);
        CliException error = Assert.Throws<CliException>(() => extraction.StageAllBytes("attachment-0001.pdf", "replaced"u8.ToArray()));

        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
        Assert.Equal("input", File.ReadAllText(input));
    }

    [Fact]
    public void ADirectoryReplacementThatHoldsAnInputThroughAJunction_IsRefusedAndTheDirectoryKept()
    {
        Requires.Windows();
        using var temp = new TempDirectory();
        string target = temp.File("evidence");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "page.png"), "input");
        string junction = temp.File("linked");
        FileSystemLinks.CreateDirectoryLink(junction, target);
        try
        {
            ResourceBudgetLedger budgets = TestBudgets.Create();
            budgets.AdmitFile(Path.Combine(junction, "page.png"));

            CliException error = Assert.Throws<CliException>(() =>
            {
                using var publication = new AtomicNewDirectoryWriter(budgets, target, "test", replace: true);
                File.WriteAllText(Path.Combine(publication.StagingDirectory, "index.html"), "replacement");
                publication.Commit();
            });

            Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
            Assert.Equal(["page.png"], Directory.EnumerateFileSystemEntries(target).Select(Path.GetFileName));
            Assert.Equal("input", File.ReadAllText(Path.Combine(target, "page.png")));
        }
        finally
        {
            Directory.Delete(junction);
        }
    }

    // The input as the invocation admitted it and the same file as the target names it.
    private static (string Input, string Target) Spell(TempDirectory temp, string alias)
    {
        string real = temp.File("real");
        Directory.CreateDirectory(real);
        string file = Path.Combine(real, "quarterly report input.xlsx");
        File.WriteAllText(file, "input");
        switch (alias)
        {
            case "case":
                return (file, Path.Combine(real, "QUARTERLY REPORT INPUT.XLSX"));
            case "hard link":
                string link = Path.Combine(real, "link.xlsx");
                HardLink(link, file);
                return (file, link);
            case "junction":
                string junction = temp.File("linked");
                FileSystemLinks.CreateDirectoryLink(junction, real);
                return (Path.Combine(junction, Path.GetFileName(file)), file);
            case "short name":
                string shortName = ShortPath(file);
                Assert.SkipWhen(string.Equals(shortName, file, StringComparison.OrdinalIgnoreCase),
                    "The volume creates no 8.3 short names.");
                return (shortName, file);
            default:
                throw new ArgumentOutOfRangeException(nameof(alias));
        }
    }

    private static void HardLink(string link, string existing) =>
        Assert.True(CreateHardLink(link, existing, IntPtr.Zero), $"CreateHardLink failed with {Marshal.GetLastWin32Error()}.");

    private static string ShortPath(string path)
    {
        var buffer = new StringBuilder(1024);
        uint length = GetShortPathName(path, buffer, (uint)buffer.Capacity);
        Assert.True(length > 0 && length < buffer.Capacity, $"GetShortPathName failed with {Marshal.GetLastWin32Error()}.");
        return buffer.ToString();
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string name, string existing, IntPtr security);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetShortPathName(string longPath, StringBuilder shortPath, uint bufferLength);
}
