using System.Globalization;
using System.Text;
using CSharpDisplay = Microsoft.CodeAnalysis.CSharp.SymbolDisplay;

namespace Aspose.Cli.Sdk.Analyzers;

/// <summary>
/// The type mapping and constraint re-creation shared by the operation and result contract
/// writers: which CLR types are contract values, their camelCase wire names, and the C# that
/// re-creates a declared value constraint at the level it applies to.
/// </summary>
internal static class ContractTypes
{
    internal static AttributeData? Find(ISymbol symbol, string attribute) =>
        symbol.GetAttributes().FirstOrDefault(data => data.AttributeClass?.ToDisplayString() == attribute);

    internal static IEnumerable<INamedTypeSymbol> BaseTypes(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type.BaseType; current is not null; current = current.BaseType)
        {
            yield return current;
        }
    }

    internal static Location SourceLocation(ISymbol symbol) =>
        symbol.Locations.FirstOrDefault(static location => location.IsInSource) ?? Location.None;

    internal static Location AttributeLocation(AttributeData attribute, ISymbol owner) =>
        attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? SourceLocation(owner);

    internal static bool Derives(INamedTypeSymbol type, INamedTypeSymbol? baseType) =>
        BaseTypes(type).Any(current => SymbolEqualityComparer.Default.Equals(current, baseType));

    /// <summary>The value kinds a constraint of the SDK applies to, or null for any other attribute.</summary>
    internal static string[]? Kinds(INamedTypeSymbol attribute)
    {
        for (INamedTypeSymbol? current = attribute; current is not null; current = current.BaseType)
        {
            if (current.ContainingNamespace?.ToDisplayString() + "." is OperationContractGenerator.Operations or OperationContractGenerator.Contracts
                && ConstraintKinds.TryGetValue(current.Name, out string[]? kinds))
            {
                return kinds;
            }
        }

        return null;
    }

    /// <summary>The value kinds each constraint of the SDK applies to, by its type or base type.</summary>
    internal static readonly Dictionary<string, string[]> ConstraintKinds = new(StringComparer.Ordinal)
    {
        ["MinItemsAttribute"] = ["Array"],
        ["MaxItemsAttribute"] = ["Array"],
        ["MinimumAttribute"] = ["Integer", "Number"],
        ["MaximumAttribute"] = ["Integer", "Number"],
        ["ExclusiveMinimumAttribute"] = ["Integer", "Number"],
        ["AllowedValuesAttribute"] = ["String", "Integer", "Number", "Boolean"],
        ["JsonScalarAttribute"] = ["Any"],
        ["MinLengthAttribute"] = ["String"],
        ["MaxLengthAttribute"] = ["String"],
        ["PatternAttribute"] = ["String"],
        ["HexColorAttribute"] = ["String"],
        ["WebLinkAttribute"] = ["String"],
        ["ValueKindAttribute"] = ["String"],
    };

    /// <summary>The value levels of a member from the outside in, such as Array, Array, String.</summary>
    internal static List<string> Levels(ITypeSymbol type)
    {
        var levels = new List<string>();
        ITypeSymbol current = type;
        while (true)
        {
            if (current is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } wrapped)
            {
                current = wrapped.TypeArguments[0];
            }
            else if (Element(current) is { } element)
            {
                levels.Add(element.Kind);
                current = element.Type;
            }
            else
            {
                levels.Add(ScalarKind(current) ?? "Record");
                return levels;
            }
        }
    }

    internal static string DepthOf(int depth) => depth > 0 ? $" {{ Depth = {depth} }}" : string.Empty;

    internal static bool Has(ISymbol symbol, INamedTypeSymbol? attribute) =>
        attribute is not null && symbol.GetAttributes().Any(data => SymbolEqualityComparer.Default.Equals(data.AttributeClass, attribute));

    internal static object? Named(AttributeData attribute, string name) =>
        attribute.NamedArguments.FirstOrDefault(argument => argument.Key == name).Value.Value;

    internal static string? ScalarKind(ITypeSymbol type) => type.SpecialType switch
    {
        SpecialType.System_String => "String",
        SpecialType.System_Boolean => "Boolean",
        SpecialType.System_Byte or SpecialType.System_SByte or SpecialType.System_Int16 or SpecialType.System_UInt16
            or SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 => "Integer",
        SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal => "Number",
        SpecialType.System_Object => "Any",
        _ => type.ToDisplayString() == "System.Text.Json.JsonElement" ? "Any" : null,
    };

    /// <summary>
    /// The item type of an array or read-only list, or the value type of a read-only map with
    /// string keys; no other collection is a contract type.
    /// </summary>
    internal static (string Kind, ITypeSymbol Type, NullableAnnotation Nullable)? Element(ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol { Rank: 1 } array => ("Array", array.ElementType, array.ElementNullableAnnotation),
        INamedTypeSymbol { IsGenericType: true } named when named.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IReadOnlyList<T>" =>
            ("Array", named.TypeArguments[0], named.TypeArgumentNullableAnnotations[0]),
        INamedTypeSymbol { IsGenericType: true } named when named.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>"
            && named.TypeArguments[0].SpecialType == SpecialType.System_String =>
            ("Map", named.TypeArguments[1], named.TypeArgumentNullableAnnotations[1]),
        _ => null,
    };

    /// <summary>The scalar inside nullable, array, list and map wrappers.</summary>
    internal static ITypeSymbol Scalar(ITypeSymbol type) =>
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } wrapped ? Scalar(wrapped.TypeArguments[0])
        : Element(type) is { } element ? Scalar(element.Type)
        : type;

    internal static bool IsUnsigned(ITypeSymbol type) =>
        type.SpecialType is SpecialType.System_Byte or SpecialType.System_UInt16 or SpecialType.System_UInt32 or SpecialType.System_UInt64;

    internal static string TypeName(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    internal static string Literal(string value) => CSharpDisplay.FormatLiteral(value, quote: true);

    internal static string Primitive(object? value) => value switch
    {
        null => "null",
        string text => Literal(text),
        bool flag => flag ? "true" : "false",
        double number => number.ToString("R", CultureInfo.InvariantCulture) + "D",
        float number => number.ToString("R", CultureInfo.InvariantCulture) + "F",
        decimal number => number.ToString(CultureInfo.InvariantCulture) + "M",
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => "null",
    };

    /// <summary>A constant as a JSON literal, or null when it is not a string, finite number or Boolean.</summary>
    internal static string? JsonLiteral(object? value) => value switch
    {
        string text => JsonString(text),
        bool flag => flag ? "true" : "false",
        double number when !double.IsNaN(number) && !double.IsInfinity(number) => number.ToString("R", CultureInfo.InvariantCulture),
        float number when !float.IsNaN(number) && !float.IsInfinity(number) => number.ToString("R", CultureInfo.InvariantCulture),
        decimal or int or long or short or byte or sbyte or uint or ulong or ushort =>
            ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture),
        _ => null,
    };

    internal static string JsonString(string value)
    {
        var text = new StringBuilder("\"");
        foreach (char character in value)
        {
            text.Append(character switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                < ' ' => "\\u" + ((int)character).ToString("x4", CultureInfo.InvariantCulture),
                _ => character.ToString(),
            });
        }

        return text.Append('"').ToString();
    }

    /// <summary>The camelCase wire name, exactly as System.Text.Json's camelCase policy writes it.</summary>
    internal static string CamelCase(string name)
    {
        if (name.Length == 0 || !char.IsUpper(name[0]))
        {
            return name;
        }

        char[] characters = name.ToCharArray();
        for (int index = 0; index < characters.Length; index++)
        {
            if (index == 1 && !char.IsUpper(characters[index]))
            {
                break;
            }

            bool hasNext = index + 1 < characters.Length;
            if (index > 0 && hasNext && !char.IsUpper(characters[index + 1]))
            {
                if (char.IsSeparator(characters[index + 1]))
                {
                    characters[index] = char.ToLowerInvariant(characters[index]);
                }

                break;
            }

            characters[index] = char.ToLowerInvariant(characters[index]);
        }

        return new string(characters);
    }

    /// <summary>
    /// The level of a member's value a constraint applies to (see ValueConstraintAttribute):
    /// an array constraint applies exactly at its declared depth, and any other constraint at
    /// the first level at or below it that is not an array or map.
    /// </summary>
    internal static int Place(AttributeData attribute, List<string> levels, IPropertySymbol property, Action<Location, string> report)
    {
        int declared = Named(attribute, "Depth") is int depth ? depth : 0;
        string[]? kinds = Kinds(attribute.AttributeClass!);
        string name = attribute.AttributeClass!.Name;
        string subject = $"'{property.ContainingType.Name}.{property.Name}': [{(name.EndsWith("Attribute", StringComparison.Ordinal) ? name.Substring(0, name.Length - "Attribute".Length) : name)}]";
        if (kinds is null)
        {
            report(AttributeLocation(attribute, property), $"{subject} must derive from a constraint of the SDK, so the contract generator knows which values it applies to.");
            return declared;
        }

        bool descends = !kinds.Contains("Array");
        int level = declared;
        while (descends && level < levels.Count && levels[level] is "Array" or "Map")
        {
            level++;
        }

        if (level < levels.Count && kinds.Contains(levels[level]))
        {
            return level;
        }

        report(
            AttributeLocation(attribute, property),
            $"{subject} applies to {string.Join(" or ", kinds)} values, and the member has none at depth {declared}{(descends ? " or below" : string.Empty)}.");
        return declared;
    }

    /// <summary>Re-creates a declared constraint attribute with its arguments, at its placed depth.</summary>
    internal static string Constraint(AttributeData attribute, ISymbol owner, int depth, Action<Location, string> report)
    {
        string named = string.Join(", ", attribute.NamedArguments
            .Where(static argument => argument.Key != "Depth")
            .Select(argument => $"{argument.Key} = {Argument(argument.Value, attribute, owner, report)}")
            .Concat(depth > 0 ? new[] { $"Depth = {depth}" } : Array.Empty<string>()));
        return $"new {TypeName(attribute.AttributeClass!)}({string.Join(", ", attribute.ConstructorArguments.Select(argument => Argument(argument, attribute, owner, report)))})"
            + (named.Length == 0 ? string.Empty : $" {{ {named} }}");
    }

    /// <summary>
    /// Renders an attribute argument as C#. A type argument stands for the public constants of
    /// that type, rendered as an object array in declaration order.
    /// </summary>
    private static string Argument(TypedConstant argument, AttributeData attribute, ISymbol owner, Action<Location, string> report)
    {
        switch (argument.Kind)
        {
            case TypedConstantKind.Array:
                return $"new {TypeName(argument.Type!)} {{ {string.Join(", ", argument.Values.Select(value => Argument(value, attribute, owner, report)))} }}";
            case TypedConstantKind.Type:
                IFieldSymbol[] constants = [.. ((ITypeSymbol)argument.Value!).GetMembers().OfType<IFieldSymbol>()
                    .Where(static field => field.IsConst && field.DeclaredAccessibility == Accessibility.Public)];
                if (constants.Length == 0 || constants.Any(static field => JsonLiteral(field.ConstantValue) is null))
                {
                    report(AttributeLocation(attribute, owner), $"'{((ITypeSymbol)argument.Value!).Name}' must declare public string, finite number or Boolean constants only.");
                }

                return "new object[] { " + string.Join(", ", constants.Select(static field => Primitive(field.ConstantValue))) + " }";
            default:
                if (argument.Value is double or float && JsonLiteral(argument.Value) is null)
                {
                    report(AttributeLocation(attribute, owner), "A constraint argument must be a finite number.");
                }

                return argument.IsNull ? "null" : $"({TypeName(argument.Type!)}){Primitive(argument.Value)}";
        }
    }
}
