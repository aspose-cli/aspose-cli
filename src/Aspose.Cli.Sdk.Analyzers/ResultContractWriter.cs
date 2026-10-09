using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Aspose.Cli.Sdk.Analyzers.ContractTypes;

namespace Aspose.Cli.Sdk.Analyzers;

/// <summary>
/// Describes the result records of one compilation and writes their descriptors onto each JSON
/// context that lists one of them, as an implementation of <c>IResultSchemaSource</c>. A record is
/// published when it is a concrete record that derives directly from one of the SDK's envelope
/// bases (<c>ResultEnvelope</c> and its abstract descendants), whose constructor it passes a
/// constant relative schema id, or when it declares <c>[SchemaId]</c>; the records it inherits
/// from and the records its members hold are described with it.
/// </summary>
internal sealed class ResultContractWriter(Compilation compilation, Action<Location, string> report)
{
    private const string Contracts = OperationContractGenerator.Contracts;
    private const string Sdk = "global::" + Contracts;
    private const string ResultEnvelope = Contracts + "ResultEnvelope";
    private const string Json = "System.Text.Json.Serialization.";
    private static readonly string[] CaseTypes = ["null", "string", "number", "integer", "boolean"];
    private readonly INamedTypeSymbol? _constraint = compilation.GetTypeByMetadataName(Contracts + "ValueConstraintAttribute");
    private readonly Dictionary<INamedTypeSymbol, (string? SchemaId, int Version)> _published = new(SymbolEqualityComparer.Default);
    private readonly List<INamedTypeSymbol> _order = [];
    private readonly Dictionary<INamedTypeSymbol, string> _code = new(SymbolEqualityComparer.Default);
    private readonly Queue<INamedTypeSymbol> _pending = new();
    private bool _failed;

    /// <summary>Every record the compilation describes as a result contract.</summary>
    public HashSet<INamedTypeSymbol> Records { get; } = new(SymbolEqualityComparer.Default);

    /// <summary>Returns one generated source per JSON context, or none when the contracts have errors.</summary>
    public IReadOnlyList<(string HintName, string Source)> Write()
    {
        INamedTypeSymbol[] declared = [.. AnalyzerTypes.Declared(compilation).OrderBy(static type => type.ToDisplayString(), StringComparer.Ordinal)];
        var ids = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
        foreach (INamedTypeSymbol type in declared)
        {
            if (Published(type) is not { } root)
            {
                continue;
            }

            if (ids.TryGetValue(root.SchemaId, out INamedTypeSymbol? other))
            {
                Report(type, $"Result records '{other.Name}' and '{type.Name}' both publish the schema '{root.SchemaId}'; merge them into one record.");
                continue;
            }

            ids.Add(root.SchemaId, type);
            _published[type] = (root.SchemaId, root.Version);
            Enqueue(type);
        }

        if (ids.Count == 0)
        {
            return [];
        }

        // The envelope bases are described where they are declared, so the results of another
        // assembly can inherit them.
        foreach (INamedTypeSymbol type in declared.Where(IsEnvelope))
        {
            Enqueue(type);
        }

        while (_pending.Count > 0)
        {
            Describe(_pending.Dequeue());
        }

        INamedTypeSymbol[] contexts = [.. declared.Where(type => AnalyzerTypes.Inherits(type, Json + "JsonSerializerContext")
            && type.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == Json + "JsonSerializableAttribute"
                && attribute.ConstructorArguments.FirstOrDefault().Value is INamedTypeSymbol listed
                && _published.ContainsKey(listed)))];
        if (contexts.Length == 0)
        {
            Report(ids.Values.First(), $"List the result '{ids.Values.First().Name}' in a JsonSerializerContext of this assembly, which then carries the assembly's result contracts.");
        }

        foreach (INamedTypeSymbol context in contexts.Where(static context => context.ContainingType is not null
            || context.DeclaringSyntaxReferences.Any(static reference => reference.GetSyntax() is TypeDeclarationSyntax syntax
                && !syntax.Modifiers.Any(static modifier => modifier.ValueText == "partial"))))
        {
            Report(context, $"JSON context '{context.Name}' lists result records, so it must be a top-level partial class.");
        }

        return _failed ? [] : [.. contexts.Select(context => (context.Name + ".ResultContracts.g.cs", Source(context)))];
    }

    /// <summary>The relative id and version a record publishes, or null when it publishes none.</summary>
    private (string SchemaId, int Version)? Published(INamedTypeSymbol type)
    {
        if (Find(type, Contracts + "SchemaIdAttribute") is { } attribute)
        {
            if (attribute.ConstructorArguments.FirstOrDefault().Value is not string id || !RelativeId.IsMatch(id)
                || AnalyzerTypes.Inherits(type, ResultEnvelope))
            {
                Report(type, $"[SchemaId] on '{type.Name}' must name a relative id such as 'backup', on a record that is not a ResultEnvelope; a result states its id to the ResultEnvelope constructor.");
                return null;
            }

            return (id, 0);
        }

        if (type.IsAbstract || type.TypeKind != TypeKind.Class || type.BaseType is not { } envelope || !IsEnvelope(envelope))
        {
            return null;
        }

        // The arguments the record passes to its envelope base, from its primary constructor's
        // base type or from a constructor initializer.
        foreach (SyntaxReference reference in type.DeclaringSyntaxReferences)
        {
            SyntaxNode syntax = reference.GetSyntax();
            IEnumerable<ArgumentListSyntax?> calls = ((syntax as TypeDeclarationSyntax)?.BaseList?.Types.OfType<PrimaryConstructorBaseTypeSyntax>()
                .Select(static baseType => baseType.ArgumentList) ?? [])
                .Concat(syntax.DescendantNodes().OfType<ConstructorInitializerSyntax>()
                    .Where(static initializer => initializer.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.BaseConstructorInitializer))
                    .Select(static initializer => initializer.ArgumentList));
            foreach (ArgumentListSyntax? arguments in calls)
            {
                if (arguments is { Arguments.Count: 2 })
                {
                    SemanticModel model = compilation.GetSemanticModel(arguments.SyntaxTree);
                    if (model.GetConstantValue(arguments.Arguments[0].Expression).Value is string id && RelativeId.IsMatch(id)
                        && model.GetConstantValue(arguments.Arguments[1].Expression).Value is int version)
                    {
                        return (id, version);
                    }
                }
            }
        }

        Report(type, $"Result '{type.Name}' must pass the ResultEnvelope constructor a constant relative schema id, such as 'render-result', and a constant version; the schema is published under that id.");
        return null;
    }

    /// <summary>
    /// Whether a type is one of the SDK's envelope bases: <c>ResultEnvelope</c> or an abstract
    /// record of the SDK's contracts that derives from it.
    /// </summary>
    private static bool IsEnvelope(INamedTypeSymbol type) =>
        type.IsAbstract && type.ContainingNamespace.ToDisplayString() + "." == Contracts
        && (type.ToDisplayString() == ResultEnvelope || AnalyzerTypes.Inherits(type, ResultEnvelope));

    private void Enqueue(INamedTypeSymbol type)
    {
        if (Records.Add(type))
        {
            _order.Add(type);
            _pending.Enqueue(type);
        }
    }

    private void Describe(INamedTypeSymbol type)
    {
        if (!IsAccessible(type))
        {
            Report(type, $"Result record '{type.Name}' must be accessible to its assembly's JSON context; it may not be private or protected.");
        }

        var code = new StringBuilder("            new() { Type = typeof(").Append(TypeName(type)).Append(')');
        if (DocumentationText.Summary(type) is { } description)
        {
            code.Append(", Description = ").Append(Literal(description));
        }

        if (type.BaseType is { SpecialType: not SpecialType.System_Object } baseType)
        {
            if (SymbolEqualityComparer.Default.Equals(baseType.ContainingAssembly, compilation.Assembly))
            {
                Enqueue(baseType);
            }
            else if (!IsEnvelope(baseType))
            {
                Report(type, $"Result record '{type.Name}' derives from '{baseType.Name}' of another assembly; only the SDK's envelope bases can be inherited across assemblies.");
            }

            code.Append(", Base = typeof(").Append(TypeName(baseType)).Append(')');
        }

        if (_published.TryGetValue(type, out (string? SchemaId, int Version) published))
        {
            code.Append(", SchemaId = ").Append(Literal(published.SchemaId!));
            if (published.Version > 0)
            {
                code.Append(", SchemaVersion = ").Append(published.Version);
            }
        }

        string[] members = [.. Properties(type).Select(property => Member(type, property)).OfType<string>()];
        code.Append(", Properties = [").Append(string.Concat(members.Select(static member => "\n                " + member + ",")))
            .Append(members.Length == 0 ? "]" : "\n            ]");
        _code[type] = code.Append(" },").ToString();
    }

    /// <summary>Public instance properties the record declares and serializes, in declaration order.</summary>
    private static IEnumerable<IPropertySymbol> Properties(INamedTypeSymbol type) =>
        type.GetMembers().OfType<IPropertySymbol>().Where(static property =>
            !property.IsStatic && !property.IsIndexer && property.DeclaredAccessibility == Accessibility.Public
            && property.GetMethod is not null && property.OverriddenProperty is null
            && property.Name != "EqualityContract"
            && IgnoreCondition(property) is not "Always");

    /// <summary>The <c>[JsonIgnore]</c> condition of a property, or null when it has none.</summary>
    private static string? IgnoreCondition(IPropertySymbol property) =>
        Find(property, Json + "JsonIgnoreAttribute") is not { } ignore ? null
        : Named(ignore, "Condition") switch
        {
            null or 1 => "Always",
            0 => null,
            2 => "WhenWritingDefault",
            _ => "WhenWritingNull",
        };

    private string? Member(INamedTypeSymbol owner, IPropertySymbol property)
    {
        SyntaxNode? syntax = property.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
        bool positional = syntax is ParameterSyntax;
        string wire = WireName(property);
        bool nullable = property.NullableAnnotation == NullableAnnotation.Annotated
            || property.Type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
        ITypeSymbol type = property.Type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } wrapped
            ? wrapped.TypeArguments[0]
            : property.Type;
        if (Value(type, nullable: false, property) is not { } value)
        {
            return null;
        }

        var code = new StringBuilder("new() { Name = ").Append(Literal(wire));
        string? description = positional ? DocumentationText.Parameter(owner, property.Name) : DocumentationText.Summary(property);
        if (description is not null)
        {
            code.Append(", Description = ").Append(Literal(description));
        }
        else if (!DescribedByRecord(type))
        {
            Invalid(property, "describe it with a documentation summary (a <param> for a positional member); it is the member's schema description");
        }

        code.Append(", Value = ").Append(value);
        if (Find(property, Json + "JsonExtensionDataAttribute") is not null)
        {
            if (ResultScalar(type)?.Kind != "Object" || Properties(owner).Count() != 1)
            {
                Invalid(property, "extension data must be the record's only member, a JsonObject");
            }

            code.Append(", Extension = true");
        }
        else if (!nullable && IgnoreCondition(property) is null)
        {
            code.Append(", Required = true");
        }

        if (!positional && property.SetMethod is null && Constant(syntax) is { } constant)
        {
            code.Append(", Const = ").Append(Literal(constant));
        }

        if (Find(property, Json + "JsonPropertyOrderAttribute")?.ConstructorArguments.FirstOrDefault().Value is int order && order != 0)
        {
            code.Append(", Order = ").Append(order);
        }

        List<string> levels = Levels(type);
        AttributeData[] declared = [.. property.GetAttributes().Where(IsConstraint)];
        List<string> constraints = [.. declared.Select(attribute => ContractTypes.Constraint(
            attribute, property, ContractTypes.Place(attribute, levels, property, Report), Report))];
        if (IsUnsigned(Scalar(type)))
        {
            constraints.Insert(0, $"new {Sdk}MinimumAttribute(0)" + DepthOf(levels.Count - 1));
        }

        if (constraints.Count > 0)
        {
            code.Append(", Constraints = [").Append(string.Join(", ", constraints)).Append(']');
        }

        if (Find(property, Contracts + "OpenEnumAttribute") is { } open)
        {
            if (levels.Count != 1 || levels[0] != "String" || !declared.Any(static attribute => attribute.AttributeClass?.Name == "AllowedValuesAttribute"))
            {
                Invalid(property, "[OpenEnum] extends the [AllowedValues] of a string member");
            }

            code.Append(", OpenPattern = ").Append(Literal(open.ConstructorArguments[0].Value as string ?? string.Empty));
        }

        AttributeData[] cases = [.. property.GetAttributes().Where(static attribute => attribute.AttributeClass?.ToDisplayString() == Contracts + "OneOfByAttribute")];
        if (cases.Length > 0)
        {
            CheckCases(owner, property, cases);
            code.Append(", Cases = [").Append(string.Join(", ", cases.Select(attribute => ContractTypes.Constraint(attribute, property, 0, Report)))).Append(']');
        }

        if (AlwaysPresent(property) is { } present)
        {
            if (levels[levels.Count - 1] != "Record" ||levels.Take(levels.Count - 1).Any(static level => level != "Array"))
            {
                Invalid(property, "[AlwaysPresent] names members of the record the member holds, so the member must hold a record or an array of records");
            }

            code.Append(", AlwaysPresent = ").Append(present);
        }

        return code.Append(" }").ToString();
    }

    /// <summary>The wire names an <c>[AlwaysPresent]</c> declares, as a collection expression, or null.</summary>
    private static string? AlwaysPresent(ISymbol symbol) =>
        Find(symbol, Contracts + "AlwaysPresentAttribute") is { } attribute
            ? "[" + string.Join(", ", attribute.ConstructorArguments.SelectMany(static argument =>
                argument.Kind == TypedConstantKind.Array ? argument.Values : ImmutableArray.Create(argument))
                .Select(static value => Literal(value.Value as string ?? string.Empty))) + "]"
            : null;

    /// <summary>Whether a member without its own summary is described by the record its schema references.</summary>
    private bool DescribedByRecord(ITypeSymbol type) =>
        ResultScalar(type) is null && Element(type) is null && type is INamedTypeSymbol { TypeKind: TypeKind.Class } record
        && (!SymbolEqualityComparer.Default.Equals(record.ContainingAssembly, compilation.Assembly) || DocumentationText.Summary(record) is not null);

    /// <summary>The JSON literal of a read-only member's constant initializer or expression body, or null.</summary>
    private string? Constant(SyntaxNode? syntax)
    {
        if (syntax is not PropertyDeclarationSyntax declaration)
        {
            return null;
        }

        ExpressionSyntax? expression = declaration.Initializer?.Value
            ?? declaration.ExpressionBody?.Expression
            ?? declaration.AccessorList?.Accessors.FirstOrDefault(static accessor => accessor.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.GetAccessorDeclaration))?.ExpressionBody?.Expression;
        if (expression is null)
        {
            return null;
        }

        Optional<object?> value = compilation.GetSemanticModel(expression.SyntaxTree).GetConstantValue(expression);
        return value.HasValue ? JsonLiteral(value.Value) : null;
    }

    /// <summary>The descriptor of a value's shape, or null when its type is not a result contract type.</summary>
    private string? Value(ITypeSymbol type, bool nullable, IPropertySymbol property)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } wrapped)
        {
            return Value(wrapped.TypeArguments[0], nullable: true, property);
        }

        string end = nullable ? ", Nullable = true }" : " }";
        if (ResultScalar(type) is { } scalar)
        {
            return $"new() {{ Kind = {Sdk}ResultValueKind.{scalar.Kind}{(scalar.Format is null ? string.Empty : ", Format = " + Literal(scalar.Format))}{end}";
        }

        if (Element(type) is { } element)
        {
            string? items = Value(element.Type, element.Nullable == NullableAnnotation.Annotated, property);
            return items is null ? null : $"new() {{ Kind = {Sdk}ResultValueKind.{element.Kind}, Items = {items}{end}";
        }

        if (type is INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false } record)
        {
            if (SymbolEqualityComparer.Default.Equals(record.ContainingAssembly, compilation.Assembly))
            {
                Enqueue(record);
            }
            else if (Find(record, Contracts + "SchemaIdAttribute") is null && !AnalyzerTypes.Inherits(record, ResultEnvelope))
            {
                Invalid(property, $"'{record.Name}' of another assembly is not a published result schema; a member may hold a record of another assembly only when it declares [SchemaId] or is a result");
                return null;
            }

            return $"new() {{ Kind = {Sdk}ResultValueKind.Record, Record = typeof({TypeName(record)}){end}";
        }

        Invalid(property, $"type '{type.ToDisplayString()}' is not a supported result type; use a scalar, a date, a JSON value, a record, T[], IReadOnlyList<T> or IReadOnlyDictionary<string, T>");
        return null;
    }

    /// <summary>The kind and format of a value that is not a record or collection, or null.</summary>
    private static (string Kind, string? Format)? ResultScalar(ITypeSymbol type) =>
        type.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString() switch
        {
            "System.DateTimeOffset" or "System.DateTime" => ("String", "date-time"),
            "System.Text.Json.Nodes.JsonObject" => ("Object", null),
            "System.Text.Json.Nodes.JsonNode" => ("Any", null),
            _ when type.TypeKind == TypeKind.Enum => null,
            _ => ScalarKind(type) is { } kind ? (kind, null) : null,
        };

    /// <summary>The value levels of a member from the outside in, such as Array, String, as constraints are placed on them.</summary>
    private static List<string> Levels(ITypeSymbol type)
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
                levels.Add(ResultScalar(current)?.Kind ?? "Record");
                return levels;
            }
        }
    }

    /// <summary>
    /// The cases of a member whose type another member decides: the discriminator is a member of
    /// the record with allowed values, and the cases cover each of them exactly once.
    /// </summary>
    private void CheckCases(INamedTypeSymbol owner, IPropertySymbol property, AttributeData[] cases)
    {
        if (Properties(owner).Count(static candidate => candidate.GetAttributes().Any(static attribute =>
                attribute.AttributeClass?.ToDisplayString() == Contracts + "OneOfByAttribute")) > 1)
        {
            Invalid(property, "only one member of a record may depend on a discriminator");
        }

        string?[] discriminators = [.. cases.Select(static attribute => attribute.ConstructorArguments[0].Value as string).Distinct()];
        IPropertySymbol? discriminator = discriminators.Length == 1
            ? Properties(owner).FirstOrDefault(candidate => CamelCase(candidate.Name) == discriminators[0])
            : null;
        AttributeData? allowed = discriminator?.GetAttributes().FirstOrDefault(static attribute => attribute.AttributeClass?.Name == "AllowedValuesAttribute");
        string[]? values = allowed is null ? null : AllowedStrings(allowed);
        if (values is null)
        {
            Invalid(property, "[OneOfBy] must name one discriminator member of the record that declares [AllowedValues] of strings");
            return;
        }

        string[] covered = [.. cases.SelectMany(static attribute => attribute.ConstructorArguments[1].Values.Select(static value => (string)value.Value!))];
        if (covered.Length != covered.Distinct(StringComparer.Ordinal).Count() || !new HashSet<string>(covered, StringComparer.Ordinal).SetEquals(values))
        {
            Invalid(property, $"[OneOfBy] cases must cover each allowed value of '{discriminators[0]}' exactly once: {string.Join(", ", values)}");
        }

        if (cases.Any(static attribute => Named(attribute, "Type") is string type && !CaseTypes.Contains(type)))
        {
            Invalid(property, $"[OneOfBy] Type must be one of: {string.Join(", ", CaseTypes)}");
        }
    }

    /// <summary>The string values an [AllowedValues] attribute lists, or null when they are not all strings.</summary>
    private static string[]? AllowedStrings(AttributeData allowed)
    {
        TypedConstant argument = allowed.ConstructorArguments.FirstOrDefault();
        object?[] values = argument.Kind switch
        {
            TypedConstantKind.Type => [.. ((ITypeSymbol)argument.Value!).GetMembers().OfType<IFieldSymbol>()
                .Where(static field => field.IsConst && field.DeclaredAccessibility == Accessibility.Public)
                .Select(static field => field.ConstantValue)],
            TypedConstantKind.Array => [.. argument.Values.Select(static value => value.Value)],
            _ => [],
        };
        return values.Length > 0 && values.All(static value => value is string) ? [.. values.Cast<string>()] : null;
    }

    private bool IsConstraint(AttributeData attribute) =>
        attribute.AttributeClass is { } type && BaseTypes(type).Any(current => SymbolEqualityComparer.Default.Equals(current, _constraint));

    private static bool IsAccessible(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility is Accessibility.Private or Accessibility.Protected or Accessibility.ProtectedAndInternal)
            {
                return false;
            }
        }

        return true;
    }

    private string Source(INamedTypeSymbol context)
    {
        var source = new StringBuilder("// <auto-generated />\n#nullable enable\n");
        if (!context.ContainingNamespace.IsGlobalNamespace)
        {
            source.Append($"\nnamespace {context.ContainingNamespace.ToDisplayString()};\n");
        }

        return source
            .Append($"\npartial class {context.Name} : {Sdk}IResultSchemaSource\n{{\n")
            .Append($"    global::System.Collections.Generic.IReadOnlyList<{Sdk}ResultRecord> {Sdk}IResultSchemaSource.ResultRecords => ResultContractRecords.All;\n\n")
            .Append("    /// <summary>The result records this assembly describes, generated from their declarations.</summary>\n")
            .Append("    private static class ResultContractRecords\n    {\n")
            .Append($"        internal static readonly global::System.Collections.Generic.IReadOnlyList<{Sdk}ResultRecord> All =\n        [\n")
            .Append(string.Join("\n", _order.Select(type => _code[type])))
            .Append("\n        ];\n    }\n}\n")
            .ToString();
    }

    private string Invalid(IPropertySymbol property, string reason)
    {
        Report(SourceLocation(property), $"'{property.ContainingType.Name}.{property.Name}': {reason}.");
        return string.Empty;
    }

    private void Report(ISymbol symbol, string message) => Report(SourceLocation(symbol), message);

    private void Report(Location location, string message)
    {
        _failed = true;
        report(location, message);
    }
}
