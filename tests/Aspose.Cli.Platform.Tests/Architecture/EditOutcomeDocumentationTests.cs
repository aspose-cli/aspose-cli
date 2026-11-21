using System.Text.RegularExpressions;
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

    /// <summary>
    /// A dry run publishes nothing, so it gives no warning about a written output, such as
    /// <c>EVALUATION_MODE</c> or <c>ENCRYPTION_REMOVED</c>: no Skill says it gives the warnings of
    /// the real run.
    /// </summary>
    [Fact]
    public void Skills_DoNotPromiseTheRealRunsWarningsFromADryRun()
    {
        string[] claims =
        [
            .. Directory.EnumerateFiles(Path.Combine(RepositoryPaths.Root, "src"), "*.md", SearchOption.AllDirectories)
                .Where(static file => file.Split(Path.DirectorySeparatorChar).Contains("Skills"))
                .SelectMany(static file => Paragraphs(file)
                    .Where(static paragraph => DryRun().IsMatch(paragraph))
                    .SelectMany(static paragraph => SentenceEnd().Split(paragraph))
                    .Where(static sentence => RealRunWarnings().IsMatch(sentence))
                    .Select(sentence => $"{Path.GetRelativePath(RepositoryPaths.Root, file)}: {sentence.Trim()}")),
        ];
        Assert.True(claims.Length == 0,
            "No Skill says that a dry run gives the warnings of the real run; output warnings come only from the real run:"
            + Environment.NewLine + string.Join(Environment.NewLine, claims));
    }

    [GeneratedRegex(@"dry run|--dry-run", RegexOptions.IgnoreCase)]
    private static partial Regex DryRun();

    [GeneratedRegex(@"warnings the real run|real run would give|same warnings|(?:all|every) (?:the )?warnings?\b", RegexOptions.IgnoreCase)]
    private static partial Regex RealRunWarnings();

    private static string[] Paragraphs(string path) =>
        [.. File.ReadAllText(path).ReplaceLineEndings("\n").Split("\n\n").Select(static paragraph => paragraph.Replace('\n', ' '))];

    /// <summary>The end of a sentence: a full stop or semicolon followed by white space.</summary>
    [GeneratedRegex(@"(?<=[.;])\s+")]
    private static partial Regex SentenceEnd();
}
