using Aspose.Cli.Product.Words.Engine.Mapping;

namespace Aspose.Cli.Product.Words.Engine.Editing;

/// <summary>
/// What each operation addresses and how the edit treats it: one method per operation, so a new
/// operation does not compile until it is placed. An operation names body blocks
/// (<see cref="WordsOpTraits.Target"/>), sections, or neither; it reports the part of the document
/// it changes when it fails without a resolved address; <c>--track-changes</c> admits it only
/// when Word records it as revisions; and the verification compares its effect without tracked
/// changes only when it is a field, protection or revision state the read-back holds.
/// </summary>
internal sealed class WordsOpAddresses : IWordsOpHandler<WordsOpTraits>
{
    private static readonly WordsOpAddresses Instance = new();

    private WordsOpAddresses()
    {
    }

    /// <summary>The traits of <paramref name="operation"/>.</summary>
    internal static WordsOpTraits Of(WordsOp operation) => operation.Accept(Instance);

    // ----- body content: the blocks it names, recorded as revisions ---------------------------

    // It searches the scope it names rather than addressing a block.
    public WordsOpTraits Apply(ReplaceTextOp operation) => Document(AllBlocks) with { Trackable = true };

    public WordsOpTraits Apply(SetTextOp operation) => Blocks(operation.Target) with { Trackable = true };

    public WordsOpTraits Apply(InsertParagraphsOp operation) => Blocks(operation.At) with { Trackable = true };

    public WordsOpTraits Apply(InsertMarkdownOp operation) => Blocks(operation.At) with { Trackable = true };

    public WordsOpTraits Apply(DeleteBlocksOp operation) => Blocks(operation.Target) with { Trackable = true };

    // Word records a page break as inserted content, but a section break changes the structure.
    public WordsOpTraits Apply(InsertBreakOp operation) => Blocks(operation.At) with { Trackable = operation.Kind == WordsBreakKinds.Page };

    public WordsOpTraits Apply(InsertImageOp operation) => Blocks(operation.At) with { Trackable = true };

    public WordsOpTraits Apply(InsertTableOp operation) => Blocks(operation.At) with { Trackable = true };

    public WordsOpTraits Apply(SetTableCellOp operation) => Blocks(operation.Target) with { Trackable = true };

    public WordsOpTraits Apply(RepeatTableRowOp operation) => Blocks(operation.At) with { Trackable = true };

    public WordsOpTraits Apply(SetTablePaginationOp operation) => Blocks(operation.Target);

    public WordsOpTraits Apply(InsertBookmarkOp operation) => Blocks(operation.At) with { Trackable = true };

    // ----- fields: inserted as content, their results compared by the verification ------------

    public WordsOpTraits Apply(InsertTocOp operation) => Blocks(operation.At) with { Trackable = true, StateCompared = true };

    public WordsOpTraits Apply(InsertHyperlinkOp operation) => Blocks(operation.At) with { Trackable = true, StateCompared = true };

    public WordsOpTraits Apply(InsertFieldOp operation) => Blocks(operation.At) with { Trackable = true, StateCompared = true };

    public WordsOpTraits Apply(RefreshFieldsOp operation) => Document(AllBlocks) with { StateCompared = true };

    public WordsOpTraits Apply(MailMergeOp operation) => Document(AllBlocks) with { StateCompared = true };

    // ----- formatting of the blocks it names --------------------------------------------------

    public WordsOpTraits Apply(FormatTextOp operation) => Blocks(operation.Target);

    public WordsOpTraits Apply(SetStyleOp operation) => Blocks(operation.Target);

    public WordsOpTraits Apply(SetListOp operation) => Blocks(operation.Target);

    public WordsOpTraits Apply(ClearListOp operation) => Blocks(operation.Target);

    // ----- sections: the one it names, or every section -----------------------------------

    public WordsOpTraits Apply(InsertSectionOp operation) => Sections(operation.At);

    public WordsOpTraits Apply(DeleteSectionOp operation) => Sections(operation.Section);

    public WordsOpTraits Apply(SetPageSetupOp operation) => Sections(operation.Section);

    public WordsOpTraits Apply(SetHeaderOp operation) => Sections(operation.Section);

    public WordsOpTraits Apply(SetFooterOp operation) => Sections(operation.Section);

    public WordsOpTraits Apply(SetPageNumbersOp operation) => Sections(operation.Section) with { StateCompared = true };

    // Imported sections arrive as inserted content after the body.
    public WordsOpTraits Apply(ImportSectionsOp operation) => Document(AllBlocks) with { Trackable = true };

    // ----- styles, properties and the watermark ------------------------------------------

    public WordsOpTraits Apply(DefineStyleOp operation) => Document(WordsReadBack.StyleAddress(operation.Name));

    public WordsOpTraits Apply(SetDefaultFontOp operation) => Document("style/all");

    public WordsOpTraits Apply(SetPropertiesOp operation) => Document("property/all");

    public WordsOpTraits Apply(SetWatermarkOp operation) => Document(WordsSnapshots.WatermarkAddress);

    public WordsOpTraits Apply(DeleteWatermarkOp operation) => Document(WordsSnapshots.WatermarkAddress);

    // ----- protection and review: states the verification compares -------------------------

    public WordsOpTraits Apply(ProtectDocumentOp operation) => Document(WordsSnapshots.ProtectionAddress) with { StateCompared = true };

    public WordsOpTraits Apply(UnprotectDocumentOp operation) => Document(WordsSnapshots.ProtectionAddress) with { StateCompared = true };

    // Resolving revisions is not itself an edit Word could record.
    public WordsOpTraits Apply(AcceptRevisionsOp operation) => Document(AllBlocks) with { StateCompared = true };

    public WordsOpTraits Apply(RejectRevisionsOp operation) => Document(AllBlocks) with { StateCompared = true };

    // Comments are review annotations rather than revisions: a batch that tracks changes admits
    // them and applies them untracked.
    public WordsOpTraits Apply(AddCommentOp operation) => Blocks(operation.Target) with { Trackable = true };

    public WordsOpTraits Apply(DeleteCommentsOp operation) => Document(AllBlocks) with { Trackable = true };

    private const string AllBlocks = WordsSnapshots.BlockUnit + "/all";

    private static WordsOpTraits Blocks(WordsTarget target) => new(AllBlocks) { Target = target };

    private static WordsOpTraits Sections(int? section) =>
        new(WordsSnapshots.SectionUnit + "/all") { AddressesSections = true, Section = section };

    private static WordsOpTraits Document(string fallback) => new(fallback);
}

/// <summary>What one operation addresses and how the edit treats it.</summary>
/// <param name="Fallback">
/// What it attempted when it failed with no resolved block or section and names no number: the
/// part of the document it changes, such as <c>style/Normal</c>, or <c>block/all</c>, the blocks
/// it searched.
/// </param>
internal sealed record WordsOpTraits(string Fallback)
{
    /// <summary>The body blocks it names, or null for one that names none.</summary>
    public WordsTarget? Target { get; init; }

    /// <summary>Whether it addresses sections: the one <see cref="Section"/> names, or every section when it is null.</summary>
    public bool AddressesSections { get; init; }

    /// <summary>The section number it names, or null.</summary>
    public int? Section { get; init; }

    /// <summary>
    /// Whether <c>--track-changes</c> admits it: Word records it as tracked changes, or it is a
    /// comment, which applies untracked. Aspose.Words tracks the insertion and deletion of
    /// content only; formatting, styles, lists, page setup, properties, protection, merges, field
    /// updates, header replacement and section structure would change silently.
    /// </summary>
    public bool Trackable { get; init; }

    /// <summary>
    /// Whether its effect is a field, protection or revision count the verification compares
    /// without tracked changes.
    /// </summary>
    public bool StateCompared { get; init; }
}
