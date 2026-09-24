using System.Text;
using Aspose.Words;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

/// <summary>
/// The fonts a document's text is drawn with, for inspection and font checks alike. The font
/// table is not the answer: it lists whatever fonts the producing application recorded, which
/// need not be the ones the text uses.
/// </summary>
internal static class WordsFonts
{
    /// <summary>
    /// The fonts the document's own text will be drawn with, sorted by name. Word chooses a
    /// run's font per character script, so a run names an East Asian and a complex-script font
    /// whether or not it contains any such character. Reporting those anyway would flag the
    /// theme fonts that sit on every ordinary Latin document.
    /// </summary>
    internal static IReadOnlyList<string> Used(Document document)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (Run run in document.GetChildNodes(NodeType.Run, true).Cast<Run>())
        {
            bool eastAsian = false;
            bool complexScript = false;
            bool latin = false;
            foreach (Rune rune in run.Text.EnumerateRunes())
            {
                if (IsEastAsian(rune)) { eastAsian = true; }
                else if (IsComplexScript(rune)) { complexScript = true; }
                else { latin = true; }
            }

            if (eastAsian) { Add(names, run.Font.NameFarEast); }
            if (complexScript) { Add(names, run.Font.NameBi); }
            if (latin)
            {
                Add(names, run.Font.Name);
                Add(names, run.Font.NameOther);
            }
        }

        return names.Order(StringComparer.Ordinal).ToArray();
    }

    private static bool IsEastAsian(Rune rune) => rune.Value is
        >= 0x1100 and <= 0x11FF        // Hangul Jamo
        or >= 0x2E80 and <= 0x9FFF     // CJK radicals, kana, Hangul compatibility, ideographs
        or >= 0xAC00 and <= 0xD7AF     // Hangul syllables
        or >= 0xF900 and <= 0xFAFF     // CJK compatibility ideographs
        or >= 0xFF00 and <= 0xFFEF     // Halfwidth and fullwidth forms
        or >= 0x20000 and <= 0x3FFFF;  // CJK ideograph extensions

    private static bool IsComplexScript(Rune rune) => rune.Value is
        >= 0x0590 and <= 0x0E7F        // Hebrew, Arabic, Indic and Thai scripts
        or >= 0xFB1D and <= 0xFDFF     // Hebrew and Arabic presentation forms
        or >= 0xFE70 and <= 0xFEFF;    // Arabic presentation forms-B

    private static void Add(ISet<string> names, string? name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            names.Add(name);
        }
    }
}
