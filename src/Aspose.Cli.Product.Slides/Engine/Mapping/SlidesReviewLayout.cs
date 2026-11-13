using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aspose.Slides;
using Aspose.Slides.Charts;
using static Aspose.Cli.Product.Slides.Engine.Mapping.SlidesStateProjection;
using static Aspose.Cli.Product.Slides.Engine.SlidesEvaluationMarks;
using static Aspose.Cli.Product.Slides.Engine.SlidesShapeText;

namespace Aspose.Cli.Product.Slides.Engine.Mapping;

/// <summary>
/// The layout of the slides a review checks: each top-level shape with its frame, text and the
/// effective facts its checks judge, such as where its text is laid out and what is behind it.
/// Only the review reads it; <c>slides read</c> reports its own projection.
/// </summary>
internal static class SlidesReviewLayout
{
    /// <summary>The first <paramref name="slides"/> slides of the presentation, with its slide size.</summary>
    internal static SlidesReviewDeck Project(Presentation presentation, int slides) => new()
    {
        SlideCount = presentation.Slides.Count,
        SlideWidth = presentation.SlideSize.Size.Width,
        SlideHeight = presentation.SlideSize.Size.Height,
        Slides = [.. presentation.Slides.Take(slides).Select(static (slide, index) => Slide(slide, index + 1))],
    };

    private static SlidesReviewSlide Slide(ISlide slide, int number) => new()
    {
        Slide = number,
        SlideId = slide.SlideId,
        Shapes = [.. slide.Shapes.Select((shape, zOrder) => Shape(slide, shape, zOrder))],
    };

    private static SlidesReviewShape Shape(ISlide slide, IShape shape, int zOrder)
    {
        (SlidesRect Lines, SlidesRect? Glyphs)? text = SlidesReviewProjection.TextArea(shape);
        return new SlidesReviewShape
        {
            ShapeId = shape.OfficeInteropShapeId,
            ShapeName = EmptyToNull(shape.Name),
            Kind = ShapeKind(shape),
            Placeholder = SlidesPlaceholders.Role(shape.Placeholder?.Type),
            Text = ShapeText(shape),
            Runs = Runs(shape),
            Rect = new SlidesRect { X = shape.X, Y = shape.Y, Width = shape.Width, Height = shape.Height },
            ZOrder = zOrder,
            HasOpaqueFill = SlidesReviewProjection.HasOpaqueFill(shape),
            TextRect = text?.Lines,
            TextGlyphRect = text?.Glyphs,
            TextAutofits = SlidesReviewProjection.TextAutofits(shape),
            Backdrop = SlidesReviewProjection.Backdrop(slide, shape),
            ChartTextColor = SlidesReviewProjection.ChartTextColor(shape),
            Content = Content(shape),
            EvaluationWatermark = IsEvaluationWatermark(shape),
        };
    }

    /// <summary>
    /// A digest of what a shape shows besides its text and frame: a picture's bytes; a video's or
    /// sound's media, embedded or linked, and its poster picture; a chart's type, title,
    /// categories and series, each series with its name and every point's stored x value, y
    /// value, value and bubble size, whichever its chart type has; each picture fill, a table
    /// cell's too; and for a group, the kind, frame and digest of each shape inside it. Null for a
    /// shape that shows none of these.
    /// </summary>
    private static string? Content(IShape shape)
    {
        string[] parts = [.. ContentParts(shape)];
        return parts.Length == 0 ? null : string.Join(';', parts);
    }

    private static IEnumerable<string> ContentParts(IShape shape)
    {
        switch (shape)
        {
            case IGroupShape group:
                yield return "group|" + FieldsDigest(group.Shapes.Select(static child => new[]
                {
                    ShapeKind(child),
                    string.Create(CultureInfo.InvariantCulture, $"{Math.Round(child.X, 1)},{Math.Round(child.Y, 1)},{Math.Round(child.Width, 1)},{Math.Round(child.Height, 1)}"),
                    Content(child) ?? string.Empty,
                }));
                break;
            case IChart chart:
                yield return "chart|" + FieldsDigest(ChartFields(chart));
                break;
            case IAudioFrame audio:
                yield return "audio|" + Media(audio.EmbeddedAudio?.GetStream(), audio.LinkPathLong);
                break;
            case IVideoFrame video:
                yield return "video|" + Media(video.EmbeddedVideo?.GetStream(), video.LinkPathLong);
                break;
        }

        if (shape is IPictureFrame { PictureFormat.Picture.Image: { } image })
        {
            yield return "image|" + Digest(image);
        }

        foreach (IFillFormat fill in SlidesShapeFormats.Fills(shape).Where(static fill => fill.FillType == FillType.Picture))
        {
            yield return "fill|" + Fill(fill);
        }
    }

    // The fields a chart's digest covers, a list per entry: its type and title, its categories,
    // then each series' name and point values.
    private static IEnumerable<IEnumerable<string>> ChartFields(IChart chart)
    {
        yield return [chart.Type.ToString(), chart.HasTitle ? Text(chart.ChartTitle?.TextFrameForOverriding?.Text) : string.Empty];
        yield return chart.ChartData.Categories.Select(static category => Text(category.Value));
        foreach (IChartSeries series in chart.ChartData.Series)
        {
            yield return [Text(series.Name)];
            yield return series.DataPoints.SelectMany(PointData);
        }

        static string Text(object? data) => Convert.ToString(data, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    // An embedded file is known by its bytes, a linked one by its address, which is not opened.
    private static string Media(Stream? embedded, string? link)
    {
        if (embedded is null)
        {
            return link ?? string.Empty;
        }

        using (embedded)
        {
            return Convert.ToHexStringLower(SHA256.HashData(embedded));
        }
    }

    // A list is prefixed with its count and each of its fields with its length, so no text in
    // one field can shift the next.
    private static string FieldsDigest(IEnumerable<IEnumerable<string>> lists)
    {
        var value = new StringBuilder();
        foreach (IEnumerable<string> list in lists)
        {
            string[] fields = [.. list];
            value.Append(fields.Length).Append('[');
            foreach (string field in fields)
            {
                value.Append(field.Length).Append(':').Append(field);
            }
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value.ToString())));
    }

    /// <summary>
    /// Each run of the shape's text that shows any, with its effective size and color. The
    /// effective size is the one a shape that shrinks its text on overflow draws it at; the size
    /// before shrinking is the run's own, else its paragraph's default, which no shrink scales.
    /// </summary>
    private static IReadOnlyList<SlidesReviewRun> Runs(IShape shape) =>
    [
        .. TextFrames(shape)
            .SelectMany(static frame => frame.Paragraphs)
            .SelectMany(static paragraph => paragraph.Portions
                .Where(static portion => !string.IsNullOrWhiteSpace(portion.Text))
                .Select(portion => Run(portion, paragraph))),
    ];

    private static SlidesReviewRun Run(IPortion portion, IParagraph paragraph)
    {
        IBasePortionFormatEffectiveData effective = portion.PortionFormat.GetEffective().AsIBasePortionFormatEffectiveData;
        float own = portion.PortionFormat.FontHeight;
        float unshrunk = float.IsNaN(own) ? paragraph.ParagraphFormat.GetEffective().DefaultPortionFormat.FontHeight : own;
        return new SlidesReviewRun
        {
            FontSize = effective.FontHeight,
            UnshrunkFontSize = unshrunk is > 0 ? unshrunk : null,
            Color = RunColor(effective),
        };
    }
}

/// <summary>The slides a review checks and the presentation's slide count and size, in points.</summary>
internal sealed record SlidesReviewDeck
{
    public required int SlideCount { get; init; }

    public required double SlideWidth { get; init; }

    public required double SlideHeight { get; init; }

    public required IReadOnlyList<SlidesReviewSlide> Slides { get; init; }
}

/// <summary>One slide a review checks.</summary>
internal sealed record SlidesReviewSlide
{
    /// <summary>The 1-based slide number.</summary>
    public required int Slide { get; init; }

    /// <summary>The stable slide id, which names the slide's view part.</summary>
    public required uint SlideId { get; init; }

    /// <summary>The slide's top-level shapes in z-order.</summary>
    public required IReadOnlyList<SlidesReviewShape> Shapes { get; init; }
}

/// <summary>One top-level shape as a review judges its layout.</summary>
internal sealed record SlidesReviewShape
{
    /// <summary>The shape id that findings name and edit operations address.</summary>
    public required long ShapeId { get; init; }

    /// <summary>The shape's name; null when it has none.</summary>
    public string? ShapeName { get; init; }

    /// <summary>What the shape is, as <c>slides read</c> reports it.</summary>
    public required string Kind { get; init; }

    /// <summary>The placeholder role, as <c>slides read</c> reports it; null for a shape that is not a placeholder.</summary>
    public string? Placeholder { get; init; }

    /// <summary>The shape's text, as <c>slides read</c> reports it; null when it has none.</summary>
    public string? Text { get; init; }

    /// <summary>The runs of the shape's text that show any.</summary>
    public IReadOnlyList<SlidesReviewRun> Runs { get; init; } = [];

    /// <summary>Where the shape is on the slide.</summary>
    public required SlidesRect Rect { get; init; }

    /// <summary>The shape's place in the slide's z-order, from the back.</summary>
    public int ZOrder { get; init; }

    /// <summary>Whether the shape hides what is behind it.</summary>
    public bool HasOpaqueFill { get; init; }

    /// <summary>Where the shape's text is laid out, in slide points; null for a shape without text.</summary>
    public SlidesRect? TextRect { get; init; }

    /// <summary>Where the characters of the shape's text are drawn, inside <see cref="TextRect"/>; null when the lines do not tell.</summary>
    public SlidesRect? TextGlyphRect { get; init; }

    /// <summary>Whether the shape resizes to fit its text or shrinks its text on overflow, so text beyond its stored frame is not an overflow.</summary>
    public bool TextAutofits { get; init; }

    /// <summary>The one solid color behind the text of a text shape or chart; null when no single color is known.</summary>
    public System.Drawing.Color? Backdrop { get; init; }

    /// <summary>The color a chart states for its text; null when the chart style decides it.</summary>
    public System.Drawing.Color? ChartTextColor { get; init; }

    /// <summary>
    /// A digest of what the shape shows besides its text and frame, such as a picture, a chart's
    /// data, a video or a group's shapes, which tells apart two shapes of one kind and frame; null
    /// for a shape that shows only text.
    /// </summary>
    public string? Content { get; init; }

    /// <summary>Whether the shape is the watermark text box an evaluation save added.</summary>
    public bool EvaluationWatermark { get; init; }
}

/// <summary>One run of a shape's text that shows any.</summary>
internal sealed record SlidesReviewRun
{
    /// <summary>The effective font size in points, as drawn after any shrink on overflow.</summary>
    public double? FontSize { get; init; }

    /// <summary>The font size in points before the shape shrinks its text on overflow; null when unknown.</summary>
    public double? UnshrunkFontSize { get; init; }

    /// <summary>Whether the shape shrank this run below the size it states or inherits.</summary>
    public bool Shrunk => FontSize is { } drawn && UnshrunkFontSize is { } stated && drawn < stated - 0.05;

    /// <summary>The solid color the text is drawn in, as #RRGGBB; null for text without a solid fill.</summary>
    public string? Color { get; init; }
}
