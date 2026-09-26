using System.Text.RegularExpressions;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// One deterministic review check, declared once by its owner: the stable code a caller can
/// filter by (<c>review --code</c>), the severity every finding of the check carries, and what
/// it detects. Findings are built only from their check, so a code's severity and meaning
/// cannot drift between call sites. Codes read <c>&lt;OWNER&gt;_&lt;OBJECT&gt;_&lt;CONDITION&gt;</c>,
/// for example <c>WORDS_PAGE_BLANK</c>.
/// </summary>
public sealed partial record ReviewCheck
{
    /// <summary>Declares a check.</summary>
    /// <param name="code">SCREAMING_SNAKE_CASE code, unique across the distribution.</param>
    /// <param name="severity">One of <see cref="ReviewSeverities"/>.</param>
    /// <param name="summary">What the check detects, as one sentence.</param>
    public ReviewCheck(string code, string severity, string summary)
    {
        if (string.IsNullOrEmpty(code) || !CodePattern().IsMatch(code))
        {
            throw new ArgumentException($"Review check code '{code}' is not SCREAMING_SNAKE_CASE.", nameof(code));
        }

        if (!ReviewSeverities.All.Contains(severity, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"Review check '{code}' has severity '{severity}'; use {string.Join(", ", ReviewSeverities.All)}.",
                nameof(severity));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        Code = code;
        Severity = severity;
        Summary = summary;
    }

    /// <summary>The stable code findings of this check carry.</summary>
    public string Code { get; }

    /// <summary>The severity findings of this check carry.</summary>
    public string Severity { get; }

    /// <summary>What the check detects.</summary>
    public string Summary { get; }

    /// <summary>A finding of this check.</summary>
    public ReviewFinding Finding(string message, string? location = null, string? hint = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new ReviewFinding
        {
            Code = Code,
            Severity = Severity,
            Message = message,
            Location = location,
            Hint = hint,
        };
    }

    [GeneratedRegex("^[A-Z][A-Z0-9]*(_[A-Z0-9]+)+$")]
    private static partial Regex CodePattern();
}

/// <summary>The severities a review finding can carry; <c>error</c> fails the review (exit 8).</summary>
public static class ReviewSeverities
{
    public const string Info = "info";
    public const string Warning = "warning";
    public const string Error = "error";

    /// <summary>Every severity, from least to most severe.</summary>
    public static IReadOnlyList<string> All { get; } = [Info, Warning, Error];
}
