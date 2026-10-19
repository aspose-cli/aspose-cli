using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Text;
using Aspose.Words;

namespace Aspose.Cli.Product.Words.Engine.Editing;

/// <summary>
/// The matches replace_text replaces and format_text formats, found where <c>words search</c>
/// finds them: in the text it reads of each paragraph, with the text boxes the paragraph anchors,
/// or of each comment or note, so ^ and $ anchor at each such text. The engine's own
/// <c>Range.Replace</c> is not used, as it anchors at the edges of its whole range
/// (WORDS-REGEX-ANCHORS) and runs past an expression's timeout (WORDS-REGEX-TIMEOUT).
/// </summary>
internal static class WordsTextMatches
{
    /// <summary>
    /// The first <paramref name="limit"/> matches of a unit that lie in the text of its runs, in
    /// order. A match that takes in a list number, a paragraph mark or a page field's placeholder,
    /// such as <c>{PAGE}</c>, is no text of runs alone, so it is skipped and not counted.
    /// </summary>
    /// <exception cref="Aspose.Cli.Sdk.Errors.CliException"><c>OPERATION_TIMEOUT</c> when the expression exceeds its budget.</exception>
    internal static IReadOnlyList<TextMatch> In(Node unit, TextReplacement replacement, int limit)
    {
        (string text, IReadOnlyList<(Run Run, int At)> runs) = WordsText.Located(unit);
        var matches = new List<TextMatch>();
        if (limit == 0)
        {
            return matches;
        }

        foreach ((int start, int length, string replaced) in replacement.Matches(text, int.MaxValue))
        {
            Piece[] pieces =
            [
                .. runs.Select(item => new Piece(item.Run, Math.Max(start, item.At) - item.At, Math.Min(start + length, item.At + item.Run.Text.Length) - item.At))
                    .Where(static piece => piece.From < piece.To),
            ];
            if (pieces.Sum(static piece => piece.To - piece.From) != length)
            {
                continue;
            }

            matches.Add(new TextMatch(pieces, replaced));
            if (matches.Count == limit)
            {
                break;
            }
        }

        return matches;
    }

    /// <summary>
    /// Splits the runs of a unit's matches from the text around them and returns, for each match
    /// in order, the runs that now hold exactly its text. Each split run keeps its own format.
    /// </summary>
    internal static IReadOnlyList<IReadOnlyList<Run>> Isolate(IReadOnlyList<TextMatch> matches)
    {
        var isolated = new IReadOnlyList<Run>[matches.Count];
        // Last match first: a split run keeps its first part, where an earlier match lies.
        for (int index = matches.Count - 1; index >= 0; index--)
        {
            var runs = new List<Run>();
            foreach ((Run run, int from, int to) in matches[index].Pieces)
            {
                if (to < run.Text.Length)
                {
                    SplitRun(run, to);
                }

                runs.Add(from > 0 ? SplitRun(run, from) : run);
            }

            isolated[index] = runs;
        }

        return isolated;
    }

    // Splits a run at a position of its text and returns the run that holds the rest.
    private static Run SplitRun(Run run, int position)
    {
        var rest = (Run)run.Clone(true);
        rest.Text = run.Text[position..];
        run.Text = run.Text[..position];
        run.ParentNode.InsertAfter(rest, run);
        return rest;
    }

    /// <summary>The part of a run's text, from <paramref name="From"/> up to <paramref name="To"/>, that a match takes in.</summary>
    internal readonly record struct Piece(Run Run, int From, int To);

    /// <summary>A match: the parts of runs it takes in, in order, and the text that replaces it.</summary>
    internal sealed record TextMatch(IReadOnlyList<Piece> Pieces, string Replacement);
}
