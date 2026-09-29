using Aspose.Slides;

namespace Aspose.Cli.Product.Slides.Engine.Mapping;

/// <summary>
/// Gives Chinese, Japanese and Korean text one fallback font per presentation. When a run's font
/// has no glyph for a character, Aspose.Slides picks a fallback font per glyph and alternates
/// between Japanese and Chinese fonts inside one word (known issue SLIDES-CJK-FALLBACK in
/// KNOWN-ISSUES.md), where PowerPoint draws the whole run in one East Asian font. The rule names
/// the fonts of the presentation's script, most common first; the engine uses the first installed
/// one that has the glyph. Rules apply only to rendering and are not saved.
/// </summary>
internal static class SlidesCjkFallback
{
    private static readonly (uint Start, uint End)[] Ranges =
    [
        (0x2E80, 0x9FFF), // radicals, CJK punctuation, kana, Hangul compatibility jamo, ideographs
        (0xAC00, 0xD7AF), // Hangul syllables
        (0xF900, 0xFAFF), // compatibility ideographs
        (0xFE30, 0xFE4F), // vertical and compatibility forms
        (0xFF00, 0xFFEF), // full-width and half-width forms
    ];

    private static readonly string[] Simplified =
        ["Microsoft YaHei", "DengXian", "SimHei", "SimSun", "Noto Sans CJK SC", "Source Han Sans SC", "PingFang SC"];

    private static readonly string[] Traditional =
        ["Microsoft JhengHei", "PMingLiU", "MingLiU", "Noto Sans CJK TC", "Source Han Sans TC", "PingFang TC"];

    private static readonly string[] Japanese =
        ["Yu Gothic", "Meiryo", "MS Gothic", "Noto Sans CJK JP", "Source Han Sans JP", "Hiragino Sans"];

    private static readonly string[] Korean =
        ["Malgun Gothic", "Gulim", "Noto Sans CJK KR", "Source Han Sans KR", "Apple SD Gothic Neo"];

    internal static void Apply(IPresentation presentation)
    {
        string[] fonts = FontsFor(presentation);
        IFontFallBackRulesCollection rules = presentation.FontsManager.FontFallBackRulesCollection;
        foreach ((uint start, uint end) in Ranges)
        {
            rules.Add(new FontFallBackRule(start, end, fonts));
        }
    }

    /// <summary>
    /// The script the presentation's text is written in: kana means Japanese, Hangul Korean, and a
    /// Traditional Chinese language tag Traditional Chinese; otherwise Simplified Chinese.
    /// </summary>
    private static string[] FontsFor(IPresentation presentation)
    {
        bool kana = false, hangul = false, traditional = false;
        foreach (ISlide slide in presentation.Slides)
        {
            foreach (IAutoShape shape in slide.Shapes.OfType<IAutoShape>())
            {
                if (shape.TextFrame is not { } frame)
                {
                    continue;
                }

                foreach (IParagraph paragraph in frame.Paragraphs)
                {
                    foreach (IPortion portion in paragraph.Portions)
                    {
                        foreach (char character in portion.Text)
                        {
                            kana |= character is >= '぀' and <= 'ヿ';
                            hangul |= character is >= '가' and <= '힯';
                        }

                        traditional |= portion.PortionFormat.LanguageId is { } language
                            && (language.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase)
                                || language.StartsWith("zh-HK", StringComparison.OrdinalIgnoreCase)
                                || language.StartsWith("zh-MO", StringComparison.OrdinalIgnoreCase));
                    }
                }
            }
        }

        return kana ? Japanese : hangul ? Korean : traditional ? Traditional : Simplified;
    }
}
