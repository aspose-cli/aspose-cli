namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// A problem found while verifying an edited output that had already been produced.
/// Any issue makes the verification <c>ok:false</c> and the command exit 8.
/// </summary>
/// <remarks>
/// The code always comes from a declared diagnostic: an issue is built only by
/// <see cref="From(Warning)"/>, which keeps a warning's code, or by <c>VerificationIssues.Of</c>,
/// which takes a verification descriptor's code. No caller can name a code of its own.
/// </remarks>
[SchemaId("verification-issue")]
public sealed record VerificationIssue
{
    internal VerificationIssue(string code, string message, string? location, string? hint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(message);
        Code = code;
        Message = message;
        Location = location;
        Hint = hint;
    }

    /// <summary>Stable SCREAMING_SNAKE_CASE identifier an agent can branch on.</summary>
    [Pattern("^[A-Z][A-Z0-9_]*$")]
    public string Code { get; }

    /// <summary>Human-readable explanation.</summary>
    public string Message { get; }

    /// <summary>
    /// The affected item's address in the targets syntax <c>&lt;unit&gt;/&lt;address&gt;</c>, such as
    /// <c>page/3</c>, <c>slide/2/shape/4</c> or <c>cell/Sheet1!A1</c>, with names escaped as targets escape
    /// them; omitted unless the issue concerns exactly one item.
    /// </summary>
    public string? Location { get; }

    /// <summary>Recommended next action for the caller.</summary>
    public string? Hint { get; }

    /// <summary>Carries a warning into verification, keeping its code, message, location and hint.</summary>
    public static VerificationIssue From(Warning warning)
    {
        ArgumentNullException.ThrowIfNull(warning);
        return new VerificationIssue(warning.Code.Name, warning.Message, warning.Location, warning.Hint);
    }
}
