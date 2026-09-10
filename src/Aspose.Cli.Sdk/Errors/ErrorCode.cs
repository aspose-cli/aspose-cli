namespace Aspose.Cli.Sdk.Errors;

/// <summary>
/// A machine-readable error identifier and the exit code category it maps to.
/// </summary>
/// <param name="Name">SCREAMING_SNAKE_CASE identifier, e.g. <c>CELLS_SHEET_NOT_FOUND</c>.</param>
/// <param name="ExitCode">The process exit code this error maps to.</param>
public sealed record ErrorCode(string Name, ExitCode ExitCode)
{
    public override string ToString() => Name;
}
