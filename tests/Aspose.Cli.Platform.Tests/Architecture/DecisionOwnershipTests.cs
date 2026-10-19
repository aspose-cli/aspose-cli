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
    public void D1_ProductRequests_TakeNoRawOutputPathString()
    {
        string[] violations = [.. ProductFiles("Contracts").SelectMany(file => file.Root.DescendantNodes()
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
            "Product request types receive the SDK's resolved output, never a raw output path string:");
    }

    /// <summary>
    /// A file's extension is read only by the SDK, which matches it against a product's format
    /// declarations (<c>FormatDescriptorExtensions.NamedBy</c>, the SDK error factories, an
    /// <c>OwnedTemporaryFile</c> named like its source). No source file of a product, in any
    /// folder, reads one.
    /// </summary>
    [Fact]
    public void D1_Products_DeriveNoFormatFromAnExtension()
    {
        string[] violations = [.. ProductFiles().SelectMany(static file => file.Root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(static invocation => InvokedName(invocation) is "GetExtension" or "ChangeExtension" or "HasExtension"
                || (InvokedName(invocation) == "EndsWith"
                    && invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax literal
                    && literal.Token.ValueText.StartsWith('.')))
            .Select(invocation => file.At(invocation, invocation.ToString())))];

        AssertNone(violations,
            "No product source file reads a file extension; the SDK matches a path against the product's format declarations:");
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

    /// <summary>
    /// Whether an output keeps the input's password, takes the <c>--encrypt</c> one or carries
    /// none is decided by the SDK's <c>OutputProtection</c>; a product engine only carries the
    /// decision out. PDF keeps its own owner (<c>PdfOutputEncryption</c>): owner-only files,
    /// permissions and operations that encrypt or decrypt are PDF's alone.
    /// </summary>
    [Fact]
    public void D1_ProductEngines_DecideNoOutputPasswordThemselves()
    {
        string[] violations = [.. ProductFiles("Engine")
            .Where(static file => file.Product != "Pdf")
            .SelectMany(file => file.Root.DescendantNodes()
                .Where(static node => node switch
                {
                    // --encrypt, or else some other password: request.EncryptPassword ?? ...
                    BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.CoalesceExpression) =>
                        string.Equals(LastIdentifier(binary.Left), "EncryptPassword", StringComparison.OrdinalIgnoreCase),
                    // The input's password where the input was encrypted: loaded.IsEncrypted ? request.Password : null
                    ConditionalExpressionSyntax conditional =>
                        Names(conditional.Condition).Contains("IsEncrypted")
                        && Names(conditional.WhenTrue).Concat(Names(conditional.WhenFalse))
                            .Any(static name => name.EndsWith("Password", StringComparison.Ordinal)),
                    _ => false,
                })
                .Select(node => file.At(node, node.ToString().Split('\n')[0].Trim())))];

        AssertNone(violations,
            "A product takes an output's password from OutputProtection.For, which decides it from --encrypt and the primary input:");

        static IEnumerable<string> Names(SyntaxNode node) =>
            node.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Select(static name => name.Identifier.ValueText);
    }

    /// <summary>
    /// <c>FormatDescriptor.Protectable</c> says whether an output can carry a password, which
    /// only <c>OutputProtection</c> decides; what else a format keeps, such as sheet protection,
    /// is a trait of the product's engine format table.
    /// </summary>
    [Fact]
    public void D1_ProductEngines_ReadNoProtectableFlag()
    {
        string[] violations = [.. ProductFiles("Engine")
            .SelectMany(file => file.Root.DescendantNodes()
                .OfType<MemberAccessExpressionSyntax>()
                .Where(static access => access.Name.Identifier.ValueText == "Protectable")
                .Select(access => file.At(access, access.ToString())))];

        AssertNone(violations,
            "A product engine leaves FormatDescriptor.Protectable to OutputProtection and reads what a format keeps from its engine format table:");
    }

    [Fact]
    public void D1_FileObjects_ComeOnlyFromTheSdkFactory()
    {
        string factory = Path.Combine(SdkRoot, "IO", "FileObjects.cs");
        string[] violations = [.. Sources.Value
            .Where(file => !string.Equals(file.Path, factory, StringComparison.OrdinalIgnoreCase))
            .SelectMany(file => file.Root.DescendantNodes()
                .Where(static node => node switch
                {
                    ObjectCreationExpressionSyntax creation => IsFileObject(creation.Type),
                    ImplicitObjectCreationExpressionSyntax creation => CreatedType(creation) is { } type && IsFileObject(type),
                    _ => false,
                })
                .Select(node => file.At(node, node.ToString().Split('\n')[0].Trim())))];

        AssertNone(violations,
            "The input and output file objects a result reports come only from the SDK's FileObjects factory, which makes paths absolute and fingerprints every file:");

        static bool IsFileObject(TypeSyntax type) => LastIdentifier(type) is "InputInfo" or "SourceInput" or "OutputInfo";

        // The type a target-typed new() creates, where the declaration beside it names it.
        static TypeSyntax? CreatedType(ImplicitObjectCreationExpressionSyntax creation) => creation.Parent switch
        {
            ArrowExpressionClauseSyntax { Parent: MethodDeclarationSyntax method } => method.ReturnType,
            ArrowExpressionClauseSyntax { Parent: PropertyDeclarationSyntax property } => property.Type,
            ArrowExpressionClauseSyntax { Parent: LocalFunctionStatementSyntax function } => function.ReturnType,
            ReturnStatementSyntax statement => statement.FirstAncestorOrSelf<MethodDeclarationSyntax>()?.ReturnType,
            EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration } } => declaration.Type,
            EqualsValueClauseSyntax { Parent: PropertyDeclarationSyntax property } => property.Type,
            _ => null,
        };
    }

    [Fact]
    public void D1_EncryptionRemoved_IsWarnedOnlyByTheWritePipeline()
    {
        string[] owners =
        [
            Path.Combine(SdkRoot, "Results", "OutputPipeline.cs"),
            // The declaration of the code itself.
            Path.Combine(SdkRoot, "Contracts", "Warning.cs"),
        ];
        string[] violations = [.. Sources.Value
            .Where(file => !owners.Contains(file.Path, StringComparer.OrdinalIgnoreCase))
            .SelectMany(file => file.Root.DescendantNodes()
                .Where(static node => node switch
                {
                    MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText == "EncryptionRemoved",
                    LiteralExpressionSyntax literal => literal.Token.ValueText == "ENCRYPTION_REMOVED",
                    _ => false,
                })
                .Select(node => file.At(node, node.ToString())))];

        AssertNone(violations,
            "Only the SDK write pipeline warns ENCRYPTION_REMOVED, comparing the encryption of the documents a product opened with that of the copies it published:");
    }

    // -- D2 Capabilities ---------------------------------------------------------------------

    [Theory]
    [InlineData("json")]
    [InlineData("md")]
    [InlineData("pdf")]
    [InlineData("xps")]
    public void D2_Cells_DeclaresNoInputItCannotRead(string format) =>
        Assert.DoesNotContain(format, CliCatalog.Current.Product("cells").LoadFormats);

    /// <summary>
    /// A product compares no value with one of its declared format ids: what a format does is a
    /// row of the product's format tables (its Contracts <c>FormatDescriptor</c> declarations and
    /// the engine and trait tables keyed by them), never an <c>== "svg"</c>, an <c>is "csv"</c>
    /// or a <c>case "pdf":</c> in a command or an engine.
    /// </summary>
    [Fact]
    public void D2_ProductEnginesAndCommands_CompareNoFormatIdLiteral()
    {
        Dictionary<string, HashSet<string>> declared = DeclaredFormatIds();
        string[] violations = [.. ProductFiles("Commands", "Engine")
            .SelectMany(file => file.Root.DescendantNodes()
                .OfType<LiteralExpressionSyntax>()
                .Where(literal => literal.IsKind(SyntaxKind.StringLiteralExpression)
                    && declared.TryGetValue(file.Product!, out HashSet<string>? ids) && ids.Contains(literal.Token.ValueText)
                    && ComparedBy(literal) is not null)
                .Select(literal => file.At(literal, ComparedBy(literal)!.ToString().Split('\n')[0].Trim())))
            .Distinct(StringComparer.Ordinal)];

        Assert.Equal(
            ProductFiles().Select(static file => file.Product!).Distinct().Order(StringComparer.Ordinal),
            declared.Keys.Order(StringComparer.Ordinal));
        AssertNone(violations,
            "A product command or engine compares no value with a declared format id; the format's row in its tables says what the format does:");

        // The comparison a literal takes part in: == or !=, a pattern (is, a switch arm, a
        // property pattern) or a case label; null for any other use.
        static SyntaxNode? ComparedBy(LiteralExpressionSyntax literal)
        {
            SyntaxNode? parent = literal.Parent;
            while (parent is ParenthesizedExpressionSyntax)
            {
                parent = parent.Parent;
            }
            return parent switch
            {
                BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.EqualsExpression) || binary.IsKind(SyntaxKind.NotEqualsExpression) => binary,
                ConstantPatternSyntax pattern => pattern.Ancestors().FirstOrDefault(static ancestor => ancestor is IsPatternExpressionSyntax
                    or SwitchExpressionArmSyntax or CasePatternSwitchLabelSyntax or SubpatternSyntax) ?? pattern,
                CaseSwitchLabelSyntax label => label,
                _ => null,
            };
        }
    }

    /// <summary>
    /// The format ids each product declares in its Contracts, by product folder name such as
    /// <c>Cells</c>: the first argument of every <c>FormatDescriptor</c> it creates, by
    /// <c>FormatDescriptor.Declare</c> or <c>Routed</c>, <c>new FormatDescriptor(...)</c> or a
    /// target-typed <c>new("id", FormatUse...)</c>.
    /// </summary>
    private static Dictionary<string, HashSet<string>> DeclaredFormatIds() =>
        ProductFiles("Contracts")
            .SelectMany(static file => file.Root.DescendantNodes()
                .Select(static node => node switch
                {
                    InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Declare" or "Routed" } factory } invocation
                        when LastIdentifier(factory.Expression) == "FormatDescriptor" => invocation.ArgumentList,
                    ObjectCreationExpressionSyntax creation when LastIdentifier(creation.Type) == "FormatDescriptor" => creation.ArgumentList,
                    ImplicitObjectCreationExpressionSyntax { ArgumentList.Arguments: [_, { Expression: var use }, ..] } creation
                        when use.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Any(static name => name.Identifier.ValueText == "FormatUse") => creation.ArgumentList,
                    _ => null,
                })
                .Select(static arguments => arguments?.Arguments is [{ Expression: LiteralExpressionSyntax id }, ..]
                    && id.IsKind(SyntaxKind.StringLiteralExpression) ? id.Token.ValueText : null)
                .OfType<string>()
                .Select(id => (Product: file.Product!, Id: id)))
            .GroupBy(static pair => pair.Product, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Select(static pair => pair.Id).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);

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
    [InlineData("cells read", "--scan-range")]
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

        CliResult result = workspace.Run("cells", "read", "missing.xlsx", "--scan-rang", "A1", "--output-format", "json");

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

    // -- D12 Path case ----------------------------------------------------------------------

    [Fact]
    public void D12_PathCase_HasOneSdkOwner()
    {
        string owner = Path.Combine(SdkRoot, "FilePaths.cs");
        string[] violations = [.. Sources.Value
            .Where(file => !string.Equals(file.Path, owner, StringComparison.OrdinalIgnoreCase))
            .SelectMany(file => PathCaseDecisions(file.Root).Select(node => file.At(node, node.ToString())))];

        AssertNone(violations,
            "Local paths are compared, collected, folded into lock keys and checked for a UNC or device prefix only through the SDK's FilePaths:");
    }

    [Fact]
    public void D13_ProcessLiveness_HasOneSdkOwner()
    {
        string owner = Path.Combine(SdkRoot, "IO", "ProcessIdentity.cs");
        string[] violations = [.. Sources.Value
            .Where(file => !string.Equals(file.Path, owner, StringComparison.OrdinalIgnoreCase))
            .SelectMany(file => file.Root.DescendantNodes()
                .Where(static node => node switch
                {
                    // process.StartTime, Process.GetProcessById(...), process?.StartTime
                    MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText is "GetProcessById" or "StartTime",
                    MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText is "GetProcessById" or "StartTime",
                    // GetProcessById(...) under using static System.Diagnostics.Process
                    InvocationExpressionSyntax { Expression: IdentifierNameSyntax name } => name.Identifier.ValueText == "GetProcessById",
                    _ => false,
                })
                .Select(node => file.At(node, node.ToString())))];

        AssertNone(violations,
            "A process is identified by its id and start time only in the SDK's ProcessIdentity, which also decides whether it still runs:");
    }

    // -- Support -----------------------------------------------------------------------------

    /// <summary>
    /// The places that decide how local paths compare (decision D12): a case rule chosen on
    /// <c>OperatingSystem.IsWindows()</c>, a path folded to upper case, a case-insensitive
    /// comparison or collection of paths, and a UNC prefix check. What a comparison compares or
    /// collects is a path when it is built with <c>Path</c> or a member named for a path, when
    /// it is a variable or collection that holds such values, or when the member that compares
    /// is named for a path. An ordering decides no equality and is left alone, and so is a
    /// comparison of extensions, which the format tables own.
    /// </summary>
    private static IEnumerable<SyntaxNode> PathCaseDecisions(SyntaxNode root) =>
        root.DescendantNodes().Where(node => node switch
        {
            ConditionalExpressionSyntax conditional => IsWindowsCheck(conditional.Condition)
                && (IsCaseRule(conditional.WhenTrue) || IsCaseRule(conditional.WhenFalse)),
            IfStatementSyntax statement => IsWindowsCheck(statement.Condition)
                && statement.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(IsCaseRule),
            InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "ToUpperInvariant" } folded }
                => !InWindowsBranch(node) && NamesAPath(root, folded.Expression, follow: false),
            MemberAccessExpressionSyntax access => LastIdentifier(access.Expression) is "StringComparer" or "StringComparison"
                && access.Name.Identifier.ValueText.EndsWith("IgnoreCase", StringComparison.Ordinal)
                && !InWindowsBranch(node) && ComparesPaths(root, access),
            LiteralExpressionSyntax literal => literal.IsKind(SyntaxKind.StringLiteralExpression) && literal.Token.ValueText == @"\\"
                && literal.Parent?.Parent?.Parent is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "StartsWith" } },
            _ => false,
        });

    private static bool IsWindowsCheck(ExpressionSyntax condition) =>
        condition.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()
            .Any(static invocation => invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "IsWindows" });

    /// <summary>A <c>StringComparer</c> or <c>StringComparison</c> value, or a fold to one case.</summary>
    private static bool IsCaseRule(ExpressionSyntax expression) => expression switch
    {
        ParenthesizedExpressionSyntax parenthesized => IsCaseRule(parenthesized.Expression),
        MemberAccessExpressionSyntax access => LastIdentifier(access.Expression) is "StringComparer" or "StringComparison",
        InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "ToUpperInvariant" or "ToLowerInvariant" } } => true,
        _ => false,
    };

    /// <summary>Inside a branch on <c>OperatingSystem.IsWindows()</c>, which is reported itself.</summary>
    private static bool InWindowsBranch(SyntaxNode node) =>
        node.Ancestors().Any(static ancestor => ancestor switch
        {
            ConditionalExpressionSyntax conditional => IsWindowsCheck(conditional.Condition),
            IfStatementSyntax statement => IsWindowsCheck(statement.Condition),
            _ => false,
        });

    private static bool ComparesPaths(SyntaxNode root, MemberAccessExpressionSyntax comparison)
    {
        SyntaxNode[]? compared = Compared(root, comparison);
        if (compared is null
            || compared.Any(static node => node.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
                .Any(static name => name.Identifier.ValueText.Contains("extension", StringComparison.OrdinalIgnoreCase))))
        {
            return false;
        }
        return PathName().IsMatch(EnclosingMember(comparison)) || compared.Any(node => NamesAPath(root, node, follow: true));
    }

    /// <summary>
    /// What a comparison compares, or what a comparer's collection holds: the other arguments
    /// and the receiver of the call, or the collection's name and what code puts in it; null for
    /// an ordering.
    /// </summary>
    private static SyntaxNode[]? Compared(SyntaxNode root, MemberAccessExpressionSyntax comparison)
    {
        if (comparison.Ancestors().OfType<ArgumentSyntax>().FirstOrDefault() is not { Parent: ArgumentListSyntax { Parent: { } owner } } argument)
        {
            return [];
        }
        IEnumerable<SyntaxNode> others = ((BaseArgumentListSyntax)argument.Parent).Arguments
            .Where(other => other != argument).Select(static other => (SyntaxNode)other.Expression);
        return owner switch
        {
            InvocationExpressionSyntax invocation when InvokedName(invocation) is "Order" or "OrderBy" or "OrderByDescending"
                or "OrderDescending" or "ThenBy" or "ThenByDescending" or "Sort" => null,
            InvocationExpressionSyntax invocation =>
                [.. others, .. invocation.Expression is MemberAccessExpressionSyntax receiver ? [receiver.Expression] : Array.Empty<SyntaxNode>()],
            BaseObjectCreationExpressionSyntax creation => DeclaredName(creation) is { } name
                ? [.. others, SyntaxFactory.IdentifierName(name), .. CollectionUses(root, name)]
                : [.. others],
            _ => [],
        };
    }

    /// <summary>
    /// Whether an expression is a path: it names a member or variable like a path, such as
    /// <c>Path.GetFullPath</c>, <c>file.Path</c> or <c>_root</c>; with <paramref name="follow"/>,
    /// also a variable whose assigned values, or a collection whose members or keys, are.
    /// </summary>
    private static bool NamesAPath(SyntaxNode root, SyntaxNode expression, bool follow)
    {
        if (expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Any(static name => PathName().IsMatch(name.Identifier.ValueText)))
        {
            return true;
        }
        string? variable = expression switch
        {
            IdentifierNameSyntax name => name.Identifier.ValueText,
            MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax name, Name.Identifier.ValueText: "Keys" or "Values" } => name.Identifier.ValueText,
            _ => null,
        };
        return follow && variable is not null
            && AssignedValues(root, variable).Concat(CollectionUses(root, variable)).Any(value => NamesAPath(root, value, follow: false));
    }

    private static IEnumerable<SyntaxNode> AssignedValues(SyntaxNode root, string name) =>
        root.DescendantNodes().SelectMany(node => node switch
        {
            VariableDeclaratorSyntax { Initializer.Value: var value } declarator when declarator.Identifier.ValueText == name => [value],
            AssignmentExpressionSyntax assignment when LastIdentifier(assignment.Left) == name => [assignment.Right],
            _ => Array.Empty<SyntaxNode>(),
        });

    /// <summary>What code adds to, looks up in or removes from the collection with this name.</summary>
    private static IEnumerable<SyntaxNode> CollectionUses(SyntaxNode root, string name) =>
        root.DescendantNodes().SelectMany(node => node switch
        {
            InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Add" or "TryAdd" or "Contains"
                or "ContainsKey" or "Remove" or "TryRemove" or "TryGetValue" or "UnionWith" or "SetEquals" or "GetValueOrDefault" or "GetOrAdd" } access } invocation
                when LastIdentifier(access.Expression) == name => invocation.ArgumentList.Arguments.Select(static argument => (SyntaxNode)argument.Expression),
            ElementAccessExpressionSyntax access when LastIdentifier(access.Expression) == name
                => access.ArgumentList.Arguments.Select(static argument => (SyntaxNode)argument.Expression),
            _ => [],
        });

    private static string? DeclaredName(SyntaxNode creation) => creation.Parent switch
    {
        EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator } => declarator.Identifier.ValueText,
        EqualsValueClauseSyntax { Parent: PropertyDeclarationSyntax property } => property.Identifier.ValueText,
        AssignmentExpressionSyntax assignment => LastIdentifier(assignment.Left),
        _ => null,
    };

    private static string EnclosingMember(SyntaxNode node) => node.Ancestors().Select(static ancestor => ancestor switch
    {
        MethodDeclarationSyntax method => method.Identifier.ValueText,
        ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
        PropertyDeclarationSyntax property => property.Identifier.ValueText,
        FieldDeclarationSyntax field => field.Declaration.Variables[0].Identifier.ValueText,
        _ => null,
    }).FirstOrDefault(static name => name is not null) ?? string.Empty;

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

    /// <summary>A name for a path, a directory or a file, such as <c>fullPath</c>, <c>_root</c> or <c>PathResolver</c>.</summary>
    [GeneratedRegex("^_?(\\w*(path|paths|dir|dirs|directory|directories|folder|folders|root|roots)|full|fullname|file|files|filename|path\\w*)$", RegexOptions.IgnoreCase)]
    private static partial Regex PathName();

    [GeneratedRegex("Load|Input|Certificate")]
    private static partial Regex InputSide();

    [GeneratedRegex("^(encrypt|encryptable|protect|protectable)\\w*ids$", RegexOptions.IgnoreCase)]
    private static partial Regex ProtectableListName();
}
