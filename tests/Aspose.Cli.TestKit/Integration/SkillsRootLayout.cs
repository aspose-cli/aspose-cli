using Xunit;

namespace Aspose.Cli.TestKit;

/// <summary>
/// The layout both Skill installers keep, <c>aspose-cli skill install</c> and <c>install.ps1</c>:
/// a host loads every directory under its Skills root as a Skill, so the root holds only Skills,
/// and an installation's transaction artifacts (its stages, the backups of replaced Skills and
/// their journals) stay in the root's parent, named with <see cref="TransactionPrefix"/>.
/// </summary>
public static class SkillsRootLayout
{
    /// <summary>The prefix of every transaction artifact a Skill installation leaves beside the root.</summary>
    public const string TransactionPrefix = ".aspose-";

    /// <summary>Asserts that the Skills root holds exactly these Skill directories and nothing else.</summary>
    public static void AssertOnlySkills(string skillsRoot, params string[] skills)
    {
        Assert.Equal(
            skills.Order(StringComparer.OrdinalIgnoreCase),
            Directory.EnumerateFileSystemEntries(skillsRoot).Select(Path.GetFileName).Order(StringComparer.OrdinalIgnoreCase));
        Assert.All(skills, skill => Assert.True(Directory.Exists(Path.Combine(skillsRoot, skill)), skill));
    }

    /// <summary>
    /// Asserts that the Skills root holds no transaction artifact and that its parent holds
    /// <paramref name="count"/> entries matching <paramref name="pattern"/>, a pattern that starts
    /// with <see cref="TransactionPrefix"/>.
    /// </summary>
    public static void AssertTransactionArtifactsBeside(string skillsRoot, string pattern, int count)
    {
        Assert.StartsWith(TransactionPrefix, pattern, StringComparison.Ordinal);
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(skillsRoot));
        if (Directory.Exists(root))
        {
            Assert.Empty(Directory.EnumerateFileSystemEntries(root, TransactionPrefix + "*"));
        }
        Assert.Equal(count, Directory.GetFileSystemEntries(Path.GetDirectoryName(root)!, pattern).Length);
    }
}
