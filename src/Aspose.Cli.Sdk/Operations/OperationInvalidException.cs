namespace Aspose.Cli.Sdk.Operations;

/// <summary>
/// Rejects one operation before it changes anything. Validators and handlers throw it
/// without knowing the operation's position; the pipeline reports it as <c>OPS_INVALID</c>
/// with the index and name, and a best-effort batch continues past it.
/// </summary>
public sealed class OperationInvalidException : Exception
{
    /// <summary>Creates a rejection with a reason and optional recovery hint.</summary>
    public OperationInvalidException(string reason, string? hint = null)
        : base(reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Hint = hint;
    }

    /// <summary>Actionable recovery guidance, or null for the pipeline default.</summary>
    public string? Hint { get; }

    /// <summary>Throws a rejection unless <paramref name="condition"/> holds.</summary>
    public static void Require(bool condition, string reason, string? hint = null)
    {
        if (!condition)
        {
            throw new OperationInvalidException(reason, hint);
        }
    }
}
