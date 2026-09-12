using System.Text.Json;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Product.Slides.Contracts.Serialization;

/// <summary>Connects the Slides operation vocabulary to the shared wire protocol.</summary>
internal sealed class SlidesOpJsonConverter()
    : OperationJsonConverter<SlidesOp>(SlidesOps.Registry, static operation => operation.OpName)
{
    protected override SlidesOp ApplyDefaults(SlidesOp value, JsonElement root) => value switch
    {
        AppendPresentationOp op when Missing(root, "masterPolicy") => op with { MasterPolicy = "keep-source" },
        SlidesReplaceTextOp op when Missing(root, "scope") => op with { Scope = "all" },
        SetShapeStyleOp op when Missing(root, "style") => op with { Style = new SlidesShapeStyleInput() },
        SetSlideSizeOp op when Missing(root, "scaleContent") => op with { ScaleContent = true },
        _ => value,
    };

    private static bool Missing(JsonElement root, string name) => !root.TryGetProperty(name, out _);
}
