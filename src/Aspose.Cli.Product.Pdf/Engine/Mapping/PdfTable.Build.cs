using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Tables;
using Aspose.Cli.Sdk.Text;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Text;
using PdfFont = Aspose.Pdf.Text.Font;

namespace Aspose.Cli.Product.Pdf.Engine.Mapping;

internal static partial class PdfTable
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
        PlacePages(table);
        PlaceBookmarks(table);
        PlaceDocumentObjects(table);
        PdfActions.Declare(table);
        table.SearchesWith(TextKind, "text", Search);
        table.Inventory(PdfInventory.Kinds, PdfInventory.Count);
        return table;
    }

    private static void PlacePages(DocumentTable<Document> table)
    {
        table.Place(DocumentKind, TaggedContentKind, new Locator<Document, Document>
        {
            Segment = "taggedContent",
            Find = static (d, _) => d,
            List = static _ => [],
            KeyOf = static (_, _) => Key.None,
        });
        table.Place(DocumentKind, PageKind, new Locator<Document, Page>
        {
            Segment = "page",
            Key = [KeyPart.Ordinal],
            Find = static (d, key) => key.Ordinal() <= d.Pages.Count ? d.Pages[key.Ordinal()] : null,
            // The engine's enumeration: in evaluation mode it gives the first pages only, while Pages.Count is the document's.
            List = static d => Each(d.Pages),
            KeyOf = static (_, page) => Key.Of(page.Number),
            Delete = static (d, page) => d.Pages.Delete(page.Number),
            ListedWhenNamed = true,
        });
        table.Adds(DocumentKind, PageKind, new Adder<Document, Page>
        {
            Name = "page",
            Description = "Document.Pages.Insert(pageNumber), a new empty page, or Pages.Insert(pageNumber, page), a copy of a page of this document; without pageNumber Pages.Add() or Pages.Add(page), at the end",
            Arguments =
            [
                Argument.Optional("pageNumber", ValueKinds.Integer, "The number, from 1, the new page has; the page there and those after it move one on. Not given, the page goes last."),
                Argument.Address("page", PageKind, "A page of this document the new page is a copy of; not given, the new page is empty, of the engine's default size.", required: false),
            ],
            Create = static (d, a) => (a.Has("pageNumber"), a.Has("page")) switch
            {
                (true, true) => d.Pages.Insert(a.Get<int>("pageNumber"), a.Get<Page>("page")),
                (true, false) => d.Pages.Insert(a.Get<int>("pageNumber")),
                (false, true) => d.Pages.Add(a.Get<Page>("page")),
                _ => d.Pages.Add(),
            },
        });
        table.Place(PageKind, TextKind, new Locator<Page, Page>
        {
            Segment = "text",
            Find = static (p, _) => p,
            List = static p => [p],
            KeyOf = static (_, _) => Key.None,
            ListedWhenNamed = true,
        });
        table.Place(PageKind, AreaKind, new Locator<Page, PdfArea>
        {
            Segment = "area",
            Key = [KeyPart.Text],
            Find = static (p, key) => PdfValues.ParseRectangle(key.Name()) is { } rect ? new PdfArea(p, rect) : null,
            List = static _ => [],
            KeyOf = static (_, area) => Key.Of(area.Rect.ToString()),
        });
        table.Place(PageKind, AnnotationKind, new Locator<Page, Annotation>
        {
            Segment = "annotation",
            Key = [KeyPart.Ordinal],
            Find = static (p, key) => key.Ordinal() <= p.Annotations.Count ? p.Annotations[key.Ordinal()] : null,
            List = static p => Each(p.Annotations),
            KeyOf = static (p, annotation) => Key.Of(Position(p.Annotations, annotation)),
            Delete = static (p, annotation) => p.Annotations.Delete(annotation),
            ListedWhenNamed = true,
        });
        table.Adds(PageKind, AnnotationKind, new Adder<Page, Annotation>
        {
            Name = "link",
            Description = "new LinkAnnotation(page, rect) { Action = new GoToURIAction(uri) }, then Page.Annotations.Add(link); it goes last among the page's annotations",
            Arguments =
            [
                Argument.Of("rect", PdfValues.Rectangle, "The link's rectangle, LLX,LLY,URX,URY in points."),
                Argument.Of("uri", ValueKinds.Text, "The address the link opens."),
            ],
            Create = static (p, a) =>
            {
                var link = new LinkAnnotation(p, a.Get<Rectangle?>("rect") ?? throw TableErrors.InvalidArguments("rect is LLX,LLY,URX,URY"))
                {
                    Action = new GoToURIAction(a.Get<string>("uri")),
                };
                p.Annotations.Add(link);
                // The collection gives its own object for the new link, the last one.
                return p.Annotations[p.Annotations.Count];
            },
        });
        table.Place(PageKind, FontKind, new Locator<Page, PdfFont>
        {
            Segment = "font",
            Key = [KeyPart.Ordinal],
            // A page that uses no font has no font collection: Resources.Fonts is null.
            Find = static (p, key) => p.Resources.Fonts is { } fonts && key.Ordinal() <= fonts.Count ? fonts[key.Ordinal()] : null,
            List = static p => p.Resources.Fonts is { } fonts ? Each(fonts) : [],
            KeyOf = static (p, font) => Key.Of(Position(p.Resources.Fonts!, font)),
            ListedWhenNamed = true,
        });
        table.Place(PageKind, LayerKind, new Locator<Page, Layer>
        {
            Segment = "layer",
            Key = [KeyPart.Text],
            FindAll = static (p, key) => [.. (p.Layers ?? []).Where(layer => string.Equals(layer.Id, key.Name(), StringComparison.Ordinal))],
            List = static p => p.Layers ?? [],
            KeyOf = static (_, layer) => Key.Of(layer.Id),
            ListedWhenNamed = true,
        });
    }

    private static void PlaceBookmarks(DocumentTable<Document> table)
    {
        table.Place(DocumentKind, BookmarkKind, new Locator<Document, OutlineItemCollection>
        {
            Segment = "bookmark",
            Key = [KeyPart.Ordinal],
            Find = static (d, key) => key.Ordinal() <= d.Outlines.Count ? d.Outlines[key.Ordinal()] : null,
            List = static d => Each(d.Outlines),
            KeyOf = static (_, item) => Key.Of(OutlinePosition(item)),
            Delete = static (d, item) => d.Outlines.Remove(item),
            ListedWhenNamed = true,
        });
        table.Place(BookmarkKind, BookmarkKind, new Locator<OutlineItemCollection, OutlineItemCollection>
        {
            Segment = "bookmark",
            Key = [KeyPart.Ordinal],
            Find = static (o, key) => key.Ordinal() <= o.Count ? o[key.Ordinal()] : null,
            List = static o => Each(o),
            KeyOf = static (_, item) => Key.Of(OutlinePosition(item)),
            Delete = static (o, item) => o.Remove(item),
            ListedWhenNamed = true,
        });
        table.Adds(DocumentKind, BookmarkKind, new Adder<Document, OutlineItemCollection>
        {
            Name = "bookmark",
            Description = BookmarkAdd + ", then Document.Outlines.Add(item); it goes last",
            Arguments = BookmarkArguments(),
            Create = static (d, a) =>
            {
                OutlineItemCollection item = NewBookmark(d.Outlines, a);
                d.Outlines.Add(item);
                return d.Outlines.Last;
            },
        });
        table.Adds(BookmarkKind, BookmarkKind, new Adder<OutlineItemCollection, OutlineItemCollection>
        {
            Name = "bookmark",
            Description = BookmarkAdd + ", then the parent's OutlineItemCollection.Add(item); it goes last",
            Arguments = BookmarkArguments(),
            Create = static (o, a) =>
            {
                o.Add(NewBookmark(RootOf(o), a));
                return o.Last;
            },
        });
    }

    private const string BookmarkAdd = "new OutlineItemCollection(Document.Outlines) { Title = title, Destination = new GoToAction(page) }, without a Destination when page is not given";

    private static IReadOnlyList<Argument> BookmarkArguments() =>
    [
        Argument.Of("title", ValueKinds.Text, "OutlineItemCollection.Title."),
        Argument.Address("page", PageKind, "The page the bookmark leads to, through new GoToAction(page).", required: false),
    ];

    private static OutlineItemCollection NewBookmark(OutlineCollection outlines, Arguments arguments)
    {
        var item = new OutlineItemCollection(outlines) { Title = arguments.Get<string>("title") };
        if (arguments.Has("page"))
        {
            item.Destination = new GoToAction(arguments.Get<Page>("page"));
        }

        return item;
    }

    /// <summary>The document's outline collection a bookmark belongs to, through its parents.</summary>
    private static OutlineCollection RootOf(OutlineItemCollection item)
    {
        Outlines? current = item;
        while (current is OutlineItemCollection child)
        {
            current = child.Parent;
        }

        return current as OutlineCollection ?? throw new InvalidOperationException("A bookmark's parents end in no outline collection.");
    }

    /// <summary>A bookmark's position among its siblings, from 1, counted along OutlineItemCollection.Prev.</summary>
    private static int OutlinePosition(OutlineItemCollection item)
    {
        int position = 1;
        for (OutlineItemCollection? previous = item.Prev; previous is not null; previous = previous.Prev)
        {
            position++;
        }

        return position;
    }

    private static void PlaceDocumentObjects(DocumentTable<Document> table)
    {
        table.Place(DocumentKind, FieldKind, new Locator<Document, Field>
        {
            Segment = "field",
            Key = [KeyPart.Text],
            FindAll = static (d, key) => [.. Fields(d).Where(field => string.Equals(field.FullName, key.Name(), StringComparison.Ordinal))],
            List = static d => Fields(d),
            KeyOf = static (_, field) => Key.Of(field.FullName),
            Delete = static (d, field) => d.Form.Delete(field),
            ListedWhenNamed = true,
        });
        table.Place(DocumentKind, AttachmentKind, new Locator<Document, FileSpecification>
        {
            Segment = "attachment",
            Key = [KeyPart.Ordinal],
            Find = static (d, key) => key.Ordinal() <= d.EmbeddedFiles.Count ? d.EmbeddedFiles[key.Ordinal()] : null,
            // The enumeration: EmbeddedFiles.Keys throws on a document without attachments (KNOWN-ISSUES PDF-ATTACHMENT-KEYS-EMPTY).
            List = static d => Each(d.EmbeddedFiles),
            KeyOf = static (d, file) => Key.Of(AttachmentPosition(d.EmbeddedFiles, file)),
            // Keys holds the name tree's keys in the enumeration's order; with an attachment present it reads.
            Delete = static (d, file) => d.EmbeddedFiles.DeleteByKey(d.EmbeddedFiles.Keys[AttachmentPosition(d.EmbeddedFiles, file) - 1]),
        });
        table.Adds(DocumentKind, AttachmentKind, new Adder<Document, FileSpecification>
        {
            Name = "attachment",
            Description = "Document.EmbeddedFiles.Add(new FileSpecification(stream, name, description)): the engine writes name to both Name and UnicodeName, and keys the file by its name, or by the name and a number when the name is taken",
            Arguments =
            [
                Argument.Of("file", ValueKinds.FileBytes, "The file to embed; its content is read under the command's input budget."),
                Argument.Of("name", ValueKinds.Text, "The name the file has in the document."),
                Argument.Optional("description", ValueKinds.Text, "FileSpecification.Description."),
            ],
            Create = static (d, a) =>
            {
                var contents = new MemoryStream(a.Get<byte[]>("file"));
                var file = a.Has("description")
                    ? new FileSpecification(contents, a.Get<string>("name"), a.Get<string>("description"))
                    : new FileSpecification(contents, a.Get<string>("name"));
                FileSpecification[] before = [.. Each(d.EmbeddedFiles)];
                d.EmbeddedFiles.Add(file);
                // The collection gives its own object for the file, which the one added does not equal.
                return Each(d.EmbeddedFiles).First(candidate => !before.Any(candidate.Equals));
            },
        });
        table.Place(DocumentKind, PageLabelKind, new Locator<Document, PdfPageLabel>
        {
            Segment = "pageLabel",
            Key = [KeyPart.Index],
            Find = static (d, key) => d.PageLabels.GetPages().Contains(key.Index()) ? new PdfPageLabel(d.PageLabels, key.Index()) : null,
            List = static d => d.PageLabels.GetPages().Select(index => new PdfPageLabel(d.PageLabels, index)),
            KeyOf = static (_, label) => Key.Of(label.PageIndex),
            Delete = static (_, label) => label.Labels.RemoveLabel(label.PageIndex),
        });
        table.Adds(DocumentKind, PageLabelKind, new Adder<Document, PdfPageLabel>
        {
            Name = "pageLabel",
            Description = "Document.PageLabels.UpdateLabel(pageIndex, new PageLabel()): a label of the engine's defaults that starts at the page, replacing one that starts there",
            Arguments = [Argument.Of("pageIndex", ValueKinds.Integer, "The engine's index of the page the label starts at, from 0.")],
            Create = static (d, a) =>
            {
                d.PageLabels.UpdateLabel(a.Get<int>("pageIndex"), new PageLabel());
                return new PdfPageLabel(d.PageLabels, a.Get<int>("pageIndex"));
            },
        });
        table.Place(DocumentKind, InfoKind, new Locator<Document, PdfInfoEntry>
        {
            Segment = "info",
            Key = [KeyPart.Text],
            Find = static (d, key) => d.Info.ContainsKey(key.Name()) && !DocumentInfo.IsPredefinedKey(key.Name()) ? new PdfInfoEntry(d.Info, key.Name()) : null,
            List = static d => d.Info.Keys.Where(key => !DocumentInfo.IsPredefinedKey(key)).Select(key => new PdfInfoEntry(d.Info, key)),
            KeyOf = static (_, entry) => Key.Of(entry.Key),
            Delete = static (_, entry) => entry.Info.Remove(entry.Key),
        });
        table.Adds(DocumentKind, InfoKind, new Adder<Document, PdfInfoEntry>
        {
            Name = "info",
            Description = "Document.Info.Add(key, value)",
            Arguments =
            [
                Argument.Of("key", ValueKinds.Text, "The entry's key."),
                Argument.Of("value", ValueKinds.Text, "The entry's value."),
            ],
            Create = static (d, a) =>
            {
                d.Info.Add(a.Get<string>("key"), a.Get<string>("value"));
                return new PdfInfoEntry(d.Info, a.Get<string>("key"));
            },
        });
        table.Place(DocumentKind, XmpKind, new Locator<Document, PdfXmpEntry>
        {
            Segment = "xmp",
            Key = [KeyPart.Text],
            Find = static (d, key) => d.Metadata.ContainsKey(key.Name()) ? new PdfXmpEntry(d.Metadata, key.Name()) : null,
            List = static d => d.Metadata.Keys.Select(key => new PdfXmpEntry(d.Metadata, key)),
            KeyOf = static (_, entry) => Key.Of(entry.Key),
        });
    }

    /// <summary>
    /// Every terminal field of the form, once: Document.Form's fields as the engine enumerates
    /// them, where a node the engine gives as the untyped Field, which holds no value of its own,
    /// stands for the fields its kids are, Field[1] to Field[Count], at any depth. A typed field,
    /// such as TextBoxField or RadioButtonField, is a terminal field with its controls.
    /// </summary>
    internal static IEnumerable<Field> Fields(Document document) => Terminal(Each(document.Form.Cast<Field>()));

    private static IEnumerable<Field> Terminal(IEnumerable<Field> fields)
    {
        foreach (Field field in fields)
        {
            Field[] kids = field.GetType() == typeof(Field) ? [.. Kids(field)] : [];
            if (kids.Length == 0)
            {
                yield return field;
                continue;
            }

            foreach (Field kid in Terminal(kids))
            {
                yield return kid;
            }
        }
    }

    private static IEnumerable<Field> Kids(Field field)
    {
        for (int index = 1; index <= field.Count; index++)
        {
            if (field[index] is Field kid && !ReferenceEquals(kid, field))
            {
                yield return kid;
            }
        }
    }

    /// <summary>
    /// The objects an engine collection's enumerator gives, one by one. Copying the collection
    /// would go through its Count, which in evaluation mode counts objects the enumerator does not give.
    /// </summary>
    internal static IEnumerable<T> Each<T>(IEnumerable<T> items)
    {
        foreach (T item in items)
        {
            yield return item;
        }
    }

    /// <summary>An object's position, from 1, in an engine collection that gives the same object each time.</summary>
    private static int Position<T>(IEnumerable<T> items, T item)
        where T : class
    {
        int position = 1;
        foreach (T candidate in items)
        {
            if (ReferenceEquals(candidate, item))
            {
                return position;
            }

            position++;
        }

        throw new InvalidOperationException("The object is not in its parent's collection.");
    }

    /// <summary>An attachment's position, from 1, in the engine's enumeration, which gives an equal object each time.</summary>
    private static int AttachmentPosition(EmbeddedFileCollection files, FileSpecification file)
    {
        int position = 1;
        foreach (FileSpecification candidate in files)
        {
            if (candidate.Equals(file))
            {
                return position;
            }

            position++;
        }

        throw new InvalidOperationException("The attachment is not among the document's.");
    }

    /// <summary>
    /// The engine's search of a page's text: Page.Accept(new TextFragmentAbsorber(regex, new
    /// TextSearchOptions(true))), the pattern escaped when it is literal; each fragment gives its
    /// text and its Rectangle.
    /// </summary>
    private static IEnumerable<TableMatch> Search(Page page, TextSearch query)
    {
        Regex regex = query.Expression ?? new Regex(
            Regex.Escape(query.Pattern),
            (query.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase) | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(5));
        var absorber = new TextFragmentAbsorber(regex, new TextSearchOptions(true));
        page.Accept(absorber);
        foreach (TextFragment fragment in absorber.TextFragments)
        {
            yield return new TableMatch(fragment.Text, fragment.Rectangle.ToString());
        }
    }
}
