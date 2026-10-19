using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Text;
using Aspose.Words;

using static Aspose.Cli.Product.Words.Engine.Editing.WordsMutationSupport;

namespace Aspose.Cli.Product.Words.Engine.Editing;

// Character formatting and styles.
internal sealed partial class WordsMutationHandlers
{
    public ValueTuple Apply(FormatTextOp operation)
    {
        if (operation.Match is not { } match)
        {
            foreach (Run run in Nodes.SelectMany(Descendants<Run>))
            {
                SetFormat(run.Font, operation);
            }

            return default;
        }

        // Each match is found where words search finds it (see WordsTextMatches); its runs are
        // split from the text around them and formatted in place, so they keep their other
        // settings and no text is rewritten.
        TextReplacement found = TextReplacement.Create(match, string.Empty, operation.Regex, operation.CaseSensitive, operation.WholeWord);
        long matched = 0;
        foreach (Node unit in Nodes.SelectMany(static node => node is CompositeNode and not Paragraph ? WordsStories.Units((CompositeNode)node) : [node]))
        {
            IReadOnlyList<IReadOnlyList<Run>> matches = WordsTextMatches.Isolate(WordsTextMatches.In(unit, found, int.MaxValue));
            foreach (Run run in matches.SelectMany(static runs => runs))
            {
                SetFormat(run.Font, operation);
            }

            matched += matches.Count;
        }

        Matched = matched;
        return default;
    }

    /// <summary>Sets the character settings format_text names on a run's font.</summary>
    private static void SetFormat(Aspose.Words.Font font, FormatTextOp operation)
    {
        if (operation.Bold is not null)
        {
            font.Bold = operation.Bold.Value;
        }

        if (operation.Italic is not null)
        {
            font.Italic = operation.Italic.Value;
        }

        if (operation.Underline is not null)
        {
            font.Underline = operation.Underline.Value ? Underline.Single : Underline.None;
        }

        if (operation.FontSize is not null)
        {
            font.Size = operation.FontSize.Value;
        }

        SetFonts(font, operation.Font, operation.LatinFont, operation.EastAsianFont);
        if (operation.Color is not null)
        {
            font.Color = ParseColor(operation.Color);
        }

        if (operation.Highlight is not null)
        {
            font.HighlightColor = WordsReadBack.HighlightColor(operation.Highlight);
        }
    }

    public ValueTuple Apply(SetStyleOp operation)
    {
        Style style = GetStyle(_document, operation.Style);
        foreach (Paragraph paragraph in Nodes.SelectMany(Paragraphs))
        {
            Run[] runs = [.. paragraph.GetChildNodes(NodeType.Run, true).Cast<Run>().Where(run => run.ParentParagraph == paragraph)];
            // What a run sets itself is where it differs from a run of its character style alone.
            object?[][] own = [.. runs.Select(run => DirectSettings(paragraph, run))];
            string[] characterStyles = [.. runs.Select(static run => run.Font.StyleName)];
            paragraph.ParagraphFormat.Style = style;
            for (int index = 0; index < runs.Length; index++)
            {
                // The run inherits again; its own settings come back but its typeface, size and color.
                runs[index].Font.ClearFormatting();
                runs[index].Font.StyleName = characterStyles[index];
                for (int setting = 0; setting < KeptSettings.Length; setting++)
                {
                    if (own[index][setting] is { } value)
                    {
                        KeptSettings[setting].Set(runs[index].Font, value);
                    }
                }
            }
        }

        return default;
    }

    /// <summary>
    /// The settings a run sets itself, of those set_style keeps, in <see cref="KeptSettings"/>
    /// order: each where the run differs from a probe run of its character style, else null.
    /// </summary>
    private static object?[] DirectSettings(Paragraph paragraph, Run run)
    {
        var probe = new Run(paragraph.Document, string.Empty);
        probe.Font.StyleName = run.Font.StyleName;
        paragraph.AppendChild(probe);
        try
        {
            return [.. KeptSettings.Select(setting =>
                setting.Get(run.Font) is var value && !Equals(value, setting.Get(probe.Font)) ? value : null)];
        }
        finally
        {
            probe.Remove();
        }
    }

    /// <summary>
    /// The writable character settings a run keeps through set_style beside its character
    /// style, which a probe of the same style shares: every one but its
    /// typeface, size and color (the font names, theme fonts, sizes, color, theme color and
    /// tint), which give way to the style. A color reads as its ARGB value.
    /// </summary>
    private static readonly (Func<Aspose.Words.Font, object> Get, Action<Aspose.Words.Font, object> Set)[] KeptSettings =
    [
        (static font => font.Bold, static (font, value) => font.Bold = (bool)value),
        (static font => font.BoldBi, static (font, value) => font.BoldBi = (bool)value),
        (static font => font.Italic, static (font, value) => font.Italic = (bool)value),
        (static font => font.ItalicBi, static (font, value) => font.ItalicBi = (bool)value),
        (static font => font.StrikeThrough, static (font, value) => font.StrikeThrough = (bool)value),
        (static font => font.DoubleStrikeThrough, static (font, value) => font.DoubleStrikeThrough = (bool)value),
        (static font => font.Shadow, static (font, value) => font.Shadow = (bool)value),
        (static font => font.Outline, static (font, value) => font.Outline = (bool)value),
        (static font => font.Emboss, static (font, value) => font.Emboss = (bool)value),
        (static font => font.Engrave, static (font, value) => font.Engrave = (bool)value),
        (static font => font.Superscript, static (font, value) => font.Superscript = (bool)value),
        (static font => font.Subscript, static (font, value) => font.Subscript = (bool)value),
        (static font => font.SmallCaps, static (font, value) => font.SmallCaps = (bool)value),
        (static font => font.AllCaps, static (font, value) => font.AllCaps = (bool)value),
        (static font => font.Hidden, static (font, value) => font.Hidden = (bool)value),
        (static font => font.Underline, static (font, value) => font.Underline = (Underline)value),
        (static font => font.UnderlineColor.ToArgb(), static (font, value) => font.UnderlineColor = System.Drawing.Color.FromArgb((int)value)),
        (static font => font.NumberSpacing, static (font, value) => font.NumberSpacing = (NumSpacing)value),
        (static font => font.Scaling, static (font, value) => font.Scaling = (int)value),
        (static font => font.Spacing, static (font, value) => font.Spacing = (double)value),
        (static font => font.Position, static (font, value) => font.Position = (double)value),
        (static font => font.Kerning, static (font, value) => font.Kerning = (double)value),
        (static font => font.HighlightColor.ToArgb(), static (font, value) => font.HighlightColor = System.Drawing.Color.FromArgb((int)value)),
        (static font => font.TextEffect, static (font, value) => font.TextEffect = (TextEffect)value),
        (static font => font.Bidi, static (font, value) => font.Bidi = (bool)value),
        (static font => font.ComplexScript, static (font, value) => font.ComplexScript = (bool)value),
        (static font => font.NoProofing, static (font, value) => font.NoProofing = (bool)value),
        (static font => font.LocaleId, static (font, value) => font.LocaleId = (int)value),
        (static font => font.LocaleIdBi, static (font, value) => font.LocaleIdBi = (int)value),
        (static font => font.LocaleIdFarEast, static (font, value) => font.LocaleIdFarEast = (int)value),
        (static font => font.SnapToGrid, static (font, value) => font.SnapToGrid = (bool)value),
        (static font => font.EmphasisMark, static (font, value) => font.EmphasisMark = (EmphasisMark)value),
        (static font => font.Border.Color.ToArgb(), static (font, value) => font.Border.Color = System.Drawing.Color.FromArgb((int)value)),
        (static font => font.Border.LineStyle, static (font, value) => font.Border.LineStyle = (LineStyle)value),
        (static font => font.Border.LineWidth, static (font, value) => font.Border.LineWidth = (double)value),
        (static font => font.Border.DistanceFromText, static (font, value) => font.Border.DistanceFromText = (double)value),
        (static font => font.Border.Shadow, static (font, value) => font.Border.Shadow = (bool)value),
        (static font => font.Shading.BackgroundPatternColor.ToArgb(), static (font, value) => font.Shading.BackgroundPatternColor = System.Drawing.Color.FromArgb((int)value)),
        (static font => font.Shading.ForegroundPatternColor.ToArgb(), static (font, value) => font.Shading.ForegroundPatternColor = System.Drawing.Color.FromArgb((int)value)),
        (static font => font.Shading.Texture, static (font, value) => font.Shading.Texture = (TextureIndex)value),
    ];

    public ValueTuple Apply(DefineStyleOp operation)
    {
        Style style = _document.Styles[operation.Name] ?? _document.Styles.Add(StyleType.Paragraph, operation.Name);
        if (operation.BasedOn is not null)
        {
            style.BaseStyleName = operation.BasedOn;
        }

        SetFonts(style.Font, operation.Font, operation.LatinFont, operation.EastAsianFont);
        if (operation.FontSize is not null)
        {
            style.Font.Size = operation.FontSize.Value;
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

        return default;
    }

    public ValueTuple Apply(SetDefaultFontOp operation)
    {
        foreach (Style style in _document.Styles)
        {
            if (style.Type is StyleType.Paragraph or StyleType.Character)
            {
                SetFonts(style.Font, operation.Font, operation.LatinFont, operation.EastAsianFont);
                if (operation.FontSize is not null)
                {
                    style.Font.Size = operation.FontSize.Value;
                }
            }
        }

        return default;
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

