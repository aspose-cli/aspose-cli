using System.Reflection;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit.Scenarios;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>
/// Every warning the CLI can emit carries a declared code: <c>Warning</c> takes its code as a
/// descriptor object, never as a string, so an undeclared code cannot be written; each descriptor
/// is a static member of a diagnostics class, emitted somewhere, and listed by capabilities. The
/// rules read the SDK's public <c>Warning</c> type, the source syntax under <c>src</c> and the live
/// capabilities, so they hold whatever the descriptor type and the diagnostics classes are named.
/// </summary>
public sealed partial class WarningCodeDeclarationTests
{
    private const string WarningTypeName = "Warning";

    [Fact]
    public void Warning_TakesItsCodeOnlyAsADescriptor()
    {
        Type warning = WarningType();
        string[] stringCodes =
        [
            .. warning.GetConstructors()
                .SelectMany(static constructor => constructor.GetParameters())
                .Where(static parameter => IsCode(parameter.Name) && parameter.ParameterType == typeof(string))
                .Select(static parameter => $"constructor parameter '{parameter.Name}'"),
            .. warning.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(static property => IsCode(property.Name) && property.PropertyType == typeof(string) && property.SetMethod?.IsPublic == true)
                .Select(static property => $"settable property '{property.Name}'"),
        ];

        Assert.True(stringCodes.Length == 0,
            $"{warning.FullName} accepts its code as a string ({string.Join(", ", stringCodes)}), so any code can be emitted "
            + "without a declaration. It must take a warning-code descriptor instead. Today's constructions that set the code as a string:"
            + Environment.NewLine + string.Join(Environment.NewLine, StringCodedConstructions().Select(static site => "  " + site.Location)));
    }

    [Fact]
    public void EveryWarningCodeDescriptor_IsAStaticMemberThatIsEmitted()
    {
        Type descriptor = DescriptorType()
            ?? throw Xunit.Sdk.FailException.ForFailure(
                $"{WarningType().FullName} takes no warning-code descriptor (a constructor parameter or settable property named code whose type is not string).");
        Declaration[] declarations = Declarations(descriptor.Name);
        Assert.NotEmpty(declarations);

        // A descriptor is created only as the initializer of a static member of a diagnostics class,
        // explicitly typed or target-typed alike.
        string[] loose =
        [
            .. SourceSemantics.Models.Value.SelectMany(model => LooseCreations(model, descriptor.Name)
                .Select(creation => SourceSemantics.At(model, creation, creation.ToString()))),
        ];
        OwnershipSourceFiles.AssertNone(loose,
            $"A {descriptor.Name} is created only as a static member of a diagnostics class, never inline at an emission site:");

        string[] duplicated =
        [
            .. declarations.GroupBy(static declaration => declaration.Code, StringComparer.Ordinal)
                .Where(static group => group.Count() > 1)
                .Select(static group => $"{group.Key}: {string.Join(", ", group.Select(static declaration => declaration.Location))}"),
        ];
        OwnershipSourceFiles.AssertNone(duplicated, "Each warning code is declared once:");

        // Every declaration is used outside its own file, other than in an All list.
        string[] orphans =
        [
            .. declarations.Where(declaration => !OwnershipSourceFiles.All.Any(file => file.Path != declaration.File
                    && file.Root.DescendantNodes().OfType<IdentifierNameSyntax>().Any(name =>
                        name.Identifier.ValueText == declaration.Member && !InAllList(name))))
                .Select(static declaration => $"{declaration.Location}: {declaration.Code}"),
        ];
        OwnershipSourceFiles.AssertNone(orphans, "Every declared warning code has an emission site; delete the codes nothing emits:");
    }

    /// <summary>
    /// The rule above catches every way C# writes a creation: a target-typed <c>new("X")</c> as
    /// the code of a warning, a local, a return value or a helper's argument is as loose as an
    /// explicit <c>new WarningCode("X")</c>; only the static declarations and the descriptor's own
    /// factory for codes read back from another process are allowed.
    /// </summary>
    [Fact]
    public void LooseCreationRule_CatchesExplicitAndTargetTypedCreations()
    {
        const string Source = """
            using Aspose.Cli.Sdk.Contracts;

            internal static class FixtureDiagnostics
            {
                internal static readonly WarningCode Declared = new("DECLARED");
                internal static readonly WarningCode DeclaredExplicitly = new WarningCode("DECLARED_EXPLICITLY");
                internal static WarningCode DeclaredAsExpression => new("DECLARED_AS_EXPRESSION");
                internal static readonly WarningCode[] All = [Declared, DeclaredExplicitly];
            }

            internal sealed class FixtureEmitter
            {
                private readonly WarningCode _instance = new("LOOSE_INSTANCE_FIELD");
                private static readonly Warning StaticWarning = new(new("LOOSE_IN_STATIC_WARNING"), "m");

                internal Warning Argument() => new Warning(new("LOOSE_ARGUMENT"), "m");
                internal Warning Named() => new(message: "m", code: new("LOOSE_NAMED"));
                internal WarningCode Local() { WarningCode code = new("LOOSE_LOCAL"); return code; }
                internal WarningCode Returned() => new("LOOSE_RETURNED");
                internal WarningCode Explicit() => new WarningCode("LOOSE_EXPLICIT");
                internal Warning Helper() => Make(new("LOOSE_HELPER"));
                internal Warning Declared() => new(FixtureDiagnostics.Declared, "m");
                private static Warning Make(WarningCode code) => new(code, "m");
            }
            """;
        CSharpCompilation compilation = SourceSemantics.Compile("Fixture",
            [CSharpSyntaxTree.ParseText(Source, SourceSemantics.ParseOptions, path: "Fixture.cs")],
            [MetadataReference.CreateFromFile(WarningType().Assembly.Location)]);
        SemanticModel model = compilation.GetSemanticModel(compilation.SyntaxTrees.Single(static tree => tree.FilePath == "Fixture.cs"));

        string[] caught =
        [
            .. LooseCreations(model, DescriptorType()!.Name)
                .Select(static creation => creation.ArgumentList!.Arguments[0].Expression)
                .OfType<LiteralExpressionSyntax>()
                .Select(static literal => literal.Token.ValueText)
                .Order(StringComparer.Ordinal),
        ];

        Assert.Equal(
            [
                "LOOSE_ARGUMENT", "LOOSE_EXPLICIT", "LOOSE_HELPER", "LOOSE_INSTANCE_FIELD", "LOOSE_IN_STATIC_WARNING",
                "LOOSE_LOCAL", "LOOSE_NAMED", "LOOSE_RETURNED",
            ],
            caught);
    }

    [Fact]
    public void Capabilities_ListExactlyTheDeclaredWarningCodes()
    {
        Dictionary<string, string> diagnostics = CliCatalog.Current.Document["diagnostics"]!.AsArray().ToDictionary(
            static diagnostic => diagnostic!["code"]!.GetValue<string>(),
            static diagnostic => diagnostic!["severity"]!.GetValue<string>() + "/" + diagnostic["category"]!.GetValue<string>(),
            StringComparer.Ordinal);

        // Emitted with a string code: each one resolved to its constant must be in capabilities.
        var problems = new List<string>();
        foreach (StringCodedConstruction site in StringCodedConstructions())
        {
            if (site.Code is null)
            {
                problems.Add($"  {site.Location}: the code '{site.Expression}' is not a declared descriptor and cannot be traced");
            }
            else if (!diagnostics.ContainsKey(site.Code))
            {
                problems.Add($"  {site.Location}: {site.Code} is emitted but capabilities does not list it");
            }
        }

        if (DescriptorType() is { } descriptor)
        {
            HashSet<string> declared = [.. Declarations(descriptor.Name).Select(static declaration => declaration.Code)];
            problems.AddRange(declared.Where(code => !diagnostics.TryGetValue(code, out string? kind) || !kind.StartsWith("warning/", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
                .Select(static code => $"  {code} is declared but capabilities does not list it as a warning"));
            problems.AddRange(diagnostics.Where(pair => pair.Value == "warning/warning" && !declared.Contains(pair.Key))
                .Select(static pair => pair.Key)
                .Order(StringComparer.Ordinal)
                .Select(static code => $"  {code} is listed by capabilities but declared by no descriptor"));
        }

        Assert.True(problems.Count == 0,
            "Every warning code the CLI emits is a declared descriptor and capabilities lists exactly the declared codes:"
            + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void HostDiagnosticIds_AreNamedConstantsAndNoEnvelopeIsHandWritten()
    {
        var problems = new List<string>();
        foreach (OwnedSourceFile file in OwnershipSourceFiles.All)
        {
            foreach (SyntaxToken token in file.Root.DescendantTokens().Where(static token =>
                token.IsKind(SyntaxKind.StringLiteralToken) || token.IsKind(SyntaxKind.InterpolatedStringTextToken)
                || token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken) || token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken)))
            {
                string text = token.Text;
                if (EnvelopeText().IsMatch(text))
                {
                    problems.Add($"{file.At(token.Parent!, "a hand-written JSON envelope")}");
                }
                bool constant = token.Parent?.Parent is EqualsValueClauseSyntax { Parent.Parent.Parent: FieldDeclarationSyntax field }
                    && field.Modifiers.Any(SyntaxKind.ConstKeyword);
                foreach (Match id in HostDiagnosticId().Matches(text))
                {
                    if (!constant || token.ValueText != id.Value)
                    {
                        problems.Add($"{file.At(token.Parent!, id.Value)} is not a named constant");
                    }
                }
            }
        }

        OwnershipSourceFiles.AssertNone(problems,
            "Every host diagnostic id (HOST-AREA-0000) is a named constant, and every envelope is serialized from its contract type, "
            + "never written as JSON text:");
    }

    // ----- helpers ---------------------------------------------------------------------------

    private static bool IsCode(string? name) => string.Equals(name, "code", StringComparison.OrdinalIgnoreCase);

    private static Type WarningType() =>
        typeof(Aspose.Cli.Sdk.Errors.ExitCode).Assembly.GetTypes()
            .Single(static type => type.IsPublic && type.Name == WarningTypeName);

    /// <summary>The non-string type through which <c>Warning</c> takes its code, or null.</summary>
    private static Type? DescriptorType()
    {
        Type warning = WarningType();
        return warning.GetConstructors()
                .SelectMany(static constructor => constructor.GetParameters())
                .Where(static parameter => IsCode(parameter.Name) && parameter.ParameterType != typeof(string))
                .Select(static parameter => parameter.ParameterType)
                .FirstOrDefault()
            ?? warning.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(static property => IsCode(property.Name) && property.PropertyType != typeof(string) && property.SetMethod?.IsPublic == true)
                .Select(static property => property.PropertyType)
                .FirstOrDefault();
    }

    private sealed record Declaration(string File, string Location, string Member, string Code);

    /// <summary>Every static field or property of the descriptor type, with the code its initializer names.</summary>
    private static Declaration[] Declarations(string descriptor)
    {
        Dictionary<string, string?> constants = Constants();
        var declarations = new List<Declaration>();
        foreach (OwnedSourceFile file in OwnershipSourceFiles.All)
        {
            foreach (SyntaxNode node in file.Root.DescendantNodes())
            {
                (TypeSyntax? type, string? name, ExpressionSyntax? value, SyntaxTokenList modifiers) = node switch
                {
                    FieldDeclarationSyntax field when field.Declaration.Variables.Count == 1 =>
                        (field.Declaration.Type, field.Declaration.Variables[0].Identifier.ValueText,
                            field.Declaration.Variables[0].Initializer?.Value, field.Modifiers),
                    PropertyDeclarationSyntax property =>
                        (property.Type, property.Identifier.ValueText, property.Initializer?.Value ?? property.ExpressionBody?.Expression, property.Modifiers),
                    _ => (null, null, null, default),
                };
                if (type is null || TypeName(type) != descriptor || !modifiers.Any(SyntaxKind.StaticKeyword) || value is null)
                {
                    continue;
                }
                ExpressionSyntax? argument = value switch
                {
                    BaseObjectCreationExpressionSyntax creation => creation.ArgumentList?.Arguments.FirstOrDefault()?.Expression,
                    _ => null,
                };
                string code = Resolve(argument, constants) ?? $"<unresolved {value}>";
                declarations.Add(new Declaration(file.Path, file.At(node, name!), name!, code));
            }
        }
        return [.. declarations];
    }

    private sealed record StringCodedConstruction(string Location, string Expression, string? Code);

    /// <summary>
    /// Every construction of a warning that sets its code through an object initializer: an
    /// explicit <c>new Warning { Code = ... }</c>, or a target-typed <c>new() { Code = ... }</c> whose
    /// target is declared as <c>Warning</c>.
    /// </summary>
    private static IEnumerable<StringCodedConstruction> StringCodedConstructions()
    {
        Dictionary<string, string?> constants = Constants();
        foreach (OwnedSourceFile file in OwnershipSourceFiles.All)
        {
            foreach (BaseObjectCreationExpressionSyntax creation in file.Root.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
            {
                bool warning = creation switch
                {
                    ObjectCreationExpressionSyntax explicitType => TypeName(explicitType.Type) == WarningTypeName,
                    ImplicitObjectCreationExpressionSyntax implicitType => TargetIsWarning(implicitType),
                    _ => false,
                };
                AssignmentExpressionSyntax? code = creation.Initializer?.Expressions.OfType<AssignmentExpressionSyntax>()
                    .FirstOrDefault(static assignment => assignment.Left is IdentifierNameSyntax { Identifier.ValueText: "Code" });
                if (warning && code is not null)
                {
                    yield return new StringCodedConstruction(file.At(creation, code.Right.ToString()), code.Right.ToString(), Resolve(code.Right, constants));
                }
            }
        }
    }

    private static bool TargetIsWarning(ImplicitObjectCreationExpressionSyntax creation)
    {
        foreach (SyntaxNode ancestor in creation.Ancestors())
        {
            TypeSyntax? target = ancestor switch
            {
                VariableDeclarationSyntax variable => variable.Type,
                PropertyDeclarationSyntax property => property.Type,
                MethodDeclarationSyntax method => method.ReturnType,
                LocalFunctionStatementSyntax local => local.ReturnType,
                _ => null,
            };
            if (target is not null)
            {
                return TypeName(target) == WarningTypeName;
            }
            if (ancestor is ArgumentSyntax or CollectionExpressionSyntax or InitializerExpressionSyntax { Parent: not ImplicitObjectCreationExpressionSyntax })
            {
                return false;
            }
        }
        return false;
    }

    /// <summary>
    /// Every string constant under <c>src</c> by <c>Type.Name</c> and by simple name; a simple name
    /// with two values maps to null.
    /// </summary>
    private static Dictionary<string, string?> Constants()
    {
        var constants = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (OwnedSourceFile file in OwnershipSourceFiles.All)
        {
            foreach (FieldDeclarationSyntax field in file.Root.DescendantNodes().OfType<FieldDeclarationSyntax>()
                .Where(static field => field.Modifiers.Any(SyntaxKind.ConstKeyword)))
            {
                foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
                {
                    if (variable.Initializer?.Value is LiteralExpressionSyntax { Token.Value: string value })
                    {
                        string name = variable.Identifier.ValueText;
                        constants[name] = constants.TryGetValue(name, out string? known) && known != value ? null : value;
                        if (field.Parent is BaseTypeDeclarationSyntax owner)
                        {
                            constants[owner.Identifier.ValueText + "." + name] = value;
                        }
                    }
                }
            }
        }
        return constants;
    }

    private static string? Resolve(ExpressionSyntax? expression, Dictionary<string, string?> constants) => expression switch
    {
        LiteralExpressionSyntax { Token.Value: string value } => value,
        IdentifierNameSyntax name => constants.GetValueOrDefault(name.Identifier.ValueText),
        MemberAccessExpressionSyntax access => constants.GetValueOrDefault(
                (access.Expression is MemberAccessExpressionSyntax inner ? inner.Name.ToString() : access.Expression.ToString())
                + "." + access.Name.Identifier.ValueText)
            ?? constants.GetValueOrDefault(access.Name.Identifier.ValueText),
        _ => null,
    };

    private static string TypeName(TypeSyntax type) => type switch
    {
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        NullableTypeSyntax nullable => TypeName(nullable.ElementType),
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        _ => type.ToString(),
    };

    /// <summary>
    /// Every creation of the descriptor in a file, explicit or target-typed, other than a static
    /// declaration and the descriptor's own <c>Received</c> factory, which reads back a code
    /// another CLI process wrote.
    /// </summary>
    private static IEnumerable<BaseObjectCreationExpressionSyntax> LooseCreations(SemanticModel model, string descriptor) =>
        model.SyntaxTree.GetRoot().DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>()
            .Where(creation => CreatedTypeName(model, creation) == descriptor
                && !IsStaticDeclaration(creation, descriptor)
                && !InReceivedFactory(creation, descriptor));

    /// <summary>
    /// The type a creation makes: the one it names, or for a target-typed one the type the
    /// compiler binds, else the type of the member or local it initializes.
    /// </summary>
    private static string? CreatedTypeName(SemanticModel model, BaseObjectCreationExpressionSyntax creation)
    {
        if (creation is ObjectCreationExpressionSyntax explicitType)
        {
            return TypeName(explicitType.Type);
        }
        Microsoft.CodeAnalysis.TypeInfo info = model.GetTypeInfo(creation);
        if (info.Type is { TypeKind: not TypeKind.Error } bound)
        {
            return bound.Name;
        }
        if (info.ConvertedType is { TypeKind: not TypeKind.Error } converted)
        {
            return converted.Name;
        }
        return creation.Parent switch
        {
            EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax variable } } => TypeName(variable.Type),
            EqualsValueClauseSyntax { Parent: PropertyDeclarationSyntax property } => TypeName(property.Type),
            ArrowExpressionClauseSyntax { Parent: PropertyDeclarationSyntax property } => TypeName(property.Type),
            ArrowExpressionClauseSyntax { Parent: MethodDeclarationSyntax method } => TypeName(method.ReturnType),
            _ => null,
        };
    }

    /// <summary>Whether a creation is the whole initializer of a static field or property of the descriptor type.</summary>
    private static bool IsStaticDeclaration(BaseObjectCreationExpressionSyntax creation, string descriptor) => creation.Parent switch
    {
        EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax { Parent: FieldDeclarationSyntax field } variable } } =>
            field.Modifiers.Any(SyntaxKind.StaticKeyword) && TypeName(variable.Type) == descriptor,
        EqualsValueClauseSyntax { Parent: PropertyDeclarationSyntax property } =>
            property.Modifiers.Any(SyntaxKind.StaticKeyword) && TypeName(property.Type) == descriptor,
        ArrowExpressionClauseSyntax { Parent: PropertyDeclarationSyntax property } =>
            property.Modifiers.Any(SyntaxKind.StaticKeyword) && TypeName(property.Type) == descriptor,
        _ => false,
    };

    private static bool InReceivedFactory(SyntaxNode creation, string descriptor) =>
        creation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault() is { Identifier.ValueText: "Received" } method
        && method.Parent is TypeDeclarationSyntax owner && owner.Identifier.ValueText == descriptor;

    /// <summary>
    /// A semantic model of every file under <c>src</c>: each project compiles on its own against
    /// the assemblies this test runs with, so a target-typed creation binds to the type it makes.
    /// Types of engines this test does not load stay unbound, which leaves the CLI's own types,
    /// such as the warning-code descriptor, bound.
    /// </summary>
    private static class SourceSemantics
    {
        public static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Preview);

        public static readonly Lazy<SemanticModel[]> Models = new(Load);

        /// <summary>The usings the SDK's <c>ImplicitUsings</c> adds to every project.</summary>
        private const string ImplicitUsings = """
            global using System;
            global using System.Collections.Generic;
            global using System.IO;
            global using System.Linq;
            global using System.Net.Http;
            global using System.Threading;
            global using System.Threading.Tasks;
            """;

        public static CSharpCompilation Compile(string name, IEnumerable<SyntaxTree> trees, IEnumerable<MetadataReference> references) =>
            CSharpCompilation.Create(name,
                [.. trees, CSharpSyntaxTree.ParseText(ImplicitUsings, ParseOptions, path: "ImplicitUsings.cs")],
                [.. PlatformReferences(), .. references],
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        public static string At(SemanticModel model, SyntaxNode node, string what) =>
            $"{Path.GetRelativePath(OwnershipSourceFiles.SourceRoot, model.SyntaxTree.FilePath)}:"
            + $"{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: {what}";

        private static SemanticModel[] Load()
        {
            var models = new List<SemanticModel>();
            foreach (IGrouping<string, OwnedSourceFile> project in OwnershipSourceFiles.All.GroupBy(ProjectOf, StringComparer.OrdinalIgnoreCase))
            {
                SyntaxTree[] trees =
                [
                    .. project.Select(static file => CSharpSyntaxTree.ParseText(File.ReadAllText(file.Path), ParseOptions, path: file.Path)),
                ];
                CSharpCompilation compilation = Compile(project.Key, trees, ApplicationReferences(except: project.Key));
                models.AddRange(trees.Select(tree => compilation.GetSemanticModel(tree)));
            }
            return [.. models];
        }

        private static string ProjectOf(OwnedSourceFile file) =>
            Path.GetRelativePath(OwnershipSourceFiles.SourceRoot, file.Path).Split(Path.DirectorySeparatorChar)[0];

        private static IEnumerable<MetadataReference> PlatformReferences() =>
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Where(static path => !path.StartsWith(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase))
                .Select(static path => MetadataReference.CreateFromFile(path));

        /// <summary>The CLI and package assemblies next to this test, other than the project's own.</summary>
        private static IEnumerable<MetadataReference> ApplicationReferences(string except) =>
            Directory.GetFiles(AppContext.BaseDirectory, "*.dll")
                .Where(path => !string.Equals(Path.GetFileNameWithoutExtension(path), except, StringComparison.OrdinalIgnoreCase)
                    && IsManaged(path))
                .Select(static path => MetadataReference.CreateFromFile(path));

        private static bool IsManaged(string path)
        {
            try
            {
                AssemblyName.GetAssemblyName(path);
                return true;
            }
            catch (BadImageFormatException)
            {
                return false;
            }
        }
    }

    private static bool InAllList(SyntaxNode node) =>
        OwnershipSourceFiles.EnclosingMember(node) switch
        {
            FieldDeclarationSyntax field => field.Declaration.Variables.Any(static variable => variable.Identifier.ValueText == "All"),
            PropertyDeclarationSyntax property => property.Identifier.ValueText == "All",
            _ => false,
        };

    /// <summary>JSON text that writes an envelope's schema version, such as <c>"schemaVersion": 2</c>.</summary>
    [GeneratedRegex("""(?:\\?")schemaVersion(?:\\?")\s*:\s*2\b""")]
    private static partial Regex EnvelopeText();

    [GeneratedRegex(@"\bHOST-[A-Z]+-[0-9]{4}\b")]
    private static partial Regex HostDiagnosticId();
}
