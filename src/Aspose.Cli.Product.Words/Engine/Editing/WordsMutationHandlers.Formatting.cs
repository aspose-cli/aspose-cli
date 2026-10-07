using Aspose.Words;

using static Aspose.Cli.Product.Words.Engine.Editing.WordsMutationSupport;

namespace Aspose.Cli.Product.Words.Engine.Editing;

// Character formatting and styles.
internal sealed partial class WordsMutationHandlers
{
    public long Apply(FormatTextOp operation)
    {
        long count = 0;
        foreach (Run run in Nodes.SelectMany(Descendants<Run>))
        {
            if (operation.Bold is not null)
            {
                run.Font.Bold = operation.Bold.Value;
            }

            if (operation.Italic is not null)
            {
                run.Font.Italic = operation.Italic.Value;
            }

            if (operation.Underline is not null)
            {
                run.Font.Underline = operation.Underline.Value ? Underline.Single : Underline.None;
            }

            if (operation.Size is not null)
            {
                run.Font.Size = operation.Size.Value;
            }

            SetFonts(run.Font, operation.Font, operation.LatinFont, operation.EastAsianFont);
            if (operation.Color is not null)
            {
                run.Font.Color = ParseColor(operation.Color);
            }

            if (operation.Highlight is not null)
            {
                run.Font.HighlightColor = ParseColor(operation.Highlight);
            }

            count++;
        }

        return count;
    }

    public long Apply(SetStyleOp operation)
    {
        Style style = GetStyle(_document, operation.Style);
        long count = 0;
        foreach (Paragraph paragraph in Nodes.SelectMany(Paragraphs))
        {
            paragraph.ParagraphFormat.Style = style;
            count++;
        }

        return count;
    }

    public long Apply(DefineStyleOp operation)
    {
        Style style = _document.Styles[operation.Name] ?? _document.Styles.Add(StyleType.Paragraph, operation.Name);
        if (operation.BasedOn is not null)
        {
            style.BaseStyleName = operation.BasedOn;
        }

        SetFonts(style.Font, operation.Font, operation.LatinFont, operation.EastAsianFont);
        if (operation.Size is not null)
        {
            style.Font.Size = operation.Size.Value;
        }

        if (operation.Bold is not null)
        {
            style.Font.Bold = operation.Bold.Value;
        }

        if (operation.Color is not null)
        {
            style.Font.Color = ParseColor(operation.Color);
        }

        if (operation.SpaceBefore is not null)
        {
            style.ParagraphFormat.SpaceBefore = operation.SpaceBefore.Value;
        }

        if (operation.SpaceAfter is not null)
        {
            style.ParagraphFormat.SpaceAfter = operation.SpaceAfter.Value;
        }

        return 1;
    }

    public long Apply(SetDefaultFontOp operation)
    {
        foreach (Style style in _document.Styles)
        {
            if (style.Type is StyleType.Paragraph or StyleType.Character)
            {
                SetFonts(style.Font, operation.Font, operation.LatinFont, operation.EastAsianFont);
                if (operation.Size is not null)
                {
                    style.Font.Size = operation.Size.Value;
                }
            }
        }

        return 1;
    }

    // One font for every script, then the Latin (ASCII and other Latin) or East Asian font in its place.
    private static void SetFonts(Aspose.Words.Font font, string? name, string? latin, string? eastAsian)
    {
        if (name is not null)
        {
            font.Name = name;
        }

        if (latin is not null)
        {
            font.NameAscii = latin;
            font.NameOther = latin;
        }

        if (eastAsian is not null)
        {
            font.NameFarEast = eastAsian;
        }
    }
}

