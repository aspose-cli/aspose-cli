using System.Globalization;
using Aspose.Cli.Sdk.Tables;
using Aspose.Slides;
using Aspose.Slides.SlideShow;
using Row = Aspose.Cli.Sdk.Tables.Row;

namespace Aspose.Cli.Product.Slides.Engine.Mapping;

/// <summary>
/// The Slides object table: one name for each Aspose.Slides member a read gets and a set sets,
/// where each kind of object sits, and the engine calls that add, delete and move objects. Its
/// addresses use the engine's own ids (a slide's, a master's and a layout's <c>SlideId</c>, a
/// shape's <c>OfficeInteropShapeId</c>, a section's <c>SectionId</c>) and positions, from 0.
/// </summary>
internal static partial class SlidesTable
{
    /// <summary>The presentation itself, addressed as <c>presentation</c>.</summary>
    internal static readonly ObjectKind<Presentation> PresentationKind = new("presentation", "The presentation and its document properties.")
    {
        Row.Length<Presentation>("slideWidth", "Presentation.SlideSize.Size.Width", static p => p.SlideSize.Size.Width),
        Row.Length<Presentation>("slideHeight", "Presentation.SlideSize.Size.Height", static p => p.SlideSize.Size.Height),
        Row.Enum<Presentation, SlideSizeType>("slideSizeType", "Presentation.SlideSize.Type", static p => p.SlideSize.Type),
        Row.Text<Presentation>("title", "Presentation.DocumentProperties.Title", static p => p.DocumentProperties.Title, static (p, v) => p.DocumentProperties.Title = v),
        Row.Text<Presentation>("author", "Presentation.DocumentProperties.Author", static p => p.DocumentProperties.Author, static (p, v) => p.DocumentProperties.Author = v),
        Row.Text<Presentation>("subject", "Presentation.DocumentProperties.Subject", static p => p.DocumentProperties.Subject, static (p, v) => p.DocumentProperties.Subject = v),
        Row.Text<Presentation>("keywords", "Presentation.DocumentProperties.Keywords", static p => p.DocumentProperties.Keywords, static (p, v) => p.DocumentProperties.Keywords = v),
        Row.Text<Presentation>("company", "Presentation.DocumentProperties.Company", static p => p.DocumentProperties.Company, static (p, v) => p.DocumentProperties.Company = v),
    };

    /// <summary>A section, addressed by its <c>SectionId</c>: <c>section/9f1c…</c>.</summary>
    internal static readonly ObjectKind<ISection> SectionKind = new("section", "A section of the presentation.")
    {
        Row.Guid<ISection>("sectionId", "ISection.SectionId", static s => s.SectionId),
        Row.Text<ISection>("name", "ISection.Name", static s => s.Name),
        Row.Of<ISection, uint?>("startSlide", ValueKinds.Optional(ValueKinds.UnsignedInteger), "ISection.StartedFromSlide, as the slide's SlideId", static s => s.StartedFromSlide?.SlideId),
    };

    /// <summary>A master, addressed by its <c>SlideId</c>: <c>master/2147483648</c>.</summary>
    internal static readonly ObjectKind<IMasterSlide> MasterKind = new("master", "A master slide.")
    {
        Row.UnsignedInteger<IMasterSlide>("slideId", "IMasterSlide.SlideId", static m => m.SlideId),
        Row.Text<IMasterSlide>("name", "IMasterSlide.Name", static m => m.Name),
    };

    /// <summary>A layout of any master, addressed by its <c>SlideId</c>: <c>layout/2147483649</c>.</summary>
    internal static readonly ObjectKind<ILayoutSlide> LayoutKind = new("layout", "A layout slide of a master; a slide's layout row takes its SlideId.")
    {
        Row.UnsignedInteger<ILayoutSlide>("slideId", "ILayoutSlide.SlideId", static l => l.SlideId),
        Row.Text<ILayoutSlide>("name", "ILayoutSlide.Name", static l => l.Name),
        Row.Enum<ILayoutSlide, SlideLayoutType>("layoutType", "ILayoutSlide.LayoutType", static l => l.LayoutType),
    };

    /// <summary>A picture the presentation holds, addressed by its position: <c>image/0</c>.</summary>
    internal static readonly ObjectKind<IPPImage> ImageKind = new("image", "A picture of the presentation's image collection.")
    {
        Row.Text<IPPImage>("contentType", "IPPImage.ContentType", static i => i.ContentType),
        Row.Integer<IPPImage>("width", "IPPImage.Width, in pixels", static i => i.Width),
        Row.Integer<IPPImage>("height", "IPPImage.Height, in pixels", static i => i.Height),
    };

    /// <summary>A slide, addressed by its <c>SlideId</c>: <c>slide/256</c>.</summary>
    internal static readonly ObjectKind<ISlide> SlideKind = new("slide", "A slide of the presentation.")
    {
        Row.UnsignedInteger<ISlide>("slideId", "ISlide.SlideId", static s => s.SlideId),
        Row.Integer<ISlide>("index", "ISlideCollection.IndexOf(slide): the slide's position, from 0", static s => s.Presentation.Slides.IndexOf(s)),
        Row.Boolean<ISlide>("hidden", "ISlide.Hidden", static s => s.Hidden, static (s, v) => s.Hidden = v),
        Row.UnsignedInteger<ISlide>("layout", "ISlide.LayoutSlide, as the layout's SlideId", static s => s.LayoutSlide.SlideId, static (s, v) => s.LayoutSlide = SlidesTable.Layout(s.Presentation, v)),
        Row.Enum<ISlide, BackgroundType>("background.type", "ISlide.Background.Type", static s => s.Background.Type, static (s, v) => s.Background.Type = v),
        Fill<ISlide>("background", static s => s.Background.FillFormat),
        Row.Enum<ISlide, PictureFillMode>("background.pictureFillMode", "ISlide.Background.FillFormat.PictureFillFormat.PictureFillMode", static s => s.Background.FillFormat.PictureFillFormat.PictureFillMode, static (s, v) => s.Background.FillFormat.PictureFillFormat.PictureFillMode = v),
        Row.Of<ISlide, object?>("background.picture", SlidesValues.Picture, "ISlide.Background.FillFormat.PictureFillFormat.Picture.Image; set with IImageCollection.AddImage(bytes) first",
            static s => SlidesValues.PictureIndex(s.Presentation, s.Background.FillFormat.PictureFillFormat.Picture.Image),
            static (s, v) => s.Background.FillFormat.PictureFillFormat.Picture.Image = s.Presentation.Images.AddImage((byte[])v!)),
        Row.Enum<ISlide, TransitionType>("transition.type", "ISlide.SlideShowTransition.Type", static s => s.SlideShowTransition.Type, static (s, v) => s.SlideShowTransition.Type = v),
        Row.Integer<ISlide>("transition.duration", "ISlide.SlideShowTransition.Duration, in milliseconds", static s => s.SlideShowTransition.Duration, static (s, v) => s.SlideShowTransition.Duration = v),
        Row.Boolean<ISlide>("footerVisible", "ISlide.HeaderFooterManager.IsFooterVisible; set with SetFooterVisibility(bool)", static s => s.HeaderFooterManager.IsFooterVisible, static (s, v) => s.HeaderFooterManager.SetFooterVisibility(v)),
        Row.Boolean<ISlide>("slideNumberVisible", "ISlide.HeaderFooterManager.IsSlideNumberVisible; set with SetSlideNumberVisibility(bool)", static s => s.HeaderFooterManager.IsSlideNumberVisible, static (s, v) => s.HeaderFooterManager.SetSlideNumberVisibility(v)),
        Row.Boolean<ISlide>("dateTimeVisible", "ISlide.HeaderFooterManager.IsDateTimeVisible; set with SetDateTimeVisibility(bool)", static s => s.HeaderFooterManager.IsDateTimeVisible, static (s, v) => s.HeaderFooterManager.SetDateTimeVisibility(v)),
    };

    /// <summary>A slide's notes slide, addressed as <c>slide/256/notes</c>.</summary>
    internal static readonly ObjectKind<INotesSlide> NotesKind = new("notes", "The notes slide of a slide.")
    {
        Row.Text<INotesSlide>("text", "INotesSlide.NotesTextFrame.Text", static n => n.NotesTextFrame?.Text, static (n, v) => (n.NotesTextFrame ?? throw TableErrors.Absent("INotesSlide.NotesTextFrame")).Text = v),
    };

    /// <summary>The table, built on first use, once every kind is initialized.</summary>
    internal static DocumentTable<Presentation> Definition => Built.Value;

    private static class Built
    {
        internal static readonly DocumentTable<Presentation> Value = Build();
    }

    private static DocumentTable<Presentation> Build()
    {
        var table = new DocumentTable<Presentation>(PresentationKind);
        table.Place(PresentationKind, SectionKind, new Locator<Presentation, ISection>
        {
            Segment = "section",
            Key = [KeyPart.Guid],
            Find = static (p, key) => p.Sections.FirstOrDefault(section => section.SectionId == key.Guid()),
            List = static p => p.Sections,
            KeyOf = static (_, section) => Key.Of(section.SectionId),
            Delete = static (p, section) => p.Sections.RemoveSection(section),
        });
        table.Adds(PresentationKind, SectionKind, new Adder<Presentation, ISection>
        {
            Name = "section",
            Description = "ISectionCollection.AddSection(string name, ISlide startedFromSlide)",
            Arguments =
            [
                Argument.Of("name", ValueKinds.Text, "The new section's name."),
                Argument.Of("startedFromSlide", ValueKinds.UnsignedInteger, "The SlideId of the slide the section starts at."),
            ],
            Create = static (p, a) => p.Sections.AddSection(a.Get<string>("name"), Slide(p, a.Get<uint>("startedFromSlide"))),
        });
        table.Place(PresentationKind, MasterKind, new Locator<Presentation, IMasterSlide>
        {
            Segment = "master",
            Key = [KeyPart.Id],
            Find = static (p, key) => p.Masters.FirstOrDefault(master => master.SlideId == key.Id()),
            List = static p => p.Masters,
            KeyOf = static (_, master) => Key.Of(master.SlideId),
        });
        table.Place(PresentationKind, LayoutKind, new Locator<Presentation, ILayoutSlide>
        {
            Segment = "layout",
            Key = [KeyPart.Id],
            Find = static (p, key) => p.LayoutSlides.FirstOrDefault(layout => layout.SlideId == key.Id()),
            List = static p => p.LayoutSlides,
            KeyOf = static (_, layout) => Key.Of(layout.SlideId),
        });
        table.Place(PresentationKind, ImageKind, new Locator<Presentation, IPPImage>
        {
            Segment = "image",
            Key = [KeyPart.Index],
            Find = static (p, key) => key.Index() < p.Images.Count ? p.Images[key.Index()] : null,
            List = static p => p.Images,
            KeyOf = static (p, image) => Key.Of(Position(p.Images, image)),
        });
        table.Place(PresentationKind, SlideKind, new Locator<Presentation, ISlide>
        {
            Segment = "slide",
            Key = [KeyPart.Id],
            Find = static (p, key) => p.Slides.FirstOrDefault(slide => slide.SlideId == key.Id()),
            List = static p => p.Slides,
            KeyOf = static (_, slide) => Key.Of(slide.SlideId),
            Delete = static (p, slide) => p.Slides.Remove(slide),
            Move = static (p, slide, to) => p.Slides.Reorder(to, slide),
        });
        table.Adds(PresentationKind, SlideKind, new Adder<Presentation, ISlide>
        {
            Name = "slide",
            Description = "ISlideCollection.InsertEmptySlide(int index, ILayoutSlide layout)",
            Arguments =
            [
                Argument.Of("index", ValueKinds.Integer, "The new slide's position, the engine's, from 0; the slide count adds it at the end."),
                Argument.Of("layout", ValueKinds.UnsignedInteger, "The SlideId of the layout the new slide takes."),
            ],
            Create = static (p, a) => p.Slides.InsertEmptySlide(a.Get<int>("index"), Layout(p, a.Get<uint>("layout"))),
        });
        table.Place(SlideKind, NotesKind, new Locator<ISlide, INotesSlide>
        {
            Segment = "notes",
            Find = static (slide, _) => slide.NotesSlideManager.NotesSlide,
            List = static slide => slide.NotesSlideManager.NotesSlide is { } notes ? [notes] : [],
            KeyOf = static (_, _) => Key.None,
            Delete = static (slide, _) => slide.NotesSlideManager.RemoveNotesSlide(),
        });
        table.Adds(SlideKind, NotesKind, new Adder<ISlide, INotesSlide>
        {
            Name = "notes",
            Description = "INotesSlideManager.AddNotesSlide()",
            Create = static (slide, _) => slide.NotesSlideManager.AddNotesSlide(),
        });
        PlaceShapes(table);
        PlaceText(table);
        PlaceTables(table);
        PlaceCharts(table);
        SlidesActions.Declare(table);
        table.SearchesText(ShapeKind, "text");
        table.SearchesText(CellKind, "text");
        table.SearchesText(NotesKind, "text");
        table.SearchesText(TitleKind, "text");
        table.Inventory(SlidesInventory.Kinds, SlidesInventory.Count);
        return table;
    }

    /// <summary>The layout whose <c>SlideId</c> is <paramref name="id"/>, or <c>ADDRESS_NOT_FOUND</c>.</summary>
    internal static ILayoutSlide Layout(IPresentation presentation, uint id) =>
        presentation.LayoutSlides.FirstOrDefault(layout => layout.SlideId == id)
            ?? throw TableErrors.NotFound("layout", Id(id), [.. presentation.LayoutSlides.Select(static layout => Id(layout.SlideId))]);

    /// <summary>The slide whose <c>SlideId</c> is <paramref name="id"/>, or <c>ADDRESS_NOT_FOUND</c>.</summary>
    internal static ISlide Slide(IPresentation presentation, uint id) =>
        presentation.Slides.FirstOrDefault(slide => slide.SlideId == id)
            ?? throw TableErrors.NotFound("slide", Id(id), [.. presentation.Slides.Select(static slide => Id(slide.SlideId))]);

    /// <summary>The master whose <c>SlideId</c> is <paramref name="id"/>, or <c>ADDRESS_NOT_FOUND</c>.</summary>
    internal static IMasterSlide Master(IPresentation presentation, uint id) =>
        presentation.Masters.FirstOrDefault(master => master.SlideId == id)
            ?? throw TableErrors.NotFound("master", Id(id), [.. presentation.Masters.Select(static master => Id(master.SlideId))]);

    /// <summary>An id as an address writes it.</summary>
    internal static string Id(uint id) => id.ToString(CultureInfo.InvariantCulture);
}
