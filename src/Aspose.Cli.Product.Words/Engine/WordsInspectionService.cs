using Aspose.Cli.Product.Words.Contracts;
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
                hint: "Accept or reject revisions explicitly in copies of both documents, then compare again.");
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
            Samples = revisions.Take(50).Select(static revision => new RevisionSample
            {
                Type = revision.RevisionType.ToString(),
                Text = Truncate(InfoProjection.Clean(revision.ParentNode?.GetText() ?? string.Empty), 300),
            }).ToArray(),
            Output = output,
            License = EnvelopeParts.License(state),
            Warnings = CompareWarnings(state, leftLoaded, rightLoaded, output is not null),
        };
    }

    /// <summary>Searches selected document scopes within the configured hit budget.</summary>
    internal WordsSearchResult Search(string filePath, WordsSearchRequest request)
    {
        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedDocument loaded = _loader.Open(filePath, request.Password);
        var index = new DocumentBlockIndex(loaded.Document, loaded.Evaluation);
        TextSearch query = TextSearch.Create(request.Pattern, request.Regex, request.CaseSensitive);
        var hits = new List<WordsSearchHit>();
        bool truncated = false;
        IEnumerable<(Node Node, string Scope)> nodes = SearchNodes(loaded.Document, request.Scope);
        foreach ((Node node, string scope) in nodes)
        {
            string text = InfoProjection.Clean(node.GetText());
            if (!query.IsMatch(text))
            {
                continue;
            }

            if (hits.Count >= request.MaxHits)
            {
                truncated = true;
                break;
            }

            int block = index.FindBlock(node) ?? 0;
            hits.Add(new WordsSearchHit
            {
                Block = block,
                Section = block == 0 ? 0 : index.Get(block).Section,
                Scope = scope,
                Snippet = Truncate(text, 300),
            });
        }

        return new WordsSearchResult
        {
            Source = InfoProjection.Source(filePath, loaded),
            Pattern = request.Pattern,
            Hits = hits,
            Truncated = truncated,
            License = EnvelopeParts.License(state),
            Warnings = InputWarnings(loaded),
        };
    }

    private static IEnumerable<(Node Node, string Scope)> SearchNodes(Document document, string scope)
    {
        if (scope is "body" or "all")
        {
            foreach (Section section in document.Sections)
            {
                foreach (Node node in section.Body.GetChildNodes(NodeType.Paragraph, true))
                {
                    // Comments and footnotes anchored in the body have their own scopes.
                    if (node.GetAncestor(NodeType.Comment) is null && node.GetAncestor(NodeType.Footnote) is null)
                    {
                        yield return (node, "body");
                    }
                }
            }
        }

        if (scope is "headers" or "all")
        {
            foreach (Section section in document.Sections)
            {
                foreach (HeaderFooter header in section.HeadersFooters)
                {
                    foreach (Node node in header.GetChildNodes(NodeType.Paragraph, true))
                    {
                        yield return (node, "headers");
                    }
                }
            }
        }

        if (scope is "footnotes" or "all")
        {
            foreach (Node node in document.GetChildNodes(NodeType.Footnote, true))
            {
                yield return (node, "footnotes");
            }
        }

        if (scope is "comments" or "all")
        {
            foreach (Node node in document.GetChildNodes(NodeType.Comment, true))
            {
                yield return (node, "comments");
            }
        }
    }
}
