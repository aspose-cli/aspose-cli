using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Rendering;

/// <summary>
/// The engine's font environment — an engine-wide, cross-product concern behind
/// the root <c>aspose-cli fonts</c> command, shared by every
/// product rather than re-declared by each. The signature is
/// product-neutral; each engine implements it for its own document kind.
/// </summary>
public interface IFontEnvironment
{
    /// <summary>Reports the engine's font environment: the sources it scans and the default fallback.</summary>
    EngineFonts ListFonts();

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

/// <summary>An engine's font environment, which <c>fonts list</c> reports for its product.</summary>
public sealed record EngineFonts
{
    /// <summary>The font substituted for any unavailable font, or null when the engine has no configured default.</summary>
    public string? DefaultFont { get; init; }

    /// <summary>The font sources the engine scans, in configured order; empty for the platform defaults.</summary>
    public required IReadOnlyList<FontSource> Sources { get; init; }
}

/// <summary>Options of <c>aspose-cli fonts check</c>.</summary>
public sealed record FontCheckRequest
{
    /// <summary>Password for an encrypted document.</summary>
    public Secret? Password { get; init; }
}
