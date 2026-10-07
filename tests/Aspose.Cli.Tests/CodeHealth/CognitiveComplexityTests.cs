using Aspose.Cli.CodeHealth;

namespace Aspose.Cli.Tests.CodeHealth;

/// <summary>Pins <see cref="SourceMetrics.CognitiveComplexity"/> against the SonarSource definition.</summary>
public sealed class CognitiveComplexityTests
{
    [Fact]
    public void NestedLoopsChargeTheirDepth_AsInTheSonarSumOfPrimesExample()
    {
        const string source = """
            class C
            {
                int SumOfPrimes(int max)
                {
                    int total = 0;
                    for (int i = 1; i <= max; ++i)      // +1
                    {
                        for (int j = 2; j < i; ++j)     // +2 (nesting 1)
                        {
                            if (i % j == 0)             // +3 (nesting 2)
                            {
                                goto Out;               // +1
                            }
                        }
                        total += i;
                        Out: ;
                    }
                    return total;
                }
            }
            """;

        Assert.Equal(7, Cognitive(source)["a.cs::C.SumOfPrimes(int)"]);
    }

    [Fact]
    public void AFlatSwitchScoresOne_WhereCyclomaticCountsEveryCase()
    {
        const string source = """
            class C
            {
                string Words(int number)
                {
                    switch (number)
                    {
                        case 1: return "one";
                        case 2: return "a couple";
                        case 3: return "a few";
                        default: return "lots";
                    }
                }

                string Arms(int number) => number switch { 1 => "one", 2 => "two", _ => "many" };
            }
            """;

        Dictionary<string, int> cognitive = Cognitive(source);

        Assert.Equal(1, cognitive["a.cs::C.Words(int)"]);
        Assert.Equal(1, cognitive["a.cs::C.Arms(int)"]);
        Assert.Equal(4, SourceMetrics.MeasureMembers(SourceMetrics.Parse("a.cs", source)).Single(member => member.Key == "a.cs::C.Words(int)").Cyclomatic);
    }

    [Fact]
    public void ElseIfAndElseAddOneAndEachRunOfALogicalOperatorAddsOne()
    {
        const string source = """
            class C
            {
                void M(bool a, bool b, bool c, object o)
                {
                    if (a && b && c) { }              // if +1, && run +1
                    else if (a || b && c) { }         // else if +1, || +1, && +1
                    else { }                          // else +1
                    if ((a && b) && c) { }            // if +1, one && run +1
                    if (o is int or long) { }         // if +1, or +1
                    if (a) { } else if (b) { } else if (c) { } // 1 + 1 + 1
                }
            }
            """;

        Assert.Equal(13, Cognitive(source)["a.cs::C.M(bool, bool, bool, object)"]);
    }

    [Fact]
    public void GuardClausesScoreOneEach_AndLambdasNestTheirContent()
    {
        const string source = """
            class C
            {
                void Guards(string? a, string? b, string? c)
                {
                    if (a is null) { return; }        // +1
                    if (b is null) { return; }        // +1
                    if (c is null) { return; }        // +1
                    string d = a ?? b?.Trim() ?? c;   // ?? and ?. add nothing
                }

                bool Any(int[] xs) => System.Linq.Enumerable.Any(xs, x => x > 0 ? true : false); // ?: +1 + nesting 1

                void Nested(int[] xs)
                {
                    try
                    {
                        foreach (int x in xs)             // +1
                        {
                            while (x > 0) { }             // +2
                        }
                    }
                    catch (System.Exception)              // +1
                    {
                        if (xs.Length > 0) { }            // +2
                    }
                    int Local(int v) => v > 0 ? v : -v;   // a unit of its own
                }
            }
            """;

        Dictionary<string, int> cognitive = Cognitive(source);

        Assert.Equal(3, cognitive["a.cs::C.Guards(string?, string?, string?)"]);
        Assert.Equal(2, cognitive["a.cs::C.Any(int[])"]);
        Assert.Equal(6, cognitive["a.cs::C.Nested(int[])"]);
        Assert.Equal(1, cognitive["a.cs::C.Nested(int[])/Local(int)"]);
    }

    private static Dictionary<string, int> Cognitive(string source) =>
        SourceMetrics.MeasureMembers(SourceMetrics.Parse("a.cs", source))
            .Where(member => member.HasBody)
            .ToDictionary(member => member.Key, member => member.Cognitive);
}
