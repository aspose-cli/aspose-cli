using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using static Aspose.Cli.Product.Words.Engine.WordsEngineSupport;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>Owns document metadata and bounded structural reading.</summary>
internal sealed class WordsReadService
{
    private readonly ILicenseGate _licenseGate;
    private readonly WordsDocumentLoader _loader;

    internal WordsReadService(
        ILicenseGate licenseGate,
        WordsDocumentLoader loader)
    {
        _licenseGate = licenseGate ?? throw new ArgumentNullException(nameof(licenseGate));
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
    }

    /// <summary>Returns structural metadata for a document.</summary>
    internal DocumentInfoResult GetInfo(string filePath, DocumentInfoRequest request)
    {
        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedDocument loaded = _loader.Open(filePath, request.Password);
        DocumentInfoResult result = InfoProjection.Project(loaded, filePath, request);
        return result with
        {
            License = EnvelopeParts.License(state),
            Warnings = EnvelopeParts.CombineWarnings(InputWarnings(loaded), result.Warnings),
        };
    }

    /// <summary>Reads a bounded structural projection of a document.</summary>
    internal DocumentReadResult Read(string filePath, DocumentReadRequest request)
    {
        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedDocument loaded = _loader.Open(filePath, request.Password);
        return ReadProjection.Project(loaded, filePath, request) with
        {
            License = EnvelopeParts.License(state),
            Warnings = InputWarnings(loaded),
        };
    }
}
