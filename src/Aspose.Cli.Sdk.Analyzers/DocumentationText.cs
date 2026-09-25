using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Aspose.Cli.Sdk.Analyzers;

/// <summary>
/// Reads the plain text of a symbol's <c>/// &lt;summary&gt;</c> from its syntax trivia, which
/// works whether or not the compilation parses documentation comments.
/// </summary>
internal static class DocumentationText
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.CultureInvariant);

    /// <summary>The summary with inline code and references rendered as text, or null when there is none.</summary>
    public static string? Summary(ISymbol symbol)
    {
        foreach (SyntaxReference reference in symbol.DeclaringSyntaxReferences)
        {
            var xml = new StringBuilder();
            foreach (string line in reference.GetSyntax().GetLeadingTrivia().ToFullString().Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("///", StringComparison.Ordinal))
                {
                    xml.Append(trimmed.Substring(3)).Append('\n');
                }
            }

            XElement? summary;
            try
            {
                summary = XElement.Parse("<doc>" + xml + "</doc>", LoadOptions.PreserveWhitespace).Element("summary");
            }
            catch (XmlException)
            {
                continue;
            }

            string? text = summary is null ? null : Whitespace.Replace(Render(summary), " ").Trim();
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }
        }

        return null;
    }

    private static string Render(XElement element) =>
        string.Concat(element.Nodes().Select(static node => node switch
        {
            XText text => text.Value,
            XElement { Name.LocalName: "see" or "seealso" } reference =>
                (string?)reference.Attribute("langword") ?? (reference.IsEmpty ? Simple((string?)reference.Attribute("cref")) : Render(reference)),
            XElement { Name.LocalName: "paramref" or "typeparamref" } reference => (string?)reference.Attribute("name") ?? string.Empty,
            XElement { Name.LocalName: "para" } paragraph => " " + Render(paragraph) + " ",
            XElement nested => Render(nested),
            _ => string.Empty,
        }));

    /// <summary>The last segment of a <c>cref</c>, such as <c>Name</c> for <c>T:Namespace.Name</c>.</summary>
    private static string Simple(string? cref)
    {
        if (cref is null)
        {
            return string.Empty;
        }

        string name = cref.Split('(')[0];
        return name.Substring(Math.Max(name.LastIndexOf('.'), name.LastIndexOf(':')) + 1);
    }
}
