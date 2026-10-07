using Aspose.Cli.CodeHealth;

namespace Aspose.Cli.Tests.CodeHealth;

/// <summary>Pins how <see cref="SourceMetrics"/> names units and counts each metric.</summary>
public sealed class SourceMetricsTests
{
    [Fact]
    public void CyclomaticComplexity_CountsEachDecisionPointAndLeavesLocalFunctionsApart()
    {
        const string source = """
            class C
            {
                int M(int a, string? s, int[] xs)
                {
                    if (a > 0 && s != null) { }
                    else if (a < 0 || s is null) { }
                    int n = s?.Length ?? 0;
                    foreach (int x in xs) { }
                    while (a-- > 0) { }
                    switch (a) { case 1: case 2: break; default: break; }
                    int y = a switch { 1 => 1, > 5 and < 9 => 2, _ => 0 };
                    try { } catch (System.Exception) { }
                    System.Func<int, int> f = v => v > 0 ? v : -v;
                    return Local(a);

                    static int Local(int v) => v is 1 or 2 ? 1 : 0;
                }
            }
            """;

        Dictionary<string, int> complexity = Measure(source, member => member.Cyclomatic);

        // 1 + if, &&, else-if, ||, ?., ??, foreach, while, two case labels, two non-discard arms, and, catch, ?:
        Assert.Equal(16, complexity["a.cs::C.M(int, string?, int[])"]);
        Assert.Equal(3, complexity["a.cs::C.M(int, string?, int[])/Local(int)"]);
    }

    [Fact]
    public void CyclomaticComplexity_CountsAGuardedDiscardArmAndPatternLabels()
    {
        const string source = """
            class C
            {
                int M(object o, bool b) => o switch
                {
                    int => 1,
                    _ when b => 2,
                    _ => 3,
                };

                void N(object o)
                {
                    switch (o) { case int i when i > 0: break; case string: break; }
                    do { } while (o is null);
                    for (;;) { break; }
                    o ??= 1;
                }
            }
            """;

        Dictionary<string, int> complexity = Measure(source, member => member.Cyclomatic);

        Assert.Equal(3, complexity["a.cs::C.M(object, bool)"]);
        Assert.Equal(6, complexity["a.cs::C.N(object)"]);
    }

    [Fact]
    public void MethodLength_CountsBodyLinesHoldingTokensOnly()
    {
        const string source = """"
            class C
            {
                /// <summary>Not counted.</summary>
                void M()
                {
                    int a = 1;

                    // Not counted.
                    string b = $$"""
                        {{a}}
                        """;
                    /* Not counted. */
                    void Local()
                    {
                        a++;
                    }
                }
            }
            """";

        Dictionary<string, int> length = Measure(source, member => member.Lines);

        // {, int a, the three lines of the raw string, }
        Assert.Equal(6, length["a.cs::C.M()"]);
        Assert.Equal(3, length["a.cs::C.M()/Local()"]);
    }

    [Fact]
    public void ParameterCount_CoversMethodsConstructorsDelegatesIndexersAndPrimaryConstructors()
    {
        const string source = """
            delegate void D(int a, int b);
            record R(int A, int B, int C);
            class C
            {
                C(int a) { }
                int this[int i, string j] => i;
                void M(ref int a, out int b, in int c, params int[] d) { b = 0; }
                abstract void Abstract(int a, int b);
            }
            """;

        Dictionary<string, int> parameters = Measure(source, member => member.Parameters, member => member.Parameters > 0);

        Assert.Equal(
            new Dictionary<string, int>
            {
                ["a.cs::D(int, int)"] = 2,
                ["a.cs::R..ctor(int, int, int)"] = 3,
                ["a.cs::C..ctor(int)"] = 1,
                ["a.cs::C.this[int, string]"] = 2,
                ["a.cs::C.M(ref int, out int, in int, params int[])"] = 4,
            },
            parameters);
    }

    [Fact]
    public void Keys_NameEveryUnitStablyWithoutLineNumbers()
    {
        const string source = """
            namespace N.M;

            class Outer<T>
            {
                static Outer() { }
                ~Outer() { }
                static readonly int Field = 1, Other = 2;
                event System.Action? Changed = null;
                int Value { get => 1; set { } }
                int Arrow => 1;
                int Initialized { get; } = 2;
                string this[int i] { get => ""; }
                public static Outer<T> operator +(Outer<T> a, Outer<T> b) => a;
                public static implicit operator string(Outer<T> value) => "";
                System.Collections.Generic.Dictionary<string,int> Map(System.Collections.Generic.List< (int a,int b) > x) => [];
                void Twice()
                {
                    { void Local() { } }
                    { void Local() { } }
                }

                class Inner { void M() { } void M(int a) { } }
            }
            """;

        SourceMetrics.Parse("src/a.cs", source).Units.Select(unit => unit.Key).ToArray().AssertKeys(
            ".cctor()",
            "Finalize()",
            "Field=",
            "Other=",
            "Changed=",
            "Value.get",
            "Value.set",
            "Arrow.get",
            "Initialized=",
            "this[int]",
            "this[int].get",
            "operator +(Outer<T>, Outer<T>)",
            "implicit operator string(Outer<T>)",
            "Map(System.Collections.Generic.List<(int a, int b)>)",
            "Twice()",
            "Twice()/Local()",
            "Twice()/Local()#2",
            "Inner.M()",
            "Inner.M(int)");
    }

    [Fact]
    public void FileLength_CountsLinesHoldingAnythingButWhitespace()
    {
        ParsedSource source = SourceMetrics.Parse("a.cs", "// one\r\n\r\n   \r\nclass C\r\n{\r\n}\r\n");

        Assert.Equal(new FileMetrics("a.cs", 4, 0, 0, 1), SourceMetrics.MeasureFile(source, SourceMetrics.MeasureMembers(source)));
    }

    [Fact]
    public void Measurements_DoNotDependOnLineEndings()
    {
        const string source = "class C\n{\n    int M(int a)\n    {\n        return a > 0 ? 1 : 0;\n    }\n}\n";

        Assert.Equal(
            SourceMetrics.MeasureMembers(SourceMetrics.Parse("a.cs", source)),
            SourceMetrics.MeasureMembers(SourceMetrics.Parse("a.cs", source.ReplaceLineEndings("\r\n"))));
    }

    [Theory]
    [InlineData("src/A.g.cs", "class C { }", true)]
    [InlineData("src/A.Designer.cs", "class C { }", true)]
    [InlineData("src/A.cs", "// <auto-generated/>\nclass C { }", true)]
    [InlineData("src/A.cs", "// Written by hand.\nclass C { }", false)]
    public void IsGenerated_FollowsTheRoslynConventions(string path, string text, bool generated) =>
        Assert.Equal(generated, SourceMetrics.IsGenerated(SourceMetrics.Parse(path, text)));

    private static Dictionary<string, int> Measure(string source, Func<MemberMetrics, int> value, Func<MemberMetrics, bool>? include = null) =>
        SourceMetrics.MeasureMembers(SourceMetrics.Parse("a.cs", source))
            .Where(member => include?.Invoke(member) ?? member.HasBody)
            .ToDictionary(member => member.Key, value);
}

file static class KeyAssertions
{
    public static void AssertKeys(this string[] actual, params string[] members) =>
        Assert.Equal(members.Select(member => "src/a.cs::Outer<T>." + member), actual);
}
