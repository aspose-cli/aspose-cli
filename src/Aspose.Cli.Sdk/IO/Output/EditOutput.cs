namespace Aspose.Cli.Sdk.IO.Output;

/// <summary>
/// Where an edit publishes its input, resolved by the command template before the input opens:
/// the output path, whether an existing file may be replaced, and whether the output replaces
/// the input itself (<c>--in-place</c>). Its format is the input's, which the edit decides once
/// the input is open, and then publishes a <see cref="ResolvedOutput"/>.
/// </summary>
/// <param name="Path">The output path.</param>
/// <param name="Overwrite">Whether an existing file at <paramref name="Path"/> may be replaced.</param>
/// <param name="InPlace">Whether the output replaces the edited input itself.</param>
public sealed record EditOutput(string Path, bool Overwrite = false, bool InPlace = false);
