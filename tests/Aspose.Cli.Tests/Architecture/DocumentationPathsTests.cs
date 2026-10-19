using System.Diagnostics;

namespace Aspose.Cli.Tests;

/// <summary>
/// scripts/test.ps1 and RepositoryInputTests split documentation from other repository files by
/// the one rule file, scripts/documentation-paths.json, and judge every path alike.
/// </summary>
public sealed class DocumentationPathsTests
{
    // Path -> whether it is documentation, as both sides judged it before they shared the rules.
    private static readonly (string Path, bool Documentation)[] Cases =
    [
        ("docs/guide/README.md", true),
        ("docs/simplify/plan.md", true),
        ("src/Aspose.Cli/README.MD", true),
        (".github/workflows/ci.yml", true),
        (".GitHub/CODEOWNERS", true),
        ("third-party/LICENSE", true),
        ("LICENSE.txt", true),
        ("docs/LICENSE-THIRD-PARTY.txt", true),
        ("license", true),
        ("eng/.gitignore", true),
        ("tests/Aspose.Cli.Tests/.gitattributes", true),
        (".editorconfig", false),
        ("scripts/test.ps1", false),
        ("eng/products.json", false),
        ("install.ps1", false),
        ("notes.md.txt", false),
        ("src/.github/x.yml", false),
        ("github/x.yml", false),
    ];

    [Fact]
    public void TheTestsJudgeEachPathAsBefore()
    {
        string[] wrong = [.. Cases.Where(static entry => DocumentationPaths.Contains(entry.Path) != entry.Documentation)
            .Select(static entry => $"{entry.Path}: expected {entry.Documentation}")];
        Assert.True(wrong.Length == 0, string.Join(Environment.NewLine, wrong));
    }

    [Fact]
    public async Task TheScriptJudgesEachPathAsBefore()
    {
        var start = new ProcessStartInfo(ToolPath.Require("pwsh"))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        string script = Path.Combine(RepositoryPaths.Root, "scripts", "documentation-paths.ps1");
        string paths = string.Join(',', Cases.Select(static entry => $"'{entry.Path}'"));
        foreach (string argument in (string[])["-NoProfile", "-NonInteractive", "-Command", $"& '{script}' -Path @({paths})"])
        {
            start.ArgumentList.Add(argument);
        }
        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await process.WaitForExitAsync(timeout.Token);
        Assert.True(process.ExitCode == 0, await error);

        string[] documentation = (await output).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(Cases.Where(static entry => entry.Documentation).Select(static entry => entry.Path), documentation);
    }
}
