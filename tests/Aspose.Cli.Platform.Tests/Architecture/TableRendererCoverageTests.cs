using Aspose.Cli.Host;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.TestKit;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>
/// Table and text output falls back to JSON for a result type without a renderer, which the
/// compiler cannot catch. Every result the SDK or the Host declares has a case in the Host's
/// <c>TableOutputWriter</c>; product results are checked against the product registrations
/// in the distribution tests (<c>ProductRendererCoverageTests</c>).
/// </summary>
public sealed class TableRendererCoverageTests
{
    [Fact]
    public void EverySdkAndHostResult_HasATableRendererCase()
    {
        string[] results = new[] { typeof(ResultEnvelope).Assembly, typeof(CliHost).Assembly }
            .SelectMany(static assembly => assembly.GetTypes())
            .Where(static type => type is { IsAbstract: false, IsClass: true } && type.IsAssignableTo(typeof(ResultEnvelope)))
            .Select(static type => type.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        HashSet<string> rendered = RenderedResultNames();

        Assert.NotEmpty(results);
        string[] missing = results.Where(name => !rendered.Contains(name)).ToArray();
        Assert.True(
            missing.Length == 0,
            "These results render as JSON in table and text mode because TableOutputWriter has no case for them: "
            + string.Join(", ", missing));
    }

    /// <summary>The types the result switch of <c>TableOutputWriter.WriteResult</c> matches.</summary>
    private static HashSet<string> RenderedResultNames()
    {
        string path = Path.Combine(RepositoryPaths.Root, "src", "Aspose.Cli.Host", "Output", "TableOutputWriter.cs");
        MethodDeclarationSyntax writeResult = CSharpSyntaxTree.ParseText(File.ReadAllText(path))
            .GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(static method => method.Identifier.Text == "WriteResult");
        HashSet<string> names = writeResult
            .DescendantNodes()
            .OfType<SwitchStatementSyntax>()
            .SelectMany(static statement => statement.Sections)
            .SelectMany(static section => section.Labels.OfType<CasePatternSwitchLabelSyntax>())
            .Select(static label => label.Pattern switch
            {
                DeclarationPatternSyntax declaration => declaration.Type.ToString(),
                TypePatternSyntax type => type.Type.ToString(),
                _ => null,
            })
            .OfType<string>()
            .Select(static name => name[(name.LastIndexOf('.') + 1)..])
            .ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(names);
        return names;
    }
}
