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

    /// <summary>
    /// The product extractions that declare they copy embedded files byte for byte
    /// (<c>ExtractionContent.EmbeddedFiles</c>), which the write pipeline does not disclose, by
    /// their source file relative to <c>src</c>. Each is a decision: the extraction writes only
    /// bytes the document stores, never bytes the engine produced, so evaluation mode adds nothing.
    /// </summary>
    private static readonly string[] EmbeddedFileExtractions =
    [
        "Aspose.Cli.Product.Pdf/Engine/PdfExtract.cs",
        "Aspose.Cli.Product.Slides/Engine/SlidesExtract.cs",
    ];

    /// <summary>
    /// A product declares an extraction of embedded files only in an extraction it is listed for,
    /// and only as the content of <c>BeginExtraction</c>, so the declaration never silences the
    /// disclosure of another output.
    /// </summary>
    [Fact]
    public void D6_OnlyListedExtractionsDeclareEmbeddedFiles()
    {
        IEnumerable<string> violations = OwnershipSourceFiles.Products.SelectMany(file => file.Root.DescendantNodes()
            .OfType<MemberAccessExpressionSyntax>()
            .Where(static access => access.Name.Identifier.ValueText == "EmbeddedFiles"
                && access.Expression.ToString().EndsWith("ExtractionContent", StringComparison.Ordinal))
            .Where(access => !EmbeddedFileExtractions.Contains(file.Relative.Replace('\\', '/'), StringComparer.Ordinal)
                || access.FirstAncestorOrSelf<InvocationExpressionSyntax>() is not { } call
                || OwnershipSourceFiles.InvokedName(call) != "BeginExtraction")
            .Select(access => file.At(access, access.ToString())));

        OwnershipSourceFiles.AssertNone(violations,
            "Only the extractions EvaluationOwnershipTests lists copy embedded files without disclosure, "
            + "and only through BeginExtraction; list a new one only when it writes no byte the engine produced:");
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

    /// <summary>
    /// An evaluation limit the engine raises is translated into <c>EVALUATION_LIMIT</c> in one
    /// place, the SDK: outside it no code builds that error while handling an exception, directly
    /// or through an error factory of its own, and the SDK has exactly one catch clause that does.
    /// </summary>
    [Fact]
    public void D6_OnlyTheSdkTranslatesAnEngineEvaluationException()
    {
        OwnedSourceFile[] outside = [.. OwnershipSourceFiles.All.Where(static file => !file.IsUnder(OwnershipSourceFiles.SdkRoot))];
        // A product's own factory of the error counts as building it, such as a refusal it raises itself.
        HashSet<string> builders =
        [
            EvaluationLimitFactory,
            .. outside.SelectMany(static file => file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Where(static invocation => OwnershipSourceFiles.InvokedName(invocation) == EvaluationLimitFactory)
                .Select(static invocation => invocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault())
                .Where(static method => method?.ReturnType.ToString() == "CliException")
                .Select(static method => method!.Identifier.ValueText)),
        ];
        IEnumerable<string> violations = outside.SelectMany(file => file.Root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => OwnershipSourceFiles.InvokedName(invocation) is { } name && builders.Contains(name)
                && invocation.Ancestors().Any(static ancestor => ancestor is CatchClauseSyntax))
            .Select(invocation => file.At(invocation, invocation.ToString().Split('\n')[0].Trim())));

        OwnershipSourceFiles.AssertNone(violations,
            "An engine's evaluation exception is translated into EVALUATION_LIMIT by the SDK alone, from the product's "
            + $"{ExpectedProfileName}; no product or host code builds the error while it handles an exception:");

        string[] sdkTranslations =
        [
            .. OwnershipSourceFiles.Sdk.SelectMany(static file => file.Root.DescendantNodes()
                .OfType<CatchClauseSyntax>()
                .Where(static clause => clause.DescendantNodes().OfType<IdentifierNameSyntax>()
                    .Any(static name => name.Identifier.ValueText.Contains(EvaluationLimitFactory, StringComparison.Ordinal)))
                .Select(clause => file.At(clause, clause.ToString().Split('\n')[0].Trim()))),
        ];
        Assert.True(sdkTranslations.Length == 1,
            "The SDK translates an engine's evaluation exception into EVALUATION_LIMIT in exactly one catch clause; found:"
            + Environment.NewLine + string.Join(Environment.NewLine, sdkTranslations));
    }

    /// <summary>
    /// <c>EVALUATION_INPUT_TRUNCATED</c> is written by the SDK alone, from the product's evaluation
    /// profile, for an input the command read; no product or host code names the code.
    /// </summary>
    [Fact]
    public void D6_OnlyTheSdkWritesTheInputTruncationWarning()
    {
        IEnumerable<string> violations = OwnershipSourceFiles.All
            .Where(static file => !file.IsUnder(OwnershipSourceFiles.SdkRoot))
            .SelectMany(file => file.Root.DescendantNodes()
                .OfType<MemberAccessExpressionSyntax>()
                .Where(static access => access.Name.Identifier.ValueText == InputTruncatedCode
                    && access.Expression.ToString().EndsWith("WarningCodes", StringComparison.Ordinal))
                .Select(access => file.At(access, access.ToString())));

        OwnershipSourceFiles.AssertNone(violations,
            "EVALUATION_INPUT_TRUNCATED is the SDK's to write for an input a command read (OutputPipeline.ReadInput); "
            + "no product or host code builds it:");
    }

    /// <summary>The SDK factory name of the <c>EVALUATION_LIMIT</c> error.</summary>
    private const string EvaluationLimitFactory = "EvaluationLimit";

    /// <summary>The SDK warning code member of <c>EVALUATION_INPUT_TRUNCATED</c>.</summary>
    private const string InputTruncatedCode = "EvaluationInputTruncated";

    private static string? BaseName(TypeSyntax type) => type switch
    {
        GenericNameSyntax generic => generic.Identifier.ValueText,
        SimpleNameSyntax name => name.Identifier.ValueText,
        QualifiedNameSyntax qualified => BaseName(qualified.Right),
        AliasQualifiedNameSyntax alias => BaseName(alias.Name),
        _ => null,
    };

    /// <summary>An evaluation code, such as <c>EVALUATION_MODE</c> or <c>EVALUATION_LIMIT</c>.</summary>
    [GeneratedRegex("^EVAL(UATION)?_[A-Z0-9_]+$")]
    private static partial Regex EvaluationCode();
}
