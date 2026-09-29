using Aspose.Words;

namespace Aspose.Cli.Product.Words.Engine.Editing;

/// <summary>
/// The revision tracking of one <c>--track-changes</c> edit: every revision carries the same
/// author and time. Aspose.Words cannot report whether tracking is on, so the edit keeps this
/// state, and an operation can stop and restart tracking to prepare detached content that it
/// then inserts as one tracked insertion.
/// </summary>
internal sealed class WordsRevisionTracking(Document document, string author)
{
    private readonly DateTime _time = DateTime.Now;

    internal void Start() => document.StartTrackRevisions(author, _time);

    internal void Stop() => document.StopTrackRevisions();
}
