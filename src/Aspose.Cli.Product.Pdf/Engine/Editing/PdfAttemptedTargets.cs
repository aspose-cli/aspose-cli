namespace Aspose.Cli.Product.Pdf.Engine.Editing;

/// <summary>
/// The addresses an operation that failed attempted to change, in the vocabulary of
/// <see cref="PdfSnapshots"/>: a page selection as written (<c>page/2-4</c>, <c>page/all</c>
/// without one), and the named objects of the operation.
/// </summary>
internal sealed class PdfAttemptedTargets : IPdfOpHandler<IReadOnlyList<string>>
{
    internal static PdfAttemptedTargets Instance { get; } = new();

    public IReadOnlyList<string> Apply(RotatePagesOp operation) => Pages(operation.Pages);

    public IReadOnlyList<string> Apply(DeletePagesOp operation) => Pages(operation.Pages);

    public IReadOnlyList<string> Apply(MovePagesOp operation) => Pages(operation.Pages);

    public IReadOnlyList<string> Apply(InsertPagesFromOp operation) => Page(operation.At);

    public IReadOnlyList<string> Apply(InsertBlankPageOp operation) => Page(operation.At);

    public IReadOnlyList<string> Apply(CropPagesOp operation) => Pages(operation.Pages);

    public IReadOnlyList<string> Apply(SetPageSizeOp operation) => Pages(operation.Pages);

    public IReadOnlyList<string> Apply(AddWatermarkTextOp operation) => Pages(operation.Pages);

    public IReadOnlyList<string> Apply(AddWatermarkImageOp operation) => Pages(operation.Pages);

    public IReadOnlyList<string> Apply(AddPageNumbersOp operation) => Pages(operation.Pages);

    public IReadOnlyList<string> Apply(AddHeaderTextOp operation) => Pages(operation.Pages);

    public IReadOnlyList<string> Apply(AddFooterTextOp operation) => Pages(operation.Pages);

    public IReadOnlyList<string> Apply(AddStampImageOp operation) => Page(operation.Page);

    public IReadOnlyList<string> Apply(AddLinkOp operation) => Page(operation.Page);

    public IReadOnlyList<string> Apply(RedactTextOp operation) => Pages(operation.Pages);

    public IReadOnlyList<string> Apply(RedactAreaOp operation) => Page(operation.Page);

    public IReadOnlyList<string> Apply(SetMetadataOp operation) =>
    [
        .. new (string Name, string? Value)[]
            {
                ("title", operation.Title),
                ("author", operation.Author),
                ("subject", operation.Subject),
                ("keywords", operation.Keywords),
            }
            .Where(static item => item.Value is not null)
            .Select(static item => "metadata/" + item.Name),
        .. (operation.Custom?.Keys ?? []).Select(static key => "metadata/" + key),
    ];

    public IReadOnlyList<string> Apply(RemoveMetadataOp operation) => ["metadata/all"];

    public IReadOnlyList<string> Apply(AddBookmarkOp operation) => [$"bookmark/{operation.Parent ?? "all"}"];

    public IReadOnlyList<string> Apply(DeleteBookmarksOp operation) =>
        operation.All ? ["bookmark/all"] : [.. operation.Indexes!.Select(static index => $"bookmark/{index}")];

    public IReadOnlyList<string> Apply(AddAttachmentOp operation) => [$"attachment/{operation.Name ?? Path.GetFileName(operation.Path)}"];

    public IReadOnlyList<string> Apply(RemoveAttachmentOp operation) => [$"attachment/{operation.Name}"];

    public IReadOnlyList<string> Apply(SetPageLabelsOp operation) =>
        [.. operation.Ranges.Select(static range => $"page-label/{range.StartPage}")];

    public IReadOnlyList<string> Apply(SetFormFieldOp operation) => [$"field/{operation.Name}"];

    public IReadOnlyList<string> Apply(FlattenFormsOp operation) =>
        operation.Fields is null ? ["field/all"] : [.. operation.Fields.Select(static name => $"field/{name}")];

    public IReadOnlyList<string> Apply(EncryptPdfOp operation) => [Security];

    public IReadOnlyList<string> Apply(DecryptPdfOp operation) => [Security];

    public IReadOnlyList<string> Apply(OptimizePdfOp operation) => ["page/all"];

    private const string Security = "security/encryption";

    private static IReadOnlyList<string> Pages(string? selection) => [$"page/{selection ?? "all"}"];

    private static IReadOnlyList<string> Page(int number) => [$"page/{number}"];
}
