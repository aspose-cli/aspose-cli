using System.Text;
using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>One <c>words convert</c>.</summary>
public sealed record WordsConvertRequest
{
    /// <summary>The document to convert.</summary>
    public required string Input { get; init; }

    /// <summary>Where the converted document goes, in its format.</summary>
    public required ResolvedOutput Output { get; init; }

    /// <summary>The pages a fixed-page output holds, or null for every page.</summary>
    public PageRange? Pages { get; init; }

    /// <summary>The password that opens the input.</summary>
    public Secret? Password { get; init; }

    /// <summary>The password the output opens with, or null.</summary>
    public Secret? EncryptPassword { get; init; }

    /// <summary>How a txt or md input is read, every option stated; null for any other input.</summary>
    public WordsTextInput? TextInput { get; init; }
}

/// <summary>
/// How a plain text or Markdown input is read: the options the engine would otherwise guess,
/// each stated. Markdown takes the encoding only.
/// </summary>
/// <param name="Encoding">LoadOptions.Encoding.</param>
/// <param name="AutoNumberingDetection">TxtLoadOptions.AutoNumberingDetection of a txt input.</param>
/// <param name="DetectNumberingWithWhitespaces">TxtLoadOptions.DetectNumberingWithWhitespaces of a txt input.</param>
/// <param name="DetectHyperlinks">TxtLoadOptions.DetectHyperlinks of a txt input.</param>
/// <param name="LeadingSpaces">TxtLoadOptions.LeadingSpacesOptions of a txt input, by its member name.</param>
public sealed record WordsTextInput(
    Encoding Encoding,
    bool? AutoNumberingDetection = null,
    bool? DetectNumberingWithWhitespaces = null,
    bool? DetectHyperlinks = null,
    string? LeadingSpaces = null);
