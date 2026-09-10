using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Ports;

/// <summary>
/// The engine's font environment — an engine-wide, cross-product concern behind
/// the root <c>aspose-cli fonts</c> command, kept off the per-document workbook port
/// so a second product shares it rather than re-declaring it. The signature is
/// product-neutral; each engine implements it for its own document kind.
/// </summary>
public interface IFontEnvironment
{
    /// <summary>Reports the engine's font environment: the sources it scans and the default fallback.</summary>
    FontListResult ListFonts();

    /// <summary>Reports whether a document's fonts are available here, and what they substitute to.</summary>
    FontCheckResult CheckFonts(string filePath, FontCheckRequest request);
}

/// <summary>Options of <c>aspose-cli fonts check</c>.</summary>
public sealed record FontCheckRequest
{
    /// <summary>Password for an encrypted workbook.</summary>
    public string? Password { get; init; }

    /// <summary>Optional explicit font roots used by the visual command.</summary>
    public Rendering.FontSearchProfile? FontProfile { get; init; }
}
