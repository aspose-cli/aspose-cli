using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Text;
using Aspose.Words;
using static Aspose.Cli.Product.Words.Engine.WordsEngineSupport;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>Serves <c>words query search</c>: bounded content search of document scopes.</summary>
internal static class WordsSearch
{
    /// <summary>Searches selected document scopes within the configured hit budget.</summary>
    internal static WordsSearchResult Run(WordsSession session, WordsSearchRequest request)
    {
        LicenseState state = session.Outputs.License;
        using LoadedDocument loaded = session.Loader.Open(request.Input, request.Password);
        var index = new DocumentBlockIndex(loaded.Document, loaded.Evaluation);
        SearchQuery query = request.Query;
        SearchHits<WordsSearchHit> hits = query.Collect<WordsSearchHit>();
        IEnumerable<(Node Node, string Scope)> units = WordsStories.In(loaded.Document, query.Scope ?? WordsTextScopes.Body)
            .SelectMany(static story => WordsStories.Units(story.Story).Select(unit => (unit, story.Scope)));
        foreach ((Node node, string scope) in units)
        {
            string text = WordsText.Of(node);
            if (query.Text.IsMatch(text) && !hits.Offer(() => Hit(index, node, scope, text)))
            {
                break;
            }
        }

        return new WordsSearchResult
        {
            Source = InfoProjection.Source(request.Input, loaded),
            Pattern = query.Text.Pattern,
            Hits = hits.Hits,
            Window = hits.Window(),
            License = EnvelopeParts.License(state),
            Warnings = InputWarnings(loaded),
        };
    }

    private static WordsSearchHit Hit(DocumentBlockIndex index, Node node, string scope, string text)
    {
        (string Location, string Kind)? place = scope == WordsTextScopes.HeadersFooters && node.GetAncestor(NodeType.HeaderFooter) is HeaderFooter headerFooter
            ? WordsStories.PlaceOf(headerFooter)
            : null;
        return new()
        {
            Block = index.FindBlock(node),
            Section = WordsStories.SectionOf(node),
            Scope = scope,
            Location = place?.Location,
            Kind = place?.Kind,
            Snippet = Truncate(text, 300),
        };
    }
}
