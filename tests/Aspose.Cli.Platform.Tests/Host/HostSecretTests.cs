using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.TestKit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Aspose.Cli.Host.Tests;

/// <summary>
/// The Host carries a password or a token only as a redacting secret type, never as a string.
/// </summary>
public sealed class HostSecretTests
{
    private static readonly string HostRoot = Path.Combine(RepositoryPaths.Root, "src", "Aspose.Cli.Host");

    [Fact]
    public void HostSource_HoldsNoPasswordAsAString()
    {
        string[] violations = [.. HostMembers()
            .Where(static member => IsString(member.Type) && member.Name.EndsWith("Password", StringComparison.OrdinalIgnoreCase))
            .Select(static member => member.Location)];

        Assert.True(violations.Length == 0,
            "The Host carries a password as a redacting secret type, never as a string:" + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void HostRecords_HoldNoTokenAsAString()
    {
        string[] violations = [.. HostMembers()
            .Where(static member => member.InRecord && IsString(member.Type) && member.Name.EndsWith("Token", StringComparison.Ordinal))
            .Select(static member => member.Location)];

        Assert.True(violations.Length == 0,
            "A Host record carries a token as a redacting secret type, never as a string, so printing the record never shows it:"
            + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    /// <summary>Every property, field, local and parameter the Host declares with a type.</summary>
    private static IEnumerable<(TypeSyntax Type, string Name, bool InRecord, string Location)> HostMembers() =>
        Directory.GetFiles(HostRoot, "*.cs", SearchOption.AllDirectories)
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .SelectMany(static path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path).GetRoot().DescendantNodes()
                .SelectMany(static node => node switch
                {
                    PropertyDeclarationSyntax property => [(node, property.Type, property.Identifier.ValueText)],
                    FieldDeclarationSyntax field => field.Declaration.Variables
                        .Select(variable => ((SyntaxNode)variable, field.Declaration.Type, variable.Identifier.ValueText)),
                    LocalDeclarationStatementSyntax local => local.Declaration.Variables
                        .Select(variable => ((SyntaxNode)variable, local.Declaration.Type, variable.Identifier.ValueText)),
                    ParameterSyntax { Type: { } type } parameter => [(node, type, parameter.Identifier.ValueText)],
                    _ => Array.Empty<(SyntaxNode Node, TypeSyntax Type, string Name)>(),
                })
                .Select(member => (
                    member.Item2,
                    member.Item3,
                    member.Item1 switch
                    {
                        PropertyDeclarationSyntax property => property.Parent is RecordDeclarationSyntax,
                        ParameterSyntax parameter => parameter.Parent?.Parent is RecordDeclarationSyntax,
                        _ => false,
                    },
                    $"{Path.GetRelativePath(HostRoot, path)}:{member.Item1.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: string {member.Item3}")));

    private static bool IsString(TypeSyntax type) => type switch
    {
        PredefinedTypeSyntax predefined => predefined.Keyword.IsKind(SyntaxKind.StringKeyword),
        NullableTypeSyntax nullable => IsString(nullable.ElementType),
        _ => false,
    };
}
