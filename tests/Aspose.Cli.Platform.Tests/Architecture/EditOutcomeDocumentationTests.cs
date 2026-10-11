using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>
/// What the Skills say an edit outcome means holds for every product (C-R38). An operation that
/// changed nothing is named by its <c>OP_NO_EFFECT</c> warning; <c>itemsAffected</c> is the count
/// the product reports, which the C-R30 invariant cases show is not yet a before-and-after
/// comparison everywhere, so no Skill tells an agent that a count of 0 means nothing changed.
/// When an operation changed more parts than <c>targets</c> holds, it names a coarser address
/// such as the document root.
/// </summary>
public sealed partial class EditOutcomeDocumentationTests
{
    private static readonly string Editing =
        Path.Combine(RepositoryPaths.Root, "src", "Aspose.Cli.Host", "Skills", "Platform", "references", "editing.md");

    [Fact]
    public void PlatformSkill_ReadsNoEffectFromTheWarning()
    {
        string[] paragraphs = Paragraphs(Editing);

        Assert.True(paragraphs.Any(static paragraph => paragraph.Contains("`itemsAffected`", StringComparison.Ordinal)
                && paragraph.Contains("`OP_NO_EFFECT`", StringComparison.Ordinal)),
            $"A paragraph of {Path.GetRelativePath(RepositoryPaths.Root, Editing)} that describes `itemsAffected` says that "
            + "`OP_NO_EFFECT` is what names an operation that changed nothing.");
        Assert.True(paragraphs.Any(static paragraph => paragraph.Contains("`targets`", StringComparison.Ordinal)
                && paragraph.Contains("root", StringComparison.OrdinalIgnoreCase)),
            $"A paragraph of {Path.GetRelativePath(RepositoryPaths.Root, Editing)} that describes `targets` says that an "
            + "operation that changed more parts than the list holds names a coarser address, such as the document root.");
    }

    [Fact]
    public void NoSkill_ReadsAZeroCountAsNoChange()
    {
        string[] claims =
        [
            .. Directory.EnumerateFiles(Path.Combine(RepositoryPaths.Root, "src"), "*.md", SearchOption.AllDirectories)
                .Where(static file => file.Split(Path.DirectorySeparatorChar).Contains("Skills"))
                .SelectMany(static file => Paragraphs(file)
                    .SelectMany(static paragraph => SentenceEnd().Split(paragraph))
                    .Where(static sentence => sentence.Contains("`itemsAffected`", StringComparison.Ordinal)
                        && Zero().IsMatch(sentence)
                        && NoChange().IsMatch(sentence))
                    .Select(sentence => $"{Path.GetRelativePath(RepositoryPaths.Root, file)}: {sentence.Trim()}")),
        ];

        Assert.True(claims.Length == 0,
            "No Skill says that `itemsAffected` 0 means an operation changed nothing; `OP_NO_EFFECT` says that:"
            + Environment.NewLine + string.Join(Environment.NewLine, claims));
    }

    /// <summary>
    /// A dry run publishes nothing, so it gives no warning about a written output, such as
    /// <c>EVALUATION_MODE</c> or <c>ENCRYPTION_REMOVED</c>: no Skill says it gives the warnings of
    /// the real run, and the platform Skill's dry-run item names the ones it leaves out.
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

        const string Marker = "**`--dry-run`.**";
        string list = Assert.Single(Paragraphs(Editing), static paragraph => paragraph.Contains(Marker, StringComparison.Ordinal));
        string item = list[list.IndexOf(Marker, StringComparison.Ordinal)..];
        int next = item.IndexOf(" - **", StringComparison.Ordinal);
        item = next < 0 ? item : item[..next];
        Assert.True(item.Contains("`EVALUATION_MODE`", StringComparison.Ordinal) && item.Contains("`ENCRYPTION_REMOVED`", StringComparison.Ordinal),
            $"The --dry-run item of {Path.GetRelativePath(RepositoryPaths.Root, Editing)} names the output warnings a dry run does not give: {item}");
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

    /// <summary>A standalone 0, not a digit of 100.</summary>
    [GeneratedRegex(@"(?<![0-9.])0(?![0-9]|\.[0-9])")]
    private static partial Regex Zero();

    [GeneratedRegex(@"changed nothing|nothing changed|no change", RegexOptions.IgnoreCase)]
    private static partial Regex NoChange();
}
