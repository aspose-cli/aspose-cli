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
        // Every part keeps the input's password, as a converted copy does.
        using var writer = new WordsSplitWriter(session.Outputs, request.Output, request.Input,
            loaded.Format.IsEncrypted ? request.Password : null);
        switch (request.Plan.By)
        {
            case SplitBy.Sections:
                writer.Number([.. Enumerable.Range(1, loaded.Document.Sections.Count)]);
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

                    int section = index + 1;
                    writer.Stage(part, section, file => new WordsPartOutput(file) { Section = section });
                }

                break;
            case SplitBy.Pages:
                IReadOnlyList<IReadOnlyList<int>> groups = request.Plan.PageGroups(loaded.Document.PageCount);
                IReadOnlyList<int> numbers = request.Plan.PartNumbers(groups);
                writer.Number(numbers);
                foreach ((IReadOnlyList<int> pages, int number) in groups.Zip(numbers))
                {
                    string held = PageRange.Describe(pages)!;
                    writer.Stage(ExtractPages(loaded.Document, pages), number, file => new WordsPartOutput(file) { Pages = held });
                }

                break;
            case SplitBy.Heading1:
                SplitByHeading(loaded, writer);
                break;
            default:
                throw new InvalidOperationException($"words split does not divide by {request.Plan.By}.");
        }

        IReadOnlyList<WordsPartOutput> outputs = writer.Commit();
        return new WordsSplitResult
        {
            Input = InfoProjection.Source(request.Input, loaded),
            Outputs = outputs,
            License = EnvelopeParts.License(state),
            Warnings = request.Plan.By == SplitBy.Pages
                ? EnvelopeParts.CombineWarnings(WrittenWarnings(loaded, "docx"), [new Warning(WordsDiagnostics.LayoutMayDiffer, "Page extraction can slightly reflow complex layouts.") { Hint = "Visually inspect the split pages." }])
                : WrittenWarnings(loaded, "docx"),
        };
    }

    /// <summary>
    /// The pages of one part: a run of consecutive pages extracted at once, and other pages
    /// appended run by run in their order.
    /// </summary>
    private static Document ExtractPages(Document document, IReadOnlyList<int> pages)
    {
        Document? part = null;
        int start = 0;
        for (int index = 1; index <= pages.Count; index++)
        {
            if (index < pages.Count && pages[index] == pages[index - 1] + 1)
            {
                continue;
            }

            Document run = document.ExtractPages(pages[start] - 1, index - start);
            if (part is null)
            {
                part = run;
            }
            else
            {
                part.AppendDocument(run, ImportFormatMode.KeepSourceFormatting);
            }

            start = index;
        }

        return part!;
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
                "Use --by sections or --by pages, or apply Heading 1 styles first.");
        }

        if (starts[0] > 1)
        {
            starts.Insert(0, 1);
        }

        writer.Number([.. Enumerable.Range(1, starts.Count)]);

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
            string blocks = PageRange.Describe(Enumerable.Range(start, end - start + 1))!;
            writer.Stage(part, group + 1, file => new WordsPartOutput(file) { Blocks = blocks });
            part.Cleanup();
        }
    }
}
