using System.Diagnostics;
using System.Text.Json;
using Aspose.Cli.CodeHealth;
using Aspose.Cli.TestKit;

namespace Aspose.Cli.Tests.CodeHealth;

/// <summary>Pins the report: comparison kinds, totals, hotspots and reading a base revision from git.</summary>
public sealed class CodeHealthReportTests
{
    private const string Simple = "class A\n{\n    int M(bool a) { if (a) { return 1; } return 0; }\n    int Gone() => 1;\n}\n";
    private const string Branchy = "class A\n{\n    int M(bool a, bool b) { if (a) { if (b) { return 2; } } return 0; }\n    int Added() => 2;\n}\n";

    [Fact]
    public void Comparison_ClassifiesMembersAndFilesAndTotalsTheMove()
    {
        Snapshot before = Snapshot.Measure([("src/a.cs", Simple), ("src/old.cs", "class Old { void X() { while (true) { } } }\n")]);
        Snapshot after = Snapshot.Measure([("src/a.cs", Branchy.Replace("int M(bool a, bool b)", "int M(bool a)", StringComparison.Ordinal).Replace("(b)", "(a)", StringComparison.Ordinal))]);

        Comparison comparison = Comparison.Create(before, after);

        Assert.Equal(
            [
                ("src/a.cs::A.Added()", ChangeKind.New),
                ("src/a.cs::A.M(bool)", ChangeKind.Worse),
                ("src/old.cs::Old.X()", ChangeKind.Removed),
                ("src/a.cs::A.Gone()", ChangeKind.Removed),
            ],
            comparison.Members.Select(change => (change.Key, change.Kind)));
        MemberChange worse = comparison.Members.Single(change => change.Kind == ChangeKind.Worse);
        Assert.Equal((1, 3), (worse.BaseCognitive, worse.HeadCognitive));
        Assert.Equal([("src/a.cs", ChangeKind.Worse), ("src/old.cs", ChangeKind.Removed)], comparison.Files.Select(change => (change.Path, change.Kind)));
        Assert.Equal((2, 3, 3, 2), (comparison.Base.Cognitive, comparison.Head.Cognitive, comparison.Base.Members, comparison.Head.Members));
    }

    [Fact]
    public void Run_ComparesTheWorkingTreeWithABaseRevisionAndRanksHotspotsFromHistory()
    {
        using TempDirectory repository = new();
        Directory.CreateDirectory(repository.File("src"));
        File.WriteAllText(repository.File("src/a.cs"), Simple);
        Git(repository.Path, "init", "--quiet");
        Commit(repository.Path, "feat: add a");
        File.WriteAllText(repository.File("src/a.cs"), Branchy);
        Commit(repository.Path, "fix(cli): branch on b");
        File.WriteAllText(repository.File("src/b.cs"), "class B { void N() { } }\n");

        string measured = CodeHealthCommand.Run(CodeHealthCommand.Parse(["--json"]), repository.Path);
        string compared = CodeHealthCommand.Run(CodeHealthCommand.Parse(["--base", "HEAD~1", "--json"]), repository.Path);

        using JsonDocument report = JsonDocument.Parse(measured);
        JsonElement hotspot = Assert.Single(report.RootElement.GetProperty("hotspots").EnumerateArray());
        Assert.Equal("src/a.cs", hotspot.GetProperty("path").GetString());
        Assert.Equal((2, 1, 3, 6), (hotspot.GetProperty("commits").GetInt32(), hotspot.GetProperty("fixes").GetInt32(),
            hotspot.GetProperty("cognitive").GetInt32(), hotspot.GetProperty("score").GetInt32()));
        Assert.Equal("src/a.cs::A.M(bool, bool)", report.RootElement.GetProperty("mostComplexMembers")[0].GetProperty("key").GetString());

        using JsonDocument comparison = JsonDocument.Parse(compared);
        Assert.Equal(
            ["new src/a.cs::A.M(bool, bool)", "new src/a.cs::A.Added()", "new src/b.cs::B.N()", "removed src/a.cs::A.M(bool)", "removed src/a.cs::A.Gone()"],
            comparison.RootElement.GetProperty("members").EnumerateArray()
                .Select(change => change.GetProperty("kind").GetString() + " " + change.GetProperty("key").GetString()));
        Assert.Equal(Branchy, File.ReadAllText(repository.File("src/a.cs")));

        string markdown = CodeHealthCommand.Run(CodeHealthCommand.Parse(["--base", "HEAD~1"]), repository.Path);
        Assert.Contains("| cognitive | 1 | 3 | +2 |", markdown, StringComparison.Ordinal);
        Assert.Contains("## New members (3)", markdown, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--top")]
    [InlineData("--top", "x")]
    [InlineData("--frobnicate")]
    public void Parse_RejectsMistakes(params string[] args) =>
        Assert.Throws<ArgumentException>(() => CodeHealthCommand.Parse(args));

    private static void Commit(string directory, string subject)
    {
        Git(directory, "add", "--all");
        Git(directory, "-c", "user.name=Test", "-c", "user.email=test@example.com", "-c", "commit.gpgsign=false",
            "commit", "--quiet", "--no-verify", "-m", subject);
    }

    private static void Git(string directory, params string[] arguments)
    {
        ProcessStartInfo start = new("git") { WorkingDirectory = directory, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        using Process process = Process.Start(start)!;
        string error = process.StandardError.ReadToEnd();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"git {string.Join(' ', arguments)} failed: {error}");
    }
}
