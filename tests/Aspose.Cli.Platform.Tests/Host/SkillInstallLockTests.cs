using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.Skills;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Host.Tests;

public sealed class SkillInstallLockTests
{
    [Fact]
    public void Install_WhileAnotherInstallerHoldsTheTarget_ConflictsAndLeavesNoLockFileBehind()
    {
        using var temp = new TempDirectory();
        BundledSkill skill = ActualCommandTree.Host.Skills.All[0];
        string target = Path.GetFullPath(temp.File(skill.Name));

        using (LocalServiceOperationLock.Acquire(
            "skill-install",
            OperatingSystem.IsWindows() ? target.ToUpperInvariant() : target,
            TimeSpan.FromSeconds(5)))
        {
            CliException conflict = Assert.Throws<CliException>(() => skill.InstallInto(Budgets(), target));
            Assert.Equal(ErrorCodes.OutputExists, conflict.Code);
        }

        skill.InstallInto(Budgets(), target);

        Assert.True(File.Exists(Path.Combine(target, "SKILL.md")));
        Assert.Equal([target], Directory.GetFileSystemEntries(temp.Path));
    }

    private static Aspose.Cli.Sdk.IO.ResourceBudgetLedger Budgets() =>
        ProductTestBudgets.Create(ActualCommandTree.Host.Catalog.Products[0]);
}
