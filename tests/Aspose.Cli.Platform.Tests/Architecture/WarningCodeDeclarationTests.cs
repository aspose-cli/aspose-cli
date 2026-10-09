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

        // A descriptor is created only as the initializer of a static member of a diagnostics class.
        string[] loose =
        [
            .. OwnershipSourceFiles.All.SelectMany(file => file.Root.DescendantNodes()
                .OfType<ObjectCreationExpressionSyntax>()
                .Where(creation => TypeName(creation.Type) == descriptor.Name && !InStaticMember(creation))
                .Select(creation => file.At(creation, creation.ToString()))),
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

    private static bool InStaticMember(SyntaxNode node) =>
        OwnershipSourceFiles.EnclosingMember(node) switch
        {
            FieldDeclarationSyntax field => field.Modifiers.Any(SyntaxKind.StaticKeyword),
            PropertyDeclarationSyntax property => property.Modifiers.Any(SyntaxKind.StaticKeyword),
            _ => false,
        };

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
