using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>
/// Where Slides holds text is one enumeration, <c>SlidesTextUnits</c>: the commands whose object is
/// the text itself (search, replace_text and the text it leaves, the counts --verify compares,
/// extract of text and notes, the evaluation cut) read it, and never walk the slides for text
/// themselves. So the per-shape text accessors of <c>SlidesShapeText</c> (<c>TextFrames</c>,
/// <c>ShapeText</c>, <c>Notes</c>) and a slide's <c>NotesTextFrame</c> are reached only by the
/// enumeration and by the members listed here, which report or write one shape's structure,
/// layout or state. The rule reads these names only: text read straight from an engine
/// <c>TextFrame</c> is not caught.
/// </summary>
public sealed class SlidesTextUnitsOwnershipTests
{
    private const string Snapshot = "A snapshot holds the state of the objects an operation changes, to tell whether they changed.";

    // rule-exemptions: begin
    /// <summary>The members that read a shape's text for another purpose than finding, replacing or counting it, by file and member, with the reason.</summary>
    private static readonly Dictionary<string, string> Readers = new(StringComparer.Ordinal)
    {
        ["SlidesRead.cs: ProjectSlide(ISlide, int, string, bool, IReadOnlyList<IComment>, int)"] = "read reports each of a slide's shapes with its text, and the notes.",
        ["SlidesStateProjection.cs: Runs(IShape, int, bool)"] = "read --content full reports each run of a shape.",
        ["SlidesStateProjection.cs: StoredShape(IShape)"] = Snapshot,
        ["SlidesSnapshots.cs: Apply(SetNotesOp)"] = Snapshot,
        ["SlidesSnapshots.cs: Apply(SlidesReplaceTextOp)"] = Snapshot,
        ["SlidesSnapshots.cs: SlideState(ISlide)"] = Snapshot,
        ["SlidesSnapshots.cs: Whole(IShape)"] = Snapshot,
        ["SlidesEditVerifier.cs: Record(SlidesOp, AppliedOperation)"] = "--verify reads set_text and set_notes back from the one shape or slide they wrote.",
        ["SlidesMutationHandlers.Content.cs: Apply(SetNotesOp)"] = "set_notes writes the slide's notes frame.",
        ["SlidesReviewLayout.cs: Runs(IShape)"] = "review measures where a shape's text sits.",
        ["SlidesReviewLayout.cs: Shape(ISlide, IShape, int)"] = "review measures where a shape's text sits.",
        ["SlidesViewLayout.cs: Label(IShape)"] = "A view labels a shape by its text.",
        ["SlidesView.cs: Render(SlidesSession, string, ViewRenderRequest, IViewArtifactSink)"] = "A view reports a slide's notes.",
        ["SlidesInfo.cs: Run(SlidesSession, SlidesInspectRequest)"] = "inspect reports whether each slide has notes and how long they are.",
        ["SlidesInfo.cs: ProjectInfo(ISlide, int, IReadOnlyList<IComment>, bool)"] = "inspect reports whether each slide has notes and how long they are.",
        ["SlidesExport.cs: BuildConvertWarnings(LoadedPresentation, string, IReadOnlyList<int>, IReadOnlyList<int>, Warning?)"] = "A convert names the slides whose notes its export leaves out.",
        ["SlidesExport.cs: StateInheritedBullets(Presentation, IReadOnlyList<int>)"] = "The Markdown export states each paragraph's bullet in its private copy.",
    };
    // rule-exemptions: end

    [Fact]
    public void SlidesText_IsReadForItselfOnlyThroughTheEnumeration()
    {
        IEnumerable<string> violations = Reaches()
            .Where(static found => !Readers.ContainsKey(found.Key))
            .Select(static found => found.File.At(found.Node, found.Key));

        OwnershipSourceFiles.AssertNone(violations,
            "A Slides command reads the text it finds, replaces or counts from SlidesTextUnits; a member that reads a shape's text for its structure, layout or state is listed with its reason:");
    }

    [Fact]
    public void EveryListedReader_StillReadsText()
    {
        HashSet<string> found = [.. Reaches().Select(static found => found.Key)];
        string[] stale = [.. Readers.Keys.Where(key => !found.Contains(key))];
        Assert.True(stale.Length == 0, "Listed readers that no longer read text: " + string.Join(", ", stale));
    }

    /// <summary>
    /// Each place outside the enumeration and the accessors' own file that names an accessor (a
    /// call, or a method group such as <c>SelectMany(TextFrames)</c>) or reads a <c>NotesTextFrame</c>,
    /// keyed by its file and member.
    /// </summary>
    private static IEnumerable<(OwnedSourceFile File, SyntaxNode Node, string Key)> Reaches() =>
        OwnershipSourceFiles.Products
            .Where(static file => file.Product == "Slides" && Path.GetFileName(file.Path) is not ("SlidesShapeText.cs" or "SlidesTextUnits.cs"))
            .SelectMany(static file => file.Root.DescendantNodes()
                .Where(static node => node switch
                {
                    IdentifierNameSyntax name => name.Identifier.ValueText is "TextFrames" or "ShapeText" or "NotesTextFrame",
                    InvocationExpressionSyntax invocation => OwnershipSourceFiles.InvokedName(invocation) == "Notes",
                    _ => false,
                })
                .Select(node => (file, node, $"{Path.GetFileName(file.Path)}: {MemberName(node)}")));

    // The name of the method, property or field that holds a node; a local function or lambda counts as its enclosing member.
    private static string MemberName(SyntaxNode node) => node.Ancestors().FirstOrDefault(static ancestor =>
        ancestor is MethodDeclarationSyntax or PropertyDeclarationSyntax or FieldDeclarationSyntax or ConstructorDeclarationSyntax) switch
    {
        MethodDeclarationSyntax method => method.Identifier.ValueText + ParameterTypes(method.ParameterList),
        PropertyDeclarationSyntax property => property.Identifier.ValueText,
        FieldDeclarationSyntax field => field.Declaration.Variables[0].Identifier.ValueText,
        ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
        _ => "?",
    };

    // Overloads such as the Apply of each operation differ by their parameter types.
    private static string ParameterTypes(ParameterListSyntax parameters) =>
        "(" + string.Join(", ", parameters.Parameters.Select(static parameter => parameter.Type?.ToString())) + ")";
}
