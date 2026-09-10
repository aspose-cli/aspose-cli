using Aspose.Cli.Generated;
using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Product.Cells.Contracts;

public sealed record CellsPreviewSelector(string? Sheet);

public sealed record CellsPreviewHint(string? Sheet, string? Range);

internal static class CellsPreviewPayloads
{
    internal const string SelectorSchema = "v2/cells/preview-selector";
    internal const string HintSchema = "v2/cells/preview-hint";

    internal static ProductPreviewPayloadContract SelectorContract { get; } =
        new()
        {
            Kind = ProductPreviewPayloadKinds.Selector,
            SchemaVersion = 2,
            SchemaId = SelectorSchema,
            MaxBytes = 1_024,
        };

    internal static ProductPreviewPayloadContract HintContract { get; } =
        new()
        {
            Kind = ProductPreviewPayloadKinds.Hint,
            SchemaVersion = 2,
            SchemaId = HintSchema,
            MaxBytes = 2_048,
        };

    internal static ProductPreviewPayload Selector(CellsPreviewSelector value) =>
        ProductPreviewPayload.Create(
            "cells",
            ProductPreviewPayloadKinds.Selector,
            2,
            SelectorSchema,
            value,
            ProductJsonContext.Default.CellsPreviewSelector);

    internal static ProductPreviewPayload Hint(CellsPreviewHint value) =>
        ProductPreviewPayload.Create(
            "cells",
            ProductPreviewPayloadKinds.Hint,
            2,
            HintSchema,
            value,
            ProductJsonContext.Default.CellsPreviewHint);

    internal static CellsPreviewSelector ResolveSelector(
        ProductPreviewPayload? selector)
    {
        if (selector is null)
        {
            return new CellsPreviewSelector(null);
        }

        return selector.Read(
            ProductJsonContext.Default.CellsPreviewSelector);
    }
}
