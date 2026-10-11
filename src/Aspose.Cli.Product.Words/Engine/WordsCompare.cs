using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Text;
using Aspose.Words;
using Aspose.Words.Comparing;
using Aspose.Words.Saving;
using static Aspose.Cli.Product.Words.Engine.WordsEngineSupport;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>Serves <c>words compare</c>: a semantic comparison and an optional redline.</summary>
internal static class WordsCompare
{
    // The revision samples a comparison returns; the revision counts always cover every revision.
    private const int SampleLimit = 50;

    /// <summary>Compares two documents and optionally writes a reviewed copy.</summary>
    internal static WordsCompareResult Run(WordsSession session, WordsCompareRequest request)
    {
        string leftPath = request.Left;
        string rightPath = request.Right;
        LicenseState state = session.Outputs.License;
        using LoadedDocument leftLoaded = session.Loader.Open(leftPath, request.LeftPassword);
        using LoadedDocument rightLoaded = session.Loader.Open(rightPath, request.RightPassword);
        if (leftLoaded.Document.Revisions.Count > 0 || rightLoaded.Document.Revisions.Count > 0)
        {
            throw new CliException(
                WordsDiagnostics.DocumentHasRevisions,
                "Words comparison requires both source documents to have no existing revisions.",
                hint: "List the revisions with 'aspose-cli words inspect <file> --detail revisions'. "
                    + "Accept or reject revisions explicitly in copies of both documents, then compare again.");
        }

        Document compared = leftLoaded.Document.Clone();
        compared.Compare(rightLoaded.Document, request.Author ?? "Aspose CLI", DateTime.Now, new CompareOptions
        {
            IgnoreFormatting = request.IgnoreFormatting,
            Granularity = request.Granularity == "char" ? Granularity.CharLevel : Granularity.WordLevel,
        });
        Revision[] revisions = compared.Revisions.Cast<Revision>().ToArray();
        OutputInfo? output = null;
        string? format = null;
        if (request.Output is { } redline)
        {
            FormatDescriptor written = redline.Keeping(leftLoaded.FormatId);
            format = written.Id;
            // The redline is a copy of the original, so it keeps the original's password where its format can.
            Secret? password = leftLoaded.Format.IsEncrypted && written.Protectable ? request.LeftPassword : null;
            SaveOptions options = WordsSavePipeline.Options(format, password);
            WordsSavePipeline.RemoveMacrosUnlessKept(compared, format);
            PublishedFile file = session.Outputs.Write(redline.Path, redline.Overwrite, compared, temp => compared.Save(temp, options),
                encrypted: password is not null);
            output = FileObjects.Output(file, written, encrypted: password is not null);
        }

        return new WordsCompareResult
        {
            Left = InfoProjection.Source(leftPath, leftLoaded),
            Right = InfoProjection.Source(rightPath, rightLoaded),
            Identical = revisions.Length == 0,
            Revisions = new RevisionCounts
            {
                InsertionCount = revisions.Count(static r => r.RevisionType == RevisionType.Insertion),
                DeletionCount = revisions.Count(static r => r.RevisionType == RevisionType.Deletion),
                FormatChangeCount = revisions.Count(static r => r.RevisionType == RevisionType.FormatChange),
                MoveCount = revisions.Count(static r => r.RevisionType == RevisionType.Moving),
            },
            Samples = revisions.Take(SampleLimit).Select(static revision => new RevisionSample
            {
                Type = InfoProjection.RevisionTypeName(revision.RevisionType),
                Text = InfoProjection.NodeText(revision) is { } text && WordsText.Clean(text) is { Length: > 0 } clean
                    ? TextSearch.Truncate(clean, 300)
                    : null,
            }).ToArray(),
            Output = output,
            License = EnvelopeParts.License(state),
            Warnings = EnvelopeParts.CombineWarnings(
                CompareWarnings(leftLoaded, rightLoaded),
                format is not null && MacrosDropped(leftLoaded, format) is { } macros ? [macros] : null,
                revisions.Length > SampleLimit
                    ? [EnvelopeParts.ListTruncated("samples", SampleLimit, revisions.Length, "Write the redline with --out to review every revision.")]
                    : null),
        };
    }
}
