namespace Aspose.Cli.TestKit.Scenarios;

/// <summary>
/// The inputs a product opens only with <c>convert</c>, the formats its formats document marks
/// <c>convert-input</c>, with the options convert reads them with: its read refuses them as
/// FORMAT_UNSUPPORTED, so a check that reopens such a file converts it to the product's own
/// format instead.
/// </summary>
public static class ConvertOnlyInputs
{
    private static readonly string[] Txt =
        ["--encoding", "utf-8", "--auto-numbering-detection", "false", "--detect-numbering-with-whitespaces", "false", "--detect-hyperlinks", "false", "--leading-spaces", "Preserve"];

    /// <summary>The options a Words convert states for its text inputs, by format id; any other input takes none.</summary>
    private static readonly Dictionary<(string Product, string Format), string[]> TextOptions = new()
    {
        [("words", "txt")] = Txt,
        [("words", "md")] = ["--encoding", "utf-8"],
    };

    /// <summary>The options convert reads <paramref name="path"/> with when only convert opens it in <paramref name="product"/>, or null.</summary>
    public static string[]? Options(string product, string path)
    {
        string extension = Path.GetExtension(path);
        string? format = CliCatalog.Current.FormatNodes(product)
            .Where(static node => node["uses"]!.AsArray().Any(static use => use!.GetValue<string>() == "convert-input"))
            .FirstOrDefault(node => node["extensions"]!.AsArray().Any(item => string.Equals(item!.GetValue<string>(), extension, StringComparison.OrdinalIgnoreCase)))?["id"]!.GetValue<string>();
        return format is null ? null : TextOptions.GetValueOrDefault((product, format)) ?? [];
    }
}
