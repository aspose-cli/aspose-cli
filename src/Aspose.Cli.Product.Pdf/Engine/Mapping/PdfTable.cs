using Aspose.Cli.Sdk.Tables;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Text;
using PdfColor = Aspose.Pdf.Color;
using PdfFont = Aspose.Pdf.Text.Font;
using Row = Aspose.Cli.Sdk.Tables.Row;

namespace Aspose.Cli.Product.Pdf.Engine.Mapping;

/// <summary>
/// The PDF object table: one name for each Aspose.PDF member a read gets and a set sets, where each
/// kind of object sits, and the engine calls that add and delete objects. Positions are the
/// engine's: pages, annotations, fonts, bookmarks and attachments from 1, as Aspose.PDF's
/// collections count them; a page label by the engine's page index, from 0. Form fields are
/// addressed by their full names, layers by their ids.
/// </summary>
internal static partial class PdfTable
{
    /// <summary>The document itself, addressed as <c>document</c>.</summary>
    internal static readonly ObjectKind<Document> DocumentKind = new("document", "The document.")
    {
        Row.Integer<Document>("pages.count", "Document.Pages.Count", static d => d.Pages.Count),
        Row.Text<Document>("version", "Document.Version", static d => d.Version),
        Row.Enum<Document, PdfFormat>("pdfFormat", "Document.PdfFormat", static d => d.PdfFormat),
        Row.Boolean<Document>("isEncrypted", "Document.IsEncrypted; the encrypt and decrypt actions change it", static d => d.IsEncrypted),
        Row.Integer<Document>("permissions", "Document.Permissions: the bits of the file's permissions as the engine gives them", static d => d.Permissions),
        Row.Boolean<Document>("isLinearized", "Document.IsLinearized", static d => d.IsLinearized),
        Row.Boolean<Document>("form.hasXfa", "Document.Form.HasXfa", static d => d.Form.HasXfa),
        Row.Enum<Document, FormType>("form.type", "Document.Form.Type", static d => d.Form.Type),
        Row.Text<Document>("info.title", "Document.Info.Title", static d => d.Info.Title, static (d, v) => d.Info.Title = v),
        Row.Text<Document>("info.author", "Document.Info.Author", static d => d.Info.Author, static (d, v) => d.Info.Author = v),
        Row.Text<Document>("info.subject", "Document.Info.Subject", static d => d.Info.Subject, static (d, v) => d.Info.Subject = v),
        Row.Text<Document>("info.keywords", "Document.Info.Keywords", static d => d.Info.Keywords, static (d, v) => d.Info.Keywords = v),
        Row.Text<Document>("info.creator", "Document.Info.Creator", static d => d.Info.Creator, static (d, v) => d.Info.Creator = v),
        Row.Text<Document>("info.producer", "Document.Info.Producer: the engine writes its own when it saves", static d => d.Info.Producer),
        Row.Text<Document>("info.creationDate", "Document.Info[\"CreationDate\"]: the date as the file holds it, such as D:20260515093000+08'00'; null when the file holds none", static d => Entry(d.Info, "CreationDate")),
        Row.Text<Document>("info.modDate", "Document.Info[\"ModDate\"]: the date as the file holds it; the engine writes its own when it saves", static d => Entry(d.Info, "ModDate")),
    };

    /// <summary>The document's structure tree: <c>taggedContent</c>, read only by --at.</summary>
    internal static readonly ObjectKind<Document> TaggedContentKind = new("taggedContent", "The document's logical structure, Document.TaggedContent; read does not list it, --at taggedContent reads it. Reading it makes the engine change the document in memory (its title, its PDF format and, when saved, its XMP), so nothing else reads it.")
    {
        Row.Integer<Document>("rootElement.childElements.count", "Document.TaggedContent.RootElement.ChildElements.Count: 0 for a document without a structure tree", static d => d.TaggedContent.RootElement.ChildElements.Count),
    };

    /// <summary>A page: <c>page/1</c>.</summary>
    internal static readonly ObjectKind<Page> PageKind = new("page", "A page: Document.Pages[n], from 1, which is Page.Number.")
    {
        Row.Enum<Page, Rotation>("rotate", "Page.Rotate", static p => p.Rotate, static (p, v) => p.Rotate = v),
        Row.Of<Page, Rectangle?>("mediaBox", PdfValues.Rectangle, "Page.MediaBox", static p => p.MediaBox, static (p, v) => p.MediaBox = Required(v, "mediaBox")),
        Row.Of<Page, Rectangle?>("cropBox", PdfValues.Rectangle, "Page.CropBox", static p => p.CropBox, static (p, v) => p.CropBox = Required(v, "cropBox")),
    };

    /// <summary>The text of a page: <c>page/1/text</c>.</summary>
    internal static readonly ObjectKind<Page> TextKind = new("text", "The text of a page, as the engine extracts it; read lists it only when --kind names it, and search looks in it with the engine's TextFragmentAbsorber.")
    {
        Row.Text<Page>("text", "TextAbsorber.Text after Page.Accept(new TextAbsorber()): the engine's default extraction, TextExtractionOptions.FormattingMode Pure", static p => PageText(p, null)),
    };

    /// <summary>The text of an area of a page: <c>page/1/area/150,700,258,713.2</c>.</summary>
    internal static readonly ObjectKind<PdfArea> AreaKind = new("area", "The text in an area of a page, keyed by the engine's Rectangle LLX,LLY,URX,URY in points; read does not list areas, --at reads one, and redact_area reports the one it redacted.")
    {
        Row.Of<PdfArea, Rectangle?>("rect", PdfValues.Rectangle, "The Rectangle the key names", static a => a.Rect),
        Row.Text<PdfArea>("text", "TextAbsorber.Text after Page.Accept(new TextAbsorber(new TextSearchOptions(rect))): the text the engine finds in the rectangle", static a => PageText(a.Page, a.Rect)),
    };

    /// <summary>An annotation of a page, of any subtype: <c>page/1/annotation/2</c>.</summary>
    internal static readonly ObjectKind<Annotation> AnnotationKind = new("annotation", "An annotation of a page: Page.Annotations[k], from 1, of any subtype, links and the widgets of form fields included. A widget is the field's control on the page; its value is the field's, at field/<name>.")
    {
        Row.Enum<Annotation, AnnotationType>("annotationType", "Annotation.AnnotationType", static a => a.AnnotationType),
        Row.Of<Annotation, Rectangle?>("rect", PdfValues.Rectangle, "Annotation.Rect", static a => a.Rect, static (a, v) => a.Rect = Required(v, "rect")),
        Row.Text<Annotation>("contents", "Annotation.Contents", static a => a.Contents, static (a, v) => a.Contents = v),
        Row.Text<Annotation>("name", "Annotation.Name", static a => a.Name, static (a, v) => a.Name = v),
        Row.Of<Annotation, PdfColor?>("color", PdfValues.Color, "Annotation.Color", static a => a.Color, static (a, v) => a.Color = v),
        Row.Text<WidgetAnnotation>("fullName", "Annotation.FullName: the full name of the field the widget belongs to", static a => a.FullName),
        Row.Text<LinkAnnotation>("action.uri", "((GoToURIAction)LinkAnnotation.Action).URI; null for a link whose action is no GoToURIAction", static a => (a.Action as GoToURIAction)?.URI, static (a, v) => (a.Action as GoToURIAction ?? throw TableErrors.Absent("LinkAnnotation.Action as GoToURIAction")).URI = v),
        Row.Of<LinkAnnotation, int?>("destinationPage", ValueKinds.Optional(ValueKinds.Integer), "ExplicitDestination.PageNumber of LinkAnnotation.Destination, or of the GoToAction's Destination; null for a link that leads to no page of this document", static a => DestinationPage(a.Destination) ?? DestinationPage((a.Action as GoToAction)?.Destination)),
        Row.Text<LinkAnnotation>("action.file", "((GoToRemoteAction)LinkAnnotation.Action).File.Name: the other document a link opens; null otherwise", static a => (a.Action as GoToRemoteAction)?.File?.Name),
        Row.Text<MarkupAnnotation>("title", "MarkupAnnotation.Title", static a => a.Title, static (a, v) => a.Title = v),
        Row.Text<MarkupAnnotation>("subject", "MarkupAnnotation.Subject", static a => a.Subject, static (a, v) => a.Subject = v),
        Row.Text<FileAttachmentAnnotation>("file.name", "FileAttachmentAnnotation.File.Name", static a => a.File?.Name),
    };

    /// <summary>A font of a page's resources: <c>page/1/font/1</c>.</summary>
    internal static readonly ObjectKind<PdfFont> FontKind = new("font", "A font of a page's resources: Page.Resources.Fonts[k], from 1. Fonts used inside form XObjects are not among them.")
    {
        Row.Text<PdfFont>("fontName", "Font.FontName", static f => f.FontName),
        Row.Text<PdfFont>("baseFont", "Font.BaseFont", static f => f.BaseFont),
        Row.Boolean<PdfFont>("isEmbedded", "Font.IsEmbedded", static f => f.IsEmbedded),
        Row.Boolean<PdfFont>("isSubset", "Font.IsSubset", static f => f.IsSubset),
        Row.Boolean<PdfFont>("isAccessible", "Font.IsAccessible", static f => f.IsAccessible),
    };

    /// <summary>A layer of a page: <c>page/1/layer/oc1</c>.</summary>
    internal static readonly ObjectKind<Layer> LayerKind = new("layer", "An optional content layer of a page: one of Page.Layers, by its Layer.Id.")
    {
        Row.Text<Layer>("name", "Layer.Name", static l => l.Name),
        Row.Boolean<Layer>("locked", "Layer.Locked", static l => l.Locked),
    };

    /// <summary>A bookmark: <c>bookmark/1</c>, <c>bookmark/1/bookmark/2</c>.</summary>
    internal static readonly ObjectKind<OutlineItemCollection> BookmarkKind = new("bookmark", "A bookmark, an outline item: Document.Outlines[i] or the parent bookmark's [i], from 1. The engine gives a new object each time, so a bookmark's position is counted along OutlineItemCollection.Prev.")
    {
        Row.Text<OutlineItemCollection>("title", "OutlineItemCollection.Title", static o => o.Title, static (o, v) => o.Title = v),
        Row.Boolean<OutlineItemCollection>("bold", "OutlineItemCollection.Bold", static o => o.Bold, static (o, v) => o.Bold = v),
        Row.Boolean<OutlineItemCollection>("italic", "OutlineItemCollection.Italic", static o => o.Italic, static (o, v) => o.Italic = v),
        Row.Boolean<OutlineItemCollection>("open", "OutlineItemCollection.Open", static o => o.Open, static (o, v) => o.Open = v),
        Row.Color<OutlineItemCollection>("color", "OutlineItemCollection.Color", static o => o.Color, static (o, v) => o.Color = v),
        Row.Integer<OutlineItemCollection>("level", "OutlineItemCollection.Level, from 1", static o => o.Level),
        Row.Of<OutlineItemCollection, int?>("destinationPage", ValueKinds.Optional(ValueKinds.Integer), "ExplicitDestination.PageNumber of OutlineItemCollection.Destination, of the GoToAction it is, or of its Action's; null for a bookmark that leads to no page of this document",
            static o => DestinationPage(o.Destination) ?? DestinationPage((o.Destination as GoToAction)?.Destination) ?? DestinationPage((o.Action as GoToAction)?.Destination)),
    };

    /// <summary>A form field: <c>field/applicant</c>.</summary>
    internal static readonly ObjectKind<Field> FieldKind = new("field", "A form field, by its full name: each terminal field of Document.Form, the same object as Form[fullName]; a node the engine gives as the untyped Field, which holds no value, is listed as the fields beneath it, so field/form.person.name is a field of a hierarchical form. A group of radio buttons is one field, RadioButtonField; each button, like each control of any field, is also an annotation of its page, of type Widget.")
    {
        Row.Text<Field>("fieldType", "The engine type of the field, such as TextBoxField or RadioButtonField", static f => f.GetType().Name),
        Row.Text<Field>("value", "Field.Value", static f => f.Value, static (f, v) => f.Value = v),
        Row.Text<Field>("partialName", "Field.PartialName", static f => f.PartialName, static (f, v) => f.PartialName = v),
        Row.Text<Field>("alternateName", "Field.AlternateName", static f => f.AlternateName, static (f, v) => f.AlternateName = v),
        Row.Boolean<Field>("readOnly", "Field.ReadOnly", static f => f.ReadOnly, static (f, v) => f.ReadOnly = v),
        Row.Boolean<Field>("required", "Field.Required", static f => f.Required, static (f, v) => f.Required = v),
        Row.Integer<Field>("pageIndex", "Field.PageIndex: the page of its first control, from 1", static f => f.PageIndex),
        Row.Of<Field, Rectangle?>("rect", PdfValues.Rectangle, "Field.Rect", static f => f.Rect),
        Row.Integer<Field>("count", "Field.Count: the controls the field holds as kids; 0 for a field that is its own one control", static f => f.Count),
        Row.Boolean<CheckboxField>("checked", "CheckboxField.Checked", static f => f.Checked, static (f, v) => f.Checked = v),
        Row.Of<CheckboxField, IReadOnlyList<string?>>("allowedStates", ValueKinds.ListOf(ValueKinds.Text), "CheckboxField.AllowedStates", static f => [.. f.AllowedStates]),
        Row.Text<CheckboxField>("exportValue", "CheckboxField.ExportValue", static f => f.ExportValue),
        Row.Of<ChoiceField, OptionCollection>("options", PdfValues.Options, "ChoiceField.Options, from 1 in the engine's order", static f => f.Options),
        Row.Integer<ChoiceField>("selected", "ChoiceField.Selected: the selected option's position, from 1; for a radio group the engine gives -1 when none is selected", static f => f.Selected),
        Row.Boolean<ChoiceField>("multiSelect", "ChoiceField.MultiSelect", static f => f.MultiSelect),
        Row.Boolean<SignatureField>("signature.exists", "SignatureField.Signature != null: whether the field is signed", static f => f.Signature is not null),
        Row.Text<SignatureField>("signature.reason", "SignatureField.Signature.Reason; null for an unsigned field", static f => f.Signature?.Reason),
        Row.Text<SignatureField>("signature.location", "SignatureField.Signature.Location", static f => f.Signature?.Location),
        Row.Text<SignatureField>("signature.contactInfo", "SignatureField.Signature.ContactInfo", static f => f.Signature?.ContactInfo),
        Row.Text<SignatureField>("signature.authority", "SignatureField.Signature.Authority", static f => f.Signature?.Authority),
        Row.Of<SignatureField, DateTime?>("signature.date", PdfValues.DateTime, "SignatureField.Signature.Date", static f => f.Signature?.Date),
        Row.Of<SignatureField, bool?>("signature.verify", ValueKinds.Optional(ValueKinds.Boolean), "SignatureField.Signature.Verify(): whether the engine verifies the signature over the bytes it signed", static f => f.Signature?.Verify()),
    };

    /// <summary>An attachment: <c>attachment/1</c>.</summary>
    internal static readonly ObjectKind<FileSpecification> AttachmentKind = new("attachment", "A file embedded in the document: Document.EmbeddedFiles[i], from 1, in the engine's order, which is the order of the keys of the document's name tree; adding or deleting one can change the positions of the others.")
    {
        Row.Text<FileSpecification>("name", "FileSpecification.Name", static f => f.Name),
        Row.Text<FileSpecification>("unicodeName", "FileSpecification.UnicodeName", static f => f.UnicodeName),
        Row.Text<FileSpecification>("description", "FileSpecification.Description", static f => f.Description, static (f, v) => f.Description = v),
        Row.Text<FileSpecification>("mimeType", "FileSpecification.MIMEType", static f => f.MIMEType, static (f, v) => f.MIMEType = v),
        Row.Of<FileSpecification, int?>("params.size", ValueKinds.Optional(ValueKinds.Integer), "FileSpecification.Params.Size: the size the file records, in bytes; null when it records none", static f => f.Params?.Size),
    };

    /// <summary>A page label range: <c>pageLabel/0</c>.</summary>
    internal static readonly ObjectKind<PdfPageLabel> PageLabelKind = new("pageLabel", "The page label that starts at a page: Document.PageLabels.GetLabel(pageIndex), keyed by the engine's page index, from 0 (pageLabel/0 starts at page/1). The engine gives a copy, so a set takes it, sets the one member and writes it back with PageLabels.UpdateLabel(pageIndex, label).")
    {
        Row.Text<PdfPageLabel>("prefix", "PageLabel.Prefix", static l => l.Label.Prefix, static (l, v) => l.Update(label => label.Prefix = v)),
        Row.Integer<PdfPageLabel>("startingValue", "PageLabel.StartingValue", static l => l.Label.StartingValue, static (l, v) => l.Update(label => label.StartingValue = v)),
        Row.Enum<PdfPageLabel, NumberingStyle>("numberingStyle", "PageLabel.NumberingStyle", static l => l.Label.NumberingStyle, static (l, v) => l.Update(label => label.NumberingStyle = v)),
    };

    /// <summary>An entry of the document information that is no predefined one: <c>info/Department</c>.</summary>
    internal static readonly ObjectKind<PdfInfoEntry> InfoKind = new("info", "An entry of the document information, Document.Info[key], whose key is no predefined one (DocumentInfo.IsPredefinedKey); the predefined ones are rows of the document.")
    {
        Row.Text<PdfInfoEntry>("value", "Document.Info[key]", static e => e.Info[e.Key], static (e, v) => e.Info[e.Key] = v),
    };

    /// <summary>An XMP metadata entry: <c>xmp/dc:title</c>.</summary>
    internal static readonly ObjectKind<PdfXmpEntry> XmpKind = new("xmp", "An entry of the document's XMP metadata, Document.Metadata[key], such as dc:title.")
    {
        Row.Text<PdfXmpEntry>("value", "Document.Metadata[key].ToString()", static e => e.Metadata[e.Key]?.ToString()),
    };

    private static string? Entry(DocumentInfo info, string key) => info.TryGetValue(key, out string? value) ? value : null;

    private static int? DestinationPage(IAppointment? destination) => destination is ExplicitDestination explicitDestination ? explicitDestination.PageNumber : null;

    private static Rectangle Required(Rectangle? value, string row) =>
        value ?? throw TableErrors.InvalidArguments($"{row} is set to a rectangle LLX,LLY,URX,URY");

    /// <summary>The text the engine's TextAbsorber gives for a page, or for a rectangle of it.</summary>
    internal static string PageText(Page page, Rectangle? rect)
    {
        TextAbsorber absorber = rect is null ? new TextAbsorber() : new TextAbsorber(new TextSearchOptions(rect));
        page.Accept(absorber);
        return absorber.Text;
    }
}

/// <summary>An area of a page, keyed by its rectangle; the engine has no object for it.</summary>
internal sealed record PdfArea(Page Page, Rectangle Rect);

/// <summary>A page label: the engine's page label collection and the page index the label starts at, which is all the engine names a label by.</summary>
internal sealed record PdfPageLabel(PageLabelCollection Labels, int PageIndex)
{
    /// <summary>The engine's copy of the label.</summary>
    public PageLabel Label => Labels.GetLabel(PageIndex);

    /// <summary>Sets one member of a copy of the label and writes the copy back.</summary>
    public void Update(Action<PageLabel> set)
    {
        PageLabel label = Label;
        set(label);
        Labels.UpdateLabel(PageIndex, label);
    }
}

/// <summary>An entry of the document information, by its key.</summary>
internal sealed record PdfInfoEntry(DocumentInfo Info, string Key);

/// <summary>An entry of the XMP metadata, by its key.</summary>
internal sealed record PdfXmpEntry(Metadata Metadata, string Key);
