using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>
/// C-R28: every code the CLI emits is declared where capabilities reads it. As the SDK's
/// <c>ErrorCodes.All</c> lists every shared code, a product's or the Host's diagnostics class lists
/// in its <c>All</c> every error code, warning code and verification descriptor it declares, each
/// one has an emission site, and no error code of a product or the Host is declared anywhere else.
/// The rules read the source syntax under <c>src</c>, so they hold whatever the classes are named.
/// </summary>
public sealed class ProductDiagnosticDeclarationTests
{
    private const string AllMember = "All";

    private static readonly string[] DescriptorTypes = ["ErrorCode", "WarningCode", "DiagnosticDescriptor"];

    [Fact]
    public void C_R28_EveryDeclaredProductDiagnostic_IsInAllAndEmitted()
    {
        DiagnosticsClass[] classes = DiagnosticsClasses();
        Assert.Contains(classes, static owner => owner.File.Product is not null);

        string[] missing =
        [
            .. classes.SelectMany(static owner => owner.Declarations
                .Where(declaration => !owner.Listed.Contains(declaration.Name))
                .Select(declaration => owner.File.At(declaration.Node, $"{owner.Name}.{declaration.Name} is not in {owner.Name}.{AllMember}"))),
        ];
        OwnershipSourceFiles.AssertNone(missing,
            $"A product's or the Host's diagnostics class lists every code it declares in its {AllMember}, which capabilities publishes:");

        string[] orphans =
        [
            .. classes.SelectMany(static owner => Orphans(owner, OwnershipSourceFiles.All)
                .Select(declaration => owner.File.At(declaration.Node, $"{owner.Name}.{declaration.Name}"))),
        ];
        OwnershipSourceFiles.AssertNone(orphans,
            "Every declared product or Host diagnostic has an emission site that names it through its class (or a using static of it); delete the codes nothing emits:");
    }

    [Fact]
    public void C_R28_ProductErrorCodes_AreDeclaredOnlyInTheirDiagnosticsClass()
    {
        HashSet<SyntaxNode> declared = [.. DiagnosticsClasses().Select(static owner => owner.Node)];
        string[] loose =
        [
            .. OwnershipSourceFiles.All
                .Where(static file => !file.IsUnder(OwnershipSourceFiles.SdkRoot))
                .SelectMany(file => file.Root.DescendantNodes()
                    .Where(static node => IsErrorCodeCreation(node))
                    .Where(node => !node.Ancestors().OfType<ClassDeclarationSyntax>().Any(declared.Contains))
                    .Select(node => file.At(node, node.ToString()))),
        ];
        OwnershipSourceFiles.AssertNone(loose,
            $"A product or the Host declares its error codes and verification descriptors only as static members of its diagnostics class, the one with an {AllMember} list:");
    }

    [Fact]
    public void C_R28_DiagnosticsClassRule_FindsUnlistedDeclarations()
    {
        const string Source = """
            internal static class FixtureDiagnostics
            {
                internal static readonly ErrorCode Listed = new("LISTED", ExitCode.ValidationError);
                internal static readonly ErrorCode Unlisted = ErrorCode.NotFound("UNLISTED_NOT_FOUND");
                internal static readonly WarningCode Warned = new("WARNED");
                internal static readonly DiagnosticDescriptor Checked = DiagnosticDescriptor.Verification("CHECKED");
                internal const string Remedy = "not a code";
                internal static IReadOnlyList<DiagnosticDescriptor> All { get; } =
                [
                    DiagnosticDescriptor.Error(Listed),
                    DiagnosticDescriptor.Warning(Warned),
                ];
            }

            internal sealed class Emitter
            {
                private static readonly ErrorCode Loose = new ErrorCode("LOOSE", ExitCode.ValidationError);
                internal static ErrorCode Missing() => ErrorCode.NotFound("LOOSE_NOT_FOUND");
                internal static DiagnosticDescriptor Issue() => DiagnosticDescriptor.Verification("LOOSE_ISSUE");
            }
            """;
        SyntaxNode root = CSharpSyntaxTree.ParseText(Source).GetRoot();
        DiagnosticsClass owner = Assert.Single(DiagnosticsClassesIn(new OwnedSourceFile("Fixture.cs", "Fixture", root)));

        Assert.Equal(["Unlisted", "Checked"], owner.Declarations.Where(declaration => !owner.Listed.Contains(declaration.Name)).Select(static declaration => declaration.Name));
        Assert.Equal(
            ["new ErrorCode(\"LOOSE\", ExitCode.ValidationError)", "ErrorCode.NotFound(\"LOOSE_NOT_FOUND\")", "DiagnosticDescriptor.Verification(\"LOOSE_ISSUE\")"],
            root.DescendantNodes().Where(IsErrorCodeCreation)
                .Where(node => !node.Ancestors().OfType<ClassDeclarationSyntax>().Contains(owner.Node))
                .Select(static node => node.ToString()));
    }

    /// <summary>
    /// An emission names the declaration through its own class: a member of the same name on
    /// another diagnostics class, a mention only in <c>All</c> or in the declaring class's other
    /// declarations, or an unrelated identifier does not count; a <c>using static</c> of the class
    /// and a use inside the class's own methods do.
    /// </summary>
    [Fact]
    public void C_R28_EmissionRule_RequiresAUseThroughTheDeclaringClass()
    {
        const string Declarations = """
            internal static class FixtureDiagnostics
            {
                internal static readonly ErrorCode Emitted = new("EMITTED", ExitCode.ValidationError);
                internal static readonly WarningCode Impersonated = new("IMPERSONATED");
                internal static readonly WarningCode OnlyListed = new("ONLY_LISTED");
                internal static readonly WarningCode Named = new("NAMED");
                internal static readonly WarningCode Static = new("STATIC");
                internal static readonly WarningCode Internal = new("INTERNAL");
                internal static IReadOnlyList<DiagnosticDescriptor> All { get; } =
                [
                    DiagnosticDescriptor.Error(Emitted), DiagnosticDescriptor.Warning(Impersonated), DiagnosticDescriptor.Warning(OnlyListed),
                    DiagnosticDescriptor.Warning(Named), DiagnosticDescriptor.Warning(Static), DiagnosticDescriptor.Warning(Internal),
                ];
                internal static Warning Warn() => new(Internal, "m");
            }
            """;
        const string Emitters = """
            using static Aspose.Cli.Product.Fixture.FixtureDiagnostics;

            internal sealed class Emitter
            {
                internal static CliException Fail() => new(FixtureDiagnostics.Emitted, "m");
                internal static Warning Other() => new(OtherDiagnostics.Impersonated, "m");
                internal static Warning Imported() => new(Static, "m");
            }
            """;
        const string Unrelated = """
            internal sealed class Unrelated
            {
                internal static Warning Other() => new(OtherDiagnostics.Named, "m");
            }
            """;
        OwnedSourceFile declarations = Fixture("FixtureDiagnostics.cs", Declarations);
        OwnedSourceFile[] files = [declarations, Fixture("Emitter.cs", Emitters), Fixture("Unrelated.cs", Unrelated)];
        DiagnosticsClass owner = Assert.Single(DiagnosticsClassesIn(declarations));

        // Emitter.cs imports the class, so its bare Static is a use; Unrelated.cs names only
        // another class's Named, and Emitter.cs another class's Impersonated.
        Assert.Equal(["Impersonated", "OnlyListed", "Named"], Orphans(owner, files).Select(static declaration => declaration.Name));
    }

    // ----- helpers ---------------------------------------------------------------------------

    private static OwnedSourceFile Fixture(string path, string source) =>
        new(path, "Fixture", CSharpSyntaxTree.ParseText(source, path: path).GetRoot());

    /// <summary>
    /// The declarations of a diagnostics class that nothing emits: no file names them as
    /// <c>&lt;Class&gt;.&lt;Name&gt;</c>, no file with a <c>using static</c> of the class names them
    /// bare, and the class's own members other than its declarations and <c>All</c> do not either.
    /// </summary>
    private static IEnumerable<Declaration> Orphans(DiagnosticsClass owner, IEnumerable<OwnedSourceFile> files)
    {
        OwnedSourceFile[] all = [.. files];
        return owner.Declarations.Where(declaration => !all.Any(file => Emits(file, owner, declaration.Name)));
    }

    private static bool Emits(OwnedSourceFile file, DiagnosticsClass owner, string name)
    {
        if (file.Root.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Any(access =>
            access.Name.Identifier.ValueText == name && LastIdentifier(access.Expression) == owner.Name))
        {
            return true;
        }
        bool imported = file.Root.DescendantNodes().OfType<UsingDirectiveSyntax>().Any(directive =>
            directive.StaticKeyword.IsKind(SyntaxKind.StaticKeyword) && directive.Name is { } imported && LastIdentifier(imported) == owner.Name);
        IEnumerable<IdentifierNameSyntax> bare = file.Root.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(identifier => identifier.Identifier.ValueText == name && !IsMemberName(identifier));
        return bare.Any(identifier => imported
            ? !identifier.Ancestors().OfType<ClassDeclarationSyntax>().Any(type => type == owner.Node)
            : identifier.Ancestors().OfType<ClassDeclarationSyntax>().Any(type => type == owner.Node)
                && !identifier.Ancestors().Any(ancestor => IsDeclarationOrAll(ancestor, owner)));
    }

    /// <summary>Whether an identifier is the member name of an access, such as <c>B</c> in <c>A.B</c>.</summary>
    private static bool IsMemberName(IdentifierNameSyntax identifier) =>
        identifier.Parent is MemberAccessExpressionSyntax access && access.Name == identifier;

    private static bool IsDeclarationOrAll(SyntaxNode node, DiagnosticsClass owner) =>
        owner.Declarations.Any(declaration => declaration.Node == node)
        || node is PropertyDeclarationSyntax { Identifier.ValueText: AllMember } && node.Parent == owner.Node;

    private static string? LastIdentifier(SyntaxNode node) => node switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
        _ => null,
    };

    /// <summary>A class outside the SDK with a static <c>All</c> list of diagnostic descriptors.</summary>
    private sealed record DiagnosticsClass(OwnedSourceFile File, ClassDeclarationSyntax Node, IReadOnlyList<Declaration> Declarations, IReadOnlySet<string> Listed)
    {
        public string Name => Node.Identifier.ValueText;
    }

    /// <summary>A static field or property of a descriptor type.</summary>
    private sealed record Declaration(string Name, SyntaxNode Node);

    private static DiagnosticsClass[] DiagnosticsClasses() =>
    [
        .. OwnershipSourceFiles.All
            .Where(static file => !file.IsUnder(OwnershipSourceFiles.SdkRoot))
            .SelectMany(static file => DiagnosticsClassesIn(file)),
    ];

    private static IEnumerable<DiagnosticsClass> DiagnosticsClassesIn(OwnedSourceFile file)
    {
        foreach (ClassDeclarationSyntax type in file.Root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            SyntaxNode? all = type.Members.OfType<PropertyDeclarationSyntax>()
                .FirstOrDefault(static property => property.Identifier.ValueText == AllMember && property.Type.ToString().Contains("DiagnosticDescriptor", StringComparison.Ordinal));
            if (all is null)
            {
                continue;
            }
            Declaration[] declarations =
            [
                .. type.Members.OfType<FieldDeclarationSyntax>()
                    .Where(static field => field.Modifiers.Any(SyntaxKind.StaticKeyword) && DescriptorTypes.Contains(field.Declaration.Type.ToString()))
                    .SelectMany(static field => field.Declaration.Variables.Select(static variable => new Declaration(variable.Identifier.ValueText, variable))),
                .. type.Members.OfType<PropertyDeclarationSyntax>()
                    .Where(static property => property.Identifier.ValueText != AllMember && property.Modifiers.Any(SyntaxKind.StaticKeyword)
                        && DescriptorTypes.Contains(property.Type.ToString()))
                    .Select(static property => new Declaration(property.Identifier.ValueText, property)),
            ];
            HashSet<string> listed = [.. all.DescendantNodes().OfType<IdentifierNameSyntax>().Select(static name => name.Identifier.ValueText)];
            yield return new DiagnosticsClass(file, type, declarations, listed);
        }
    }

    /// <summary>
    /// A creation of an error code or verification descriptor: <c>new ErrorCode(...)</c>, a
    /// target-typed <c>new(...)</c> that initializes an <c>ErrorCode</c> field, or
    /// <c>ErrorCode.NotFound(...)</c> and <c>DiagnosticDescriptor.Verification(...)</c>.
    /// </summary>
    private static bool IsErrorCodeCreation(SyntaxNode node) => node switch
    {
        ObjectCreationExpressionSyntax creation => creation.Type.ToString() is "ErrorCode" or "Aspose.Cli.Sdk.Errors.ErrorCode",
        ImplicitObjectCreationExpressionSyntax implicitCreation => implicitCreation.Parent is EqualsValueClauseSyntax
        {
            Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax { Type: var type } },
        } && type.ToString() == "ErrorCode",
        InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } =>
            (access.Expression.ToString(), access.Name.Identifier.ValueText) is ("ErrorCode", "NotFound") or ("DiagnosticDescriptor", "Verification"),
        _ => false,
    };
}
