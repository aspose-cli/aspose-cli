namespace Aspose.Cli.Sdk.IO.Output;

/// <summary>
/// A file the command line named as the value of one of the command's input file options, as
/// <c>StandardInvocation.Source</c> resolves it; a result reports it with
/// <c>FileObjects.Sources</c>.
/// </summary>
/// <param name="Option">The option that named the file, e.g. <c>--template</c>.</param>
/// <param name="Path">The resolved file.</param>
public sealed record SourceFile(string Option, string Path)
{
    /// <summary>
    /// Where the option stands on the command line, counted in tokens; sources are reported in
    /// this order, and files of the same option in the order it lists them.
    /// </summary>
    public int Position { get; init; }
}
