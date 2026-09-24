using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

internal static class WordsErrors
{
    public static CliException BlockNotFound(int block, int available) => new(
        WordsDiagnostics.BlockNotFound,
        $"Block {block} does not exist; the document has {available} block(s).",
        hint: "Run 'aspose-cli words query blocks' and use a returned block number.",
        details: new JsonObject { ["block"] = block, ["of"] = available });

    public static CliException BlockRangeNotFound(PageRange range, int available) => new(
        WordsDiagnostics.BlockNotFound,
        $"Block range '{range.Text}' goes past the document's {available} block(s).",
        hint: "Run 'aspose-cli words query blocks' and use a returned block number.",
        details: new JsonObject { ["range"] = range.Text, ["of"] = available });

    public static CliException SectionNotFound(int section, int available) => new(
        WordsDiagnostics.SectionNotFound,
        $"Section {section} does not exist; the document has {available} section(s).",
        hint: "Run 'aspose-cli words inspect --detail sections' and use a returned section.",
        details: new JsonObject { ["section"] = section, ["available"] = available });
}
