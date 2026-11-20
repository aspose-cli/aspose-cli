using Aspose.Cli.Sdk.Tables;
using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Fields;
using Aspose.Words.Lists;
using Aspose.Words.Markup;
using Aspose.Words.Notes;
using Aspose.Words.Properties;
using Aspose.Words.Tables;
using WordList = Aspose.Words.Lists.List;
using WordRange = Aspose.Words.Range;
using WordRow = Aspose.Words.Tables.Row;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

internal static partial class WordsTable
{
    /// <summary>The table, built on first use, once every kind is initialized.</summary>
    internal static DocumentTable<Document> Definition => Built.Value;

    private static class Built
    {
        internal static readonly DocumentTable<Document> Value = Build();
    }

    private static DocumentTable<Document> Build()
    {
        var table = new DocumentTable<Document>(DocumentKind);
        table.Place(DocumentKind, SectionKind, new Locator<Document, Section>
        {
            Segment = "section",
            Key = [KeyPart.Index],
            Find = static (d, key) => d.Sections[key.Index()],
            List = static d => d.Sections.Cast<Section>(),
            KeyOf = static (d, section) => Key.Of(d.Sections.IndexOf(section)),
            Delete = static (_, section) => section.Remove(),
        });
        table.Place(SectionKind, HeaderFooterKind, new Locator<Section, HeaderFooter>
        {
            Segment = "headerFooter",
            Key = [KeyPart.Name],
            Find = static (s, key) => Enum.GetNames<HeaderFooterType>().Contains(key.Name(), StringComparer.Ordinal)
                ? s.HeadersFooters[Enum.Parse<HeaderFooterType>(key.Name())]
                : null,
            List = static s => s.HeadersFooters.Cast<HeaderFooter>(),
            KeyOf = static (_, footer) => Key.Of(footer.HeaderFooterType.ToString()),
            Delete = static (_, footer) => footer.Remove(),
        });
        table.Adds(SectionKind, HeaderFooterKind, new Adder<Section, HeaderFooter>
        {
            Name = "headerFooter",
            Description = "new HeaderFooter(document, headerFooterType), then Section.HeadersFooters.Add; it holds no paragraph",
            Arguments = [Argument.Of("headerFooterType", ValueKinds.Enum<HeaderFooterType>(), "The engine's HeaderFooterType.")],
            Create = static (s, a) =>
            {
                var footer = new HeaderFooter(s.Document, a.Get<HeaderFooterType>("headerFooterType"));
                s.HeadersFooters.Add(footer);
                return footer;
            },
        });
        table.Place(SectionKind, BodyKind, new Locator<Section, Body>
        {
            Segment = "body",
            Find = static (s, _) => s.Body,
            List = static s => s.Body is { } body ? [body] : [],
            KeyOf = static (_, _) => Key.None,
        });

        table.Place(DocumentKind, LayoutKind, new Locator<Document, Document>
        {
            Segment = "layout",
            Find = static (d, _) => d,
            List = static _ => [],
            KeyOf = static (_, _) => Key.None,
        });
        PlaceRange(table, DocumentKind);
        PlaceStory(table, BodyKind);
        PlaceStory(table, HeaderFooterKind);
        PlaceStory(table, CommentKind);
        PlaceStory(table, FootnoteKind);
        PlaceStory(table, CellKind);
        PlaceBlocks(table, ShapeKind);
        PlaceInline(table);
        PlaceTables(table);
        PlaceControl(table);
        PlaceDocumentObjects(table);
        WordsActions.Declare(table);
        table.SearchesText(ParagraphKind, "text");
        table.Inventory(WordsInventory.Kinds, WordsInventory.Count);
        return table;
    }

    /// <summary>A story: the blocks it holds and the range of its text.</summary>
    private static void PlaceStory<TStory>(DocumentTable<Document> table, ObjectKind<TStory> story)
        where TStory : CompositeNode
    {
        PlaceRange(table, story);
        PlaceBlocks(table, story);
        table.Adds(story, ParagraphKind, ParagraphAdder<TStory>());
        table.Adds(story, TableKind, TableAdder<TStory>());
    }

    /// <summary>
    /// What a content control holds, below it. The engine's paragraph, table, run, row and cell
    /// collections hold the control's content as its parent's own, and read lists it there, so
    /// these placements list nothing; they address the content by its position in the control,
    /// which an add in the control reports. A content control in a control is not its parent's own:
    /// GetChildNodes(NodeType.StructuredDocumentTag, false) holds a node's direct controls only, so a
    /// nested control is listed below the control that holds it.
    /// </summary>
    private static void PlaceControl(DocumentTable<Document> table)
    {
        PlaceRange(table, SdtKind);
        table.Place(SdtKind, SdtKind, Children<StructuredDocumentTag, StructuredDocumentTag>("sdt", NodeType.StructuredDocumentTag, listedWhenNamed: false));
        table.Place(SdtKind, ParagraphKind, Unlisted(Children<StructuredDocumentTag, Paragraph>("paragraph", NodeType.Paragraph, listedWhenNamed: false)));
        table.Place(SdtKind, TableKind, Unlisted(Children<StructuredDocumentTag, Table>("table", NodeType.Table, listedWhenNamed: false)));
        table.Place(SdtKind, RunKind, Unlisted(Children<StructuredDocumentTag, Run>("run", NodeType.Run, listedWhenNamed: false)));
        table.Place(SdtKind, RowKind, Unlisted(Children<StructuredDocumentTag, WordRow>("row", NodeType.Row, listedWhenNamed: false)));
        table.Place(SdtKind, CellKind, Unlisted(Children<StructuredDocumentTag, Cell>("cell", NodeType.Cell, listedWhenNamed: false)));
        table.Adds(SdtKind, ParagraphKind, ParagraphAdder<StructuredDocumentTag>());
        table.Adds(SdtKind, TableKind, TableAdder<StructuredDocumentTag>());
        table.Adds(SdtKind, RunKind, RunAdder<StructuredDocumentTag>());
        table.Adds(SdtKind, RowKind, RowAdder<StructuredDocumentTag>());
        table.Adds(SdtKind, CellKind, CellAdder<StructuredDocumentTag>());
    }

    private static Locator<TParent, T> Unlisted<TParent, T>(Locator<TParent, T> locator)
        where TParent : class
        where T : class => new()
        {
            Segment = locator.Segment,
            Key = locator.Key,
            Find = locator.Find,
            List = static _ => [],
            KeyOf = locator.KeyOf,
            Delete = locator.Delete,
        };

    /// <summary>
    /// The paragraphs, tables and content controls a node holds, listed in the engine's order: read
    /// lists paragraphs only when --kind names them, and tables and content controls wherever it
    /// walks, which is not below a cell, shape or paragraph unless --kind names a kind there.
    /// </summary>
    private static void PlaceBlocks<TParent>(DocumentTable<Document> table, ObjectKind<TParent> parent)
        where TParent : CompositeNode
    {
        table.Place(parent, ParagraphKind, Children<TParent, Paragraph>("paragraph", NodeType.Paragraph, listedWhenNamed: true));
        table.Place(parent, TableKind, Children<TParent, Table>("table", NodeType.Table, listedWhenNamed: false));
        table.Place(parent, SdtKind, Children<TParent, StructuredDocumentTag>("sdt", NodeType.StructuredDocumentTag, listedWhenNamed: false));
        table.Orders(parent, static (owner, child) => Position(owner, child));
    }

    private static void PlaceRange<TParent>(DocumentTable<Document> table, ObjectKind<TParent> parent)
        where TParent : Node =>
        table.Place(parent, RangeKind, new Locator<TParent, WordRange>
        {
            Segment = "range",
            Find = static (node, _) => node.Range,
            List = static _ => [],
            KeyOf = static (_, _) => Key.None,
        });

    /// <summary>What a paragraph holds.</summary>
    private static void PlaceInline(DocumentTable<Document> table)
    {
        table.Place(ParagraphKind, RunKind, Children<Paragraph, Run>("run", NodeType.Run, listedWhenNamed: true));
        table.Place(ParagraphKind, FieldKind, new Locator<Paragraph, Field>
        {
            Segment = "field",
            Key = [KeyPart.Index],
            Find = static (p, key) => FieldsOf(p).ElementAtOrDefault(key.Index()),
            List = FieldsOf,
            KeyOf = static (p, field) => Key.Of(FieldsOf(p).TakeWhile(candidate => candidate.Start != field.Start).Count()),
            Delete = static (_, field) => field.Remove(),
            ListedWhenNamed = true,
        });
        table.Place(ParagraphKind, ShapeKind, Children<Paragraph, Shape>("shape", NodeType.Shape, listedWhenNamed: true));
        table.Place(ParagraphKind, SdtKind, Children<Paragraph, StructuredDocumentTag>("sdt", NodeType.StructuredDocumentTag, listedWhenNamed: false));
        table.Orders(ParagraphKind, static (owner, child) => Position(owner, child));
        table.Adds(ParagraphKind, RunKind, RunAdder<Paragraph>());
        table.Adds(ParagraphKind, FieldKind, new Adder<Paragraph, Field>
        {
            Name = "field",
            Description = "Paragraph.AppendField(fieldCode), which updates the new field, or Paragraph.AppendField(fieldCode, fieldValue), whose result is fieldValue; the field ends the paragraph",
            Arguments =
            [
                Argument.Of("fieldCode", ValueKinds.Text, "The field's code, such as PAGE, TOC \\o \"1-3\" \\h or HYPERLINK \"https://example.com\"."),
                Argument.Optional("fieldValue", ValueKinds.Text, "The field's result as the document shows it until the field is updated."),
            ],
            Create = static (p, a) => a.Has("fieldValue")
                ? p.AppendField(a.Get<string>("fieldCode"), a.Get<string>("fieldValue"))
                : p.AppendField(a.Get<string>("fieldCode")),
        });
        table.Adds(ParagraphKind, ShapeKind, new Adder<Paragraph, Shape>
        {
            Name = "image",
            Description = "DocumentBuilder.MoveTo(paragraph), then DocumentBuilder.InsertImage(bytes): an inline picture at the image's own size, at the end of the paragraph",
            Arguments = [Argument.Of("image", ValueKinds.Image, "The image file the picture shows.")],
            Create = static (p, a) =>
            {
                var builder = new DocumentBuilder((Document)p.Document);
                builder.MoveTo(p);
                return builder.InsertImage(a.Get<byte[]>("image"));
            },
        });
    }

    /// <summary>
    /// The fields that start among a paragraph's own children: FieldStart.GetField() of each node of
    /// Paragraph.GetChildNodes(NodeType.FieldStart, false), in order. Paragraph.Range.Fields would
    /// also count the fields of the footnotes, comments and text boxes anchored in the paragraph,
    /// which their own paragraphs list.
    /// </summary>
    private static IEnumerable<Field> FieldsOf(Paragraph paragraph) =>
        paragraph.GetChildNodes(NodeType.FieldStart, false).Cast<FieldStart>().Select(static start => start.GetField());

    /// <summary>The rows and cells of tables, and the content controls around rows or cells.</summary>
    private static void PlaceTables(DocumentTable<Document> table)
    {
        table.Place(TableKind, RowKind, Children<Table, WordRow>("row", NodeType.Row, listedWhenNamed: true));
        table.Place(TableKind, SdtKind, Children<Table, StructuredDocumentTag>("sdt", NodeType.StructuredDocumentTag, listedWhenNamed: false));
        table.Orders(TableKind, static (owner, child) => Position(owner, child));
        table.Place(RowKind, CellKind, Children<WordRow, Cell>("cell", NodeType.Cell, listedWhenNamed: true));
        table.Place(RowKind, SdtKind, Children<WordRow, StructuredDocumentTag>("sdt", NodeType.StructuredDocumentTag, listedWhenNamed: false));
        table.Orders(RowKind, static (owner, child) => Position(owner, child));
        table.Adds(TableKind, RowKind, RowAdder<Table>());
        table.Adds(RowKind, CellKind, CellAdder<WordRow>());
    }

    /// <summary>What the document holds by name, id or in document order beside its sections.</summary>
    private static void PlaceDocumentObjects(DocumentTable<Document> table)
    {
        table.Place(DocumentKind, CommentKind, DeepChildren<Comment>("comment", NodeType.Comment));
        table.Place(DocumentKind, FootnoteKind, DeepChildren<Footnote>("footnote", NodeType.Footnote));
        table.Adds(DocumentKind, CommentKind, new Adder<Document, Comment>
        {
            Name = "comment",
            Description = "new Comment(document, author, initial, dateTime), then Paragraph.AppendChild(comment); it holds no paragraph",
            Arguments =
            [
                Argument.Address("paragraph", ParagraphKind, "The paragraph the comment is anchored at the end of."),
                Argument.Of("author", ValueKinds.Text, "Comment.Author."),
                Argument.Of("initial", ValueKinds.Text, "Comment.Initial."),
                Argument.Of("dateTime", WordsValues.DateTime, "Comment.DateTime."),
            ],
            Create = static (d, a) => a.Get<Paragraph>("paragraph").AppendChild(
                new Comment(d, a.Get<string>("author"), a.Get<string>("initial"), a.Get<DateTime>("dateTime"))),
        });
        table.Adds(DocumentKind, FootnoteKind, new Adder<Document, Footnote>
        {
            Name = "footnote",
            Description = "new Footnote(document, footnoteType), then Paragraph.AppendChild(footnote); it holds no paragraph",
            Arguments =
            [
                Argument.Address("paragraph", ParagraphKind, "The paragraph whose end the reference mark goes at."),
                Argument.Of("footnoteType", ValueKinds.Enum<FootnoteType>(), "The engine's FootnoteType."),
            ],
            Create = static (d, a) => a.Get<Paragraph>("paragraph").AppendChild(new Footnote(d, a.Get<FootnoteType>("footnoteType"))),
        });
        table.Place(DocumentKind, BookmarkKind, new Locator<Document, Bookmark>
        {
            Segment = "bookmark",
            Key = [KeyPart.Text],
            // The engine's bookmark names ignore case: a name two bookmarks share but for case matches both.
            FindAll = static (d, key) => [.. d.Range.Bookmarks.Where(bookmark => string.Equals(bookmark.Name, key.Name(), StringComparison.OrdinalIgnoreCase))],
            List = static d => d.Range.Bookmarks,
            KeyOf = static (_, bookmark) => Key.Of(bookmark.Name),
            Delete = static (_, bookmark) => bookmark.Remove(),
            ListedWhenNamed = true,
        });
        table.Adds(DocumentKind, BookmarkKind, new Adder<Document, Bookmark>
        {
            Name = "bookmark",
            Description = "new BookmarkStart(document, name) as the paragraph's first child and new BookmarkEnd(document, name) as its last",
            Arguments =
            [
                Argument.Address("paragraph", ParagraphKind, "The paragraph the bookmark spans."),
                Argument.Of("name", ValueKinds.Text, "The bookmark's name."),
            ],
            Create = static (d, a) =>
            {
                Paragraph paragraph = a.Get<Paragraph>("paragraph");
                string name = a.Get<string>("name");
                var start = new BookmarkStart(d, name);
                paragraph.PrependChild(start);
                paragraph.AppendChild(new BookmarkEnd(d, name));
                return start.Bookmark;
            },
        });
        table.Place(DocumentKind, StyleKind, new Locator<Document, Style>
        {
            Segment = "style",
            Key = [KeyPart.Text],
            FindAll = static (d, key) => [.. d.Styles.Where(style => string.Equals(style.Name, key.Name(), StringComparison.Ordinal))],
            List = static d => d.Styles,
            KeyOf = static (_, style) => Key.Of(style.Name),
            Delete = static (_, style) => style.Remove(),
            ListedWhenNamed = true,
        });
        table.Adds(DocumentKind, StyleKind, new Adder<Document, Style>
        {
            Name = "style",
            Description = "StyleCollection.Add(styleType, name)",
            Arguments =
            [
                Argument.Of("styleType", ValueKinds.Enum<StyleType>(), "The engine's StyleType."),
                Argument.Of("name", ValueKinds.Text, "The style's name."),
            ],
            Create = static (d, a) => d.Styles.Add(a.Get<StyleType>("styleType"), a.Get<string>("name")),
        });
        table.Place(DocumentKind, ListKind, new Locator<Document, WordList>
        {
            Segment = "list",
            Key = [KeyPart.Id],
            Find = static (d, key) => key.Id() <= int.MaxValue ? d.Lists.GetListByListId((int)key.Id()) : null,
            List = static d => d.Lists,
            KeyOf = static (_, list) => Key.Of((uint)list.ListId),
        });
        table.Adds(DocumentKind, ListKind, new Adder<Document, WordList>
        {
            Name = "list",
            Description = "ListCollection.Add(listTemplate)",
            Arguments = [Argument.Of("listTemplate", ValueKinds.Enum<ListTemplate>(), "The engine's ListTemplate.")],
            Create = static (d, a) => d.Lists.Add(a.Get<ListTemplate>("listTemplate")),
        });
        table.Place(ListKind, ListLevelKind, new Locator<WordList, ListLevel>
        {
            Segment = "level",
            Key = [KeyPart.Index],
            Find = static (l, key) => key.Index() < l.ListLevels.Count ? l.ListLevels[key.Index()] : null,
            List = static l => l.ListLevels,
            KeyOf = static (l, level) => Key.Of(l.ListLevels.TakeWhile(candidate => candidate != level).Count()),
            ListedWhenNamed = true,
        });
        table.Place(DocumentKind, RevisionKind, new Locator<Document, Revision>
        {
            Segment = "revision",
            Key = [KeyPart.Index],
            Find = static (d, key) => key.Index() < d.Revisions.Count ? d.Revisions[key.Index()] : null,
            List = static d => d.Revisions,
            KeyOf = static (d, revision) => Key.Of(d.Revisions.TakeWhile(candidate => candidate != revision).Count()),
            ListedWhenNamed = true,
        });
        table.Place(DocumentKind, CustomPropertyKind, new Locator<Document, DocumentProperty>
        {
            Segment = "customProperty",
            Key = [KeyPart.Text],
            FindAll = static (d, key) => [.. d.CustomDocumentProperties.Where(property => string.Equals(property.Name, key.Name(), StringComparison.OrdinalIgnoreCase))],
            List = static d => d.CustomDocumentProperties,
            KeyOf = static (_, property) => Key.Of(property.Name),
            Delete = static (d, property) => d.CustomDocumentProperties.Remove(property.Name),
        });
        table.Adds(DocumentKind, CustomPropertyKind, new Adder<Document, DocumentProperty>
        {
            Name = "customProperty",
            Description = "CustomDocumentProperties.Add(name, value), the overload of the value's type: string, bool, int or double",
            Arguments =
            [
                Argument.Of("name", ValueKinds.Text, "The property's name."),
                Argument.Of("value", WordsValues.PropertyValue, "The property's value."),
            ],
            Create = static (d, a) => WordsValues.AddProperty(d.CustomDocumentProperties, a.Get<string>("name"), a.Get<object>("value")),
        });
    }

    private static Adder<TParent, Paragraph> ParagraphAdder<TParent>()
        where TParent : CompositeNode => new()
        {
            Name = "paragraph",
            Description = "new Paragraph(document), then CompositeNode.InsertBefore(paragraph, before); it holds no run, and its style is the engine's default, Normal",
            Arguments = [Before([ParagraphKind, TableKind, SdtKind])],
            Create = static (parent, a) => parent.InsertBefore(new Paragraph(parent.Document), BeforeOf(a)),
        };

    private static Adder<TParent, Table> TableAdder<TParent>()
        where TParent : CompositeNode => new()
        {
            Name = "table",
            Description = "new Table(document), then CompositeNode.InsertBefore(table, before); it holds no row, and the engine keeps no empty table when it saves",
            Arguments = [Before([ParagraphKind, TableKind, SdtKind])],
            Create = static (parent, a) => parent.InsertBefore(new Table(parent.Document), BeforeOf(a)),
        };

    private static Adder<TParent, Run> RunAdder<TParent>()
        where TParent : CompositeNode => new()
        {
            Name = "run",
            Description = "new Run(document, text), then CompositeNode.InsertBefore(run, before); it takes the paragraph's formatting",
            Arguments =
            [
                Argument.Of("text", ValueKinds.Text, "The run's text."),
                Before([RunKind, ShapeKind, SdtKind]),
            ],
            Create = static (parent, a) => parent.InsertBefore(new Run(parent.Document, a.Get<string>("text")), BeforeOf(a)),
        };

    private static Adder<TParent, WordRow> RowAdder<TParent>()
        where TParent : CompositeNode => new()
        {
            Name = "row",
            Description = "new Row(document), then CompositeNode.InsertBefore(row, before); it holds no cell, and the engine keeps no row without cells when it saves",
            Arguments = [Before([RowKind, SdtKind])],
            Create = static (parent, a) => parent.InsertBefore(new WordRow(parent.Document), BeforeOf(a)),
        };

    private static Adder<TParent, Cell> CellAdder<TParent>()
        where TParent : CompositeNode => new()
        {
            Name = "cell",
            Description = "new Cell(document), then CompositeNode.InsertBefore(cell, before); it holds no paragraph",
            Arguments = [Before([CellKind, SdtKind])],
            Create = static (parent, a) => parent.InsertBefore(new Cell(parent.Document), BeforeOf(a)),
        };

    /// <summary>The before argument of an add: the child the new one goes before; not given, InsertBefore's refChild is null and it goes last.</summary>
    private static Argument Before(IReadOnlyList<ObjectKind> kinds) =>
        Argument.Address("before", kinds, "The child of in that the new object goes before; not given, it goes last (InsertBefore's refChild is null).", required: false);

    private static Node? BeforeOf(Arguments arguments) => arguments.Has("before") ? arguments.Get<Node>("before") : null;

    /// <summary>
    /// The objects of one node type among a node's children, GetChildNodes(type, false), by their
    /// position among those: the engine's collection, which holds the content of a content control
    /// as the parent's own.
    /// </summary>
    private static Locator<TParent, T> Children<TParent, T>(string segment, NodeType type, bool listedWhenNamed)
        where TParent : CompositeNode
        where T : Node => new()
        {
            Segment = segment,
            Key = [KeyPart.Index],
            Find = (parent, key) => parent.GetChildNodes(type, false)[key.Index()] as T,
            List = parent => parent.GetChildNodes(type, false).Cast<T>(),
            KeyOf = (parent, item) => Key.Of(parent.GetChildNodes(type, false).IndexOf(item)),
            Delete = static (_, item) => item.Remove(),
            ListedWhenNamed = listedWhenNamed,
        };

    /// <summary>The objects of one node type anywhere in the document, in document order.</summary>
    private static Locator<Document, T> DeepChildren<T>(string segment, NodeType type)
        where T : Node => new()
        {
            Segment = segment,
            Key = [KeyPart.Index],
            Find = (d, key) => d.GetChildNodes(type, true)[key.Index()] as T,
            List = d => d.GetChildNodes(type, true).Cast<T>(),
            KeyOf = (d, item) => Key.Of(d.GetChildNodes(type, true).IndexOf(item)),
            Delete = static (_, item) => item.Remove(),
        };

    /// <summary>
    /// Where a child sits among its parent's child nodes, by CompositeNode.IndexOf of the node that
    /// holds it there: the engine's collections hold the content of a content control as their
    /// own, so a paragraph in a content control sits just after the control, before its next
    /// sibling. A field sits where its FieldStart does.
    /// </summary>
    private static int Position(CompositeNode parent, object child)
    {
        Node? node = child switch
        {
            Field field => field.Start,
            Node own => own,
            _ => null,
        };
        Node? holder = node;
        while (holder is not null && holder.ParentNode != parent)
        {
            holder = holder.ParentNode;
        }

        return holder is null ? int.MaxValue : (2 * parent.IndexOf(holder)) + (holder == node ? 0 : 1);
    }
}
