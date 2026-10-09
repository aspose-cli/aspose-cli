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
    public static string? Summary(ISymbol symbol) =>
        Text(symbol, static documentation => documentation.Element("summary"));

    /// <summary>
    /// The text a record's <c>&lt;param&gt;</c> gives a primary constructor parameter, which
    /// describes the positional property of the same name; null when there is none.
    /// </summary>
    public static string? Parameter(INamedTypeSymbol record, string name) =>
        Text(record, documentation => documentation.Elements("param").FirstOrDefault(parameter => (string?)parameter.Attribute("name") == name));

    private static string? Text(ISymbol symbol, Func<XElement, XElement?> select)
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

            XElement? element;
            try
            {
                element = select(XElement.Parse("<doc>" + xml + "</doc>", LoadOptions.PreserveWhitespace));
            }
            catch (XmlException)
            {
                continue;
            }

            INamedTypeSymbol? context = symbol as INamedTypeSymbol ?? symbol.ContainingType;
            string? text = element is null ? null : Whitespace.Replace(Render(element, context), " ").Trim();
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }
        }

        return null;
    }

    /// <summary>
    /// The text of a documentation element. A reference to a property renders as the property's
    /// wire name, so a description names the JSON field a caller sees, not the C# member.
    /// </summary>
    private static string Render(XElement element, INamedTypeSymbol? context) =>
        string.Concat(element.Nodes().Select(node => node switch
        {
            XText text => text.Value,
            XElement { Name.LocalName: "see" or "seealso" } reference => (string?)reference.Attribute("langword")
                ?? (reference.IsEmpty ? Reference((string?)reference.Attribute("cref"), context) : Render(reference, context)),
            XElement { Name.LocalName: "paramref" } reference => Reference((string?)reference.Attribute("name"), context),
            XElement { Name.LocalName: "typeparamref" } reference => (string?)reference.Attribute("name") ?? string.Empty,
            XElement { Name.LocalName: "para" } paragraph => " " + Render(paragraph, context) + " ",
            XElement nested => Render(nested, context),
            _ => string.Empty,
        }));

    /// <summary>
    /// The wire name of the property a <c>cref</c> names, such as <c>hint</c> for <c>Hint</c> or
    /// <c>code</c> for <c>ErrorPayload.Code</c>; the cref's last segment when it names anything else.
    /// </summary>
    private static string Reference(string? cref, INamedTypeSymbol? context)
    {
        if (cref is null)
        {
            return string.Empty;
        }

        string path = cref.Split('(')[0];
        if (path.Length > 1 && path[1] == ':')
        {
            path = path.Substring(2);
        }

        string[] segments = path.Split('.');
        string member = segments[segments.Length - 1];
        IEnumerable<INamedTypeSymbol> owners = segments.Length == 1 ? Scopes(context)
            : Type(context, segments[segments.Length - 2]) is { } qualifier ? [qualifier]
            : [];
        foreach (INamedTypeSymbol owner in owners)
        {
            for (INamedTypeSymbol? type = owner; type is not null; type = type.BaseType)
            {
                if (type.GetMembers(member).OfType<IPropertySymbol>().FirstOrDefault() is { } property)
                {
                    return ContractTypes.WireName(property);
                }
            }
        }

        return Simple(cref);
    }

    /// <summary>The documented type and the types that contain it, innermost first.</summary>
    private static IEnumerable<INamedTypeSymbol> Scopes(INamedTypeSymbol? context)
    {
        for (INamedTypeSymbol? scope = context; scope is not null; scope = scope.ContainingType)
        {
            yield return scope;
        }
    }

    /// <summary>The type a qualified cref names, looked up from the documented type outwards.</summary>
    private static INamedTypeSymbol? Type(INamedTypeSymbol? context, string name)
    {
        foreach (INamedTypeSymbol scope in Scopes(context))
        {
            if (scope.Name == name)
            {
                return scope;
            }

            if (scope.GetTypeMembers(name).FirstOrDefault() is { } nested)
            {
                return nested;
            }
        }

        for (INamespaceSymbol? scope = context?.ContainingNamespace; scope is not null; scope = scope.ContainingNamespace)
        {
            if (scope.GetTypeMembers(name).FirstOrDefault() is { } type)
            {
                return type;
            }
        }

        return null;
    }

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
