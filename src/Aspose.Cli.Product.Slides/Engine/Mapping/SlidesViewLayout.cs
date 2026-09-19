using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aspose.Cli.Sdk.Views;
using Aspose.Slides;
using Aspose.Slides.Charts;
using static Aspose.Cli.Product.Slides.Engine.SlidesEngineSupport;

namespace Aspose.Cli.Product.Slides.Engine.Mapping;

/// <summary>
/// Places the top-level shapes of a slide in CSS pixel space with their
/// persisted shape ids and a digest of geometry, text, formatting, fill and
/// media, so a viewer can point at exactly the shape that changed.
/// </summary>
internal static class SlidesViewLayout
{
    private const int LabelLength = 80;

    internal static IReadOnlyList<ViewElement>? Elements(ISlide slide, double cssPerPoint)
    {
        var elements = new List<ViewElement>(slide.Shapes.Count);
        foreach (IShape shape in slide.Shapes)
        {
            elements.Add(new ViewElement
            {
                Id = "shape-" + shape.OfficeInteropShapeId.ToString(CultureInfo.InvariantCulture),
                Kind = ShapeTypeName(shape),
                Label = Label(shape),
                Digest = Digest(shape),
                Box = new ViewBox(
                    Math.Round(shape.X * cssPerPoint, 2),
                    Math.Round(shape.Y * cssPerPoint, 2),
                    Math.Round(Math.Max(0, shape.Width) * cssPerPoint, 2),
                    Math.Round(Math.Max(0, shape.Height) * cssPerPoint, 2)),
            });
        }
        return elements.Count == 0 ? null : elements;
    }

    private static string? Label(IShape shape)
    {
        string? text = ShapeText(shape) is { } value
            ? string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            : EmptyToNull(shape.Name);
        return text is { Length: > LabelLength } ? text[..LabelLength] : text;
    }

    private static string Digest(IShape shape)
    {
        var canonical = new StringBuilder();
        Append(canonical, shape);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))[..16]
            .ToLowerInvariant();
    }

    private static void Append(StringBuilder canonical, IShape shape)
    {
        canonical.Append(ShapeTypeName(shape)).Append('|').Append(shape.Name)
            .Append('|').Append(Number(shape.X)).Append('|').Append(Number(shape.Y))
            .Append('|').Append(Number(shape.Width)).Append('|').Append(Number(shape.Height))
            .Append('|').Append(Number(shape.Rotation)).Append('|').Append(shape.Hidden ? 'h' : '-');
        IFillFormatEffectiveData fill = shape.FillFormat.GetEffective();
        canonical.Append("|F").Append((int)fill.FillType);
        if (fill.FillType == FillType.Solid)
        {
            canonical.Append('|').Append(fill.SolidFillColor.ToArgb());
        }
        ILineFormatEffectiveData line = shape.LineFormat.GetEffective();
        canonical.Append("|L").Append(Number(line.Width)).Append('|').Append((int)line.FillFormat.FillType);
        if (line.FillFormat.FillType == FillType.Solid)
        {
            canonical.Append('|').Append(line.FillFormat.SolidFillColor.ToArgb());
        }

        switch (shape)
        {
            case IAutoShape { TextFrame: not null } auto:
                AppendText(canonical, auto.TextFrame);
                break;
            case IPictureFrame frame when frame.PictureFormat.Picture.Image is { } image:
                canonical.Append("|I").Append(Convert.ToHexString(SHA256.HashData(image.BinaryData)));
                break;
            case ITable table:
                for (int row = 0; row < table.Rows.Count; row++)
                {
                    for (int column = 0; column < table.Columns.Count; column++)
                    {
                        canonical.Append("|C");
                        if (table[column, row].TextFrame is { } cellText)
                        {
                            AppendText(canonical, cellText);
                        }
                    }
                }
                break;
            case IChart chart:
                canonical.Append("|G").Append((int)chart.Type).Append('|').Append(chart.ChartData.Series.Count);
                foreach (IChartSeries series in chart.ChartData.Series)
                {
                    canonical.Append('|').Append(series.DataPoints.Count);
                }
                break;
            case IGroupShape group:
                foreach (IShape child in group.Shapes)
                {
                    canonical.Append("|{");
                    Append(canonical, child);
                    canonical.Append('}');
                }
                break;
        }
    }

    private static void AppendText(StringBuilder canonical, ITextFrame frame)
    {
        foreach (IParagraph paragraph in frame.Paragraphs)
        {
            canonical.Append("|P").Append((int)paragraph.ParagraphFormat.Alignment);
            foreach (IPortion portion in paragraph.Portions)
            {
                IPortionFormatEffectiveData effective = portion.PortionFormat.GetEffective();
                IBasePortionFormatEffectiveData format = effective.AsIBasePortionFormatEffectiveData;
                canonical.Append("|T").Append(portion.Text)
                    .Append('|').Append(format.LatinFont?.FontName)
                    .Append('|').Append(Number(format.FontHeight))
                    .Append(format.FontBold ? 'b' : '-')
                    .Append(format.FontItalic ? 'i' : '-')
                    .Append('|').Append((int)effective.FillFormat.FillType);
                if (effective.FillFormat.FillType == FillType.Solid)
                {
                    canonical.Append('|').Append(effective.FillFormat.SolidFillColor.ToArgb());
                }
            }
        }
    }

    private static string Number(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);
}
