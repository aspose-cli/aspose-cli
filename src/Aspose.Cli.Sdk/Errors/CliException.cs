using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Errors;

/// <summary>
/// The only exception type that crosses layer boundaries. Engine adapters
/// translate every SDK exception into a <see cref="CliException"/>; the
/// command executor turns it into the error envelope and the exit code.
/// A product builds its own codes with the constructor; a shared code of
/// <see cref="ErrorCodes"/> is built only by an SDK error factory, such as
/// <see cref="CliErrors"/>, so each situation has one message, hint and details shape.
/// </summary>
public sealed class CliException : Exception
{
    /// <summary>Creates an error with a code the product declares.</summary>
    /// <exception cref="ArgumentException"><paramref name="code"/> is a shared code of <see cref="ErrorCodes"/>.</exception>
    public CliException(
        ErrorCode code,
        string message,
        string? hint = null,
        JsonObject? details = null,
        string? docs = null,
        Exception? innerException = null)
        : this(message, innerException, RefuseShared(code), hint, details, docs)
    {
    }

    private CliException(
        string message,
        Exception? innerException,
        ErrorCode code,
        string? hint,
        JsonObject? details,
        string? docs)
        : base(message, innerException)
    {
        ArgumentNullException.ThrowIfNull(code);
        if (code.DetailsSchemaId == NotFoundDetails.CatalogId
            && (details?["subject"] is null || details["requested"] is null || details["availableCount"] is null))
        {
            throw new ArgumentException(
                $"Error code '{code.Name}' is a not-found code; build it with CliErrors.NotFound or CliErrors.NotFoundAt.",
                nameof(details));
        }

        Code = code;
        Hint = hint;
        Details = details;
        Docs = docs;
    }

    /// <summary>Creates an error with any code; the SDK's factories build shared codes with it.</summary>
    internal static CliException Create(
        ErrorCode code,
        string message,
        string? hint = null,
        JsonObject? details = null,
        string? docs = null,
        Exception? innerException = null) => new(message, innerException, code, hint, details, docs);

    private static ErrorCode RefuseShared(ErrorCode code)
    {
        ArgumentNullException.ThrowIfNull(code);
        return ErrorCodes.IsShared(code)
            ? throw new ArgumentException(
                $"Error code '{code.Name}' is a shared code; build it with a CliErrors factory.", nameof(code))
            : code;
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
