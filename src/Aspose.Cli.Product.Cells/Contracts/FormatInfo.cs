namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>A target format the CLI can produce.</summary>
/// <param name="Id">Canonical id used on the command line, e.g. <c>xlsx</c>.</param>
/// <param name="Extension">File extension including the dot, e.g. <c>.xlsx</c>.</param>
/// <param name="Aliases">Accepted alternative spellings, e.g. <c>markdown</c> for <c>md</c>.</param>
public sealed record FormatInfo(string Id, string Extension, params string[] Aliases);
