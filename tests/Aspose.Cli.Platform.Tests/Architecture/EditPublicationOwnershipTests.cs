using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>
/// An edit publishes its output through the SDK's <c>PublishEdit</c>, which stages the output with
/// its backup and the input precondition, reads the staged file back and commits. A product's
/// edit (its <c>*Edit.cs</c> engine files) never stages or commits an output set itself.
/// </summary>
public sealed class EditPublicationOwnershipTests
{
    /// <summary>The output-set steps <c>PublishEdit</c> takes for an edit.</summary>
    private static readonly string[] PublicationSteps = ["CreateOutputSet", "BeginSet", "Stage", "Commit"];

    private static IEnumerable<OwnedSourceFile> EditFiles => OwnershipSourceFiles.Products
        .Where(static file => file.Path.EndsWith("Edit.cs", StringComparison.Ordinal));

    [Fact]
    public void ProductEdits_PublishThroughPublishEdit()
    {
        string[] missing = [.. OwnershipSourceFiles.ProductIds
            .Where(product => !EditFiles.Any(file => file.Product == product && Invocations(file).Any(static call => call.Name == "PublishEdit")))];

        Assert.True(missing.Length == 0,
            "Every product's edit publishes its output through the SDK's PublishEdit; these do not: " + string.Join(", ", missing));
    }

    [Fact]
    public void ProductEdits_NeverStageOrCommitAnOutputSet()
    {
        IEnumerable<string> violations = EditFiles.SelectMany(file => Invocations(file)
            .Where(static call => PublicationSteps.Contains(call.Name, StringComparer.Ordinal))
            .Select(call => file.At(call.Node, call.Node.Expression.ToString())));

        OwnershipSourceFiles.AssertNone(violations,
            "A product's edit never stages or commits an output set (CreateOutputSet, BeginSet, Stage, Commit); "
            + "it hands its save and verification to the SDK's PublishEdit:");
    }

    private static IEnumerable<(InvocationExpressionSyntax Node, string? Name)> Invocations(OwnedSourceFile file) =>
        file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Select(static invocation => (invocation, OwnershipSourceFiles.InvokedName(invocation)));
}
