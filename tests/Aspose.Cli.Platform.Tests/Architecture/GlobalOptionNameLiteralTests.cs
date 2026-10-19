using Aspose.Cli.Sdk;
using Aspose.Cli.TestKit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>
/// <see cref="GlobalOptionNames"/> owns the spelling of every global option, help and version
/// name. Every source project names those options through its constants, so a child-process
/// argument list, an option error or a follow-up command cannot drift from the declared option;
/// only <c>src/Aspose.Cli.Sdk/GlobalOptionNames.cs</c> itself spells them as string literals.
/// </summary>
public sealed class GlobalOptionNameLiteralTests
{
    private static readonly string SourceRoot = Path.Combine(RepositoryPaths.Root, "src");
    private static readonly string OwnerFile = Path.Combine("Aspose.Cli.Sdk", "GlobalOptionNames.cs");

    [Fact]
    public void Source_NamesGlobalOptionsThroughGlobalOptionNames()
    {
        (string Path, int Line, string Literal)[] literals = [.. SourceFiles().SelectMany(static path => OptionNameLiterals(path))];

        Assert.Contains(literals, static literal => IsOwner(literal.Path));
        string[] violations = [.. literals
            .Where(static literal => !IsOwner(literal.Path))
            .Select(static literal => $"{Path.GetRelativePath(SourceRoot, literal.Path)}:{literal.Line}: {literal.Literal}")];
        Assert.True(
            violations.Length == 0,
            "Name a global option through GlobalOptionNames instead of a string literal:"
                + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    private static IEnumerable<(string Path, int Line, string Literal)> OptionNameLiterals(string path)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path);
        return tree.GetRoot().DescendantNodes()
            .OfType<LiteralExpressionSyntax>()
            .Where(static literal => literal.IsKind(SyntaxKind.StringLiteralExpression)
                && GlobalOptionNames.Reserved.Contains(literal.Token.ValueText, StringComparer.Ordinal))
            .Select(literal => (path, literal.GetLocation().GetLineSpan().StartLinePosition.Line + 1, literal.ToString()));
    }

    private static bool IsOwner(string path) =>
        string.Equals(Path.GetRelativePath(SourceRoot, path), OwnerFile, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> SourceFiles() =>
        Directory.GetFiles(SourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(static path => !IsBuildOutput(path));

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
        || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
}
