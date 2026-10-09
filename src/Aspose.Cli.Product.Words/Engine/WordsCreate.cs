using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Words;
using Aspose.Words.Saving;
using static Aspose.Cli.Product.Words.Engine.WordsEngineSupport;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>Serves <c>words create</c>: a document from one content source, optionally inside a template.</summary>
internal static class WordsCreate
{
    /// <summary>Creates a document from a bounded source or blank template.</summary>
    internal static WordsCreateResult Run(WordsSession session, NewDocumentRequest request)
    {
        LicenseState state = session.Outputs.License;
        using CreatedDocument created = Build(session, request);
        string formatId = request.Output.Format.Id;
        SaveOptions options = WordsSavePipeline.Options(formatId, request.EncryptPassword);
        WordsSavePipeline.RemoveMacrosUnlessKept(created.Document, formatId);
        long size = session.Outputs.Write(request.Output.Path, request.Output.Overwrite, created.Document, temp => created.Save(temp, options));

        return new WordsCreateResult
        {
            Output = BuildOutput(request.Output.Path, formatId, size),
            License = EnvelopeParts.License(state),
            Warnings = CreationWarnings(created, formatId),
        };
    }

    /// <summary>
    /// A template (the built-in A4 design unless one is given) supplies styles, page setup,
    /// headers and footers; Markdown or text supplies the body. Content takes the
    /// destination's styles, so the template alone owns the look of the created document.
    /// </summary>
    private static CreatedDocument Build(WordsSession session, NewDocumentRequest request)
    {
        var sources = new List<LoadedDocument>();
        try
        {
            LoadedDocument? template = request.TemplatePath is null ? null : session.Loader.Open(request.TemplatePath, null);
            if (template is not null)
            {
                sources.Add(template);
            }

            Document document = template?.Document ?? WordsDocumentLoader.OpenDefaultTemplate();
            if (request.MarkdownPath is not null)
            {
                LoadedDocument markdown = session.Loader.Open(request.MarkdownPath, null);
                sources.Add(markdown);
                ReplaceBody(document, WordsMarkdownImport.Blocks(document, markdown.Document));
            }
            else if (request.TextPath is not null)
            {
                ReplaceBody(document, session.Budgets.Inputs.ReadTextFile(request.TextPath)
                    .Split(["\r\n", "\n", "\r"], StringSplitOptions.None)
                    .Select(line =>
                    {
                        var paragraph = new Paragraph(document);
                        paragraph.AppendChild(new Run(document, line));
                        return (Node)paragraph;
                    })
                    .ToArray());
            }

            ApplyTitle(document, request.Title);
            return new CreatedDocument(document, template, sources);
        }
        catch
        {
            sources.ForEach(static source => source.Dispose());
            throw;
        }
    }

    private static void ReplaceBody(Document destination, IReadOnlyList<Node> blocks)
    {
        while (destination.Sections.Count > 1)
        {
            destination.LastSection.Remove();
        }

        Body body = destination.FirstSection.Body;
        body.RemoveAllChildren();
        foreach (Node block in blocks)
        {
            body.AppendChild(block);
        }

        destination.FirstSection.EnsureMinimum();
    }

    private static void ApplyTitle(Document document, string? title)
    {
        if (title is not null)
        {
            document.BuiltInDocumentProperties.Title = title;
        }
    }

    private static IReadOnlyList<Warning>? CreationWarnings(
        CreatedDocument created,
        string format)
    {
        var extra = new List<Warning>();
        if (LocalDocumentResourceLoader.OmissionWarning(created.RemoteResourcesBlocked) is { } omitted)
        {
            extra.Add(omitted);
        }

        if (created.Sources.Any(static source => source.EvaluationInputTruncated))
        {
            extra.Add(EvaluationTruncated);
        }

        if (created.HasMacros && !WordsFormats.KeepsMacros(format))
        {
            extra.Add(new Warning { Code = WordsDiagnostics.MacrosDropped, Message = "The source template contains macros which the target format does not preserve.", Hint = "Create a docm or dotm output to preserve macros." });
        }

        if (created.WasSigned)
        {
            extra.Add(new Warning { Code = WarningCodes.SignatureInvalidated, Message = "Creating from the signed source invalidates its digital signature.", Hint = "Sign the produced document after review." });
        }

        return EnvelopeParts.CombineWarnings(extra);
    }

    private sealed record CreatedDocument(
        Document Document,
        LoadedDocument? Template,
        IReadOnlyList<LoadedDocument> Sources) : IDisposable
    {
        internal int RemoteResourcesBlocked => Sources.Sum(static source => source.RemoteResourcesBlocked);
        internal bool HasMacros => Template?.Format.HasMacros ?? false;
        internal bool WasSigned => Template?.Format.HasDigitalSignature ?? false;

        internal void Save(string path, SaveOptions options)
        {
            try { Document.Save(path, options); }
            finally
            {
                foreach (LoadedDocument source in Sources) { source.Resources.ThrowIfFailed(); }
            }
        }

        public void Dispose()
        {
            if (Template is null) { Document.Cleanup(); }
            foreach (LoadedDocument source in Sources) { source.Dispose(); }
        }
    }
}
