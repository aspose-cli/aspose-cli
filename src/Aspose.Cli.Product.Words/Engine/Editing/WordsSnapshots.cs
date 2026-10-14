using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Words;
using Aspose.Words.Properties;

namespace Aspose.Cli.Product.Words.Engine.Editing;

/// <summary>
/// The objects of a document one operation can change, as the SDK snapshots them around the
/// operation (<see cref="IOperationModel{TOp}.Snapshot"/>): the body blocks (<c>block/3</c>, by
/// their content, numbered as <c>words read</c> numbers them), the sections (<c>section/2</c>, by
/// their page setup and their headers and footers), the styles (<c>style/Normal</c>), the
/// document properties (<c>property/title</c>, a custom one by its own name), the watermark
/// (<c>watermark/1</c>) and the editing restriction (<c>protection/document</c>). Blocks and
/// sections are added as the nodes the engine keeps them as, so the batch reports each one where
/// the output holds it (<see cref="WordsEditModel.Positions"/>). The state of each block is
/// cached until the next operation applies.
/// </summary>
internal sealed partial class WordsSnapshots(LoadedDocument loaded) : IWordsOpHandler<OperationSnapshot>
{
    /// <summary>The unit of a body block's address.</summary>
    internal const string BlockUnit = "block";

    /// <summary>The unit of a section's address.</summary>
    internal const string SectionUnit = "section";

    /// <summary>The address of the document's watermark.</summary>
    internal const string WatermarkAddress = "watermark/1";

    /// <summary>The address of the document's editing restriction.</summary>
    internal const string ProtectionAddress = "protection/document";

    private readonly Document _document = loaded.Document;
    private readonly Dictionary<Node, string> _states = new(ReferenceEqualityComparer.Instance);

    /// <summary>Forgets the block states read so far; called before an operation applies.</summary>
    internal void Invalidate() => _states.Clear();

    public OperationSnapshot Apply(ReplaceTextOp operation) => BlocksAndSections();

    public OperationSnapshot Apply(SetTextOp operation) => Blocks();

    public OperationSnapshot Apply(InsertParagraphsOp operation) => Blocks();

    public OperationSnapshot Apply(InsertMarkdownOp operation) => Blocks();

    public OperationSnapshot Apply(DeleteBlocksOp operation) => Blocks();

    public OperationSnapshot Apply(InsertBreakOp operation) => BlocksAndSections();

    public OperationSnapshot Apply(InsertImageOp operation) => Blocks();

    public OperationSnapshot Apply(InsertTableOp operation) => Blocks();

    public OperationSnapshot Apply(SetTableCellOp operation) => Blocks();

    public OperationSnapshot Apply(RepeatTableRowOp operation) => Blocks();

    public OperationSnapshot Apply(SetTablePaginationOp operation) => Blocks();

    public OperationSnapshot Apply(InsertTocOp operation) => Blocks();

    public OperationSnapshot Apply(InsertBookmarkOp operation) => Blocks();

    public OperationSnapshot Apply(InsertHyperlinkOp operation) => Blocks();

    public OperationSnapshot Apply(InsertFieldOp operation) => Blocks();

    public OperationSnapshot Apply(InsertSectionOp operation) => BlocksAndSections();

    public OperationSnapshot Apply(DeleteSectionOp operation) => BlocksAndSections();

    public OperationSnapshot Apply(SetPageSetupOp operation) => Sections();

    public OperationSnapshot Apply(SetHeaderOp operation) => Sections();

    public OperationSnapshot Apply(SetFooterOp operation) => Sections();

    public OperationSnapshot Apply(SetPageNumbersOp operation) => Sections();

    public OperationSnapshot Apply(FormatTextOp operation) => Blocks();

    public OperationSnapshot Apply(SetStyleOp operation) => Blocks();

    public OperationSnapshot Apply(DefineStyleOp operation) => Styles();

    public OperationSnapshot Apply(SetListOp operation) => Blocks();

    public OperationSnapshot Apply(SetDefaultFontOp operation) => Styles();

    public OperationSnapshot Apply(SetPropertiesOp operation) => Properties();

    public OperationSnapshot Apply(SetWatermarkOp operation) => Watermark();

    public OperationSnapshot Apply(DeleteWatermarkOp operation) => Watermark();

    public OperationSnapshot Apply(ProtectDocumentOp operation) => Protection();

    public OperationSnapshot Apply(UnprotectDocumentOp operation) => Protection();

    public OperationSnapshot Apply(AcceptRevisionsOp operation) => BlocksAndSections();

    public OperationSnapshot Apply(RejectRevisionsOp operation) => BlocksAndSections();

    public OperationSnapshot Apply(AddCommentOp operation) => Blocks();

    public OperationSnapshot Apply(DeleteCommentsOp operation) => Blocks();

    public OperationSnapshot Apply(ImportSectionsOp operation) => BlocksAndSections();

    public OperationSnapshot Apply(MailMergeOp operation) => BlocksAndSections();

    public OperationSnapshot Apply(RefreshFieldsOp operation) => BlocksAndSections();

    private OperationSnapshot Blocks() => AddBlocks(new OperationSnapshot());

    private OperationSnapshot Sections() => AddSections(new OperationSnapshot());

    private OperationSnapshot BlocksAndSections() => AddSections(AddBlocks(new OperationSnapshot()));

    private OperationSnapshot AddBlocks(OperationSnapshot snapshot) =>
        snapshot.AddObjects(BlockUnit, Blocks(loaded).Select(block => ((object)block, State(block))));

    private OperationSnapshot AddSections(OperationSnapshot snapshot) =>
        snapshot.AddObjects(SectionUnit, _document.Sections.Cast<Section>().Select(static section => ((object)section, WordsDigest.OfSection(section))));

    /// <summary>The body blocks in order, as <c>words read</c> numbers them.</summary>
    internal static IEnumerable<Node> Blocks(LoadedDocument loaded) =>
        new DocumentBlockIndex(loaded.Document, loaded.Evaluation).Entries.Select(static entry => entry.Node);

    private string State(Node block)
    {
        if (!_states.TryGetValue(block, out string? state))
        {
            state = WordsDigest.Of(block);
            _states[block] = state;
        }

        return state;
    }

    private OperationSnapshot Styles()
    {
        var snapshot = new OperationSnapshot();
        foreach (Style style in _document.Styles)
        {
            snapshot.Add(StyleAddress(style.Name), WordsDigest.OfStyle(style));
        }

        return snapshot;
    }

    /// <summary>The title, author, subject and keywords, and every custom property by its own name.</summary>
    private OperationSnapshot Properties()
    {
        BuiltInDocumentProperties builtIn = _document.BuiltInDocumentProperties;
        var snapshot = new OperationSnapshot()
            .Add("property/title", builtIn.Title ?? string.Empty)
            .Add("property/author", builtIn.Author ?? string.Empty)
            .Add("property/subject", builtIn.Subject ?? string.Empty)
            .Add("property/keywords", builtIn.Keywords ?? string.Empty);
        foreach (DocumentProperty property in _document.CustomDocumentProperties)
        {
            snapshot.Add(PropertyAddress(property.Name), $"{property.Type}|{property.Value}");
        }

        return snapshot;
    }

    /// <summary>The watermark's kind and the header shapes that draw it; absent when the document has none.</summary>
    private OperationSnapshot Watermark()
    {
        var snapshot = new OperationSnapshot();
        if (_document.Watermark.Type != WatermarkType.None)
        {
            string shapes = string.Join("|", WordsReadBack.WatermarkShapes(_document).Select(WordsDigest.Of));
            snapshot.Add(WatermarkAddress, $"{_document.Watermark.Type}|{shapes}");
        }

        return snapshot;
    }

    /// <summary>
    /// The editing restriction: its kind and, as the saved document states it, its password hash,
    /// so a protect that only sets or changes the password changes it too; the password itself is
    /// never part of it.
    /// </summary>
    private OperationSnapshot Protection()
    {
        using var stream = new MemoryStream();
        _document.Save(stream, SaveFormat.Docx);
        stream.Position = 0;
        using var package = new ZipArchive(stream, ZipArchiveMode.Read);
        string settings = string.Empty;
        if (package.GetEntry("word/settings.xml") is { } entry)
        {
            using var reader = new StreamReader(entry.Open());
            settings = reader.ReadToEnd();
        }

        string protection = DocumentProtection().Match(settings).Value;
        return new OperationSnapshot().Add(
            ProtectionAddress, $"{_document.ProtectionType}|{WordsDigest.Hash(Encoding.UTF8.GetBytes(protection))}");
    }

    [GeneratedRegex(@"<w:documentProtection\b[^>]*>", RegexOptions.CultureInvariant)]
    private static partial Regex DocumentProtection();

    internal static string StyleAddress(string name) => $"style/{TargetName.Escape(name)}";

    internal static string PropertyAddress(string name) => $"property/{TargetName.Escape(WordsReadBack.PropertyKey(name))}";
}
