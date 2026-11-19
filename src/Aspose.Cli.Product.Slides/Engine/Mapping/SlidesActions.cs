using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Tables;
using Aspose.Slides;

namespace Aspose.Cli.Product.Slides.Engine.Mapping;

/// <summary>The Slides actions: one name for one engine call on the presentation, whose parameters the caller states.</summary>
internal static class SlidesActions
{
    internal static void Declare(DocumentTable<Presentation> table)
    {
        table.Action(new TableAction<Presentation>
        {
            Name = "copy_slide",
            Description = "ISlideCollection.InsertClone(int index, ISlide sourceSlide); returns the new slide's address.",
            Arguments =
            [
                Argument.Of("sourceSlide", ValueKinds.UnsignedInteger, "The SlideId of the slide to copy."),
                Argument.Of("index", ValueKinds.Integer, "The copy's position, the engine's, from 0; the slide count adds it at the end."),
            ],
            Run = static (p, a, _) => Returned(p.Slides.InsertClone(a.Get<int>("index"), SlidesTable.Slide(p, a.Get<uint>("sourceSlide")))),
        });
        table.Action(new TableAction<Presentation>
        {
            Name = "import_slide",
            Description = "ISlideCollection.AddClone(ISlide sourceSlide), or AddClone(ISlide sourceSlide, IMasterSlide destMaster, bool allowCloneMissingLayout) when master is given; the source slide is in another presentation. Returns the new slide's address.",
            Arguments =
            [
                Argument.Of("file", ValueKinds.File, "The presentation the slide is copied from."),
                Argument.Of("sourceSlide", ValueKinds.UnsignedInteger, "The SlideId of the slide in that presentation."),
                Argument.Optional("passwordEnv", ValueKinds.SecretVariable, "The environment variable that holds the password of that presentation."),
                Argument.Optional("master", ValueKinds.UnsignedInteger, "The SlideId of the master of this presentation the copy takes."),
                Argument.Optional("allowCloneMissingLayout", ValueKinds.Boolean, "With master: whether the engine may copy a layout the master lacks."),
            ],
            Run = static (p, a, context) =>
            {
                if (a.Has("master") != a.Has("allowCloneMissingLayout"))
                {
                    throw TableErrors.InvalidArguments("import_slide takes args.master and args.allowCloneMissingLayout together, or neither");
                }

                IOpenedDocument<Presentation> source = context.Open(a.Get<string>("file"), a.Has("passwordEnv") ? a.Get<Secret>("passwordEnv") : null);
                ISlide slide = SlidesTable.Slide(source.Document, a.Get<uint>("sourceSlide"));
                return Returned(a.Has("master")
                    ? p.Slides.AddClone(slide, SlidesTable.Master(p, a.Get<uint>("master")), a.Get<bool>("allowCloneMissingLayout"))
                    : p.Slides.AddClone(slide));
            },
        });
        table.Action(new TableAction<Presentation>
        {
            Name = "set_slide_size",
            Description = "ISlideSize.SetSize(float width, float height, SlideSizeScaleType scaleType), or SetSize(SlideSizeType type, SlideSizeScaleType scaleType) when type is given; returns nothing.",
            Arguments =
            [
                Argument.Optional("width", ValueKinds.Length, "The slide width, in points; with height."),
                Argument.Optional("height", ValueKinds.Length, "The slide height, in points; with width."),
                Argument.Optional("type", ValueKinds.Enum<SlideSizeType>(), "The engine's SlideSizeType; instead of width and height."),
                Argument.Of("scaleType", ValueKinds.Enum<SlideSizeScaleType>(), "How the engine scales the content: DoNotScale, EnsureFit or Maximize."),
            ],
            Run = static (p, a, _) =>
            {
                SlideSizeScaleType scale = a.Get<SlideSizeScaleType>("scaleType");
                if (a.Has("type") && !a.Has("width") && !a.Has("height"))
                {
                    p.SlideSize.SetSize(a.Get<SlideSizeType>("type"), scale);
                }
                else if (!a.Has("type") && a.Has("width") && a.Has("height"))
                {
                    p.SlideSize.SetSize(a.Get<float>("width"), a.Get<float>("height"), scale);
                }
                else
                {
                    throw TableErrors.InvalidArguments("set_slide_size takes args.type, or args.width and args.height");
                }

                return new ActionResult(null);
            },
        });
        table.Action(new TableAction<Presentation>
        {
            Name = "replace_text",
            Description = "Presentation.ReplaceText(string oldText, string newText, ITextSearchOptions options, IFindResultCallback callback), with new TextSearchOptions() and the options given set on it; returns each match the callback reported: its text frame's address, the text found and its position.",
            Arguments =
            [
                Argument.Of("oldText", ValueKinds.Text, "The text to find."),
                Argument.Of("newText", ValueKinds.Text, "The text to put in its place."),
                Argument.Optional("caseSensitive", ValueKinds.Boolean, "TextSearchOptions.CaseSensitive; false when not given."),
                Argument.Optional("wholeWordsOnly", ValueKinds.Boolean, "TextSearchOptions.WholeWordsOnly; false when not given."),
                Argument.Optional("includeNotes", ValueKinds.Boolean, "TextSearchOptions.IncludeNotes; false when not given."),
            ],
            Run = static (p, a, _) =>
            {
                var options = new TextSearchOptions();
                if (a.Has("caseSensitive"))
                {
                    options.CaseSensitive = a.Get<bool>("caseSensitive");
                }

                if (a.Has("wholeWordsOnly"))
                {
                    options.WholeWordsOnly = a.Get<bool>("wholeWordsOnly");
                }

                if (a.Has("includeNotes"))
                {
                    options.IncludeNotes = a.Get<bool>("includeNotes");
                }

                var found = new Matches();
                p.ReplaceText(a.Get<string>("oldText"), a.Get<string>("newText"), options, found);
                return new ActionResult(found.Written);
            },
        });
    }

    private static ActionResult Returned(ISlide slide)
    {
        string address = $"slide/{SlidesTable.Id(slide.SlideId)}";
        return new ActionResult(JsonValue.Create(address), address);
    }

    /// <summary>Each match <c>ReplaceText</c> reports, with its text frame's address where the table has one.</summary>
    private sealed class Matches : IFindResultCallback
    {
        internal JsonArray Written { get; } = [];

        public void FoundResult(ITextFrame textFrame, string sourceText, string foundText, int textPosition)
        {
            var match = new JsonObject();
            if (Address(textFrame) is { } at)
            {
                match["at"] = at;
            }

            match["foundText"] = foundText;
            match["textPosition"] = textPosition;
            Written.Add(match);
        }
    }

    /// <summary>
    /// The address of a text frame's owner: a table cell, a slide's shape or its notes slide; null
    /// for a frame the table does not address, such as a SmartArt node's.
    /// </summary>
    internal static string? Address(ITextFrame frame)
    {
        IShape? shape = frame.ParentCell is { } cell ? Owner(cell) : frame.ParentShape;
        if (shape?.Slide is INotesSlide notes)
        {
            return $"slide/{SlidesTable.Id(notes.ParentSlide.SlideId)}/notes";
        }

        if (shape?.Slide is not ISlide slide || !SlidesShapeTypes.All(slide.Shapes).Contains(shape))
        {
            return null;
        }

        string address = $"slide/{SlidesTable.Id(slide.SlideId)}/shape/{SlidesTable.Id(shape.OfficeInteropShapeId)}";
        return frame.ParentCell is { } owned ? $"{address}/cell/{owned.FirstRowIndex}/{owned.FirstColumnIndex}" : address;
    }

    private static IShape? Owner(ICell cell) => cell.Table as IShape;
}
