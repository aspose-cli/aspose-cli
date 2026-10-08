using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>
/// Each decision a command makes has one owner, and nothing else can make it. These rules read
/// the source and the live capabilities, not the owner's implementation: D1 outputs (the SDK
/// resolves the output path, its format, encryption and part names), D2 format capabilities
/// (a product declares no input it cannot read), D3 mistakes (one SDK type writes every
/// suggestion), D4 errors (shared codes come only from SDK factories) and D5 secrets (an SDK
/// secret type that only an engine adapter reveals).
/// </summary>
public sealed partial class DecisionOwnershipTests
{
    /// <summary>
    /// The SDK type that carries a secret value: its text form is redacted, it does not serialize
    /// to JSON, and only an engine adapter reveals the value. The decision names it <c>Secret</c>.
    /// </summary>
    private const string ExpectedSecretTypeName = "Secret";

    private const string SecretSentinel = "Decision-Secret-5c2e";

    private static readonly string SourceRoot = Path.Combine(RepositoryPaths.Root, "src");
    private static readonly string SdkRoot = Path.Combine(SourceRoot, "Aspose.Cli.Sdk");
    private static readonly string HostRoot = Path.Combine(SourceRoot, "Aspose.Cli.Host");
    private static readonly Lazy<SourceFile[]> Sources = new(LoadSources);

    // -- D1 Output ---------------------------------------------------------------------------

    [Fact]
    public void D1_ProductRequestsAndPorts_TakeNoRawOutputPathString()
    {
        string[] violations = [.. ProductFiles("Contracts", "Ports").SelectMany(file => file.Root.DescendantNodes()
            .SelectMany(static node => node switch
            {
                PropertyDeclarationSyntax property => [(node, property.Type, property.Identifier.ValueText)],
                FieldDeclarationSyntax field => field.Declaration.Variables
                    .Select(variable => ((SyntaxNode)variable, field.Declaration.Type, variable.Identifier.ValueText)),
                ParameterSyntax { Type: { } type } parameter => [(node, type, parameter.Identifier.ValueText)],
                _ => Array.Empty<(SyntaxNode, TypeSyntax, string)>(),
            })
            .Where(static member => IsString(member.Item2) && OutputPathName().IsMatch(member.Item3))
            .Select(member => file.At(member.Item1, $"string {member.Item3}")))];

        AssertNone(violations,
            "Product request and port types receive the SDK's resolved output, never a raw output path string:");
    }

    [Fact]
    public void D1_ProductCommandsAndContracts_DeriveNoFormatFromAnExtension()
    {
        string[] violations = [.. ProductFiles("Commands", "Contracts", "Ports").SelectMany(file => file.Root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(static invocation => InvokedName(invocation) is "GetExtension" or "ChangeExtension" or "HasExtension"
                || (InvokedName(invocation) == "EndsWith"
                    && invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax literal
                    && literal.Token.ValueText.StartsWith('.')))
            .Select(invocation => file.At(invocation, invocation.ToString())))];

        AssertNone(violations,
            "Product commands and contracts read no file extension; the SDK resolves an output's format:");
    }

    [Fact]
    public void D1_Products_NeverRefuseAnOutputFormatThemselves()
    {
        string[] violations = [.. ProductFiles().SelectMany(file => file.Root.DescendantNodes()
            .Where(static node => node switch
            {
                InvocationExpressionSyntax invocation => InvokedName(invocation) is "FormatUnsupported" or "OutputFormatUnsupported",
                MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText == "FormatUnsupported"
                    && LastIdentifier(access.Expression) == "ErrorCodes",
                _ => false,
            })
            .Where(static node => !RefusesAnInput(node))
            .Select(node => file.At(node, node.ToString())))];

        AssertNone(violations,
            "Only the SDK refuses an output format (FORMAT_UNSUPPORTED); a product never refuses one itself:");
    }

    [Fact]
    public void D1_Products_DeclareNoOutputFormatResolver()
    {
        string[] violations = [.. ProductFiles().SelectMany(file => file.Root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(static method => method.Identifier.ValueText.StartsWith("ForOutput", StringComparison.Ordinal))
            .Select(method => file.At(method, method.Identifier.ValueText)))];

        AssertNone(violations,
            "A product never maps an output path to a format; the SDK resolves the output before the product runs:");
    }

    [Fact]
    public void D1_Products_ListNoProtectableFormats()
    {
        string[] violations = [.. ProductFiles().SelectMany(file => file.Root.DescendantNodes()
            .SelectMany(static node => node switch
            {
                PropertyDeclarationSyntax property => [(node, property.Identifier.ValueText)],
                FieldDeclarationSyntax field => field.Declaration.Variables.Select(variable => ((SyntaxNode)variable, variable.Identifier.ValueText)),
                _ => Array.Empty<(SyntaxNode, string)>(),
            })
            .Where(static member => ProtectableListName().IsMatch(member.Item2))
            .Select(member => file.At(member.Item1, member.Item2)))];

        AssertNone(violations,
            "Whether a format can carry a password is declared once, on its format descriptor, not in a product list:");
    }

    // -- D2 Capabilities ---------------------------------------------------------------------

    [Theory]
    [InlineData("json")]
    [InlineData("md")]
    [InlineData("pdf")]
    [InlineData("xps")]
    public void D2_Cells_DeclaresNoInputItCannotRead(string format) =>
        Assert.DoesNotContain(format, CliCatalog.Current.Product("cells").LoadFormats);

    // -- D3 Mistakes -------------------------------------------------------------------------

    [Fact]
    public void D3_DidYouMeanText_HasOneSdkOwner()
    {
        string[] owners = DidYouMeanFiles();

        Assert.True(
            owners.Length == 1 && IsUnder(owners[0], SdkRoot),
            "Exactly one SDK type writes \"did you mean\" text; it is written in:" + Environment.NewLine
            + string.Join(Environment.NewLine, owners.Select(Relative)));
    }

    [Fact]
    public void D3_SuggestionDetails_AreWrittenOnlyByTheMistakeOwner()
    {
        string[] writers = [.. Sources.Value.Where(static file => SuggestionWrites(file, "suggestions").Any()).Select(static file => file.Path)];
        string[] owners = DidYouMeanFiles();

        Assert.True(
            writers.Length == 1 && owners.SequenceEqual(writers, StringComparer.OrdinalIgnoreCase),
            "details.suggestions is written by the one SDK type that writes \"did you mean\" text."
            + Environment.NewLine + "suggestions writers: " + string.Join(", ", writers.Select(Relative))
            + Environment.NewLine + "did you mean writers: " + string.Join(", ", owners.Select(Relative)));
    }

    [Fact]
    public void D3_NoError_WritesASingleSuggestion()
    {
        string[] violations = [.. Sources.Value.SelectMany(file => SuggestionWrites(file, "suggestion")
            .Select(node => file.At(node, node.ToString())))];

        AssertNone(violations,
            "Every mistake publishes details.suggestions as an array; no error writes a single details.suggestion:");
    }

    [Fact]
    public void D3_ACommandWithSubcommands_TakesNoArgument()
    {
        string[] violations = [.. CliCatalog.Current.Commands
            .Where(static command => command.Subcommands.Count > 0 && command.Arguments.Count > 0)
            .Select(static command => $"{command} takes {command.Arguments.Count} argument(s) beside its commands {string.Join(", ", command.Subcommands)}")];

        AssertNone(violations,
            "A command with subcommands takes no positional argument, so a mistyped subcommand is a usage error with suggestions:");
        Assert.Contains("open", CliCatalog.Current.Command("app").Subcommands);
    }

    [Theory]
    [InlineData("app", "--route")]
    [InlineData("cells query range", "--scan-range")]
    public void D3_Capabilities_MarkAHiddenOptionHidden(string command, string option)
    {
        JsonObject[] declared = [.. CommandOptions(command).Where(item => item["name"]?.GetValue<string>() == option)];

        Assert.NotEmpty(declared);
        Assert.All(declared, item => Assert.True(
            item["hidden"]?.GetValue<bool>() == true,
            $"capabilities publishes {option} of {command} without hidden: true: {item.ToJsonString()}"));
        Assert.All(
            CommandOptions("cells convert").Where(static item => item["name"]?.GetValue<string>() == "--out"),
            static item => Assert.NotEqual(true, item["hidden"]?.GetValue<bool>()));
    }

    [Fact]
    public void D3_AHiddenOption_IsNeverSuggested()
    {
        using var workspace = new TempWorkspace();

        CliResult result = workspace.Run("cells", "query", "range", "missing.xlsx", "--scan-rang", "A1", "--output", "json");

        Assert.Equal(2, result.ExitCode);
        Assert.DoesNotContain("--scan-range", result.StdErr, StringComparison.Ordinal);
    }

    // -- D4 Errors ---------------------------------------------------------------------------

    [Fact]
    public void D4_SharedErrorCodes_AreBuiltOnlyBySdkFactories()
    {
        string[] violations = [.. Sources.Value
            .Where(static file => file.Product is not null || IsUnder(file.Path, HostRoot))
            .SelectMany(file => file.Root.DescendantNodes()
                .Where(static node => node switch
                {
                    ObjectCreationExpressionSyntax creation =>
                        LastIdentifier(creation.Type)?.EndsWith("Exception", StringComparison.Ordinal) == true
                        && PassesASharedCode(creation.ArgumentList),
                    ImplicitObjectCreationExpressionSyntax creation => PassesASharedCode(creation.ArgumentList),
                    _ => false,
                })
                .Select(node => file.At(node, node.ToString().Split('\n')[0].Trim())))];

        AssertNone(violations,
            "The Host and the products build a shared error code (Aspose.Cli.Sdk.Errors.ErrorCodes) only through an SDK factory:");
    }

    // -- D5 Secrets --------------------------------------------------------------------------

    [Fact]
    public void D5_Products_ReadNoEnvironmentVariable()
    {
        string[] violations = [.. ProductFiles().SelectMany(file => file.Root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(static invocation => InvokedName(invocation)
                is "GetEnvironmentVariable" or "GetEnvironmentVariables" or "ExpandEnvironmentVariables" or "ReadEnvironment")
            .Select(invocation => file.At(invocation, invocation.ToString())))];

        AssertNone(violations,
            "A product never reads the environment; the SDK resolves *-env options and *Env operation fields to secrets:");
    }

    [Fact]
    public void D5_PasswordsOutsideTheEngine_AreSecrets()
    {
        string[] violations = [.. ProductFiles().Where(static file => file.Area != "Engine").SelectMany(file => file.Root.DescendantNodes()
            .SelectMany(static node => node switch
            {
                PropertyDeclarationSyntax property => [(node, property.Type, property.Identifier.ValueText)],
                FieldDeclarationSyntax field => field.Declaration.Variables
                    .Select(variable => ((SyntaxNode)variable, field.Declaration.Type, variable.Identifier.ValueText)),
                LocalDeclarationStatementSyntax local => local.Declaration.Variables
                    .Select(variable => ((SyntaxNode)variable, local.Declaration.Type, variable.Identifier.ValueText)),
                ParameterSyntax { Type: { } type } parameter => [(node, type, parameter.Identifier.ValueText)],
                _ => Array.Empty<(SyntaxNode, TypeSyntax, string)>(),
            })
            .Where(static member => IsString(member.Item2) && member.Item3.EndsWith("Password", StringComparison.OrdinalIgnoreCase))
            .Select(member => file.At(member.Item1, $"string {member.Item3}")))];

        AssertNone(violations,
            $"Outside its engine adapter a product holds a password only as the SDK {ExpectedSecretTypeName} type, never as a string:");
    }

    [Fact]
    public void D5_SecretType_RedactsItsTextAndJson()
    {
        Type? secret = typeof(IProductModule).Assembly.GetTypes()
            .SingleOrDefault(static type => type.Name == ExpectedSecretTypeName);
        Assert.True(secret is not null, $"The SDK declares no {ExpectedSecretTypeName} type.");

        object value = CreateSecret(secret!, SecretSentinel);

        Assert.DoesNotContain(SecretSentinel, value.ToString() ?? string.Empty, StringComparison.Ordinal);
        string? json = null;
        try
        {
            json = JsonSerializer.Serialize(value, secret!);
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException or JsonException)
        {
        }
        Assert.DoesNotContain(SecretSentinel, json ?? string.Empty, StringComparison.Ordinal);
    }

    // -- Support -----------------------------------------------------------------------------

    private static object CreateSecret(Type secret, string text)
    {
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic;
        if (secret.GetConstructors(Any | BindingFlags.Instance)
                .FirstOrDefault(static constructor => constructor.GetParameters() is [{ ParameterType: var type }] && type == typeof(string))
            is { } fromConstructor)
        {
            return fromConstructor.Invoke([text]);
        }
        MethodInfo factory = secret.GetMethods(Any | BindingFlags.Static)
            .FirstOrDefault(method => method.ReturnType == secret
                && method.GetParameters() is [{ ParameterType: var type }] && type == typeof(string))
            ?? throw new InvalidOperationException($"{secret.FullName} has no constructor or static factory that takes the secret text.");
        return factory.Invoke(null, [text])!;
    }

    private static IEnumerable<JsonObject> CommandOptions(string words) =>
        CliCatalog.Current.Document["commands"]!.AsArray()
            .OfType<JsonObject>()
            .Where(command => command["path"]?.GetValue<string>() is { } path
                && (path == "aspose-cli " + words || path.StartsWith("aspose-cli " + words + " ", StringComparison.Ordinal)))
            .SelectMany(static command => command["options"]!.AsArray().OfType<JsonObject>());

    private static string[] DidYouMeanFiles() =>
        [.. Sources.Value
            .Where(static file => file.Root.DescendantTokens().Any(static token =>
                (token.IsKind(SyntaxKind.StringLiteralToken)
                    || token.IsKind(SyntaxKind.InterpolatedStringTextToken)
                    || token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken)
                    || token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken))
                && token.ValueText.Contains("did you mean", StringComparison.OrdinalIgnoreCase)))
            .Select(static file => file.Path)
            .Order(StringComparer.OrdinalIgnoreCase)];

    /// <summary>Assignments and initializers that set a JSON member with the given literal name.</summary>
    private static IEnumerable<SyntaxNode> SuggestionWrites(SourceFile file, string key) =>
        file.Root.DescendantNodes().Where(node => node switch
        {
            AssignmentExpressionSyntax { Left: ElementAccessExpressionSyntax access } =>
                IsKey(access.ArgumentList.Arguments, key),
            AssignmentExpressionSyntax { Left: ImplicitElementAccessSyntax access } =>
                IsKey(access.ArgumentList.Arguments, key),
            InitializerExpressionSyntax { RawKind: (int)SyntaxKind.ComplexElementInitializerExpression } pair =>
                pair.Expressions.FirstOrDefault() is LiteralExpressionSyntax literal && literal.Token.ValueText == key,
            _ => false,
        });

    /// <summary>
    /// An input-side refusal (decision D8, input loading): its arguments or the member that makes
    /// it name a load, an input or a certificate, such as a loader refusing a format it cannot read.
    /// </summary>
    private static bool RefusesAnInput(SyntaxNode node)
    {
        string arguments = node is InvocationExpressionSyntax invocation ? invocation.ArgumentList.ToString() : string.Empty;
        string member = node.Ancestors().Select(static ancestor => ancestor switch
        {
            MethodDeclarationSyntax method => method.Identifier.ValueText,
            LocalFunctionStatementSyntax local => local.Identifier.ValueText,
            PropertyDeclarationSyntax property => property.Identifier.ValueText,
            _ => null,
        }).FirstOrDefault(static name => name is not null) ?? string.Empty;
        return InputSide().IsMatch(arguments) || InputSide().IsMatch(member);
    }

    private static bool IsKey(SeparatedSyntaxList<ArgumentSyntax> arguments, string key) =>
        arguments is [{ Expression: LiteralExpressionSyntax literal }] && literal.Token.ValueText == key;

    private static bool PassesASharedCode(ArgumentListSyntax? arguments) =>
        arguments?.Arguments.Any(static argument => argument.Expression is MemberAccessExpressionSyntax access
            && LastIdentifier(access.Expression) == "ErrorCodes") == true;

    private static bool IsString(TypeSyntax type) => type switch
    {
        PredefinedTypeSyntax predefined => predefined.Keyword.IsKind(SyntaxKind.StringKeyword),
        NullableTypeSyntax nullable => IsString(nullable.ElementType),
        _ => false,
    };

    private static string? InvokedName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
        SimpleNameSyntax name => name.Identifier.ValueText,
        _ => null,
    };

    private static string? LastIdentifier(SyntaxNode node) => node switch
    {
        SimpleNameSyntax name => name.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        _ => null,
    };

    private static IEnumerable<SourceFile> ProductFiles(params string[] areas) =>
        Sources.Value.Where(file => file.Product is not null && (areas.Length == 0 || areas.Contains(file.Area, StringComparer.Ordinal)));

    private static void AssertNone(string[] violations, string rule) =>
        Assert.True(violations.Length == 0, rule + Environment.NewLine + string.Join(Environment.NewLine, violations));

    private static bool IsUnder(string path, string directory) =>
        path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static string Relative(string path) => Path.GetRelativePath(SourceRoot, path);

    private static SourceFile[] LoadSources() =>
        [.. Directory.GetDirectories(SourceRoot, "Aspose.Cli*")
            .Where(static project => !Path.GetFileName(project).EndsWith(".Analyzers", StringComparison.Ordinal))
            .SelectMany(static project => Directory.GetFiles(project, "*.cs", SearchOption.AllDirectories)
                .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                    && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Select(path => SourceFile.Read(project, path)))
            .OrderBy(static file => file.Path, StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// One source file. <see cref="Product"/> is the product id for a product project, and
    /// <see cref="Area"/> the first folder below the project, such as <c>Engine</c>.
    /// </summary>
    private sealed record SourceFile(string Path, string? Product, string Area, SyntaxNode Root)
    {
        public static SourceFile Read(string project, string path)
        {
            string name = System.IO.Path.GetFileName(project);
            string relative = System.IO.Path.GetRelativePath(project, path);
            string[] parts = relative.Split(System.IO.Path.DirectorySeparatorChar);
            return new SourceFile(
                path,
                name.StartsWith("Aspose.Cli.Product.", StringComparison.Ordinal) ? name["Aspose.Cli.Product.".Length..] : null,
                parts.Length > 1 ? parts[0] : string.Empty,
                CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path).GetRoot());
        }

        public string At(SyntaxNode node, string what) =>
            $"{System.IO.Path.GetRelativePath(SourceRoot, Path)}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: {what}";
    }

    /// <summary>A string member that names an output location rather than a resolved output.</summary>
    [GeneratedRegex("^(out|\\w*(output|out)\\w*(path|paths|directory|dir|file|folder))$", RegexOptions.IgnoreCase)]
    private static partial Regex OutputPathName();

    [GeneratedRegex("Load|Input|Certificate")]
    private static partial Regex InputSide();

    [GeneratedRegex("^(encrypt|encryptable|protect|protectable)\\w*ids$", RegexOptions.IgnoreCase)]
    private static partial Regex ProtectableListName();
}
