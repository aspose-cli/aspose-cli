using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.TestKit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>
/// Every process exit code the CLI returns is a named <see cref="ExitCode"/> member, including
/// 130, the code of a command its caller cancelled, so no exit code is a bare number in the
/// sources and the platform Skill's exit-code table lists every one.
/// </summary>
public sealed partial class ExitCodeDeclarationTests
{
    /// <summary>The conventional exit code of a process ended by Ctrl+C.</summary>
    private const int Cancelled = 130;

    [Fact]
    public void Cancellation_IsANamedExitCode()
    {
        Assert.True(Enum.GetValues<ExitCode>().Any(static code => (int)code == Cancelled),
            $"{nameof(ExitCode)} has no member for {Cancelled}, the exit code of a cancelled command; its members: "
            + string.Join(", ", Enum.GetValues<ExitCode>().Select(static code => $"{code} = {(int)code}")));
    }

    [Fact]
    public void Sources_NeverWriteTheCancelledExitCodeAsANumber()
    {
        IEnumerable<string> literals = OwnershipSourceFiles.All.SelectMany(static file => file.Root.DescendantTokens()
            .Where(static token => token.IsKind(SyntaxKind.NumericLiteralToken) && token.Value is int value && value == Cancelled
                && token.Parent?.Parent?.Parent is not EnumMemberDeclarationSyntax)
            .Select(token => file.At(token.Parent!, token.Parent!.Parent?.ToString().Split('\n')[0].Trim() ?? token.Text)));

        OwnershipSourceFiles.AssertNone(literals,
            $"The exit code {Cancelled} is read from its {nameof(ExitCode)} member, never written as a number:");
    }

    [Fact]
    public void PlatformSkill_ListsEveryExitCode()
    {
        string path = Path.Combine(RepositoryPaths.Root, "src", "Aspose.Cli.Host", "Skills", "Platform", "references", "troubleshooting.md");
        string text = File.ReadAllText(path).ReplaceLineEndings("\n");
        int start = text.IndexOf("## Exit codes", StringComparison.Ordinal);
        Assert.True(start >= 0, $"{path} has no '## Exit codes' section.");
        int end = text.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
        string section = end < 0 ? text[start..] : text[start..end];
        HashSet<int> listed = [.. TableRow().Matches(section).Select(static row => int.Parse(row.Groups["exit"].Value, System.Globalization.CultureInfo.InvariantCulture))];

        int[] missing = [.. Enum.GetValues<ExitCode>().Select(static code => (int)code).Where(code => !listed.Contains(code)).Order()];
        int[] unknown = [.. listed.Where(static code => !Enum.GetValues<ExitCode>().Any(member => (int)member == code)).Order()];
        Assert.True(missing.Length == 0 && unknown.Length == 0,
            $"The exit-code table of the platform Skill ({Path.GetRelativePath(RepositoryPaths.Root, path)}) lists exactly the {nameof(ExitCode)} members; "
            + $"missing: [{string.Join(", ", missing)}], not a member: [{string.Join(", ", unknown)}].");
    }

    /// <summary>
    /// Ctrl+C ends a command with 130 only under <c>--timeout</c>, where a supervisor runs it in a
    /// worker; otherwise the process ends as the operating system ends it. The table says so.
    /// </summary>
    [Fact]
    public void PlatformSkill_SaysWhenCancellationReturns130()
    {
        string path = Path.Combine(RepositoryPaths.Root, "src", "Aspose.Cli.Host", "Skills", "Platform", "references", "troubleshooting.md");
        string[] rows =
        [
            .. File.ReadAllText(path).ReplaceLineEndings("\n").Split('\n')
                .Where(static line => TableRow().Match(line) is { Success: true } row && row.Groups["exit"].Value == $"{Cancelled}"),
        ];

        string row = Assert.Single(rows);
        Assert.True(row.Contains("--timeout", StringComparison.Ordinal),
            $"The {Cancelled} row of the platform Skill's exit-code table ({Path.GetRelativePath(RepositoryPaths.Root, path)}) "
            + $"says that cancellation returns {Cancelled} only when the command runs under --timeout: {row}");
    }

    [GeneratedRegex(@"(?m)^\|\s*(?<exit>[0-9]+)\s*\|")]
    private static partial Regex TableRow();
}
