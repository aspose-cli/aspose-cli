using System.Globalization;

using Aspose.Cli.CodeHealth;

namespace Aspose.Cli.Tests.CodeHealth;

/// <summary>Pins what <see cref="CloneDetector"/> reports as a clone and how it keys it.</summary>
public sealed class CloneDetectorTests
{
    [Fact]
    public void Detect_ReportsAMaximalCloneAcrossFilesUnderBothMembers()
    {
        // ( ) { int total = 0 ; } + 20 * (total += n ;) + return total ; } } = 2 + 1 + 5 + 80 + 3 + 2
        IReadOnlyList<Clone> clones = Detect(
            ("src/a.cs", Method("A", "Sum", statements: 20)),
            ("src/b.cs", Method("B", "Total", statements: 20)));

        Clone clone = Assert.Single(clones);
        Assert.Equal("src/a.cs::A.Sum() <-> src/b.cs::B.Total()", clone.Key);
        Assert.Equal(93, clone.Tokens);
    }

    [Fact]
    public void Detect_IgnoresRunsShorterThanTheMinimum()
    {
        // 2 + 1 + 5 + 40 + 3 + 2 = 53 tokens
        Assert.Empty(Detect(
            ("src/a.cs", Method("A", "Sum", statements: 10)),
            ("src/b.cs", Method("B", "Total", statements: 10))));
    }

    [Fact]
    public void Detect_FindsNonOverlappingClonesWithinOneFile()
    {
        string body = Body(statements: 20);
        string source = $"class A\n{{\n    int Sum()\n    {{\n{body}    }}\n\n    int Total()\n    {{\n{body}    }}\n}}\n";

        Clone clone = Assert.Single(Detect(("src/a.cs", source)));

        // ( ) { body } = 2 + 1 + 88 + 1; the next tokens are int Total versus }
        Assert.Equal("src/a.cs::A.Sum() <-> src/a.cs::A.Total()", clone.Key);
        Assert.Equal(92, clone.Tokens);
    }

    [Fact]
    public void Detect_IgnoresSharedUsingDirectivesAndTrivia()
    {
        string usings = string.Concat(Enumerable.Range(0, 15).Select(index => $"using System.Collections.Generic{index};\n"));

        Assert.Empty(Detect(
            ("src/a.cs", usings + "// Shared comment that is long enough to matter if comments counted.\nclass A { }\n"),
            ("src/b.cs", usings + "// Shared comment that is long enough to matter if comments counted.\nclass B { }\n")));
    }

    [Fact]
    public void Detect_ComparesIdentifiersExactly()
    {
        Assert.Empty(Detect(
            ("src/a.cs", Method("A", "Sum", statements: 20, variable: "total")),
            ("src/b.cs", Method("B", "Total", statements: 20, variable: "sum"))));
    }

    [Fact]
    public void Detect_LocatesAClonedTypeMemberByItsType()
    {
        string fields = string.Concat(Enumerable.Range(0, 25).Select(index => $"    int f{index};\n"));

        Clone clone = Assert.Single(Detect(
            ("src/a.cs", $"class A\n{{\n{fields}}}\n"),
            ("src/b.cs", $"class B\n{{\n{fields}}}\n")));

        Assert.Equal("src/a.cs::A <-> src/b.cs::B", clone.Key);
        // { + 25 * (int fN ;) + }
        Assert.Equal(1 + 75 + 1, clone.Tokens);
    }

    private static IReadOnlyList<Clone> Detect(params (string Path, string Text)[] files) =>
        CloneDetector.Detect(files.Select(file => SourceMetrics.Parse(file.Path, file.Text)).ToList());

    private static string Method(string type, string name, int statements, string variable = "total") =>
        $"class {type}\n{{\n    int {name}()\n    {{\n{Body(statements, variable)}    }}\n}}\n";

    private static string Body(int statements, string variable = "total") =>
        $"        int {variable} = 0;\n"
        + string.Concat(Enumerable.Range(1, statements).Select(index =>
            $"        {variable} += {index.ToString(CultureInfo.InvariantCulture)};\n"))
        + $"        return {variable};\n";
}
