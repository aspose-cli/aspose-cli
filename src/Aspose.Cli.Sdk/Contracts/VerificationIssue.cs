namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// A problem found while verifying an edited output that had already been produced.
/// Any issue makes the verification <c>ok:false</c> and the command exit 8.
/// </summary>
[SchemaId("verification-issue")]
public sealed record VerificationIssue
{
    /// <summary>Stable SCREAMING_SNAKE_CASE identifier an agent can branch on.</summary>
    [Pattern("^[A-Z][A-Z0-9_]*$")]
    public required string Code { get; init; }

    /// <summary>Human-readable explanation.</summary>
    public required string Message { get; init; }

    /// <summary>Stable product-owned address of the affected item, when there is exactly one.</summary>
    public string? Location { get; init; }

    /// <summary>Recommended next action for the caller.</summary>
    public string? Hint { get; init; }

    /// <summary>Carries a warning into verification, keeping its code, message, location and hint.</summary>
    public static VerificationIssue From(Warning warning)
    {
        ArgumentNullException.ThrowIfNull(warning);
        return new VerificationIssue
        {
            Code = warning.Code.Name,
            Message = warning.Message,
            Location = warning.Location,
            Hint = warning.Hint,
        };
    }
}
