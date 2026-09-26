using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Text;

/// <summary>
/// Collects one window of search hits in reading order: the first <c>skip</c> matches are
/// passed over without being built, then at most <c>maxHits</c> hits are kept. Every product
/// search offers its matches here, so paging and truncation behave the same everywhere.
/// </summary>
/// <typeparam name="THit">The product's hit record.</typeparam>
public sealed class SearchHits<THit>
{
    private readonly List<THit> _hits = [];
    private readonly int _maxHits;
    private int _toSkip;

    /// <summary>Starts an empty window.</summary>
    /// <param name="skip">Matches to pass over before the window starts.</param>
    /// <param name="maxHits">The most hits the window keeps.</param>
    public SearchHits(int skip, int maxHits)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxHits);
        _toSkip = skip;
        _maxHits = maxHits;
    }

    /// <summary>The hits in the window, in the order they were offered.</summary>
    public IReadOnlyList<THit> Hits => _hits;

    /// <summary>Whether a match was offered after the window was full.</summary>
    public bool Truncated { get; private set; }

    /// <summary>
    /// The result window of these hits; the command adds <c>next</c> with
    /// <see cref="Extensibility.Commanding.SearchOptions.Continue"/>.
    /// </summary>
    public ResultWindow Window() => new()
    {
        Unit = "hit",
        Returned = _hits.Count,
        Truncated = Truncated,
    };

    /// <summary>
    /// Offers the next match. Returns false once the window is full, which marks it truncated;
    /// the caller stops searching then. <paramref name="build"/> runs only for kept hits.
    /// </summary>
    public bool Offer(Func<THit> build)
    {
        ArgumentNullException.ThrowIfNull(build);
        if (_toSkip > 0)
        {
            _toSkip--;
            return true;
        }

        if (_hits.Count == _maxHits)
        {
            Truncated = true;
            return false;
        }

        _hits.Add(build());
        return true;
    }
}
