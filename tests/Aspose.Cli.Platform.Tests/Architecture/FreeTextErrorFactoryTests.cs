using Aspose.Cli.TestKit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>
/// <c>CliErrors.FromRemote</c> restates an error another CLI process reported, and
/// <c>CliErrors.Internal</c> reports a defect; both take free text and can carry any code, so only
/// the Host, which owns process boundaries and the last-resort error, calls them. A product or the
/// SDK outside its <c>Errors</c> folder builds a shared code through its situation's factory.
/// </summary>
public sealed class FreeTextErrorFactoryTests
{
    private static readonly string[] FreeTextFactories = ["FromRemote", "Internal"];

    private static readonly string SourceRoot = Path.Combine(RepositoryPaths.Root, "src");
    private static readonly string HostRoot = Path.Combine(SourceRoot, "Aspose.Cli.Host");
    private static readonly string SdkErrorsRoot = Path.Combine(SourceRoot, "Aspose.Cli.Sdk", "Errors");

    [Fact]
    public void FreeTextErrorFactories_AreCalledOnlyByTheHost()
    {
        (string Path, string Use)[] uses = [.. SourceFiles().SelectMany(static path => FreeTextUses(path))];

        Assert.Contains(uses, static use => IsUnder(use.Path, HostRoot) && use.Use.Contains("FromRemote", StringComparison.Ordinal));
        string[] violations = [.. uses
            .Where(static use => !IsUnder(use.Path, HostRoot) && !IsUnder(use.Path, SdkErrorsRoot))
            .Select(static use => $"{Path.GetRelativePath(SourceRoot, use.Path)}: {use.Use}")];
        Assert.True(
            violations.Length == 0,
            "Only the Host calls CliErrors.FromRemote and CliErrors.Internal; build a shared code through its SDK factory:"
                + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    private static IEnumerable<(string Path, string Use)> FreeTextUses(string path)
    {
        SyntaxNode root = CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path).GetRoot();
        bool importsCliErrors = root.DescendantNodes().OfType<UsingDirectiveSyntax>()
            .Any(static directive => directive.StaticKeyword.IsKind(SyntaxKind.StaticKeyword)
                && directive.Name?.ToString().EndsWith("CliErrors", StringComparison.Ordinal) == true);
        return root.DescendantNodes()
            .Where(node => node switch
            {
                MemberAccessExpressionSyntax access =>
                    FreeTextFactories.Contains(access.Name.Identifier.ValueText, StringComparer.Ordinal)
                    && LastIdentifier(access.Expression) == "CliErrors",
                InvocationExpressionSyntax { Expression: IdentifierNameSyntax name } =>
                    importsCliErrors && FreeTextFactories.Contains(name.Identifier.ValueText, StringComparer.Ordinal),
                _ => false,
            })
            .Select(node => (path, node.ToString().Split('\n')[0].Trim()));
    }

    private static string? LastIdentifier(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax name => name.Identifier.ValueText,
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
        _ => null,
    };

    private static IEnumerable<string> SourceFiles() =>
        Directory.GetDirectories(SourceRoot, "Aspose.Cli*")
            .SelectMany(static project => Directory.GetFiles(project, "*.cs", SearchOption.AllDirectories))
            .Where(static path => !IsBuildOutput(path));

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
        || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

    private static bool IsUnder(string path, string directory) =>
        path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
