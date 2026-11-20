using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.IO.Extraction;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Tables;
using Aspose.Words;
using Aspose.Words.Drawing;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary><c>words extract</c>: the bytes of each image a document stores, ImageData.ImageBytes, as the document stores them.</summary>
internal static class WordsExtract
{
    internal static WordsExtractResult Run(WordsSession session, WordsExtractRequest request)
    {
        LicenseState state = session.Outputs.License;
        var log = new EngineWarningLog();
        using LoadedDocument loaded = session.Loader.Open(request.Input, request.Password, log);
        using ExtractionGuard guard = session.Outputs.BeginExtraction(request.Output.Path, request.Output.Overwrite, ExtractionContent.EmbeddedFiles, byteOrderMark: false);
        var outputs = new List<WordsExtractedOutput>();
        int number = 0;
        // A linked image stores no bytes: reading them would fetch the link.
        foreach (Shape shape in loaded.Document.GetChildNodes(NodeType.Shape, true).Cast<Shape>().Where(static shape => shape.HasImage && !shape.ImageData.IsLinkOnly))
        {
            ImageType type = shape.ImageData.ImageType;
            // FileFormatUtil.ImageTypeToExtension names no extension for a type the engine does not know, such as an SVG picture's.
            string extension = type is ImageType.Unknown or ImageType.NoImage ? string.Empty : FileFormatUtil.ImageTypeToExtension(type);
            PublishedFile file = guard.StageAllBytes($"image-{++number:000}{extension}", shape.ImageData.ImageBytes);
            outputs.Add(new WordsExtractedOutput(FileObjects.Companion(file)) { ImageType = type.ToString() });
        }

        guard.Commit();
        return new WordsExtractResult
        {
            Input = FileObjects.Input(request.Input, loaded.FormatId, loaded.Protection.Encrypted),
            What = request.What,
            Outputs = outputs,
            EngineWarnings = log.Warnings.Count == 0 ? null : log.Warnings,
            License = EnvelopeParts.License(state),
            Warnings = loaded.Warnings.Count == 0 ? null : loaded.Warnings,
        };
    }
}
