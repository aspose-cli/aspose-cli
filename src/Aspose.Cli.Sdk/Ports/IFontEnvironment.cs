using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Sdk.Ports;

/// <summary>
/// The engine's font environment — an engine-wide, cross-product concern behind
/// the root <c>aspose-cli fonts</c> command, kept off the per-document port
/// so a second product shares it rather than re-declaring it. The signature is
/// product-neutral; each engine implements it for its own document kind.
/// </summary>
public interface IFontEnvironment
{
    /// <summary>Reports the engine's font environment: the sources it scans and the default fallback.</summary>
    FontListResult ListFonts();

    /// <summary>Reports whether a document's fonts are available here, and what they substitute to.</summary>
    FontCheckResult CheckFonts(string filePath, FontCheckRequest request);

    /// <summary>
    /// Makes the profile's directories available, in addition to the system
    /// fonts, to every render, layout and font check of this engine until the
    /// returned scope is disposed. Implementations enter through
    /// <see cref="FontScope.Enter"/>, so scopes never overlap within a process
    /// and each one restores the engine's previous fonts when it ends.
    /// </summary>
    IDisposable UseFonts(FontSearchProfile profile);
}

/// <summary>Options of <c>aspose-cli fonts check</c>.</summary>
public sealed record FontCheckRequest
{
    /// <summary>Password for an encrypted document.</summary>
    public string? Password { get; init; }
}
