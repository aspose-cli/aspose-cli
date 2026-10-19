using System.Diagnostics;

namespace Aspose.Cli.Tests;

/// <summary>
/// scripts/require-approval.ps1, which the pull request check runs with the owner's
/// 'quality-exception' label: a change that adds or widens a known violation or a rule exemption,
/// or drops a generated case, fails without the label and passes with it; removing any of them
/// needs no label; a malformed or unowned rule-exemptions block fails whatever the label.
/// </summary>
public sealed class RequireApprovalTests
{
    private const string Script = "scripts/require-approval.ps1";
    private const string Owners = ".github/CODEOWNERS";
    private const string Rules = "tests/Aspose.Cli.Invariants.Tests/Rules/Exemptions.cs";
    private const string Elsewhere = "tests/Aspose.Cli.Platform.Tests/Other.cs";
    private const string Product = "src/Product.cs";
    private const string Renamed = "tests/Aspose.Cli.Invariants.Tests/Rules/RoundTripExemptions.cs";
    private const string KnownViolations = "tests/Aspose.Cli.Invariants.Tests/known-violations.json";
    private const string GeneratedCases = "tests/Aspose.Cli.Invariants.Tests/generated-cases.txt";

    private const string Comment = """    ["cells add_comment"] = "Adds a note each time.",""";
    private const string Encrypt = """    ["pdf encrypt_document"] = "Encrypting again re-keys the file.",""";
    private const string Toc = """    ["words insert_toc"] = "Adds a table of contents each time.",""";

    private static readonly string RulesFile = string.Join('\n',
        "internal static class Exemptions",
        "{",
        "    // rule-exemptions: begin",
        "    /// <summary>The operations C-R39 cannot judge, with the reason.</summary>",
        "    internal static readonly Dictionary<string, string> RoundTripExempt = new(StringComparer.Ordinal)",
        "    {",
        "    " + Comment,
        "    " + Encrypt,
        "    };",
        "    // rule-exemptions: end",
        "}",
        "");

    // A new entry between an end and a begin marker inserted in the block.
    private static readonly string CutOut = RulesFile.Replace(Encrypt + "\n", Encrypt + "\n    // rule-exemptions: end\n    private const string Added = \"slides insert_chart\";\n    // rule-exemptions: begin\n", StringComparison.Ordinal);

    private static readonly Dictionary<string, string?> BaseFiles = new(StringComparer.Ordinal)
    {
        [Owners] = "# The gates.\n/tests/ @owner\n",
        [Rules] = RulesFile,
        [Elsewhere] = "internal static class Other\n{\n" + Toc + "\n}\n",
        [Product] = "internal static class Product\n{\n}\n",
        [KnownViolations] = """{"violations":[{"case":"c1","invariant":"C-R01","actual":"one","modes":["licensed"]}]}""",
        [GeneratedCases] = "c1\nc2\n",
    };

    // Name -> the files the change rewrites (null deletes), whether it passes without and with the
    // label, and a text the output holds.
    private static readonly Dictionary<string, (Dictionary<string, string?> Files, bool Without, bool With, string Output)> Changes =
        new(StringComparer.Ordinal)
        {
            ["nothing"] = (new(), true, true, "No known violation or rule exemption was added or widened"),
            ["an exemption added"] = (
                new() { [Rules] = RulesFile.Replace(Encrypt, Encrypt + "\n        [\"slides insert_chart\"] = \"Adds a chart each time.\",", StringComparison.Ordinal) },
                false, true, $"{Rules}:9 adds a rule exemption: [\"slides insert_chart\"]"),
            ["an exemption edited"] = (
                new() { [Rules] = RulesFile.Replace("re-keys the file.", "re-keys or reorders the file.", StringComparison.Ordinal) },
                false, true, "adds a rule exemption: [\"pdf encrypt_document\"] = \"Encrypting again re-keys or reorders the file.\","),
            ["an exemption deleted"] = (
                new() { [Rules] = RulesFile.Replace("    " + Encrypt + "\n", string.Empty, StringComparison.Ordinal) },
                true, true, "No known violation or rule exemption was added or widened"),
            ["an exemption moved to another owned file"] = (
                new()
                {
                    [Rules] = RulesFile.Replace("    " + Encrypt + "\n", string.Empty, StringComparison.Ordinal),
                    [Elsewhere] = "internal static class Other\n{\n    // rule-exemptions: begin\n" + Encrypt + "\n    // rule-exemptions: end\n}\n",
                },
                true, true, "No known violation or rule exemption was added or widened"),
            ["a block around exemptions the change leaves alone"] = (
                new() { [Elsewhere] = "internal static class Other\n{\n    // rule-exemptions: begin\n" + Toc + "\n    // rule-exemptions: end\n}\n" },
                true, true, "No known violation or rule exemption was added or widened"),
            ["the exemptions reindented"] = (
                new() { [Rules] = RulesFile.Replace("    " + Comment, "  " + Comment, StringComparison.Ordinal) },
                true, true, "No known violation or rule exemption was added or widened"),
            ["an exemption collection declared outside a block"] = (
                new() { [Elsewhere] = "internal static class Other\n{\n    private static readonly (string Command, string Reason)[] Waived = [(\"cells merge\", \"why\")];\n}\n" },
                false, true, $"{Elsewhere}:3 declares Waived, which looks like a rule exemption outside a rule-exemptions block"),
            ["the markers deleted and an exemption added"] = (
                new()
                {
                    [Rules] = RulesFile.Replace("    // rule-exemptions: begin\n", string.Empty, StringComparison.Ordinal)
                        .Replace("    // rule-exemptions: end\n", string.Empty, StringComparison.Ordinal)
                        .Replace(Encrypt, Encrypt + "\n        [\"slides insert_chart\"] = \"Adds a chart each time.\",", StringComparison.Ordinal),
                },
                false, true, $"{Rules}:3 removes a rule-exemptions marker: // rule-exemptions: begin"),
            ["the begin marker moved below the exemptions and one added"] = (
                new()
                {
                    [Rules] = RulesFile.Replace("    // rule-exemptions: begin\n", string.Empty, StringComparison.Ordinal)
                        .Replace("    };\n", "    };\n    // rule-exemptions: begin\n    private const string Added = \"slides insert_chart\";\n", StringComparison.Ordinal),
                },
                false, true, $"{Rules}:3 removes a rule-exemptions marker: // rule-exemptions: begin"),
            ["an exemption carved out of its block"] = (
                new() { [Rules] = RulesFile.Replace("    " + Encrypt + "\n", "    // rule-exemptions: end\n    " + Encrypt + "\n    // rule-exemptions: begin\n", StringComparison.Ordinal) },
                false, true, $"{Rules}:8 adds a rule-exemptions marker to a file that has them: // rule-exemptions: end"),
            ["a new entry cut out of a block by an inserted end and begin"] = (
                new() { [Rules] = CutOut },
                false, true, $"{Rules}:9 adds a rule-exemptions marker to a file that has them: // rule-exemptions: end"),
            ["a marker moved alone"] = (
                new() { [Rules] = RulesFile.Replace("    };\n    // rule-exemptions: end\n", "    };\n", StringComparison.Ordinal).Replace("    " + Comment, "    // rule-exemptions: end\n    " + Comment, StringComparison.Ordinal) },
                false, true, $"{Rules}:10 removes a rule-exemptions marker: // rule-exemptions: end"),
            ["a file with exemptions renamed"] = (
                new() { [Rules] = null, [Renamed] = RulesFile },
                true, true, "No known violation or rule exemption was added or widened"),
            ["a file with exemptions renamed while a new entry is cut out of its block"] = (
                new() { [Rules] = null, [Renamed] = CutOut },
                false, true, $"{Renamed}:9 adds a rule-exemptions marker to a file that has them: // rule-exemptions: end"),
            ["an exemption moved out of its block"] = (
                new()
                {
                    [Rules] = RulesFile.Replace("    " + Encrypt + "\n", string.Empty, StringComparison.Ordinal),
                    [Elsewhere] = "internal static class Other\n{\n" + Toc + "\n" + Encrypt + "\n}\n",
                },
                false, true, $"{Elsewhere}:4 moves a rule exemption out of its block: [\"pdf encrypt_document\"]"),
            ["a block in a file without an owner"] = (
                new()
                {
                    [Rules] = RulesFile.Replace("    " + Encrypt + "\n", string.Empty, StringComparison.Ordinal),
                    [Product] = "internal static class Product\n{\n    // rule-exemptions: begin\n" + Encrypt + "\n    // rule-exemptions: end\n}\n",
                },
                false, false, $"{Product} holds rule exemptions but has no owner in .github/CODEOWNERS."),
            ["a block never closed"] = (
                new() { [Rules] = RulesFile.Replace("    // rule-exemptions: end\n", string.Empty, StringComparison.Ordinal) },
                false, false, $"{Rules}:3 opens a rule-exemptions block that is never closed."),
            ["a known violation added"] = (
                new() { [KnownViolations] = """{"violations":[{"case":"c1","invariant":"C-R01","actual":"one","modes":["licensed"]},{"case":"c2","invariant":"C-R02","actual":"two"}]}""" },
                false, true, $"{KnownViolations} added c2 / C-R02"),
            ["a known violation added that differs only in case"] = (
                new() { [KnownViolations] = """{"violations":[{"case":"c1","invariant":"C-R01","actual":"one","modes":["licensed"]},{"case":"C1","invariant":"C-R01","actual":"one","modes":["licensed"]}]}""" },
                false, true, $"{KnownViolations} added C1 / C-R01"),
            ["a known violation widened to a mode that differs only in case"] = (
                new() { [KnownViolations] = """{"violations":[{"case":"c1","invariant":"C-R01","actual":"one","modes":["Licensed"]}]}""" },
                false, true, $"{KnownViolations} widened the modes of c1 / C-R01"),
            ["a known violation widened"] = (
                new() { [KnownViolations] = """{"violations":[{"case":"c1","invariant":"C-R01","actual":"one"}]}""" },
                false, true, $"{KnownViolations} widened the modes of c1 / C-R01"),
            ["a known violation removed"] = (
                new() { [KnownViolations] = """{"violations":[]}""" },
                true, true, "No known violation or rule exemption was added or widened"),
            ["a known violation reworded"] = (
                new() { [KnownViolations] = """{"violations":[{"case":"c1","invariant":"C-R01","actual":"first","modes":["licensed"]}]}""" },
                true, true, $"::notice::{KnownViolations} reworded c1 / C-R01: first"),
            ["a generated case dropped"] = (
                new() { [GeneratedCases] = "c1\n" },
                false, true, $"{GeneratedCases} dropped c2"),
            ["a generated case added"] = (
                new() { [GeneratedCases] = "c1\nc2\nc3\n" },
                true, true, "No known violation or rule exemption was added or widened"),
        };

    public static TheoryData<string> ChangeNames => [.. Changes.Keys];

    [Theory]
    [MemberData(nameof(ChangeNames))]
    public async Task EachChangeNeedsTheApprovalItLoosensTheGatesBy(string name)
    {
        (Dictionary<string, string?> files, bool without, bool with, string expected) = Changes[name];
        using TempDirectory repository = new();
        Write(repository, BaseFiles);
        Git(repository.Path, "init", "--quiet");
        Commit(repository.Path, "test: base");
        Git(repository.Path, "tag", "base");
        Write(repository, files);
        Commit(repository.Path, "test: change");

        foreach ((bool approved, bool passes) in new[] { (false, without), (true, with) })
        {
            string[] arguments = ["-Base", "base", "-Repository", repository.Path];
            (int exitCode, string output) = await RunScript(approved ? [.. arguments, "-Approved"] : arguments);
            Assert.True((exitCode == 0) == passes, $"{name}, approved {approved}: exit code {exitCode}\n{output}");
            Assert.Contains(expected, output, StringComparison.Ordinal);
            if (!passes && !approved && with)
            {
                Assert.Contains("'quality-exception' label", output, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>The repository's own rule-exemptions blocks are well formed and owned.</summary>
    [Fact]
    public async Task TheRepositoryExemptionsAreWellFormedAndOwned()
    {
        (int exitCode, string output) = await RunScript("-Base", "HEAD", "-Repository", RepositoryPaths.Root);
        Assert.True(exitCode == 0, output);
    }

    [Fact]
    public void ThePullRequestCheckRunsTheScriptWithTheLabel()
    {
        string workflow = File.ReadAllText(Path.Combine(RepositoryPaths.Root, ".github", "workflows", "pull-request.yml")).ReplaceLineEndings("\n");
        Assert.Contains("APPROVED: ${{ contains(github.event.pull_request.labels.*.name, 'quality-exception') }}\n", workflow, StringComparison.Ordinal);
        Assert.Contains("run: ./" + Script + " -Base \"origin/$env:GITHUB_BASE_REF\" -Head $env:HEAD -Approved:($env:APPROVED -eq 'true')\n", workflow, StringComparison.Ordinal);
    }

    private static void Write(TempDirectory repository, Dictionary<string, string?> files)
    {
        foreach ((string path, string? content) in files)
        {
            string full = repository.File(path);
            if (content is null)
            {
                File.Delete(full);
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }
    }

    private static void Commit(string directory, string subject)
    {
        Git(directory, "add", "--all");
        Git(directory, "-c", "user.name=Test", "-c", "user.email=test@example.com", "-c", "commit.gpgsign=false",
            "commit", "--quiet", "--no-verify", "--allow-empty", "-m", subject);
    }

    private static void Git(string directory, params string[] arguments)
    {
        ProcessStartInfo start = new(ToolPath.Require("git")) { WorkingDirectory = directory, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (string argument in (string[])["-c", "core.autocrlf=false", .. arguments])
        {
            start.ArgumentList.Add(argument);
        }
        using Process process = Process.Start(start)!;
        string error = process.StandardError.ReadToEnd();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"git {string.Join(' ', arguments)} failed: {error}");
    }

    private static async Task<(int ExitCode, string Output)> RunScript(params string[] arguments)
    {
        var start = new ProcessStartInfo(ToolPath.Require("pwsh"))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in (string[])["-NoProfile", "-NonInteractive", "-File", Path.Combine(RepositoryPaths.Root, Script), .. arguments])
        {
            start.ArgumentList.Add(argument);
        }
        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await process.WaitForExitAsync(timeout.Token);
        return (process.ExitCode, await output + await error);
    }
}
