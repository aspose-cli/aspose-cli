using Aspose.Cli.Generated;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Product.Words;

/// <summary>Words-specific preview semantics and browser presentation.</summary>
internal sealed class WordsPreviewAdapter
    : ProductPreviewAdapterBase<IDocumentEngine>
{
    public const string DocumentView = "document";

    public WordsPreviewAdapter()
        : base(
            typeof(WordsPreviewAdapter).Assembly,
            "Words",
            DocumentView,
            [new(DocumentView, "Document")],
            "Omit --fx. Words preview uses the paginated document view.")
    {
    }

    public override IReadOnlyList<ProductPreviewPayloadContract>
        PayloadContracts { get; } =
    [
        WordsPreviewPayloads.HintContract,
    ];

    public override PreviewRenderer CreateRenderer(
        IDocumentEngine port,
        string filePath,
        ProductPreviewRequest request)
    {
        ValidateProductRequest(request);
        return context => port.RenderPreview(
            filePath,
            new WordsPreviewRequest { Password = request.Password },
            context.Artifacts);
    }

    public override void ValidatePayload(ProductPreviewPayload payload)
    {
        if (payload.Kind != ProductPreviewPayloadKinds.Hint)
        {
            return;
        }
        WordsPreviewHint hint = payload.Read(
            ProductJsonContext.Default.WordsPreviewHint);
        if (hint.Page < 1 || hint.Block is < 1)
        {
            throw CliErrors.OptionInvalid(
                "preview payload",
                "Words page and block values must be positive",
                "Use one-based page and block numbers.");
        }
    }
}
