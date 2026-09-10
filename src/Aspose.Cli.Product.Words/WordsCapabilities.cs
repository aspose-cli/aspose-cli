using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Capabilities;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words;

internal sealed class WordsDocumentCapabilities(
    ProductBinding<IDocumentEngine> binding)
    : IDocumentToPdfConverter, IDocumentPageRenderer
{
    public DocumentCapabilityOutput ConvertToPdf(
        string inputPath,
        string outputPath,
        bool overwrite)
    {
        Aspose.Cli.Product.Words.Contracts.WordsConvertResult result =
            binding.Port.Convert(inputPath, new WordsConvertRequest
            {
                TargetFormatId = "pdf",
                OutputPath = outputPath,
                Overwrite = overwrite,
            });
        return new DocumentCapabilityOutput
        {
            Output = result.Output,
            License = result.License,
            Warnings = result.Warnings,
        };
    }

    public DocumentCapabilityOutput RenderFirstPageToPng(
        string inputPath,
        string outputPath,
        bool overwrite,
        int dpi)
    {
        Aspose.Cli.Product.Words.Contracts.WordsRenderResult result =
            binding.Port.Render(inputPath, new WordsRenderRequest
            {
                TargetFormatId = "png",
                OutputPath = outputPath,
                Overwrite = overwrite,
                Dpi = dpi,
            });
        return new DocumentCapabilityOutput
        {
            Output = result.Outputs.Single().Output,
            License = result.License,
            Warnings = result.Warnings,
        };
    }
}
