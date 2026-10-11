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
/// sources.
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

    /// <summary>
    /// The exit codes that end a command without an error envelope cannot be looked up through an
    /// error code in capabilities, so the platform Skill states each one: success, a command that
    /// finished with a result that did not pass, and cancellation, which returns 130 with or without
    /// <c>--timeout</c>. Every other exit code comes with an error code whose exit code
    /// capabilities lists, so the Skill points there instead of repeating a table.
    /// </summary>
    [Fact]
    public void PlatformSkill_StatesTheExitCodesThatComeWithoutAnError()
    {
        string path = Path.Combine(RepositoryPaths.Root, "src", "Aspose.Cli.Host", "Skills", "Platform", "references", "troubleshooting.md");
        string text = File.ReadAllText(path).ReplaceLineEndings("\n");
        int start = text.IndexOf("## Exit codes", StringComparison.Ordinal);
        Assert.True(start >= 0, $"{path} has no '## Exit codes' section.");
        int end = text.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
        string section = end < 0 ? text[start..] : text[start..end];
        Dictionary<int, string> stated = ExitItem().Matches(section)
            .ToDictionary(static item => int.Parse(item.Groups["exit"].Value, System.Globalization.CultureInfo.InvariantCulture),
                static item => item.Groups["text"].Value.ReplaceLineEndings(" "));

        int[] missing = [.. new[] { ExitCode.Success, ExitCode.PartialFailure, ExitCode.Cancelled }
            .Select(static code => (int)code).Where(code => !stated.ContainsKey(code))];
        Assert.True(missing.Length == 0,
            $"The exit-code section of the platform Skill ({Path.GetRelativePath(RepositoryPaths.Root, path)}) states each exit code "
            + $"that comes without an error envelope as a '- <exit>:' item; missing: [{string.Join(", ", missing)}].");
        Assert.True(stated[Cancelled].Contains("with or without --timeout", StringComparison.Ordinal),
            $"The {Cancelled} item of the platform Skill's exit codes says that Ctrl+C returns {Cancelled} with or without --timeout: {stated[Cancelled]}");
    }

    /// <summary>A list item <c>- 8: ...</c> up to the next item or the end of the list.</summary>
    [GeneratedRegex(@"(?m)^- (?<exit>[0-9]+): (?<text>(?:.|\n(?!- |\n))*)")]
    private static partial Regex ExitItem();
}
