using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Text;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

public sealed class TextReplacementTests
{
    [Fact]
    public void WholeWord_TreatsLettersDecimalDigitsAndUnderscoreAsWordCharacters()
    {
        // ² and Ⅻ are numbers but not decimal digits, so they end a word; é, 7 and _ do not.
        TextReplacement replacement = TextReplacement.Create("cat", "dog", regex: false, caseSensitive: false, wholeWord: true);

        Assert.Equal(("dog² cat_ dogⅫ caté cat7 dog", 3), replacement.Replace("cat² cat_ catⅫ caté cat7 cat", int.MaxValue));
    }

    [Fact]
    public void WholeWord_FindsALiteralWordAfterAPartialMatchThatOverlapsIt()
    {
        TextReplacement replacement = TextReplacement.Create("aa", "b", regex: false, caseSensitive: true, wholeWord: true);

        Assert.Equal([(4, 2, "b")], replacement.Matches("aaa aa", int.MaxValue));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WholeWord_KeepsOnlyMatchesThatAreWholeWords(bool regex)
    {
        TextReplacement replacement = TextReplacement.Create(regex ? "cat|category" : "cat", "dog", regex, caseSensitive: false, wholeWord: true);

        Assert.Empty(replacement.Matches("category", int.MaxValue));
    }

    [Fact]
    public void Literal_IgnoresCaseOrdinallyUnlessAsked()
    {
        Assert.Equal(("x straße", 1), TextReplacement.Create("STRASSE", "x", false, false, false).Replace("strasse straße", int.MaxValue));
        Assert.Equal(("Cat x", 1), TextReplacement.Create("cat", "x", false, true, false).Replace("Cat cat", int.MaxValue));
    }

    [Fact]
    public void Replacement_HonorsSubstitutionsOnlyForARegularExpression()
    {
        Assert.Equal(("20-10", 1), TextReplacement.Create(@"(\d+)-(\d+)", "$2-$1", true, false, false).Replace("10-20", int.MaxValue));
        Assert.Equal(("$1 $$", 1), TextReplacement.Create("x", "$1", false, false, false).Replace("x $$", int.MaxValue));
        Assert.Equal(("$ 5", 1), TextReplacement.Create(@"\d", "$$", true, false, false).Replace("4 5", 1));
    }

    [Fact]
    public void Matches_StopsAtTheBudget()
    {
        TextReplacement literal = TextReplacement.Create("a", "b", false, false, false);
        TextReplacement regex = TextReplacement.Create("a", "b", true, false, false);

        Assert.Equal(("bba", 2), literal.Replace("aaa", 2));
        Assert.Equal(("bba", 2), regex.Replace("aaa", 2));
        Assert.Equal(("aaa", 0), regex.Replace("aaa", 0));
    }

    [Theory]
    [InlineData("x*")]
    [InlineData("^")]
    [InlineData("a?")]
    public void Problem_RefusesAnExpressionThatMatchesAnEmptyString(string find)
    {
        Assert.Equal("find matches an empty string", TextReplacement.Problem(find, regex: true, caseSensitive: false)?.Reason);
        Assert.Null(TextReplacement.Problem(find, regex: false, caseSensitive: false));
        Assert.Throws<ArgumentException>(() => TextReplacement.Create(find, "y", regex: true, caseSensitive: false, wholeWord: false));
    }

    [Fact]
    public void Problem_ExplainsAnInvalidExpressionWithAHint()
    {
        (string Reason, string Hint)? problem = TextReplacement.Problem("(", regex: true, caseSensitive: false);

        Assert.NotNull(problem);
        Assert.StartsWith("find is not a valid .NET regular expression: ", problem.Value.Reason, StringComparison.Ordinal);
        Assert.Contains("set regex to false", problem.Value.Hint, StringComparison.Ordinal);
        Assert.Null(TextReplacement.Problem("(", regex: false, caseSensitive: false));
        Assert.NotNull(TextReplacement.Problem(string.Empty, regex: false, caseSensitive: false));
    }

    [Fact]
    public void Matches_SkipsAnEmptyMatchOfAnExpressionThatNeverMatchesAnEmptyString()
    {
        // \b matches no empty string on its own, yet matches between the characters of a text.
        TextReplacement replacement = TextReplacement.Create(@"\b", "|", regex: true, caseSensitive: false, wholeWord: false);

        Assert.Equal(("ab", 0), replacement.Replace("ab", int.MaxValue));
    }

    [Fact]
    public void Matches_ReportsAnExpressionPastItsBudgetAsOperationTimeout()
    {
        TextReplacement replacement = TextReplacement.Create("(a+)+$", "b", regex: true, caseSensitive: false, wholeWord: false);

        CliException error = Assert.Throws<CliException>(() => replacement.Matches(new string('a', 40) + "!", int.MaxValue));

        Assert.Equal(ErrorCodes.OperationTimeout, error.Code);
    }
}
