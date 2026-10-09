using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Words;
using static Aspose.Cli.Product.Words.Engine.WordsEngineSupport;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>Serves <c>words split</c>: one document split into an atomically committed set of parts.</summary>
internal static class WordsSplit
{
    /// <summary>Splits a document and commits all outputs atomically.</summary>
    internal static WordsSplitResult Run(WordsSession session, WordsSplitRequest request)
    {
        LicenseState state = session.Outputs.License;
        using LoadedDocument loaded = session.Loader.Open(request.Input, request.Password);
        using var writer = new WordsSplitWriter(session.Outputs, request.Output.Path, request.Output.Overwrite);
        if (request.By == "section")
        {
            for (int index = 0; index < loaded.Document.Sections.Count; index++)
            {
                Document part = loaded.Document.Clone();
                for (int remove = part.Sections.Count - 1; remove >= 0; remove--)
                {
                    if (remove != index)
                    {
                        part.Sections.RemoveAt(remove);
                    }
                }

                writer.Stage(part, index + 1, $"section-{index + 1}");
            }
        }
        else if (request.By == "pages")
        {
            IReadOnlyList<int> pages = request.Pages?.Resolve(loaded.Document.PageCount)
                ?? Enumerable.Range(1, loaded.Document.PageCount).ToArray();
            int outputIndex = 0;
            foreach (int page in pages)
            {
                Document part = loaded.Document.ExtractPages(page - 1, 1);
                writer.Stage(part, ++outputIndex, $"page-{page}");
            }
        }
        else if (request.By == "heading1")
        {
            SplitByHeading(loaded, writer);
        }
        else
        {
            throw CliErrors.OptionInvalid("--by", $"unknown split mode '{request.By}'", "Use section, heading1 or pages.");
        }

        IReadOnlyList<SplitOutput> outputs = writer.Commit();
        return new WordsSplitResult
        {
            Input = InfoProjection.Source(request.Input, loaded),
            Outputs = outputs,
            License = EnvelopeParts.License(state),
            Warnings = request.By == "pages"
                ? EnvelopeParts.CombineWarnings(WrittenWarnings(loaded, "docx"), [new Warning { Code = WordsDiagnostics.LayoutMayDiffer, Message = "Page extraction can slightly reflow complex layouts.", Hint = "Visually inspect the split pages." }])
                : WrittenWarnings(loaded, "docx"),
        };
    }

    /// <summary>
    /// Writes one part per Heading 1, and a leading part for the blocks before the first one
    /// (a title page or table of contents), so no block is left out. Each part is a copy of
    /// the whole document with the other blocks removed, so it keeps its sections' page
    /// setup, headers, footers and styles.
    /// </summary>
    private static void SplitByHeading(LoadedDocument loaded, WordsSplitWriter writer)
    {
        var index = new DocumentBlockIndex(loaded.Document, loaded.Evaluation);
        List<int> starts = index.Entries.Where(static entry => entry.Node is Paragraph p && InfoProjection.HeadingLevel(p) == 1)
            .Select(static entry => entry.Index).ToList();
        if (starts.Count == 0)
        {
            throw CliErrors.NotFoundAt(WordsDiagnostics.AnchorNotFound, "Heading 1 paragraph", "1", 0,
                "Use --by section or --by pages, or apply Heading 1 styles first.");
        }

        if (starts[0] > 1)
        {
            starts.Insert(0, 1);
        }

        for (int group = 0; group < starts.Count; group++)
        {
            int start = starts[group];
            int end = group + 1 < starts.Count ? starts[group + 1] - 1 : index.Count;
            Document part = loaded.Document.Clone();
            var partIndex = new DocumentBlockIndex(part, loaded.Evaluation);
            foreach (BlockEntry entry in partIndex.Entries.Where(entry => entry.Index < start || entry.Index > end))
            {
                DocumentBlockIndex.Remove(entry.Node);
            }

            foreach (Section section in part.Sections.Cast<Section>().ToArray())
            {
                if (part.Sections.Count > 1 && !section.Body.HasChildNodes)
                {
                    section.Remove();
                }
            }

            part.EnsureMinimum();
            writer.Stage(part, group + 1, $"blocks-{start}-{end}");
            part.Cleanup();
        }
    }
}
