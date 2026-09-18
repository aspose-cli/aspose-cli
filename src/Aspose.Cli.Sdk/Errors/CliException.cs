using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Errors;

/// <summary>
/// The only exception type that crosses layer boundaries. Engine adapters
/// translate every SDK exception into a <see cref="CliException"/>; the
/// command executor turns it into the error envelope and the exit code.
/// </summary>
public sealed class CliException : Exception
{
    public CliException(
        ErrorCode code,
        string message,
        string? hint = null,
        JsonObject? details = null,
        string? docs = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ArgumentNullException.ThrowIfNull(code);
        Code = code;
        Hint = hint;
        Details = details;
        Docs = docs;
    }

    /// <summary>The stable error code.</summary>
    public ErrorCode Code { get; }

    /// <summary>Recommended next action for the caller.</summary>
    public string? Hint { get; }

    /// <summary>Structured context for the error.</summary>
    public JsonObject? Details { get; }

    /// <summary>Topic name for <c>aspose-cli docs</c>.</summary>
    public string? Docs { get; }

    /// <summary>The process exit code this error maps to.</summary>
    public ExitCode ExitCode => Code.ExitCode;

    /// <summary>Invocation-wide failures must never be converted into best-effort operation outcomes.</summary>
    public bool IsInvocationFailure => Code == ErrorCodes.OperationTimeout
        || Code == ErrorCodes.InputBudgetExceeded || Code == ErrorCodes.FileTooLarge;

    /// <summary>Converts the exception to the public error contract.</summary>
    public ErrorEnvelope ToEnvelope() => new()
    {
        Error = new ErrorPayload
        {
            Code = Code.Name,
            Message = Message,
            Details = Details,
            Hint = Hint,
            Docs = Docs,
        },
    };
}
