using System.Reflection;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Contracts;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

/// <summary>
/// The SDK builds the warnings that name what an output or an operation left behind from closed
/// sets of tokens: <c>ContentLoss</c> a <c>LOSSY_CONVERSION</c> with the <c>ContentKind</c> tokens
/// in <c>details.content</c>, and <c>UnreplacedText</c> a <c>TEXT_NOT_REPLACED</c> with the
/// <c>TextPart</c> token in <c>details.part</c>. A product picks a member of a set; a token outside
/// it does not compile.
/// </summary>
public sealed class ContentLossTests
{
    private const string Message = "The pdf output leaves out 2 comment(s): 2 on worksheet 'Data'.";
    private const string Hint = "Read the comments with 'aspose-cli cells inspect <file> --detail comments'.";

    public static TheoryData<string> Kinds() => [.. ContentKind.All.Select(static kind => kind.Token)];

    public static TheoryData<string> Parts() => [.. TextPart.All.Select(static part => part.Token)];

    [Theory]
    [MemberData(nameof(Kinds))]
    public void EveryKind_MakesALossyConversionThatNamesIt(string token)
    {
        ContentKind kind = Assert.Single(ContentKind.All, kind => kind.Token == token);

        Warning warning = ContentLoss.Warning([kind], Message, Hint, "sheet/Data", affectsCompleteness: true);

        Assert.Equal(WarningCodes.LossyConversion, warning.Code);
        Assert.Equal(Message, warning.Message);
        Assert.Equal(Hint, warning.Hint);
        Assert.Equal("sheet/Data", warning.Location);
        Assert.True(warning.AffectsCompleteness);
        Assert.Equal([token], Content(warning));
        Assert.Equal(["content"], warning.Details!.Select(static member => member.Key));
    }

    /// <summary>
    /// The location and completeness are optional, as on any warning: without them the warning
    /// has no location and does not affect completeness.
    /// </summary>
    [Fact]
    public void ContentLoss_WithoutLocation_LeavesItOutAndKeepsTheOutputComplete()
    {
        Warning warning = ContentLoss.Warning([ContentKind.SpeakerNotes], Message, Hint);

        Assert.Null(warning.Location);
        Assert.False(warning.AffectsCompleteness);
        Assert.Equal(["speaker-notes"], Content(warning));
    }

    /// <summary>A warning that names several kinds lists each once, in the order its message names them.</summary>
    [Fact]
    public void SeveralKinds_AreListedInTheOrderGiven()
    {
        Warning warning = ContentLoss.Warning([ContentKind.Charts, ContentKind.Pictures, ContentKind.Shapes], Message, Hint);

        Assert.Equal(["charts", "pictures", "shapes"], Content(warning));
    }

    /// <summary>A content loss names at least one kind, each once: no kind, or a kind twice, is refused.</summary>
    [Fact]
    public void ContentLoss_RefusesNoKindAndARepeatedKind()
    {
        Assert.Throws<ArgumentException>(() => ContentLoss.Warning([], Message, Hint));
        Assert.Throws<ArgumentException>(() => ContentLoss.Warning([ContentKind.Comments, ContentKind.Comments], Message, Hint));
    }

    /// <summary>
    /// The kinds are kebab-case plural nouns, each once, and the set lists every kind the type
    /// declares; the parts are kebab-case, each once, and the set lists every part. Each has the
    /// sentence the schema publishes as its meaning.
    /// </summary>
    [Fact]
    public void TheSets_ListEveryDeclaredMemberAsADistinctKebabCaseToken()
    {
        Assert.Equal(Declared<ContentKind>().Select(static kind => kind.Token).Order(StringComparer.Ordinal),
            ContentKind.All.Select(static kind => kind.Token).Order(StringComparer.Ordinal));
        Assert.Equal(ContentKind.All.Count, ContentKind.All.Select(static kind => kind.Token).Distinct(StringComparer.Ordinal).Count());
        Assert.All(ContentKind.All, static kind => Assert.Matches("^[a-z]+(-[a-z]+)*s$", kind.Token));
        Assert.All(ContentKind.All, static kind => Assert.Matches(@"^[A-Z].*\.$", kind.Description));

        Assert.Equal(Declared<TextPart>().Select(static part => part.Token).Order(StringComparer.Ordinal),
            TextPart.All.Select(static part => part.Token).Order(StringComparer.Ordinal));
        Assert.Equal(TextPart.All.Count, TextPart.All.Select(static part => part.Token).Distinct(StringComparer.Ordinal).Count());
        Assert.All(TextPart.All, static part => Assert.Matches("^[a-z]+(-[a-z]+)*$", part.Token));
        Assert.All(TextPart.All, static part => Assert.Matches(@"^[A-Z].*\.$", part.Description));
    }

    [Theory]
    [MemberData(nameof(Parts))]
    public void EveryPart_MakesATextNotReplacedThatNamesItAndTheOperation(string token)
    {
        TextPart part = Assert.Single(TextPart.All, part => part.Token == token);

        Warning warning = UnreplacedText.Warning(part, new Operation { Id = "rename" }, "replace_text",
            "Operation rename (replace_text) found a match in the comment of cell B2 and left it as it was.",
            "Set the new text of the comment with set_comment.", "comment/Data!B2", docs: "cells/editing");

        Assert.Equal(WarningCodes.TextNotReplaced, warning.Code);
        Assert.Equal("TEXT_NOT_REPLACED", warning.Code.Name);
        Assert.Equal("comment/Data!B2", warning.Location);
        Assert.Equal("Set the new text of the comment with set_comment.", warning.Hint);
        Assert.Equal("cells/editing", warning.Docs);
        Assert.False(warning.AffectsCompleteness);
        Assert.True(JsonNode.DeepEquals(new JsonObject { ["id"] = "rename", ["op"] = "replace_text", ["part"] = token }, warning.Details),
            warning.Details?.ToJsonString());
        Assert.Contains(WarningCodes.TextNotReplaced, WarningCodes.All);
    }

    /// <summary>A warning about an operation needs the id the batch gave it.</summary>
    [Fact]
    public void UnreplacedText_RefusesAnOperationWithoutAnId() =>
        Assert.Throws<ArgumentException>(() => UnreplacedText.Warning(TextPart.Comment, new Operation(), "replace_text", "m", "h", "comment/Data!B2"));

    /// <summary>
    /// A product picks a member of a set: a token written as a string, or a member it makes
    /// itself, does not compile, while the same call with a member of the set does.
    /// </summary>
    [Theory]
    [InlineData("""ContentLoss.Warning([ContentKind.Comments], "m", "h")""", true)]
    [InlineData("""ContentLoss.Warning(["comments"], "m", "h")""", false)]
    [InlineData("""ContentLoss.Warning([new ContentKind("comments")], "m", "h")""", false)]
    [InlineData("""UnreplacedText.Warning(TextPart.Comment, operation, "replace_text", "m", "h", "comment/Data!B2")""", true)]
    [InlineData("""UnreplacedText.Warning("comment", operation, "replace_text", "m", "h", "comment/Data!B2")""", false)]
    [InlineData("""UnreplacedText.Warning(new TextPart("comment"), operation, "replace_text", "m", "h", "comment/Data!B2")""", false)]
    public void ATokenOutsideTheSet_DoesNotCompile(string call, bool compiles)
    {
        string source = $$"""
            using Aspose.Cli.Sdk.Contracts;

            internal static class Fixture
            {
                internal static Warning Make(BoundedOperation operation) => {{call}};
            }
            """;
        CSharpCompilation compilation = CSharpCompilation.Create("Fixture",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))],
            References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        string[] errors = [.. compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(static diagnostic => diagnostic.ToString())];

        Assert.True(compiles == (errors.Length == 0), $"{call}: {string.Join(Environment.NewLine, errors)}");
    }

    /// <summary>The framework assemblies and the SDK, which the fixture compiles against.</summary>
    private static readonly Lazy<MetadataReference[]> References = new(static () =>
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Where(static path => !path.StartsWith(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase))
            .Select(static path => MetadataReference.CreateFromFile(path)),
        MetadataReference.CreateFromFile(typeof(Warning).Assembly.Location),
    ]);

    private static string[] Content(Warning warning) =>
        [.. warning.Details!["content"]!.AsArray().Select(static token => token!.GetValue<string>())];

    /// <summary>The members a set type declares: its public static properties of its own type.</summary>
    private static IEnumerable<T> Declared<T>() =>
        typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(static property => property.PropertyType == typeof(T))
            .Select(static property => (T)property.GetValue(null)!);

    private sealed record Operation : BoundedOperation;
}
