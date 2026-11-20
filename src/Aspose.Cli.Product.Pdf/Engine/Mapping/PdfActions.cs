using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Tables;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Optimization;
using Aspose.Pdf.Text;
using PdfColor = Aspose.Pdf.Color;

namespace Aspose.Cli.Product.Pdf.Engine.Mapping;

/// <summary>
/// The actions of the PDF table: each one engine call on the document, whose parameters the
/// caller states; a parameter not given is not set, so the engine's default holds.
/// </summary>
internal static class PdfActions
{
    internal static void Declare(DocumentTable<Document> table)
    {
        table.Action(new TableAction<Document>
        {
            Name = "stamp_text",
            Description = "Page.AddStamp(new TextStamp(value) { … }) with the members given; the engine's defaults are no alignment, indents and margins 0, opacity 1, Helvetica 10 in black. The read-back is the page's text",
            Arguments =
            [
                PageArgument("The page the stamp goes on."),
                Argument.Of("value", ValueKinds.Text, "The stamp's text, TextStamp.Value."),
                .. Placing,
                Argument.Optional("textState.font", ValueKinds.Text, "The font's name, set as TextState.Font = FontRepository.FindFont(name)."),
                Argument.Optional("textState.fontSize", ValueKinds.Single, "TextState.FontSize, in points."),
                Argument.Optional("textState.foregroundColor", PdfValues.Color, "TextState.ForegroundColor."),
            ],
            Run = static (_, a, _) =>
            {
                var stamp = new TextStamp(a.Get<string>("value"));
                Place(stamp, a);
                if (a.Has("textState.font"))
                {
                    stamp.TextState.Font = FontRepository.FindFont(a.Get<string>("textState.font"));
                }

                if (a.Has("textState.fontSize"))
                {
                    stamp.TextState.FontSize = a.Get<float>("textState.fontSize");
                }

                if (a.Has("textState.foregroundColor"))
                {
                    stamp.TextState.ForegroundColor = a.Get<PdfColor?>("textState.foregroundColor");
                }

                a.Get<Page>("page").AddStamp(stamp);
                return new ActionResult(null, $"{a.AddressOf("page")}/text");
            },
        });
        table.Action(new TableAction<Document>
        {
            Name = "stamp_image",
            Description = "Page.AddStamp(new ImageStamp(stream) { … }) with the members given; the engine's default is the image's own size at the page's lower-left corner. The read-back is the page",
            Arguments =
            [
                PageArgument("The page the stamp goes on."),
                Argument.Of("image", ValueKinds.Image, "The image the stamp shows."),
                .. Placing,
                Argument.Optional("width", ValueKinds.Number, "ImageStamp.Width, in points."),
                Argument.Optional("height", ValueKinds.Number, "ImageStamp.Height, in points."),
            ],
            Run = static (_, a, _) =>
            {
                var stamp = new ImageStamp(new MemoryStream(a.Get<byte[]>("image")));
                Place(stamp, a);
                if (a.Has("width"))
                {
                    stamp.Width = a.Get<double>("width");
                }

                if (a.Has("height"))
                {
                    stamp.Height = a.Get<double>("height");
                }

                a.Get<Page>("page").AddStamp(stamp);
                return new ActionResult(null, a.AddressOf("page"));
            },
        });
        table.Action(new TableAction<Document>
        {
            Name = "redact_area",
            Description = "Page.Annotations.Add(new RedactionAnnotation(page, rect) { … }), then RedactionAnnotation.Redact(): the engine removes what lies in the rectangle and draws the fill there; FillColor's default is black. The read-back is the text the engine now finds in the rectangle, page/<n>/area/<rect>",
            Arguments =
            [
                PageArgument("The page to redact."),
                Argument.Of("rect", PdfValues.Rectangle, "The area to redact, LLX,LLY,URX,URY in points, such as a search hit's rect."),
                Argument.Optional("fillColor", PdfValues.Color, "RedactionAnnotation.FillColor."),
                Argument.Optional("overlayText", ValueKinds.Text, "RedactionAnnotation.OverlayText: text drawn over the area."),
            ],
            Run = static (_, a, _) =>
            {
                Page page = a.Get<Page>("page");
                Rectangle rect = a.Get<Rectangle?>("rect") ?? throw TableErrors.InvalidArguments("rect is LLX,LLY,URX,URY");
                var redaction = new RedactionAnnotation(page, rect);
                if (a.Has("fillColor"))
                {
                    redaction.FillColor = a.Get<PdfColor?>("fillColor");
                }

                if (a.Has("overlayText"))
                {
                    redaction.OverlayText = a.Get<string>("overlayText");
                }

                page.Annotations.Add(redaction);
                redaction.Redact();
                return new ActionResult(null, $"{a.AddressOf("page")}/area/{TableAddress(rect.ToString())}");
            },
        });
        table.Action(new TableAction<Document>
        {
            Name = "flatten_form",
            Description = "Document.Form.Flatten(): every field's value becomes page content and the fields are gone",
            Run = static (d, _, _) =>
            {
                d.Form.Flatten();
                return new ActionResult(null);
            },
        });
        table.Action(new TableAction<Document>
        {
            Name = "flatten_field",
            Description = "Field.Flatten(): the field's value becomes page content and the field is gone",
            Arguments = [Argument.Address("field", PdfTable.FieldKind, "The field to flatten.")],
            Run = static (_, a, _) =>
            {
                a.Get<Field>("field").Flatten();
                return new ActionResult(null);
            },
        });
        table.Action(new TableAction<Document>
        {
            Name = "resize_page",
            Description = "Page.Resize(new PageSize(width, height)): the engine scales the page's content to the new size. The read-back is the page",
            Arguments =
            [
                PageArgument("The page to resize."),
                Argument.Of("width", ValueKinds.Single, "The new width, in points."),
                Argument.Of("height", ValueKinds.Single, "The new height, in points."),
            ],
            Run = static (_, a, _) =>
            {
                a.Get<Page>("page").Resize(new PageSize(a.Get<float>("width"), a.Get<float>("height")));
                return new ActionResult(null, a.AddressOf("page"));
            },
        });
        table.Action(new TableAction<Document>
        {
            Name = "import_pages",
            Description = "Document.Pages.Insert(pageNumber, pages) with pages of another PDF, or Pages.Add(pages) at the end when pageNumber is not given; it returns how many pages the document then has",
            Arguments =
            [
                Argument.Of("file", ValueKinds.File, "The PDF the pages come from; it may be the edited file itself, as it was before the edit."),
                Argument.Of("pages", ValueKinds.ListOf(ValueKinds.Integer), "The numbers of the pages to import, from 1, in the order they go in."),
                Argument.Optional("pageNumber", ValueKinds.Integer, "The number, from 1, the first imported page has; not given, they go last."),
                Argument.Optional("passwordEnv", ValueKinds.SecretVariable, "The environment variable that holds the password the file opens with."),
            ],
            Run = static (d, a, context) =>
            {
                Document source = context.Open(a.Get<string>("file"), a.Has("passwordEnv") ? a.Get<Secret>("passwordEnv") : null).Document;
                Page[] pages = [.. a.Get<IReadOnlyList<int>>("pages").Select(number => source.Pages[number])];
                if (a.Has("pageNumber"))
                {
                    d.Pages.Insert(a.Get<int>("pageNumber"), pages);
                }
                else
                {
                    d.Pages.Add(pages);
                }

                return new ActionResult(d.Pages.Count);
            },
        });
        table.Action(new TableAction<Document>
        {
            Name = "clear_info",
            Description = "Document.Info.Clear(): every entry of the document information",
            Run = static (d, _, _) =>
            {
                d.Info.Clear();
                return new ActionResult(null, "document");
            },
        });
        table.Action(new TableAction<Document>
        {
            Name = "clear_xmp",
            Description = "Document.Metadata.Clear(): every entry of the XMP metadata",
            Run = static (d, _, _) =>
            {
                d.Metadata.Clear();
                return new ActionResult(null);
            },
        });
        table.Action(new TableAction<Document>
        {
            Name = "encrypt",
            Description = "Document.Encrypt(userPassword, ownerPassword, permissions, cryptoAlgorithm), or the overload with usePdf20 when it is given; the output is saved, read back and reported with this encryption, so it is not given with --encrypt",
            Arguments =
            [
                Argument.Optional("userPasswordEnv", ValueKinds.SecretVariable, "The environment variable that holds the password the file opens with; not given, the user password is empty and the file opens without one."),
                Argument.Of("ownerPasswordEnv", ValueKinds.SecretVariable, "The environment variable that holds the owner password, which lifts the permissions."),
                Argument.Of("permissions", ValueKinds.ListOf(ValueKinds.Enum<Permissions>()), "The members of the engine's Permissions the file grants without the owner password, combined; an empty list grants none."),
                Argument.Of("cryptoAlgorithm", ValueKinds.Enum<CryptoAlgorithm>(), "The engine's CryptoAlgorithm."),
                Argument.Optional("usePdf20", ValueKinds.Boolean, "The usePdf20 parameter of the overload that takes it."),
            ],
            Run = static (d, a, context) =>
            {
                Secret? user = a.Has("userPasswordEnv") ? a.Get<Secret>("userPasswordEnv") : null;
                Permissions permissions = a.Get<IReadOnlyList<Permissions>>("permissions").Aggregate((Permissions)0, static (all, permission) => all | permission);
                string owner = a.Get<Secret>("ownerPasswordEnv").Reveal();
                CryptoAlgorithm algorithm = a.Get<CryptoAlgorithm>("cryptoAlgorithm");
                if (a.Has("usePdf20"))
                {
                    d.Encrypt(user?.Reveal() ?? string.Empty, owner, permissions, algorithm, a.Get<bool>("usePdf20"));
                }
                else
                {
                    d.Encrypt(user?.Reveal() ?? string.Empty, owner, permissions, algorithm);
                }

                context.SetOutputProtection(new OutputProtection.SetByTheDocument(user));
                return new ActionResult(null, "document");
            },
        });
        table.Action(new TableAction<Document>
        {
            Name = "decrypt",
            Description = "Document.Decrypt(): the output is saved, read back and reported without encryption, so it is not given with --encrypt",
            Run = static (d, _, context) =>
            {
                d.Decrypt();
                context.SetOutputProtection(OutputProtection.None);
                return new ActionResult(null, "document");
            },
        });
        table.Action(new TableAction<Document>
        {
            Name = "optimize",
            Description = "Document.OptimizeResources(new OptimizationOptions { … }) with the members given; the engine's defaults are false for every switch, MaxResoultion 6000 and the images' encoding unchanged",
            Arguments =
            [
                Switch("allowReusePageContent"),
                Switch("compressAllContentStreams"),
                Switch("compressObjects"),
                Switch("linkDuplicateStreams"),
                Switch("removePrivateInfo"),
                Switch("removeUnusedObjects"),
                Switch("removeUnusedStreams"),
                Switch("subsetFonts"),
                Switch("unembedFonts"),
                Argument.Optional("maxResoultion", ValueKinds.Integer, "OptimizationOptions.MaxResoultion, the engine's spelling."),
                Argument.Optional("imageEncoding", ValueKinds.Enum<ImageEncoding>(), "OptimizationOptions.ImageEncoding."),
                Argument.Optional("imageCompressionOptions.compressImages", ValueKinds.Boolean, "OptimizationOptions.ImageCompressionOptions.CompressImages."),
                Argument.Optional("imageCompressionOptions.imageQuality", ValueKinds.Integer, "OptimizationOptions.ImageCompressionOptions.ImageQuality, from 0 to 100."),
                Argument.Optional("imageCompressionOptions.resizeImages", ValueKinds.Boolean, "OptimizationOptions.ImageCompressionOptions.ResizeImages."),
                Argument.Optional("imageCompressionOptions.maxResolution", ValueKinds.Integer, "OptimizationOptions.ImageCompressionOptions.MaxResolution."),
                Argument.Optional("imageCompressionOptions.version", ValueKinds.Enum<ImageCompressionVersion>(), "OptimizationOptions.ImageCompressionOptions.Version."),
                Argument.Optional("imageCompressionOptions.encoding", ValueKinds.Enum<ImageEncoding>(), "OptimizationOptions.ImageCompressionOptions.Encoding."),
            ],
            Run = static (d, a, _) =>
            {
                var options = new OptimizationOptions();
                Set(a, "allowReusePageContent", (bool value) => options.AllowReusePageContent = value);
                Set(a, "compressAllContentStreams", (bool value) => options.CompressAllContentStreams = value);
                Set(a, "compressObjects", (bool value) => options.CompressObjects = value);
                Set(a, "linkDuplicateStreams", (bool value) => options.LinkDuplicateStreams = value);
                Set(a, "removePrivateInfo", (bool value) => options.RemovePrivateInfo = value);
                Set(a, "removeUnusedObjects", (bool value) => options.RemoveUnusedObjects = value);
                Set(a, "removeUnusedStreams", (bool value) => options.RemoveUnusedStreams = value);
                Set(a, "subsetFonts", (bool value) => options.SubsetFonts = value);
                Set(a, "unembedFonts", (bool value) => options.UnembedFonts = value);
                Set(a, "maxResoultion", (int value) => options.MaxResoultion = value);
                Set(a, "imageEncoding", (ImageEncoding value) => options.ImageEncoding = value);
                Set(a, "imageCompressionOptions.compressImages", (bool value) => options.ImageCompressionOptions.CompressImages = value);
                Set(a, "imageCompressionOptions.imageQuality", (int value) => options.ImageCompressionOptions.ImageQuality = value);
                Set(a, "imageCompressionOptions.resizeImages", (bool value) => options.ImageCompressionOptions.ResizeImages = value);
                Set(a, "imageCompressionOptions.maxResolution", (int value) => options.ImageCompressionOptions.MaxResolution = value);
                Set(a, "imageCompressionOptions.version", (ImageCompressionVersion value) => options.ImageCompressionOptions.Version = value);
                Set(a, "imageCompressionOptions.encoding", (ImageEncoding value) => options.ImageCompressionOptions.Encoding = value);
                d.OptimizeResources(options);
                return new ActionResult(null);
            },
        });
    }

    private static Argument PageArgument(string description) => Argument.Address("page", PdfTable.PageKind, description);

    private static Argument Switch(string name) =>
        Argument.Optional(name, ValueKinds.Boolean, $"OptimizationOptions.{char.ToUpperInvariant(name[0])}{name[1..]}.");

    /// <summary>The members of a stamp that place it, each set only when given.</summary>
    private static readonly IReadOnlyList<Argument> Placing =
    [
        Argument.Optional("horizontalAlignment", ValueKinds.Enum<HorizontalAlignment>(), "Stamp.HorizontalAlignment."),
        Argument.Optional("verticalAlignment", ValueKinds.Enum<VerticalAlignment>(), "Stamp.VerticalAlignment."),
        Argument.Optional("xIndent", ValueKinds.Number, "Stamp.XIndent, in points from the page's left edge."),
        Argument.Optional("yIndent", ValueKinds.Number, "Stamp.YIndent, in points from the page's bottom edge."),
        Argument.Optional("leftMargin", ValueKinds.Number, "Stamp.LeftMargin, in points."),
        Argument.Optional("rightMargin", ValueKinds.Number, "Stamp.RightMargin, in points."),
        Argument.Optional("topMargin", ValueKinds.Number, "Stamp.TopMargin, in points."),
        Argument.Optional("bottomMargin", ValueKinds.Number, "Stamp.BottomMargin, in points."),
        Argument.Optional("rotateAngle", ValueKinds.Number, "Stamp.RotateAngle, in degrees."),
        Argument.Optional("opacity", ValueKinds.Number, "Stamp.Opacity, from 0 to 1."),
        Argument.Optional("zoom", ValueKinds.Number, "Stamp.Zoom."),
        Argument.Optional("background", ValueKinds.Boolean, "Stamp.Background: true puts the stamp behind the page's content."),
    ];

    private static void Place(Stamp stamp, Arguments a)
    {
        Set(a, "horizontalAlignment", (HorizontalAlignment value) => stamp.HorizontalAlignment = value);
        Set(a, "verticalAlignment", (VerticalAlignment value) => stamp.VerticalAlignment = value);
        Set(a, "xIndent", (double value) => stamp.XIndent = value);
        Set(a, "yIndent", (double value) => stamp.YIndent = value);
        Set(a, "leftMargin", (double value) => stamp.LeftMargin = value);
        Set(a, "rightMargin", (double value) => stamp.RightMargin = value);
        Set(a, "topMargin", (double value) => stamp.TopMargin = value);
        Set(a, "bottomMargin", (double value) => stamp.BottomMargin = value);
        Set(a, "rotateAngle", (double value) => stamp.RotateAngle = value);
        Set(a, "opacity", (double value) => stamp.Opacity = value);
        Set(a, "zoom", (double value) => stamp.Zoom = value);
        Set(a, "background", (bool value) => stamp.Background = value);
    }

    private static void Set<T>(Arguments arguments, string name, Action<T> set)
    {
        if (arguments.Has(name))
        {
            set(arguments.Get<T>(name));
        }
    }

    /// <summary>A key part as an address writes it.</summary>
    private static string TableAddress(string key) => Key.Of(key).ToString();
}
