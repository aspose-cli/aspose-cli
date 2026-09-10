using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Generated;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Product.Cells;

/// <summary>Cells-specific preview semantics and browser presentation.</summary>
internal sealed class CellsPreviewAdapter
    : ProductPreviewAdapterBase<IWorkbookEngine>
{
    public CellsPreviewAdapter()
        : base(
            typeof(CellsPreviewAdapter).Assembly,
            "Cells",
            CellsPreviewViews.Workbook,
            [
                new(CellsPreviewViews.Workbook, "Workbook"),
                new(CellsPreviewViews.Sheet, "Sheet"),
            ],
            "Use --fx demo, or omit --fx.")
    {
    }

    public override IReadOnlyList<ProductPreviewPayloadContract>
        PayloadContracts { get; } =
    [
        CellsPreviewPayloads.SelectorContract,
        CellsPreviewPayloads.HintContract,
    ];

    public override PreviewRenderer CreateRenderer(
        IWorkbookEngine port,
        string filePath,
        ProductPreviewRequest request)
    {
        ValidateProductRequest(request);
        CellsPreviewSelector selector =
            CellsPreviewPayloads.ResolveSelector(request.Selector);
        return context => port.RenderPreview(
            filePath,
            new PreviewRenderRequest
            {
                View = request.View,
                SheetName = selector.Sheet,
                Password = request.Password,
            },
            context.Artifacts);
    }

    protected override string CreateClientScript(string? effect)
    {
        ValidateEffect(effect);
        return effect is null
            ? ClientScript
            : $"window.__asposePreviewFx={JsonSerializer.Serialize(effect)};\n{ClientScript}";
    }

    protected override void ValidateEffect(string? effect)
    {
        if (effect is not null
            && !string.Equals(effect, "demo", StringComparison.Ordinal))
        {
            throw CliErrors.OptionInvalid(
                "--fx",
                $"unsupported presentation effect '{effect}'",
                "Use --fx demo, or omit --fx.");
        }
    }

    protected override void ValidateProductRequest(ProductPreviewRequest request)
    {
        if (!ViewDefinitions.Any(view =>
                string.Equals(view.Id, request.View, StringComparison.Ordinal)))
        {
            var available = new JsonArray();
            foreach (ProductPreviewView view in ViewDefinitions)
            {
                available.Add(view.Id);
            }

            throw new CliException(
                ErrorCodes.FeatureUnsupported,
                $"Preview view '{request.View}' is not supported by the cells product.",
                hint: $"Use one of: {string.Join(", ", ViewDefinitions.Select(static view => view.Id))}.",
                details: new JsonObject { ["available"] = available });
        }
        CellsPreviewSelector selector =
            CellsPreviewPayloads.ResolveSelector(request.Selector);
        if (selector.Sheet is not null && request.View != CellsPreviewViews.Sheet)
        {
            throw CliErrors.OptionInvalid(
                "--sheet",
                "the workbook view always previews the whole workbook",
                "Pass --view sheet to preview a single worksheet, or omit --sheet.");
        }
    }

    public override void ValidatePayload(ProductPreviewPayload payload)
    {
        if (payload.Kind == ProductPreviewPayloadKinds.Selector)
        {
            CellsPreviewSelector selector = payload.Read(
                ProductJsonContext.Default.CellsPreviewSelector);
            ValidateOptionalText(selector.Sheet, "sheet");
            return;
        }
        if (payload.Kind == ProductPreviewPayloadKinds.Hint)
        {
            CellsPreviewHint hint = payload.Read(
                ProductJsonContext.Default.CellsPreviewHint);
            ValidateOptionalText(hint.Sheet, "sheet");
            ValidateOptionalText(hint.Range, "range");
        }
    }

    private static void ValidateOptionalText(string? value, string field)
    {
        if (value is not null
            && (string.IsNullOrWhiteSpace(value)
                || value.Length > 255
                || value.Contains('\r')
                || value.Contains('\n')))
        {
            throw CliErrors.OptionInvalid(
                "preview payload",
                $"Cells {field} is invalid",
                $"Use a non-empty {field} of at most 255 characters.");
        }
    }
}
