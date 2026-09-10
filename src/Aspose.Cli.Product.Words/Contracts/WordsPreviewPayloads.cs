using Aspose.Cli.Generated;
using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Product.Words.Contracts;

public sealed record WordsPreviewHint(int Page, int? Block = null);

internal static class WordsPreviewPayloads
{
    internal const string HintSchema = "v2/words/preview-hint";

    internal static ProductPreviewPayloadContract HintContract { get; } =
        new()
        {
            Kind = ProductPreviewPayloadKinds.Hint,
            SchemaVersion = 2,
            SchemaId = HintSchema,
            MaxBytes = 1_024,
        };

    internal static ProductPreviewPayload Hint(WordsPreviewHint value) =>
        ProductPreviewPayload.Create(
            "words",
            ProductPreviewPayloadKinds.Hint,
            2,
            HintSchema,
            value,
            ProductJsonContext.Default.WordsPreviewHint);
}
