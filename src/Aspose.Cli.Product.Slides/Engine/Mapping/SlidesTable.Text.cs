using Aspose.Cli.Sdk.Tables;
using Aspose.Slides;
using Row = Aspose.Cli.Sdk.Tables.Row;

namespace Aspose.Cli.Product.Slides.Engine.Mapping;

internal static partial class SlidesTable
{
    /// <summary>A paragraph of a text frame, addressed by its position: <c>…/paragraph/0</c>.</summary>
    internal static readonly ObjectKind<IParagraph> ParagraphKind = new("paragraph", "A paragraph of a shape's, a table cell's or the notes' text.")
    {
        Row.Text<IParagraph>("text", "IParagraph.Text", static p => p.Text, static (p, v) => p.Text = v),
        Row.Of<IParagraph, short>("depth", ValueKinds.ShortInteger, "IParagraph.ParagraphFormat.Depth", static p => p.ParagraphFormat.Depth, static (p, v) => p.ParagraphFormat.Depth = v),
        Row.Enum<IParagraph, BulletType>("bullet.type", "IParagraph.ParagraphFormat.Bullet.Type", static p => p.ParagraphFormat.Bullet.Type, static (p, v) => p.ParagraphFormat.Bullet.Type = v),
        Row.Of<IParagraph, char>("bullet.char", ValueKinds.Character, "IParagraph.ParagraphFormat.Bullet.Char", static p => p.ParagraphFormat.Bullet.Char, static (p, v) => p.ParagraphFormat.Bullet.Char = v),
        Row.Enum<IParagraph, NumberedBulletStyle>("bullet.numberedStyle", "IParagraph.ParagraphFormat.Bullet.NumberedBulletStyle", static p => p.ParagraphFormat.Bullet.NumberedBulletStyle, static (p, v) => p.ParagraphFormat.Bullet.NumberedBulletStyle = v),
        Row.Length<IParagraph>("marginLeft", "IParagraph.ParagraphFormat.MarginLeft", static p => p.ParagraphFormat.MarginLeft, static (p, v) => p.ParagraphFormat.MarginLeft = v),
        Row.Length<IParagraph>("indent", "IParagraph.ParagraphFormat.Indent", static p => p.ParagraphFormat.Indent, static (p, v) => p.ParagraphFormat.Indent = v),
        Row.Of<IParagraph, short>("effective.depth", ValueKinds.ShortInteger, "IParagraph.ParagraphFormat.GetEffective().Depth", static p => p.ParagraphFormat.GetEffective().Depth),
        Row.Enum<IParagraph, BulletType>("effective.bullet.type", "IParagraph.ParagraphFormat.GetEffective().Bullet.Type", static p => p.ParagraphFormat.GetEffective().Bullet.Type),
    };

    /// <summary>A text run (portion) of a paragraph, addressed by its position: <c>…/paragraph/0/run/1</c>.</summary>
    internal static readonly ObjectKind<IPortion> RunKind = new("run", "A text run (portion) of a paragraph.")
    {
        Row.Text<IPortion>("text", "IPortion.Text", static r => r.Text, static (r, v) => r.Text = v),
        Characters<IPortion>(static r => r.PortionFormat),
        Row.Length<IPortion>("effective.fontHeight", "IPortion.PortionFormat.GetEffective().FontHeight", static r => r.PortionFormat.GetEffective().FontHeight),
        Row.Text<IPortion>("effective.latinFont", "IPortion.PortionFormat.GetEffective().LatinFont.FontName", static r => r.PortionFormat.GetEffective().LatinFont?.FontName),
        Row.Boolean<IPortion>("effective.fontBold", "IPortion.PortionFormat.GetEffective().FontBold", static r => r.PortionFormat.GetEffective().FontBold),
        Row.Color<IPortion>("effective.color", "IPortion.PortionFormat.GetEffective().FillFormat.SolidFillColor; null unless the effective FillType is Solid, for which alone the engine gives it", static r => r.PortionFormat.GetEffective().FillFormat is { FillType: FillType.Solid } fill ? fill.SolidFillColor : System.Drawing.Color.Empty),
    };

    /// <summary>
    /// The character format rows of a portion format, which a run and a chart's text formats
    /// share: fonts, height, bold and the text fill.
    /// </summary>
    internal static IEnumerable<Row<T>> Characters<T>(Func<T, IBasePortionFormat> format)
        where T : class =>
    [
        Row.Text<T>("latinFont", "IPortionFormat.LatinFont.FontName; set as new FontData(name)", item => format(item).LatinFont?.FontName, (item, v) => format(item).LatinFont = new FontData(v)),
        Row.Text<T>("eastAsianFont", "IPortionFormat.EastAsianFont.FontName; set as new FontData(name)", item => format(item).EastAsianFont?.FontName, (item, v) => format(item).EastAsianFont = new FontData(v)),
        Row.Text<T>("complexScriptFont", "IPortionFormat.ComplexScriptFont.FontName; set as new FontData(name)", item => format(item).ComplexScriptFont?.FontName, (item, v) => format(item).ComplexScriptFont = new FontData(v)),
        Row.Length<T>("fontHeight", "IPortionFormat.FontHeight", item => format(item).FontHeight, (item, v) => format(item).FontHeight = v),
        Row.Enum<T, NullableBool>("fontBold", "IPortionFormat.FontBold", item => format(item).FontBold, (item, v) => format(item).FontBold = v),
        .. Fill<T>("fill", item => format(item).FillFormat),
    ];

    /// <summary>The rows of a fill format under <paramref name="prefix"/>: its fill type and its solid color.</summary>
    internal static IEnumerable<Row<T>> Fill<T>(string prefix, Func<T, IFillFormat> fill)
        where T : class =>
    [
        Row.Enum<T, FillType>($"{prefix}.fillType", "IFillFormat.FillType", item => fill(item).FillType, (item, v) => fill(item).FillType = v),
        Row.Color<T>($"{prefix}.color", "IFillFormat.SolidFillColor.Color", item => fill(item).SolidFillColor.Color, (item, v) => fill(item).SolidFillColor.Color = v),
    ];

    /// <summary>The rows of a line's fill format under <paramref name="prefix"/>: its fill type and its solid color.</summary>
    internal static IEnumerable<Row<T>> LineFill<T>(string prefix, Func<T, ILineFillFormat> fill)
        where T : class =>
    [
        Row.Enum<T, FillType>($"{prefix}.fillType", "ILineFillFormat.FillType", item => fill(item).FillType, (item, v) => fill(item).FillType = v),
        Row.Color<T>($"{prefix}.color", "ILineFillFormat.SolidFillColor.Color", item => fill(item).SolidFillColor.Color, (item, v) => fill(item).SolidFillColor.Color = v),
    ];

    private static void PlaceText(DocumentTable<Presentation> table)
    {
        PlaceParagraphs<IShape, IAutoShape>(table, ShapeKind, static shape => shape.TextFrame, "IAutoShape.TextFrame");
        PlaceParagraphs<INotesSlide, INotesSlide>(table, NotesKind, static notes => notes.NotesTextFrame, "INotesSlide.NotesTextFrame");
        table.Place(ParagraphKind, RunKind, new Locator<IParagraph, IPortion>
        {
            Segment = "run",
            Key = [KeyPart.Index],
            Find = static (paragraph, key) => key.Index() < paragraph.Portions.Count ? paragraph.Portions[key.Index()] : null,
            List = static paragraph => paragraph.Portions,
            KeyOf = static (paragraph, run) => Key.Of(Position(paragraph.Portions, run)),
            Delete = static (paragraph, run) => paragraph.Portions.Remove(run),
        });
        table.Adds(ParagraphKind, RunKind, new Adder<IParagraph, IPortion>
        {
            Name = "run",
            Description = "IPortionCollection.Insert(int index, IPortion value) with new Portion()",
            Arguments = [Argument.Of("index", ValueKinds.Integer, "The new run's position, the engine's, from 0.")],
            Create = static (paragraph, a) =>
            {
                var run = new Portion();
                paragraph.Portions.Insert(a.Get<int>("index"), run);
                return run;
            },
        });
    }

    /// <summary>
    /// Places the paragraphs of a text frame below a kind, with the add that inserts one: on the
    /// objects of the kind that are <typeparamref name="TParent"/>, through <paramref name="frame"/>,
    /// the engine member <paramref name="member"/>, which can be null.
    /// </summary>
    private static void PlaceParagraphs<TKind, TParent>(DocumentTable<Presentation> table, ObjectKind<TKind> parent, Func<TParent, ITextFrame?> frame, string member)
        where TKind : class
        where TParent : class, TKind
    {
        IParagraphCollection? Paragraphs(TKind owner) => owner is TParent text ? frame(text)?.Paragraphs : null;
        table.Place(parent, ParagraphKind, new Locator<TKind, IParagraph>
        {
            Segment = "paragraph",
            Key = [KeyPart.Index],
            Find = (owner, key) => Paragraphs(owner) is { } found && key.Index() < found.Count ? found[key.Index()] : null,
            List = owner => Paragraphs(owner) ?? Enumerable.Empty<IParagraph>(),
            KeyOf = (owner, paragraph) => Key.Of(Position(Paragraphs(owner)!, paragraph)),
            Delete = (owner, paragraph) => Paragraphs(owner)!.Remove(paragraph),
        });
        table.Adds(parent, ParagraphKind, new Adder<TParent, IParagraph>
        {
            Name = "paragraph",
            Description = $"IParagraphCollection.Insert(int index, IParagraph value) with new Paragraph(), in {member}",
            Arguments = [Argument.Of("index", ValueKinds.Integer, "The new paragraph's position, the engine's, from 0.")],
            Create = (owner, a) =>
            {
                var paragraph = new Paragraph();
                (frame(owner) ?? throw TableErrors.Absent(member)).Paragraphs.Insert(a.Get<int>("index"), paragraph);
                return paragraph;
            },
        });
    }

    /// <summary>The position of an item in an engine collection, by reference, from 0.</summary>
    internal static int Position<T>(IEnumerable<T> items, T item)
        where T : class =>
        items.Select(static (candidate, index) => (candidate, index)).First(found => ReferenceEquals(found.candidate, item)).index;
}
