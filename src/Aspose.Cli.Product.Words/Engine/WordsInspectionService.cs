using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Text;
using Aspose.Words;
using Aspose.Words.Comparing;
using Aspose.Words.Saving;
using static Aspose.Cli.Product.Words.Engine.WordsEngineSupport;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>Owns comparison and bounded content search.</summary>
internal sealed class WordsInspectionService
{
    // The revision samples a comparison returns; the revision counts always cover every revision.
    private const int SampleLimit = 50;

    private readonly ILicenseGate _licenseGate;
    private readonly SafeFileWriter _writer;
    private readonly WordsDocumentLoader _loader;

    internal WordsInspectionService(
        ILicenseGate licenseGate,
        SafeFileWriter writer,
        WordsDocumentLoader loader)
    {
        _licenseGate = licenseGate ?? throw new ArgumentNullException(nameof(licenseGate));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
    }

    /// <summary>Compares two documents and optionally writes a reviewed copy.</summary>
    internal WordsCompareResult Compare(string leftPath, string rightPath, WordsCompareRequest request)
    {
        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedDocument leftLoaded = _loader.Open(leftPath, request.LeftPassword);
        using LoadedDocument rightLoaded = _loader.Open(rightPath, request.RightPassword);
        if (leftLoaded.Document.Revisions.Count > 0 || rightLoaded.Document.Revisions.Count > 0)
        {
            throw new CliException(
                WordsDiagnostics.DocumentHasRevisions,
                "Words comparison requires both source documents to have no existing revisions.",
                hint: "List the revisions with 'aspose-cli words inspect <file> --detail revisions'. "
                    + "Accept or reject revisions explicitly in copies of both documents, then compare again.");
        }

        Document compared = leftLoaded.Document.Clone();
        compared.Compare(rightLoaded.Document, "Aspose CLI", DateTime.Now, new CompareOptions
        {
            IgnoreFormatting = request.IgnoreFormatting,
            Granularity = Granularity.WordLevel,
        });
        Revision[] revisions = compared.Revisions.Cast<Revision>().ToArray();
        OutputInfo? output = null;
        if (request.OutputPath is not null)
        {
            string format = WordsFormats.ForOutput(request.OutputPath, leftLoaded.FormatId);
            SaveOptions options = WordsSavePipeline.Options(format);
            long size = _writer.Write(request.OutputPath, request.Overwrite, temp => compared.Save(temp, options));
            output = BuildOutput(request.OutputPath, format, size);
        }

        return new WordsCompareResult
        {
            Left = InfoProjection.Source(leftPath, leftLoaded),
            Right = InfoProjection.Source(rightPath, rightLoaded),
            Identical = revisions.Length == 0,
            Revisions = new RevisionCounts
            {
                Insertions = revisions.Count(static r => r.RevisionType == RevisionType.Insertion),
                Deletions = revisions.Count(static r => r.RevisionType == RevisionType.Deletion),
                FormatChanges = revisions.Count(static r => r.RevisionType == RevisionType.FormatChange),
                Moves = revisions.Count(static r => r.RevisionType == RevisionType.Moving),
            },
            Samples = revisions.Take(SampleLimit).Select(static revision => new RevisionSample
            {
                Type = revision.RevisionType.ToString(),
                Text = Truncate(WordsText.Clean(revision.ParentNode?.GetText() ?? string.Empty), 300),
            }).ToArray(),
            Output = output,
            License = EnvelopeParts.License(state),
            Warnings = EnvelopeParts.CombineWarnings(
                CompareWarnings(state, leftLoaded, rightLoaded, output is not null),
                revisions.Length > SampleLimit
                    ? [EnvelopeParts.ListTruncated("samples", SampleLimit, revisions.Length, "Write the redline with --out to review every revision.")]
                    : null),
        };
    }

    /// <summary>Searches selected document scopes within the configured hit budget.</summary>
    internal WordsSearchResult Search(string filePath, WordsSearchRequest request)
    {
        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedDocument loaded = _loader.Open(filePath, request.Password);
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
            Source = InfoProjection.Source(filePath, loaded),
            Pattern = query.Text.Pattern,
            Hits = hits.Hits,
            Window = hits.Window(),
            License = EnvelopeParts.License(state),
            Warnings = InputWarnings(loaded),
        };
    }

    private static WordsSearchHit Hit(DocumentBlockIndex index, Node node, string scope, string text)
    {
        int block = index.FindBlock(node) ?? 0;
        return new WordsSearchHit
        {
            Block = block,
            Section = block == 0 ? 0 : index.Get(block).Section,
            Scope = scope,
            Snippet = Truncate(text, 300),
        };
    }
}
