using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.IO.Output;

/// <summary>
/// The facts of one file a publication writes, taken from its staged copy, so the target itself
/// may not exist yet when the result describes it. <see cref="FileObjects"/> turns it into the
/// <see cref="OutputInfo"/> a result reports.
/// </summary>
/// <param name="Path">The absolute target path.</param>
/// <param name="SizeBytes">The file size in bytes.</param>
public sealed record PublishedFile(string Path, long SizeBytes);
