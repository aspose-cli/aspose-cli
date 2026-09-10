using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Capabilities;

/// <summary>Neutral output of a cross-product document conversion.</summary>
public sealed record DocumentCapabilityOutput
{
    /// <summary>Produced file.</summary>
    public required OutputInfo Output { get; init; }

    /// <summary>Provider license state.</summary>
    public LicenseInfo? License { get; init; }

    /// <summary>Visible provider warnings.</summary>
    public IReadOnlyList<Warning>? Warnings { get; init; }
}

/// <summary>Narrow capability for converting a trusted local document to PDF.</summary>
public interface IDocumentToPdfConverter
{
    /// <summary>Converts one local document to PDF without remote resources.</summary>
    DocumentCapabilityOutput ConvertToPdf(
        string inputPath,
        string outputPath,
        bool overwrite);
}

/// <summary>Narrow capability for rendering the first document page to PNG.</summary>
public interface IDocumentPageRenderer
{
    /// <summary>Renders the first page without remote resources.</summary>
    DocumentCapabilityOutput RenderFirstPageToPng(
        string inputPath,
        string outputPath,
        bool overwrite,
        int dpi);
}
