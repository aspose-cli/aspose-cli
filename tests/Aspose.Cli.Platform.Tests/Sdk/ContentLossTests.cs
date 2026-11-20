using System.Reflection;
using Aspose.Cli.Sdk.Contracts;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

/// <summary>
/// The SDK builds the warning that names what an output left out from a closed set of tokens:
/// <c>ContentLoss</c> a <c>LOSSY_CONVERSION</c> with the <c>ContentKind</c> tokens in
/// <c>details.content</c>. A product picks a member of the set; a token outside it does not compile.
/// </summary>
public sealed class ContentLossTests
{
    private const string Message = "The pdf output leaves out 2 comment(s): 2 on worksheet 'Data'.";

    public static TheoryData<string> Kinds() => [.. ContentKind.All.Select(static kind => kind.Token)];

    [Theory]
    [MemberData(nameof(Kinds))]
    public void EveryKind_MakesALossyConversionThatNamesIt(string token)
    {
        ContentKind kind = Assert.Single(ContentKind.All, kind => kind.Token == token);

        Warning warning = ContentLoss.Warning([kind], Message, "sheet/Data", affectsCompleteness: true);

        Assert.Equal(WarningCodes.LossyConversion, warning.Code);
        Assert.Equal(Message, warning.Message);
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
        Warning warning = ContentLoss.Warning([ContentKind.SpeakerNotes], Message);

        Assert.Null(warning.Location);
        Assert.False(warning.AffectsCompleteness);
        Assert.Equal(["speaker-notes"], Content(warning));
    }

    /// <summary>A warning that names several kinds lists each once, in the order its message names them.</summary>
    [Fact]
    public void SeveralKinds_AreListedInTheOrderGiven()
    {
        Warning warning = ContentLoss.Warning([ContentKind.Charts, ContentKind.Pictures, ContentKind.Shapes], Message);

        Assert.Equal(["charts", "pictures", "shapes"], Content(warning));
    }

    /// <summary>A content loss names at least one kind, each once: no kind, or a kind twice, is refused.</summary>
    [Fact]
    public void ContentLoss_RefusesNoKindAndARepeatedKind()
    {
        Assert.Throws<ArgumentException>(() => ContentLoss.Warning([], Message));
        Assert.Throws<ArgumentException>(() => ContentLoss.Warning([ContentKind.Comments, ContentKind.Comments], Message));
    }

    /// <summary>
    /// The kinds are kebab-case plural nouns, each once, and the set lists every kind the type
    /// declares. Each has the sentence the schema publishes as its meaning.
    /// </summary>
    [Fact]
    public void TheSets_ListEveryDeclaredMemberAsADistinctKebabCaseToken()
    {
        Assert.Equal(Declared<ContentKind>().Select(static kind => kind.Token).Order(StringComparer.Ordinal),
            ContentKind.All.Select(static kind => kind.Token).Order(StringComparer.Ordinal));
        Assert.Equal(ContentKind.All.Count, ContentKind.All.Select(static kind => kind.Token).Distinct(StringComparer.Ordinal).Count());
        Assert.All(ContentKind.All, static kind => Assert.Matches("^[a-z]+(-[a-z]+)*s$", kind.Token));
        Assert.All(ContentKind.All, static kind => Assert.Matches(@"^[A-Z].*\.$", kind.Description));
    }

    /// <summary>
    /// A product picks a member of the set: a token written as a string, or a member it makes
    /// itself, does not compile, while the same call with a member of the set does.
    /// </summary>
    [Theory]
    [InlineData("""ContentLoss.Warning([ContentKind.Comments], "m", "h")""", true)]
    [InlineData("""ContentLoss.Warning(["comments"], "m", "h")""", false)]
    [InlineData("""ContentLoss.Warning([new ContentKind("comments")], "m", "h")""", false)]
    public void ATokenOutsideTheSet_DoesNotCompile(string call, bool compiles)
    {
        string source = $$"""
            using Aspose.Cli.Sdk.Contracts;

            internal static class Fixture
            {
                internal static object Make(BoundedOperation operation) => {{call}};
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
