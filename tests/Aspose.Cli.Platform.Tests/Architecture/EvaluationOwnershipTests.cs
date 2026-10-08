using System.Text.RegularExpressions;
using Aspose.Cli.TestKit.Scenarios;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>
/// Decision D6, evaluation mode: the SDK write pipeline is the sole owner of evaluation
/// disclosure. It resolves the license state, applies the license gate, writes every output and
/// derives the evaluation warnings by comparing the evaluation marks of the input with those of
/// the staged output. A product only recognizes its own marks through an
/// <see cref="ExpectedProfileName"/> implementation, and every evaluation warning code is an SDK
/// code. These rules read source syntax and the live catalog, not the owner's implementation.
/// </summary>
public sealed partial class EvaluationOwnershipTests
{
    /// <summary>The SDK contract through which a product recognizes evaluation marks; the decision names it.</summary>
    private const string ExpectedProfileName = "IEvaluationProfile";

    /// <summary>The license gate members that only the SDK pipeline calls.</summary>
    private static readonly string[] LicenseGateMembers = ["EnsureApplied", "OutputWarnings"];

    /// <summary>The SDK writer that only the SDK pipeline uses to publish outputs.</summary>
    private const string OutputWriter = "AtomicOutputSetWriter";

    [Fact]
    public void D6_Products_NeverCallTheLicenseGate()
    {
        IEnumerable<string> violations = OwnershipSourceFiles.Products.SelectMany(file => file.Root.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(static name => LicenseGateMembers.Contains(name.Identifier.ValueText, StringComparer.Ordinal))
            .Select(name => file.At(name, (name.Parent is MemberAccessExpressionSyntax access ? access : (SyntaxNode)name).ToString())));

        OwnershipSourceFiles.AssertNone(violations,
            "A product never applies the license or asks it for warnings (EnsureApplied, OutputWarnings); "
            + "the SDK write pipeline resolves the license state once per invocation:");
    }

    [Fact]
    public void D6_Products_NeverReferenceTheOutputWriter()
    {
        IEnumerable<string> violations = OwnershipSourceFiles.Products.SelectMany(file => file.Root.DescendantNodes()
            .OfType<SimpleNameSyntax>()
            .Where(static name => name.Identifier.ValueText == OutputWriter)
            .Select(name => file.At(name, name.Parent?.ToString().Split('\n')[0].Trim() ?? name.ToString())));

        OwnershipSourceFiles.AssertNone(violations,
            $"A product never writes an output through {OutputWriter} itself; it hands the document to the SDK write pipeline:");
    }

    [Fact]
    public void D6_Sdk_DeclaresTheEvaluationProfile()
    {
        InterfaceDeclarationSyntax[] declared =
        [
            .. OwnershipSourceFiles.Sdk.SelectMany(static file => file.Root.DescendantNodes()
                .OfType<InterfaceDeclarationSyntax>()
                .Where(static type => type.Identifier.ValueText == ExpectedProfileName)),
        ];

        Assert.True(declared.Length == 1,
            $"The SDK declares {ExpectedProfileName} once, the contract through which a product recognizes its evaluation marks; found {declared.Length}.");
        string[] methods = [.. declared[0].Members.OfType<MethodDeclarationSyntax>().Select(static method => method.Identifier.ValueText)];
        Assert.True(methods.Contains("Inspect", StringComparer.Ordinal),
            $"{ExpectedProfileName} inspects a document for its evaluation marks (Inspect); it declares: {string.Join(", ", methods)}");
        // Inspect's marks, or the profile itself, say whether evaluation mode truncated the document.
        Assert.True(OwnershipSourceFiles.Sdk.Any(static file => file.Root.DescendantNodes()
                .Any(static node => node switch
                {
                    PropertyDeclarationSyntax property => property.Identifier.ValueText == "IsTruncated",
                    ParameterSyntax { Parent.Parent: RecordDeclarationSyntax } parameter => parameter.Identifier.ValueText == "IsTruncated",
                    _ => false,
                })),
            "The SDK's evaluation marks say whether evaluation mode truncated the document (IsTruncated).");
    }

    [Fact]
    public void D6_EveryProduct_RecognizesItsMarksThroughTheEvaluationProfile()
    {
        string[] products = [.. OwnershipSourceFiles.ProductIds];
        string[] implementing =
        [
            .. OwnershipSourceFiles.Products
                .Where(static file => file.Root.DescendantNodes().OfType<BaseTypeSyntax>()
                    .Any(static baseType => BaseName(baseType.Type) == ExpectedProfileName))
                .Select(static file => file.Product!)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];

        Assert.True(products.Length > 0, "No product project was found.");
        Assert.True(products.SequenceEqual(implementing),
            $"Every product implements {ExpectedProfileName} to recognize its evaluation marks; missing: "
            + string.Join(", ", products.Except(implementing, StringComparer.Ordinal)));
    }

    [Fact]
    public void D6_Products_DeclareNoEvaluationCode()
    {
        IEnumerable<string> violations = OwnershipSourceFiles.Products.SelectMany(file => file.Root.DescendantTokens()
            .Where(static token => token.IsKind(SyntaxKind.StringLiteralToken) && EvaluationCode().IsMatch(token.ValueText))
            .Select(token => file.At(token.Parent!, token.Text)));

        OwnershipSourceFiles.AssertNone(violations,
            "Evaluation warning and error codes are SDK codes; a product declares none of its own:");
    }

    [Fact]
    public void D6_EveryEvaluationCodeInTheCatalog_IsAnSdkCode()
    {
        HashSet<string> sdkLiterals = [.. OwnershipSourceFiles.Sdk.SelectMany(static file => file.Root.DescendantTokens()
            .Where(static token => token.IsKind(SyntaxKind.StringLiteralToken))
            .Select(static token => token.ValueText))];

        string[] codes = [.. CliCatalog.Current.Diagnostics.Keys.Where(static code => EvaluationCode().IsMatch(code)).Order(StringComparer.Ordinal)];

        Assert.NotEmpty(codes);
        OwnershipSourceFiles.AssertNone(codes.Where(code => !sdkLiterals.Contains(code)),
            "Every evaluation code the CLI publishes in its diagnostics catalog is declared by the SDK:");
    }

    private static string? BaseName(TypeSyntax type) => type switch
    {
        GenericNameSyntax generic => generic.Identifier.ValueText,
        SimpleNameSyntax name => name.Identifier.ValueText,
        QualifiedNameSyntax qualified => BaseName(qualified.Right),
        AliasQualifiedNameSyntax alias => BaseName(alias.Name),
        _ => null,
    };

    /// <summary>An evaluation code, such as <c>EVAL_MODE</c> or <c>EVALUATION_LIMIT</c>.</summary>
    [GeneratedRegex("^EVAL(UATION)?_[A-Z0-9_]+$")]
    private static partial Regex EvaluationCode();
}
