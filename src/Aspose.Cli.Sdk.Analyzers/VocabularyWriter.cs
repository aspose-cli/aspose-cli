using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CSharpDisplay = Microsoft.CodeAnalysis.CSharp.SymbolDisplay;

namespace Aspose.Cli.Sdk.Analyzers;

/// <summary>Writes the generated source of one operation vocabulary and reports its contract errors.</summary>
internal sealed class VocabularyWriter(
    Compilation compilation,
    INamedTypeSymbol vocabulary,
    List<(string Name, INamedTypeSymbol Type)> operations,
    Action<Location, string> report)
{
    private const string Sdk = "global::Aspose.Cli.Sdk.Operations.";
    private const string BoundedOperation = "Aspose.Cli.Sdk.Contracts.BoundedOperation";
    private const string Json = "System.Text.Json.Serialization.";
    private readonly Dictionary<INamedTypeSymbol, string?> _nestedLocals = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<string, INamedTypeSymbol> _definitions = new(StringComparer.Ordinal);
    private readonly StringBuilder _nested = new();
    private readonly INamedTypeSymbol? _constraint = compilation.GetTypeByMetadataName(OperationContractGenerator.Operations + "ValueConstraintAttribute");
    private readonly INamedTypeSymbol? _recordRule = compilation.GetTypeByMetadataName(OperationContractGenerator.Operations + "RecordRuleAttribute");
    private readonly INamedTypeSymbol? _memberCount = compilation.GetTypeByMetadataName(OperationContractGenerator.Operations + "MemberCountAttribute");
    private readonly INamedTypeSymbol? _inputPath = compilation.GetTypeByMetadataName(OperationContractGenerator.Operations + "InputPathAttribute");
    private readonly INamedTypeSymbol? _secretEnv = compilation.GetTypeByMetadataName(OperationContractGenerator.Operations + "SecretEnvAttribute");
    private readonly INamedTypeSymbol? _minProperties = compilation.GetTypeByMetadataName(OperationContractGenerator.Operations + "MinPropertiesAttribute");
    private readonly INamedTypeSymbol? _presentWhen = compilation.GetTypeByMetadataName(OperationContractGenerator.Operations + "PresentWhenAttribute");
    private bool _failed;

    /// <summary>
    /// Every record the vocabulary describes: its operations, the base records they inherit
    /// members from and their nested records.
    /// </summary>
    public HashSet<INamedTypeSymbol> Records { get; } = new(SymbolEqualityComparer.Default);

    /// <summary>Returns the generated source, or null when the contract has errors.</summary>
    public string? Write()
    {
        AttributeData declaration = Find(vocabulary, OperationContractGenerator.Operations + "OperationVocabularyAttribute")!;
        string schemaId = SchemaId(declaration);
        int maximum = Named(declaration, "MaximumOperations") is int value ? value : 0;
        INamedTypeSymbol? context = Named(declaration, "JsonContext") as INamedTypeSymbol;
        if (!vocabulary.IsAbstract || !vocabulary.IsRecord || vocabulary.ContainingType is not null
            || !BaseTypes(vocabulary).Any(static type => type.ToDisplayString() == BoundedOperation)
            || vocabulary.DeclaringSyntaxReferences.Any(static reference =>
                reference.GetSyntax() is TypeDeclarationSyntax syntax
                && !syntax.Modifiers.Any(static modifier => modifier.ValueText == "partial")))
        {
            Report(vocabulary, $"Vocabulary '{vocabulary.Name}' must be a top-level abstract partial record that derives from BoundedOperation.");
        }

        if (schemaId.Length == 0 || maximum < 1)
        {
            Report(vocabulary, $"Vocabulary '{vocabulary.Name}' must name its schema id and a positive MaximumOperations.");
        }

        CheckJsonContext(context);
        (string Name, INamedTypeSymbol Type)[] ordered = [.. operations.OrderBy(static operation => operation.Name, StringComparer.Ordinal)];
        _definitions["id"] = vocabulary;
        foreach ((string name, INamedTypeSymbol type) in ordered)
        {
            if (_definitions.ContainsKey(name))
            {
                Report(type, $"Operation name '{name}' is declared more than once.");
            }

            _definitions[name] = type;
        }

        string[] descriptors = [.. ordered.Select(operation => Operation(operation.Name, operation.Type))];
        return _failed ? null : Source(schemaId, maximum, context!, ordered, descriptors);
    }

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

    /// <summary>
    /// The schema id as a C# expression. A constant built from generated build metadata has no
    /// value this generator can see, so a field reference is emitted as that field.
    /// </summary>
    private string SchemaId(AttributeData declaration)
    {
        if (declaration.ConstructorArguments.FirstOrDefault().Value is string { Length: > 0 } literal)
        {
            return Literal(literal);
        }

        return declaration.ApplicationSyntaxReference?.GetSyntax() is AttributeSyntax { ArgumentList.Arguments: { Count: > 0 } arguments }
            && arguments[0] is var argument
            && compilation.GetSemanticModel(argument.SyntaxTree).GetSymbolInfo(argument.Expression).Symbol is IFieldSymbol { IsConst: true } field
                ? $"{TypeName(field.ContainingType)}.{field.Name}"
                : string.Empty;
    }

    private void CheckJsonContext(INamedTypeSymbol? context)
    {
        if (context is null || !BaseTypes(context).Any(static type => type.ToDisplayString() == Json + "JsonSerializerContext"))
        {
            Report(vocabulary, $"Vocabulary '{vocabulary.Name}' must name its source-generated JsonSerializerContext as JsonContext.");
            return;
        }

        // JsonKnownNamingPolicy.CamelCase and JsonSerializerDefaults.Web are both 1.
        AttributeData? options = Find(context, Json + "JsonSourceGenerationOptionsAttribute");
        object? policy = options is null ? null : Named(options, "PropertyNamingPolicy") ?? options.ConstructorArguments.FirstOrDefault().Value;
        if (policy is not 1)
        {
            Report(context, $"JSON context '{context.Name}' of vocabulary '{vocabulary.Name}' must use camelCase property names.");
        }

        var listed = new HashSet<ITypeSymbol>(
            context.GetAttributes()
                .Where(static attribute => attribute.AttributeClass?.ToDisplayString() == Json + "JsonSerializableAttribute")
                .Select(static attribute => attribute.ConstructorArguments.FirstOrDefault().Value)
                .OfType<ITypeSymbol>(),
            SymbolEqualityComparer.Default);
        foreach ((string _, INamedTypeSymbol type) in operations.Where(operation => !listed.Contains(operation.Type)))
        {
            Report(type, $"Operation '{type.Name}' is missing from JSON context '{context.Name}'; add [JsonSerializable(typeof({type.Name}))].");
        }
    }

    private string Operation(string name, INamedTypeSymbol type)
    {
        (string record, IReadOnlyList<IPropertySymbol> properties) = Record(type, name, isOperation: true);
        var code = new StringBuilder("            new() { Record = new() ").Append(record);
        IPropertySymbol[] paths = [.. properties.Where(property => Has(property, _inputPath))];
        if (paths.Length > 0)
        {
            IEnumerable<string> resolved = paths.Select(static property => property.NullableAnnotation == NullableAnnotation.Annotated
                ? $"{property.Name} = value.{property.Name} is null ? null : resolve(value.{property.Name})"
                : $"{property.Name} = resolve(value.{property.Name})");
            code.Append($", ResolveInputPaths = static (operation, resolve) => {{ var value = ({TypeName(type)})operation; return value with {{ {string.Join(", ", resolved)} }}; }}");
        }

        IPropertySymbol[] secrets = [.. properties.Where(property => Has(property, _secretEnv))];
        if (secrets.Length > 0)
        {
            code.Append($", SecretVariables = static operation => {{ var value = ({TypeName(type)})operation; return new string?[] {{ {string.Join(", ", secrets.Select(static property => "value." + property.Name))} }}; }}");
        }

        return code.Append(" },").ToString();
    }

    /// <summary>The object initializer of a record's descriptor and the members it describes.</summary>
    private (string Code, IReadOnlyList<IPropertySymbol> Properties) Record(INamedTypeSymbol type, string name, bool isOperation)
    {
        Records.UnionWith(Chain(type));
        foreach (IPropertySymbol property in ConstrainedOverrides(type))
        {
            Invalid(property, "an override restates only the summary; state the rest on the base member");
        }

        var members = new List<Member>();
        foreach (IPropertySymbol property in Properties(type))
        {
            if (Describe(type, property, isOperation) is not { } member)
            {
                continue;
            }

            if (member.Wire is "op" or "id")
            {
                Invalid(property, $"the wire name '{member.Wire}' belongs to the operation envelope");
            }
            else if (members.Any(other => other.Wire == member.Wire))
            {
                Invalid(property, $"another member of '{type.Name}' already has the wire name '{member.Wire}'");
            }

            members.Add(member);
        }

        var code = new StringBuilder("{ Type = typeof(").Append(TypeName(type)).Append("), Name = ").Append(Literal(name));
        if (DocumentationText.Summary(type) is { } description)
        {
            code.Append(", Description = ").Append(Literal(description));
        }

        if (Find(type, OperationContractGenerator.Operations + "MistakenForAttribute") is { } mistaken)
        {
            if (isOperation)
            {
                code.Append(", MistakenFor = ").Append(MistakenNames(mistaken));
            }
            else
            {
                Report(type, $"[MistakenFor] on '{type.Name}' names mistaken operation names, but '{type.Name}' is not an operation; declare it on a member instead.");
            }
        }

        // One member per line keeps the generated file reviewable.
        code.Append(", Properties = [").Append(string.Concat(members.Select(static member => "\n                " + member.Code + ",")))
            .Append(members.Count == 0 ? "]" : "\n            ]");
        string[] rules = [.. Chain(type).SelectMany(current => current.GetAttributes()
            .Where(IsConstraint)
            .Select(attribute =>
            {
                CheckRule(type, members, attribute, current, isOperation);
                return Constraint(attribute, current);
            }))];
        if (rules.Length > 0)
        {
            code.Append(", Constraints = [").Append(string.Join(", ", rules)).Append(']');
        }

        return (code.Append(" }").ToString(), [.. members.Select(static member => member.Property)]);
    }

    /// <summary>
    /// A record rule must name members of the record, and every member it counts (the named
    /// ones, or all when it names none) must be unset exactly when the JSON omits it, because
    /// defaults are written before the rule counts: a nullable member without a default, or
    /// for a member count a Boolean that defaults to false, which the schema states as
    /// <c>const: true</c>. <c>[PresentWhen]</c> counts only its member; <c>[MinProperties]</c>
    /// fits nested records only, because an operation's schema also counts <c>op</c> and <c>id</c>.
    /// </summary>
    private void CheckRule(INamedTypeSymbol type, List<Member> members, AttributeData attribute, INamedTypeSymbol owner, bool isOperation)
    {
        if (!Derives(attribute.AttributeClass!, _recordRule))
        {
            return;
        }

        Location location = AttributeLocation(attribute, owner);
        if (isOperation && SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, _minProperties))
        {
            Report(location, $"[MinProperties] on '{owner.Name}' would also count the operation's op and id; name the members with [AtLeastOneOf].");
            return;
        }

        bool countsBooleans = Derives(attribute.AttributeClass!, _memberCount);
        bool presentWhen = SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, _presentWhen);
        string[] names = presentWhen
            ? [.. attribute.ConstructorArguments.Take(2).Select(static argument => argument.Value).OfType<string>()]
            : [.. attribute.ConstructorArguments.SelectMany(static argument =>
                argument.Kind == TypedConstantKind.Array ? argument.Values : [argument]).Select(static value => value.Value).OfType<string>()];
        foreach (string name in names.Where(name => members.All(member => member.Wire != name)))
        {
            Report(location, $"Record rule on '{owner.Name}' names '{name}', which is not a member of '{type.Name}'.");
        }

        string[] counted = presentWhen ? [.. names.Take(1)] : names;
        foreach (Member member in members.Where(member => counted.Length == 0 || counted.Contains(member.Wire)))
        {
            bool unsetWhenOmitted = member.Nullable ? member.Default is null : countsBooleans && member.IsBoolean && member.Default == "false";
            if (!unsetWhenOmitted)
            {
                Report(
                    location,
                    $"Record rule on '{owner.Name}' counts '{member.Wire}', which must be nullable without a default{(countsBooleans ? " or a Boolean that defaults to false" : string.Empty)}.");
            }
        }

        if (presentWhen)
        {
            object? value = attribute.ConstructorArguments.ElementAtOrDefault(2).Value;
            Member? condition = members.FirstOrDefault(member => member.Wire == names.ElementAtOrDefault(1));
            if (JsonLiteral(value) is null)
            {
                Report(location, $"[PresentWhen] on '{owner.Name}' must compare with a string, finite number or Boolean constant.");
            }
            else if (condition is { IsBoolean: true, Nullable: false } && value is false)
            {
                Report(location, $"[PresentWhen] on '{owner.Name}' compares '{condition.Wire}' with false, but a Boolean that is not nullable is set only when true; compare it with true.");
            }
        }
    }

    private static bool Derives(INamedTypeSymbol type, INamedTypeSymbol? baseType) =>
        BaseTypes(type).Any(current => SymbolEqualityComparer.Default.Equals(current, baseType));

    /// <summary>The record's own and inherited contract types, base first, below BoundedOperation.</summary>
    private static IEnumerable<INamedTypeSymbol> Chain(INamedTypeSymbol type) =>
        BaseTypes(type)
            .TakeWhile(static current => current.ToDisplayString() is not (BoundedOperation or "object"))
            .Reverse()
            .Append(type);

    /// <summary>Public instance members in declaration order, inherited members first.</summary>
    private static IEnumerable<IPropertySymbol> Properties(INamedTypeSymbol type) =>
        Chain(type).SelectMany(static current => current.GetMembers().OfType<IPropertySymbol>().Where(static property =>
            !property.IsStatic && !property.IsIndexer && !property.IsImplicitlyDeclared
            && property.DeclaredAccessibility == Accessibility.Public && property.GetMethod is not null
            && property.OverriddenProperty is null));

    /// <summary>
    /// Overrides in the record's chain that state more than a summary. A member is described from
    /// its base declaration, so an attribute, an initializer, an accessor body or an added 'required'
    /// on an override would be neither enforced nor in the schema. A 'new' member is not an override; it is
    /// described on its own and collides with the base member's wire name.
    /// </summary>
    private static IEnumerable<IPropertySymbol> ConstrainedOverrides(INamedTypeSymbol type) =>
        Chain(type).SelectMany(static current => current.GetMembers().OfType<IPropertySymbol>().Where(static property =>
            property.OverriddenProperty is { } overridden
            && (property.GetAttributes().Length > 0
                || property.IsRequired != overridden.IsRequired
                || property.DeclaringSyntaxReferences.Any(static reference =>
                    reference.GetSyntax() is PropertyDeclarationSyntax syntax
                    && (syntax.Initializer is not null || syntax.ExpressionBody is not null
                        || syntax.AccessorList?.Accessors.Any(static accessor => accessor.Body is not null || accessor.ExpressionBody is not null) == true)))));

    /// <summary>
    /// A member's summary as the record states it: an override of the member in the record's
    /// chain restates what the member means for that record, and the most derived one wins.
    /// </summary>
    private static string? Summary(INamedTypeSymbol owner, IPropertySymbol property) =>
        Chain(owner).Reverse()
            .SelectMany(current => current.GetMembers(property.Name).OfType<IPropertySymbol>())
            .Where(candidate => Overrides(candidate, property))
            .Select(DocumentationText.Summary)
            .FirstOrDefault(static text => text is not null);

    private static bool Overrides(IPropertySymbol candidate, IPropertySymbol property)
    {
        for (IPropertySymbol? current = candidate; current is not null; current = current.OverriddenProperty)
        {
            if (SymbolEqualityComparer.Default.Equals(current, property))
            {
                return true;
            }
        }

        return false;
    }

    private Member? Describe(INamedTypeSymbol owner, IPropertySymbol property, bool isOperation)
    {
        if (property.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not PropertyDeclarationSyntax declaration)
        {
            Invalid(property, "declare it as a property with an initializer or 'required', not as a positional record parameter");
            return null;
        }

        if ((Has(property, _inputPath) || Has(property, _secretEnv))
            && (!isOperation || property.Type.SpecialType != SpecialType.System_String))
        {
            Invalid(property, "an input path or secret variable must be a string member of an operation");
        }

        if (Find(property, Json + "JsonPropertyNameAttribute") is not null || Find(property, Json + "JsonIgnoreAttribute") is not null)
        {
            Invalid(property, "a contract member always takes part under its camelCase name; remove [JsonPropertyName] and [JsonIgnore]");
        }

        string wire = CamelCase(property.Name);
        bool nullable = property.NullableAnnotation == NullableAnnotation.Annotated;
        var code = new StringBuilder("new() { Name = ").Append(Literal(wire));
        if (Summary(owner, property) is { } description)
        {
            code.Append(", Description = ").Append(Literal(description));
        }

        code.Append(", Value = ").Append(Value(property.Type, nullable, property));
        string? literal = property.IsRequired ? null : Default(property, declaration);
        if (property.IsRequired)
        {
            code.Append(", Required = true");
        }
        else if (literal is not null)
        {
            code.Append(", Default = ").Append(Literal(literal));
        }

        List<string> levels = Levels(property.Type);
        List<string> constraints = [.. property.GetAttributes().Where(IsConstraint)
            .Select(attribute => Constraint(attribute, property, Place(attribute, levels, property)))];
        if (IsUnsigned(Scalar(property.Type)))
        {
            constraints.Insert(0, $"new {Sdk}MinimumAttribute(0)" + DepthOf(levels.Count - 1));
        }

        if (constraints.Count > 0)
        {
            code.Append(", Constraints = [").Append(string.Join(", ", constraints)).Append(']');
        }

        if (Find(property, OperationContractGenerator.Operations + "MistakenForAttribute") is { } mistaken)
        {
            code.Append(", MistakenFor = ").Append(MistakenNames(mistaken));
        }

        code.Append(", Get = static value => ((").Append(TypeName(owner)).Append(")value).").Append(property.Name).Append(" }");
        return new Member(
            property,
            wire,
            code.ToString(),
            literal,
            nullable || property.Type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T,
            property.Type.SpecialType == SpecialType.System_Boolean);
    }

    /// <summary>The collection expression of the names a <c>[MistakenFor]</c> attribute declares.</summary>
    private static string MistakenNames(AttributeData mistaken) =>
        "[" + string.Join(", ", mistaken.ConstructorArguments[0].Values.Select(static name => Literal((string)name.Value!))) + "]";

    /// <summary>The JSON literal an omitted member takes, or null when it has none.</summary>
    private string? Default(IPropertySymbol property, PropertyDeclarationSyntax declaration)
    {
        bool nullable = property.NullableAnnotation == NullableAnnotation.Annotated
            || property.Type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
        ExpressionSyntax? initializer = declaration.Initializer?.Value;
        if (initializer is null)
        {
            // An omitted member keeps its CLR default, which for Booleans and numbers is a value.
            return nullable
                ? null
                : ScalarKind(property.Type) switch
                {
                    "Boolean" => "false",
                    "Integer" or "Number" => "0",
                    _ => Invalid(property, "a member that is not nullable needs 'required' or an initializer"),
                };
        }

        if (IsEmpty(initializer))
        {
            // An empty collection or object initializer: its JSON form follows the member's kind.
            return Element(property.Type)?.Kind switch
            {
                "Array" => "[]",
                "Map" => "{}",
                _ => property.Type.TypeKind == TypeKind.Class && ScalarKind(property.Type) is null
                    ? "{}"
                    : Invalid(property, "a default must be a string, a finite number or a Boolean constant, or an empty object or collection"),
            };
        }

        Optional<object?> constant = compilation.GetSemanticModel(declaration.SyntaxTree).GetConstantValue(initializer);
        if (constant is { HasValue: true, Value: null } && nullable)
        {
            return null;
        }

        return (constant.HasValue ? JsonLiteral(constant.Value) : null)
            ?? Invalid(property, "a default must be a string, a finite number or a Boolean constant, or an empty object or collection");
    }

    private static bool IsEmpty(ExpressionSyntax initializer) => initializer switch
    {
        ImplicitObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 0, Initializer: null } => true,
        ObjectCreationExpressionSyntax { ArgumentList: null or { Arguments.Count: 0 }, Initializer: null } => true,
        CollectionExpressionSyntax { Elements.Count: 0 } => true,
        ArrayCreationExpressionSyntax { Initializer: { Expressions.Count: 0 } } => true,
        ArrayCreationExpressionSyntax { Initializer: null } creation =>
            creation.Type.RankSpecifiers.Count == 1
            && creation.Type.RankSpecifiers[0].Sizes.Count == 1
            && creation.Type.RankSpecifiers[0].Sizes[0] is LiteralExpressionSyntax { Token.ValueText: "0" },
        _ => false,
    };

    private string Value(ITypeSymbol type, bool nullable, IPropertySymbol property)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } wrapped)
        {
            return Value(wrapped.TypeArguments[0], nullable: true, property);
        }

        string end = nullable ? ", Nullable = true }" : " }";
        if (ScalarKind(type) is { } kind)
        {
            return $"new() {{ Kind = {Sdk}OperationValueKind.{kind}{end}";
        }

        if (Element(type) is { } element)
        {
            string items = Value(element.Type, element.Nullable == NullableAnnotation.Annotated, property);
            return $"new() {{ Kind = {Sdk}OperationValueKind.{element.Kind}, Items = {items}{end}";
        }

        if (type is INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false } record
            && SymbolEqualityComparer.Default.Equals(record.ContainingAssembly, compilation.Assembly))
        {
            return $"new() {{ Kind = {Sdk}OperationValueKind.Record, Record = {Nested(record, property)}{end}";
        }

        return Invalid(property, $"type '{type.ToDisplayString()}' is not a supported contract type; use a scalar, a contract record, T[], IReadOnlyList<T> or IReadOnlyDictionary<string, T>");
    }

    /// <summary>Declares a nested record once, as a local of the generated method, and returns the local.</summary>
    private string Nested(INamedTypeSymbol type, IPropertySymbol property)
    {
        if (_nestedLocals.TryGetValue(type, out string? local))
        {
            return local ?? Invalid(property, $"record '{type.Name}' contains itself");
        }

        string name = CamelCase(type.Name);
        if (_definitions.TryGetValue(name, out INamedTypeSymbol? owner) && !SymbolEqualityComparer.Default.Equals(owner, type))
        {
            Invalid(property, $"record '{type.Name}' would publish the schema definition '{name}', which '{owner.Name}' already has");
        }

        _definitions[name] = type;
        _nestedLocals.Add(type, null);
        (string initializer, _) = Record(type, name, isOperation: false);
        local = "record" + _nestedLocals.Values.Count(static name => name is not null);
        _nestedLocals[type] = local;
        _nested.Append($"        var {local} = new {Sdk}OperationRecord ").Append(initializer).Append(";\n");
        return local;
    }

    private bool IsConstraint(AttributeData attribute) => attribute.AttributeClass is { } type && Derives(type, _constraint);

    /// <summary>
    /// The level of a member's value a constraint applies to (see ValueConstraintAttribute):
    /// an array constraint applies exactly at its declared depth, and any other constraint at
    /// the first level at or below it that is not an array or map.
    /// </summary>
    private int Place(AttributeData attribute, List<string> levels, IPropertySymbol property)
    {
        int declared = Named(attribute, "Depth") is int depth ? depth : 0;
        string[]? kinds = Kinds(attribute.AttributeClass!);
        string name = attribute.AttributeClass!.Name;
        string subject = $"'{property.ContainingType.Name}.{property.Name}': [{(name.EndsWith("Attribute", StringComparison.Ordinal) ? name.Substring(0, name.Length - "Attribute".Length) : name)}]";
        if (kinds is null)
        {
            Report(AttributeLocation(attribute, property), $"{subject} must derive from a constraint of the SDK, so the operation generator knows which values it applies to.");
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

        Report(
            AttributeLocation(attribute, property),
            $"{subject} applies to {string.Join(" or ", kinds)} values, and the member has none at depth {declared}{(descends ? " or below" : string.Empty)}.");
        return declared;
    }

    /// <summary>The value kinds a constraint of the SDK applies to, or null for any other attribute.</summary>
    private static string[]? Kinds(INamedTypeSymbol attribute)
    {
        for (INamedTypeSymbol? current = attribute; current is not null; current = current.BaseType)
        {
            if (current.ContainingNamespace?.ToDisplayString() + "." == OperationContractGenerator.Operations
                && ConstraintKinds.TryGetValue(current.Name, out string[]? kinds))
            {
                return kinds;
            }
        }

        return null;
    }

    /// <summary>The value kinds each constraint of the SDK applies to, by its type or base type.</summary>
    private static readonly Dictionary<string, string[]> ConstraintKinds = new(StringComparer.Ordinal)
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
                levels.Add(ScalarKind(current) ?? "Record");
                return levels;
            }
        }
    }

    /// <summary>Re-creates a declared constraint attribute with its arguments, at its placed depth.</summary>
    private string Constraint(AttributeData attribute, ISymbol owner, int depth = 0)
    {
        string named = string.Join(", ", attribute.NamedArguments
            .Where(static argument => argument.Key != "Depth")
            .Select(argument => $"{argument.Key} = {Argument(argument.Value, attribute, owner)}")
            .Concat(depth > 0 ? new[] { $"Depth = {depth}" } : Array.Empty<string>()));
        return $"new {TypeName(attribute.AttributeClass!)}({string.Join(", ", attribute.ConstructorArguments.Select(argument => Argument(argument, attribute, owner)))})"
            + (named.Length == 0 ? string.Empty : $" {{ {named} }}");
    }

    private static string DepthOf(int depth) => depth > 0 ? $" {{ Depth = {depth} }}" : string.Empty;

    /// <summary>
    /// Renders an attribute argument as C#. A type argument stands for the public constants of
    /// that type, rendered as an object array in declaration order.
    /// </summary>
    private string Argument(TypedConstant argument, AttributeData attribute, ISymbol owner)
    {
        switch (argument.Kind)
        {
            case TypedConstantKind.Array:
                return $"new {TypeName(argument.Type!)} {{ {string.Join(", ", argument.Values.Select(value => Argument(value, attribute, owner)))} }}";
            case TypedConstantKind.Type:
                IFieldSymbol[] constants = [.. ((ITypeSymbol)argument.Value!).GetMembers().OfType<IFieldSymbol>()
                    .Where(static field => field.IsConst && field.DeclaredAccessibility == Accessibility.Public)];
                if (constants.Length == 0 || constants.Any(static field => JsonLiteral(field.ConstantValue) is null))
                {
                    Report(AttributeLocation(attribute, owner), $"'{((ITypeSymbol)argument.Value!).Name}' must declare public string, finite number or Boolean constants only.");
                }

                return "new object[] { " + string.Join(", ", constants.Select(static field => Primitive(field.ConstantValue))) + " }";
            default:
                if (argument.Value is double or float && JsonLiteral(argument.Value) is null)
                {
                    Report(AttributeLocation(attribute, owner), "A constraint argument must be a finite number.");
                }

                return argument.IsNull ? "null" : $"({TypeName(argument.Type!)}){Primitive(argument.Value)}";
        }
    }

    private string Source(
        string schemaId,
        int maximum,
        INamedTypeSymbol context,
        (string Name, INamedTypeSymbol Type)[] ordered,
        string[] descriptors)
    {
        string self = TypeName(vocabulary);
        string handler = $"I{vocabulary.Name}Handler";
        string description = DocumentationText.Summary(vocabulary) is { } summary ? Literal(summary) : "null";
        var source = new StringBuilder("// <auto-generated />\n#nullable enable\n");
        if (!vocabulary.ContainingNamespace.IsGlobalNamespace)
        {
            source.Append($"\nnamespace {vocabulary.ContainingNamespace.ToDisplayString()};\n");
        }

        source
            .Append($"\n/// <summary>Applies each {vocabulary.Name} operation; an engine implements one method per operation.</summary>\n")
            .Append($"{(vocabulary.DeclaredAccessibility == Accessibility.Public ? "public" : "internal")} interface {handler}<out TResult>\n{{\n");
        foreach ((string name, INamedTypeSymbol type) in ordered)
        {
            source.Append($"    /// <summary>Applies <c>{name}</c>.</summary>\n    TResult Apply({TypeName(type)} operation);\n");
        }

        source
            .Append("}\n\n")
            .Append($"partial record {vocabulary.Name} : {Sdk}IOperationVocabulary<{self}>\n{{\n")
            .Append("    /// <summary>The vocabulary's operations, generated from their records.</summary>\n")
            .Append($"    public static {Sdk}OperationCatalog<{self}> Catalog {{ get; }} = new(\n")
            .Append($"        {schemaId},\n        {maximum},\n        {description},\n")
            .Append($"        {TypeName(context)}.Default,\n        CreateOperations());\n\n")
            .Append("    /// <summary>Calls the handler method of this operation.</summary>\n")
            .Append($"    public TResult Accept<TResult>({handler}<TResult> handler) => this switch\n    {{\n");
        foreach ((string _, INamedTypeSymbol type) in ordered)
        {
            source.Append($"        {TypeName(type)} operation => handler.Apply(operation),\n");
        }

        return source
            .Append("        _ => throw new global::System.InvalidOperationException($\"{GetType().Name} is not an operation of its vocabulary.\"),\n    };\n\n")
            .Append($"    private static global::System.Collections.Generic.IReadOnlyList<{Sdk}OperationDescriptor> CreateOperations()\n    {{\n")
            .Append(_nested)
            .Append("        return\n        [\n")
            .Append(string.Join("\n", descriptors))
            .Append("\n        ];\n    }\n}\n")
            .ToString();
    }

    private string Invalid(IPropertySymbol property, string reason)
    {
        Report(property, $"'{property.ContainingType.Name}.{property.Name}': {reason}.");
        return string.Empty;
    }

    private void Report(ISymbol symbol, string message) => Report(SourceLocation(symbol), message);

    private void Report(Location location, string message)
    {
        _failed = true;
        report(location, message);
    }

    private static bool Has(ISymbol symbol, INamedTypeSymbol? attribute) =>
        attribute is not null && symbol.GetAttributes().Any(data => SymbolEqualityComparer.Default.Equals(data.AttributeClass, attribute));

    private static object? Named(AttributeData attribute, string name) =>
        attribute.NamedArguments.FirstOrDefault(argument => argument.Key == name).Value.Value;

    private static string? ScalarKind(ITypeSymbol type) => type.SpecialType switch
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
    private static (string Kind, ITypeSymbol Type, NullableAnnotation Nullable)? Element(ITypeSymbol type) => type switch
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
    private static ITypeSymbol Scalar(ITypeSymbol type) =>
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } wrapped ? Scalar(wrapped.TypeArguments[0])
        : Element(type) is { } element ? Scalar(element.Type)
        : type;

    private static bool IsUnsigned(ITypeSymbol type) =>
        type.SpecialType is SpecialType.System_Byte or SpecialType.System_UInt16 or SpecialType.System_UInt32 or SpecialType.System_UInt64;

    private static string TypeName(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private static string Literal(string value) => CSharpDisplay.FormatLiteral(value, quote: true);

    private static string Primitive(object? value) => value switch
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
    private static string? JsonLiteral(object? value) => value switch
    {
        string text => JsonString(text),
        bool flag => flag ? "true" : "false",
        double number when !double.IsNaN(number) && !double.IsInfinity(number) => number.ToString("R", CultureInfo.InvariantCulture),
        float number when !float.IsNaN(number) && !float.IsInfinity(number) => number.ToString("R", CultureInfo.InvariantCulture),
        decimal or int or long or short or byte or sbyte or uint or ulong or ushort =>
            ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture),
        _ => null,
    };

    private static string JsonString(string value)
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
    private static string CamelCase(string name)
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

    /// <summary>One described member of a contract record.</summary>
    private sealed class Member(IPropertySymbol property, string wire, string code, string? @default, bool nullable, bool isBoolean)
    {
        public IPropertySymbol Property { get; } = property;

        public string Wire { get; } = wire;

        public string Code { get; } = code;

        public string? Default { get; } = @default;

        public bool Nullable { get; } = nullable;

        public bool IsBoolean { get; } = isBoolean;
    }
}
