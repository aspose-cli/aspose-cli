using Aspose.Pdf;
using Aspose.Pdf.Text;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Enumerates native font resources; an absent font dictionary is an empty set.</summary>
internal static class PdfFontResources
{
    internal static IEnumerable<Font> Enumerate(Document document)
    {
        foreach (Page page in document.Pages)
        {
            if (page.Resources?.Fonts is not { } fonts)
            {
                continue;
            }

            foreach (Font font in fonts)
            {
                yield return font;
            }
        }
    }
}
