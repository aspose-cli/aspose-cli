using Aspose.Cli.TestKit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>
/// The parsed C# sources of every project under <c>src</c> (analyzers, <c>bin</c> and <c>obj</c>
/// left out), for ownership rules that read syntax only and so never depend on the type names an
/// implementation chooses.
/// </summary>
internal static class OwnershipSourceFiles
{
    public static readonly string SourceRoot = Path.Combine(RepositoryPaths.Root, "src");
    public static readonly string SdkRoot = Path.Combine(SourceRoot, "Aspose.Cli.Sdk");

    private static readonly Lazy<OwnedSourceFile[]> Loaded = new(Load);

    public static IReadOnlyList<OwnedSourceFile> All => Loaded.Value;

    /// <summary>The files of the product projects.</summary>
    public static IEnumerable<OwnedSourceFile> Products => All.Where(static file => file.Product is not null);

    /// <summary>The files of the SDK project.</summary>
    public static IEnumerable<OwnedSourceFile> Sdk => All.Where(static file => file.IsUnder(SdkRoot));

    /// <summary>The product ids that have a product project.</summary>
    public static IReadOnlyList<string> ProductIds =>
        [.. Products.Select(static file => file.Product!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    public static void AssertNone(IEnumerable<string> violations, string rule)
    {
        string[] listed = [.. violations];
        Assert.True(listed.Length == 0, rule + Environment.NewLine + string.Join(Environment.NewLine, listed));
    }

    /// <summary>The name an invocation calls, such as <c>Contains</c> for <c>a.b.Contains(x)</c>.</summary>
    public static string? InvokedName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
        SimpleNameSyntax name => name.Identifier.ValueText,
        _ => null,
    };

    /// <summary>The innermost method, constructor, accessor, local function or lambda that holds a node.</summary>
    public static SyntaxNode? EnclosingMember(SyntaxNode node) =>
        node.AncestorsAndSelf().FirstOrDefault(static ancestor => ancestor is BaseMethodDeclarationSyntax
            or AccessorDeclarationSyntax
            or LocalFunctionStatementSyntax
            or PropertyDeclarationSyntax
            or FieldDeclarationSyntax);

    private static OwnedSourceFile[] Load() =>
        [.. Directory.GetDirectories(SourceRoot, "Aspose.Cli*")
            .Where(static project => !Path.GetFileName(project).EndsWith(".Analyzers", StringComparison.Ordinal))
            .SelectMany(static project => Directory.GetFiles(project, "*.cs", SearchOption.AllDirectories)
                .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                    && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Select(path => OwnedSourceFile.Read(project, path)))
            .OrderBy(static file => file.Path, StringComparer.OrdinalIgnoreCase)];
}

/// <summary>
/// One source file. <see cref="Product"/> is the product id (<c>Cells</c>, <c>Pdf</c>, ...) of a
/// product project and null otherwise.
/// </summary>
internal sealed record OwnedSourceFile(string Path, string? Product, SyntaxNode Root)
{
    private const string ProductPrefix = "Aspose.Cli.Product.";

    public static OwnedSourceFile Read(string project, string path)
    {
        string name = System.IO.Path.GetFileName(project);
        return new OwnedSourceFile(
            path,
            name.StartsWith(ProductPrefix, StringComparison.Ordinal) ? name[ProductPrefix.Length..] : null,
            CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path).GetRoot());
    }

    public bool IsUnder(string directory) =>
        Path.StartsWith(directory + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    public string Relative => System.IO.Path.GetRelativePath(OwnershipSourceFiles.SourceRoot, Path);

    public string At(SyntaxNode node, string what) =>
        $"{Relative}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: {what}";
}
