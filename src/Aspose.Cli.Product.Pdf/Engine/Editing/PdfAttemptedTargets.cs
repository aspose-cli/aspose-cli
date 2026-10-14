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

    public IReadOnlyList<string> Apply(ImportPagesOp operation) => Page(operation.At);

    public IReadOnlyList<string> Apply(InsertPageOp operation) => Page(operation.At);

    public IReadOnlyList<string> Apply(CropPagesOp operation) => Pages(operation.Pages);

    public IReadOnlyList<string> Apply(SetPageSizeOp operation) => Pages(operation.Pages);

    public IReadOnlyList<string> Apply(AddWatermarkTextOp operation) => Pages(operation.Pages);

    public IReadOnlyList<string> Apply(AddWatermarkImageOp operation) => Pages(operation.Pages);

    public IReadOnlyList<string> Apply(AddPageNumbersOp operation) => Pages(operation.Pages);

    public IReadOnlyList<string> Apply(AddHeaderTextOp operation) => Pages(operation.Pages);

    public IReadOnlyList<string> Apply(AddFooterTextOp operation) => Pages(operation.Pages);

    public IReadOnlyList<string> Apply(AddStampImageOp operation) => Page(operation.Page);

    public IReadOnlyList<string> Apply(AddHyperlinkOp operation) => Page(operation.Page);

    public IReadOnlyList<string> Apply(RedactTextOp operation) => Pages(operation.Pages);

    public IReadOnlyList<string> Apply(RedactAreaOp operation) => Page(operation.Page);

    public IReadOnlyList<string> Apply(SetPropertiesOp operation) =>
    [
        .. new (string Name, string? Value)[]
            {
                ("title", operation.Title),
                ("author", operation.Author),
                ("subject", operation.Subject),
                ("keywords", operation.Keywords),
            }
            .Where(static item => item.Value is not null)
            .Select(static item => "property/" + item.Name),
        .. (operation.Custom?.Keys ?? []).Select(static key => "property/" + TargetName.Escape(key)),
    ];

    public IReadOnlyList<string> Apply(ClearPropertiesOp operation) => ["property/all"];

    public IReadOnlyList<string> Apply(AddBookmarkOp operation) => [$"bookmark/{operation.Parent ?? "all"}"];

    public IReadOnlyList<string> Apply(DeleteBookmarksOp operation) =>
        operation.Bookmarks is null ? ["bookmark/all"] : [.. operation.Bookmarks.Select(static address => $"bookmark/{address}")];

    public IReadOnlyList<string> Apply(AddAttachmentOp operation) => [$"attachment/{TargetName.Escape(operation.Name ?? Path.GetFileName(operation.Path))}"];

    public IReadOnlyList<string> Apply(DeleteAttachmentOp operation) => [$"attachment/{TargetName.Escape(operation.Name)}"];

    public IReadOnlyList<string> Apply(SetPageLabelsOp operation) =>
        [.. operation.Ranges.Select(static range => $"page-label/{range.StartPage}")];

    public IReadOnlyList<string> Apply(SetFormFieldOp operation) => [$"field/{TargetName.Escape(operation.Name)}"];

    public IReadOnlyList<string> Apply(FlattenFormsOp operation) =>
        operation.Fields is null ? ["field/all"] : [.. operation.Fields.Select(static name => $"field/{TargetName.Escape(name)}")];

    public IReadOnlyList<string> Apply(EncryptDocumentOp operation) => [Security];

    public IReadOnlyList<string> Apply(DecryptDocumentOp operation) => [Security];

    public IReadOnlyList<string> Apply(OptimizeDocumentOp operation) => ["page/all"];

    private const string Security = "security/encryption";

    private static IReadOnlyList<string> Pages(string? selection) => [$"page/{selection ?? "all"}"];

    private static IReadOnlyList<string> Page(int number) => [$"page/{number}"];
}
