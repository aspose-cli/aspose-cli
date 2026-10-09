using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Words.Saving;
using static Aspose.Cli.Product.Words.Engine.WordsEngineSupport;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>Serves <c>words convert</c>: one document saved in another format.</summary>
internal static class WordsConvert
{
    /// <summary>Converts a document using the selected save pipeline.</summary>
    internal static WordsConvertResult Run(WordsSession session, WordsConvertRequest request)
    {
        LicenseState state = session.Outputs.License;
        using LoadedDocument loaded = session.Loader.Open(request.Input, request.Password);
        IReadOnlyList<int>? pages = request.Pages?.Resolve(loaded.Document.PageCount);
        SaveOptions options = WordsSavePipeline.Options(request.Output.Format.Id, request.EncryptPassword, pages);
        WordsSavePipeline.RemoveMacrosUnlessKept(loaded.Document, request.Output.Format.Id);
        long size = session.Outputs.Write(request.Output.Path, request.Output.Overwrite, loaded.Document, temp =>
        {
            try { loaded.Document.Save(temp, options); }
            finally { loaded.Resources.ThrowIfFailed(); }
        });
        var warnings = WrittenWarnings(loaded, request.Output.Format.Id);
        return new WordsConvertResult
        {
            Input = InfoProjection.Source(request.Input, loaded),
            Output = BuildOutput(request.Output.Path, request.Output.Format.Id, size),
            Pages = request.Pages?.Text,
            License = EnvelopeParts.License(state),
            Warnings = warnings,
        };
    }
}
