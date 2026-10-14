using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>
/// What the Skills say an edit outcome means holds for every product (C-R38). Every product applies
/// its batches through the SDK snapshot runner, so <c>itemsAffected</c> is the number of objects that
/// changed and is 0 exactly when the operation warns <c>OP_NO_EFFECT</c>
/// (<c>OperationBehaviorRules</c> checks that on every operation sample); <c>targets</c> past the cap
/// cover each unit with one range or <c>&lt;unit&gt;/all</c>, and a failed operation reports the
/// targets it attempted (<c>BoundedOperationModelTests</c>).
/// </summary>
public sealed partial class EditOutcomeDocumentationTests
{
    private static readonly string Editing =
        Path.Combine(RepositoryPaths.Root, "src", "Aspose.Cli.Host", "Skills", "Platform", "references", "editing.md");

    [Fact]
    public void PlatformSkill_ReadsTheOutcomeAsWhatChanged()
    {
        string outcome = Section(Editing, "## Reading the outcome");
        string[] items = [.. ListItems(outcome)];

        Assert.True(items.Any(static item => item.StartsWith("`itemsAffected`", StringComparison.Ordinal)
                && item.Contains("exactly when", StringComparison.Ordinal)
                && Zero().IsMatch(item)
                && item.Contains("`OP_NO_EFFECT`", StringComparison.Ordinal)),
            $"The Reading the outcome section of {Path.GetRelativePath(RepositoryPaths.Root, Editing)} says that `itemsAffected` "
            + "is 0 exactly when the operation changed nothing and warns `OP_NO_EFFECT`.");
        Assert.True(items.Any(static item => item.StartsWith("`targets`", StringComparison.Ordinal)
                && item.Contains("`<unit>/all`", StringComparison.Ordinal)
                && !item.Contains("root", StringComparison.OrdinalIgnoreCase)),
            $"The Reading the outcome section of {Path.GetRelativePath(RepositoryPaths.Root, Editing)} says that `targets` past "
            + "the cap names one range or `<unit>/all` per unit, not the document root.");
        Assert.True(items.Any(static item => item.Contains("failed", StringComparison.Ordinal)
                && item.Contains("`targets`", StringComparison.Ordinal)
                && item.Contains("attempted", StringComparison.Ordinal)),
            $"The Reading the outcome section of {Path.GetRelativePath(RepositoryPaths.Root, Editing)} says that a failed "
            + "operation's `targets` are the addresses it attempted, not ones it changed.");
    }

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

    /// <summary>The text of a section, from its heading to the next heading of any level.</summary>
    private static string Section(string path, string heading)
    {
        string text = File.ReadAllText(path).ReplaceLineEndings("\n");
        int start = text.IndexOf(heading + "\n", StringComparison.Ordinal);
        Assert.True(start >= 0, $"{Path.GetRelativePath(RepositoryPaths.Root, path)} has a {heading} section.");
        int end = text.IndexOf("\n#", start + heading.Length, StringComparison.Ordinal);
        return end < 0 ? text[start..] : text[start..end];
    }

    /// <summary>The items of the bulleted lists in a text, each on one line without its marker.</summary>
    private static IEnumerable<string> ListItems(string text) =>
        ListItem().Split("\n" + text).Skip(1).Select(static item => item.Split("\n\n")[0].Replace('\n', ' ').Trim());

    [GeneratedRegex(@"\n- ")]
    private static partial Regex ListItem();
}
