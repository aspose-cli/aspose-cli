using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Words;
using Aspose.Words.Saving;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>Serves <c>words create</c>: a document from one content source, optionally inside a template.</summary>
internal static class WordsCreate
{
    /// <summary>Creates a document from a bounded source or blank template.</summary>
    internal static WordsCreateResult Run(WordsSession session, WordsCreateRequest request)
    {
        LicenseState state = session.Outputs.License;
        using CreatedDocument created = Build(session, request);
        string formatId = request.Output.Format.Id;
        // A new document has no input whose password it could keep.
        OutputProtection protection = OutputProtection.For(request.Output.Format, request.EncryptPassword, new InputProtection(Encrypted: false), inputPassword: null);
        SaveOptions options = WordsSavePipeline.Options(formatId, protection.Password);
        WordsSavePipeline.RemoveMacrosUnlessKept(created.Document, formatId);
        PublishedFile written = session.Outputs.Write(request.Output.Path, request.Output.Format, request.Output.Overwrite, created.Document, temp => created.Save(temp, options),
            encrypted: protection.Encrypted);

        return new WordsCreateResult
        {
            Output = FileObjects.Output(written, request.Output.Format, encrypted: protection.Encrypted),
            Inputs = created.Inputs,
            License = EnvelopeParts.License(state),
            Warnings = CreationWarnings(created, formatId),
        };
    }

    // A path as a command line names it: quoted when it holds a space.
    private static string Quoted(string path) => path.Contains(' ', StringComparison.Ordinal) ? $"\"{path}\"" : path;

    /// <summary>
    /// A template, which opens without a password: an encrypted one is refused with the convert
    /// that writes an unencrypted copy of it, Flat OPC, which carries no password but keeps
    /// everything a template supplies, or else the advice to ask for an unencrypted copy.
    /// </summary>
    private static LoadedDocument OpenTemplate(WordsSession session, string path)
    {
        string copy = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, Path.GetFileNameWithoutExtension(path) + ".unencrypted.xml");
        return session.Loader.Open(path, PasswordSource.None(
            "'words create' cannot open an encrypted template. With its password, write an unencrypted copy: "
            + $"'aspose-cli words convert {Quoted(path)} --to flatopc --out {Quoted(copy)} --password-env <password-env>', "
            + "then pass that copy to --template. Without the password, ask the user for an unencrypted copy of the template."));
    }

    /// <summary>
    /// A template (the built-in A4 design unless one is given) supplies styles, page setup,
    /// headers and footers; Markdown or text supplies the body. Content takes the
    /// destination's styles, so the template alone owns the look of the created document.
    /// </summary>
    private static CreatedDocument Build(WordsSession session, WordsCreateRequest request)
    {
        var sources = new List<LoadedDocument>();
        try
        {
            LoadedDocument? template = request.Template is null ? null : OpenTemplate(session, request.Template.Path);
            if (template is not null)
            {
                sources.Add(template);
            }

            Document document = template?.Document ?? WordsDocumentLoader.OpenDefaultTemplate();
            LoadedDocument? markdown = null;
            if (request.Markdown is not null)
            {
                markdown = session.Loader.Open(request.Markdown.Path, null);
                sources.Add(markdown);
                ReplaceBody(document, WordsMarkdownImport.Blocks(document, markdown.Document));
            }
            else if (request.Text is not null)
            {
                ReplaceBody(document, session.Budgets.Inputs.ReadTextFile(request.Text.Path)
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
            IReadOnlyList<SourceInput> inputs = FileObjects.Sources(
                (request.Template, template?.FormatId ?? string.Empty, template?.Format.IsEncrypted ?? false),
                (request.Markdown, markdown?.FormatId ?? string.Empty, markdown?.Format.IsEncrypted ?? false),
                (request.Text, "txt", false));
            return new CreatedDocument(document, template, sources, inputs);
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
        if (LocalDocumentResourceLoader.OmissionWarning(created.OmittedResources) is { } omitted)
        {
            extra.Add(omitted);
        }

        // The created document is the template's, so its save loses and gains what saving the template would.
        if (created.Template is { } template)
        {
            extra.AddRange(WordsEngineSupport.SaveWarnings(template, format));
        }

        if (created.WasSigned)
        {
            extra.Add(new Warning(WarningCodes.SignatureInvalidated, "Creating from the signed source invalidates its digital signature.") { Hint = "Sign the produced document after review." });
        }

        return EnvelopeParts.CombineWarnings(extra);
    }

    private sealed record CreatedDocument(
        Document Document,
        LoadedDocument? Template,
        IReadOnlyList<LoadedDocument> Sources,
        IReadOnlyList<SourceInput> Inputs) : IDisposable
    {
        internal IReadOnlyList<OmittedResource> OmittedResources => [.. Sources.SelectMany(static source => source.OmittedResources)];
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
