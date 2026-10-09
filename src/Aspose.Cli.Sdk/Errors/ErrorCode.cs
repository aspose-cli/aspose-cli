using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Errors;

/// <summary>
/// A machine-readable error identifier and the exit code category it maps to.
/// </summary>
/// <param name="Name">SCREAMING_SNAKE_CASE identifier, e.g. <c>SHEET_NOT_FOUND</c>.</param>
/// <param name="ExitCode">The process exit code this error maps to.</param>
public sealed record ErrorCode(string Name, ExitCode ExitCode)
{
    /// <summary>
    /// Schema id of the error's <c>details</c> object, published with the code in the
    /// diagnostic catalog.
    /// </summary>
    public string DetailsSchemaId { get; private init; } = DiagnosticDetails.CatalogId;

    /// <summary>
    /// Declares the code for a named or numbered target that the document does not contain,
    /// such as a sheet, slide or bookmark. Its errors are built only by
    /// <see cref="CliErrors.NotFound"/> and <see cref="CliErrors.NotFoundAt"/>, so every
    /// such error lists what the document does contain.
    /// </summary>
    public static ErrorCode NotFound(string name) =>
        new(name, ExitCode.ValidationError) { DetailsSchemaId = NotFoundDetails.CatalogId };

    public override string ToString() => Name;
}
