using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>
/// What the Skills say about the outputs a command writes holds for every product.
/// </summary>
public sealed partial class EditOutcomeDocumentationTests
{
    private static readonly string Editing =
        Path.Combine(RepositoryPaths.Root, "src", "Aspose.Cli.Host", "Skills", "Platform", "references", "editing.md");

    /// <summary>
    /// The files of a command that writes several follow the encryption rule of a single output,
    /// and each <c>outputs[]</c> entry reports <c>encrypted</c> as <c>output</c> does.
    /// </summary>
    [Fact]
    public void PlatformSkill_AppliesTheEncryptionRuleToEveryFileOfOutputs()
    {
        string encryption = Assert.Single(Paragraphs(Editing), static paragraph => paragraph.Contains("`ENCRYPTION_REMOVED`", StringComparison.Ordinal)
            && paragraph.Contains("password", StringComparison.Ordinal));

        Assert.True(encryption.Contains("`outputs[]`", StringComparison.Ordinal) && encryption.Contains("same rule", StringComparison.Ordinal),
            $"The encryption paragraph of {Path.GetRelativePath(RepositoryPaths.Root, Editing)} says that the files a command lists "
            + $"in `outputs[]` follow the same rule: {encryption}");
        Assert.True(encryption.Contains("each `outputs[]` entry", StringComparison.Ordinal) && !encryption.Contains("omit", StringComparison.Ordinal),
            $"The encryption paragraph of {Path.GetRelativePath(RepositoryPaths.Root, Editing)} says that each `outputs[]` entry "
            + $"reports `encrypted` as `output` does: {encryption}");
    }

    private static string[] Paragraphs(string path) =>
        [.. File.ReadAllText(path).ReplaceLineEndings("\n").Split("\n\n").Select(static paragraph => paragraph.Replace('\n', ' '))];

}
