using Aspose.Cli.Sdk.Capabilities;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>
/// Canonical typed slots shared by independently packaged products. Slot
/// identity is static and compile-time; display names are diagnostic only.
/// </summary>
public static class StandardProductCapabilities
{
    /// <summary>Local document-to-PDF conversion.</summary>
    public static ProductCapability<IDocumentToPdfConverter> DocumentToPdf
    {
        get;
    } = new("document-to-pdf");

    /// <summary>First-page local document rendering.</summary>
    public static ProductCapability<IDocumentPageRenderer> DocumentPageRenderer
    {
        get;
    } = new("document-page-renderer");
}
