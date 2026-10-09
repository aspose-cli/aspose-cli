using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>
/// The platform Skill explains how to read an edit outcome's <c>targets</c>: they list at most
/// 100 addresses, and the document root address alone means "too many to list" when
/// <c>itemsAffected</c> is over 100 and "nothing changed" when it is 0.
/// </summary>
public sealed partial class EditTargetDocumentationTests
{
    [Fact]
    public void PlatformSkill_StatesTheTargetCapAndTheRootAddress()
    {
        string path = Path.Combine(RepositoryPaths.Root, "src", "Aspose.Cli.Host", "Skills", "Platform", "references", "editing.md");
        string[] paragraphs = File.ReadAllText(path).ReplaceLineEndings("\n").Split("\n\n");

        Assert.True(paragraphs.Any(static paragraph => paragraph.Contains("`targets`", StringComparison.Ordinal)
                && paragraph.Contains("`itemsAffected`", StringComparison.Ordinal)
                && paragraph.Contains("100", StringComparison.Ordinal)
                && Zero().IsMatch(paragraph)
                && paragraph.Contains("root", StringComparison.OrdinalIgnoreCase)),
            $"A paragraph of {Path.GetRelativePath(RepositoryPaths.Root, path)} that describes `targets` and `itemsAffected` "
            + "states the cap of 100 targets, and that the document root address with `itemsAffected` over 100 means too "
            + "many to list, while with `itemsAffected` 0 it means nothing changed.");
    }

    /// <summary>A standalone 0, not a digit of 100.</summary>
    [GeneratedRegex(@"(?<![0-9.])0(?![0-9]|\.[0-9])")]
    private static partial Regex Zero();
}
