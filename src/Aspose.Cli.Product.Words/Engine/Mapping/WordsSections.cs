using System.Globalization;
using Aspose.Cli.Sdk.Errors;
using Aspose.Words;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

internal static class WordsSections
{
    /// <summary>The 1-based section of a document; a number past the last section is SECTION_NOT_FOUND.</summary>
    public static Section Get(Document document, int number)
    {
        int count = document.Sections.Count;
        if (number < 1 || number > count)
        {
            throw CliErrors.NotFoundAt(
                WordsDiagnostics.SectionNotFound, "section", number.ToString(CultureInfo.InvariantCulture), count);
        }

        return document.Sections[number - 1];
    }
}
