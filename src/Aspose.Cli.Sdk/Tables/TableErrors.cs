using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.Tables;

/// <summary>
/// The errors of the object table. Each states the fact, the operation's position in the batch
/// (<c>details.index</c>) and the address (<c>details.at</c>) when it has them, and never a hint.
/// </summary>
public static class TableErrors
{
    /// <summary>
    /// The <c>ADDRESS_NOT_FOUND</c> of an object a row value or an argument names by its key, such
    /// as a layout by its id; the edit adds the operation's position and address.
    /// </summary>
    /// <param name="subject">The kind looked up, such as <c>layout</c>.</param>
    /// <param name="requested">The key as the caller wrote it.</param>
    /// <param name="available">The keys that exist, in document order.</param>
    public static CliException NotFound(string subject, string requested, IReadOnlyCollection<string> available) =>
        NotFound(subject, requested, parent: null, available, at: null);

    /// <summary>
    /// The <c>OPS_INVALID</c> of arguments an add or an action refuses together, such as two that
    /// pick different engine calls; the edit adds the operation's position.
    /// </summary>
    /// <param name="fact">The fact, such as <c>args gives both type and width</c>.</param>
    public static CliException InvalidArguments(string fact) =>
        CliException.Create(ErrorCodes.OpsInvalid, fact, new JsonObject { ["reason"] = OpsInvalidReasons.InvalidValue, ["field"] = "args" });

    /// <summary>
    /// The <c>OPS_INVALID</c> of an engine member a row or an add goes through that is null on the
    /// object, such as a shape's text frame: there is nothing to set, and the CLI makes none. The
    /// edit adds the operation's position, address and row.
    /// </summary>
    /// <param name="member">The engine member, such as <c>IAutoShape.TextFrame</c>.</param>
    public static CliException Absent(string member) =>
        CliException.Create(ErrorCodes.OpsInvalid, $"{member} is null on this object", new JsonObject { ["reason"] = OpsInvalidReasons.InvalidValue });

    /// <summary>
    /// The <c>OPS_INVALID</c> of a value that is of the row's kind but does not fit the object it
    /// is set on, such as a matrix of another size than the range: the fact, stated with both
    /// sides. The edit adds the operation's position, address and row.
    /// </summary>
    /// <param name="fact">The fact, such as <c>the value is 2 rows by 3 columns and the range 6 rows by 6 columns</c>.</param>
    public static CliException DoesNotFit(string fact) =>
        CliException.Create(ErrorCodes.OpsInvalid, fact, new JsonObject { ["reason"] = OpsInvalidReasons.InvalidValue });

    /// <summary>The <c>OPS_INVALID</c> of a batch or one of its operations.</summary>
    /// <param name="index">The operation's position, or null for the batch as a whole.</param>
    /// <param name="reason">One of <see cref="OpsInvalidReasons"/>.</param>
    /// <param name="field">The field of the operation it is about, such as <c>props.text</c>.</param>
    /// <param name="fact">The fact, a sentence without its subject.</param>
    /// <param name="at">The address it is about.</param>
    /// <param name="available">The names that exist, when a name is not one of them.</param>
    internal static CliException Invalid(int? index, string reason, string? field, string fact, string? at = null, IEnumerable<string>? available = null)
    {
        var details = new JsonObject { ["index"] = index, ["reason"] = reason, ["field"] = field, ["at"] = at };
        string message = $"{Subject(index)}{fact}";
        if (available is not null)
        {
            Mistake mistake = Mistake.Of(field ?? string.Empty, available);
            mistake.WriteTo(details);
            message = $"{message}. {mistake.MustBeOneOf()}";
        }

        return CliException.Create(ErrorCodes.OpsInvalid, message, details);
    }

    /// <summary>
    /// The <c>OPS_INVALID</c> of an add whose <c>in</c> names an object of the right kind but not of
    /// the engine type whose member the add calls, such as a paragraph added to a table shape:
    /// what the object is not, and the adds it takes.
    /// </summary>
    internal static CliException NotTheParent(int index, string at, string kind, string add, string target, IReadOnlyList<string> takes) =>
        CliException.Create(
            ErrorCodes.OpsInvalid,
            $"{Subject(index)}{at}, of kind {kind}, is no {target}, and the '{add}' add adds only to one that is; "
                + (takes.Count == 0 ? "this one takes no add." : $"this one takes: {string.Join(", ", takes)}."),
            new JsonObject
            {
                ["index"] = index,
                ["reason"] = OpsInvalidReasons.InvalidValue,
                ["field"] = "in",
                ["at"] = at,
                ["available"] = new JsonArray([.. takes.Select(static name => (JsonNode)name)]),
            });

    /// <summary>Every invalid operation at once: the first leads, <c>details.errors</c> lists each.</summary>
    internal static CliException InvalidAll(IReadOnlyList<CliException> failures)
    {
        if (failures.Count == 1)
        {
            return failures[0];
        }

        JsonObject details = failures[0].Details!.DeepClone().AsObject();
        details["errors"] = new JsonArray([.. failures.Select(static failure => failure.Details!.DeepClone())]);
        return CliException.Create(
            ErrorCodes.OpsInvalid,
            $"{failures.Count} problems make the batch invalid, each listed in details.errors; the first: {failures[0].Message}",
            details);
    }

    /// <summary>The <c>ADDRESS_NOT_FOUND</c> of an object a step of an address names.</summary>
    /// <param name="subject">The kind or segment looked up, such as <c>shape</c>.</param>
    /// <param name="requested">The key as the address writes it.</param>
    /// <param name="parent">The address of the parent it was looked up in, or null for the document.</param>
    /// <param name="available">The keys that exist there, in document order.</param>
    /// <param name="at">The whole address.</param>
    internal static CliException NotFound(string subject, string requested, string? parent, IReadOnlyCollection<string> available, string? at)
    {
        CliException found = CliErrors.NotFound(ErrorCodes.AddressNotFound, subject, requested, available);
        JsonObject details = found.Details!.DeepClone().AsObject();
        details["at"] = at;
        string where = parent is null ? string.Empty : $" in {parent}";
        string message = available.Count == 0
            ? $"No {subject} '{requested}' was found{where}; there is none."
            : $"No {subject} '{requested}' was found{where}. {Mistake.Of(requested, available).MustBeOneOf()}";
        return CliException.Create(ErrorCodes.AddressNotFound, message, details);
    }

    /// <summary>The <c>ADDRESS_AMBIGUOUS</c> of an alias that matches several objects.</summary>
    internal static CliException Ambiguous(string subject, string requested, string parent, IReadOnlyList<string> matches, string at) =>
        CliException.Create(
            ErrorCodes.AddressAmbiguous,
            $"{subject} '{requested}' in {parent} matches {matches.Count} objects: {string.Join(", ", matches)}.",
            new JsonObject
            {
                ["subject"] = subject,
                ["requested"] = requested,
                ["at"] = at,
                ["available"] = new JsonArray([.. matches.Take(CliErrors.MaximumAvailableNames).Select(static match => (JsonNode)match)]),
                ["availableCount"] = matches.Count > CliErrors.MaximumAvailableNames ? matches.Count : null,
            });

    /// <summary>
    /// Whether the engine threw <paramref name="exception"/>, the one place that decides an
    /// <c>ENGINE_ERROR</c>: from the frame that threw outward, the first frame of the engine's
    /// assembly or of the CLI's own (<c>Aspose.Cli.*</c>) decides; a frame of neither, such as the
    /// runtime's, is passed over. An exception the CLI's own code threw is no engine's, and is not
    /// reported as one.
    /// </summary>
    /// <param name="exception">What was thrown.</param>
    /// <param name="engine">The engine's assembly, the one the document type comes from.</param>
    internal static bool FromEngine(Exception exception, Assembly engine)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(engine);
        foreach (StackFrame frame in new StackTrace(exception, fNeedFileInfo: false).GetFrames())
        {
            if (Assembly(frame) is not { } assembly)
            {
                continue;
            }

            if (assembly == engine)
            {
                return true;
            }

            if (assembly.GetName().Name?.StartsWith("Aspose.Cli", StringComparison.Ordinal) == true)
            {
                return false;
            }
        }

        return false;
    }

    // A frame without metadata (none in an untrimmed build) is passed over.
    private static Assembly? Assembly(StackFrame frame) => frame.GetMethod()?.DeclaringType?.Assembly;

    /// <summary>
    /// Runs a whole read, search or edit; what the engine throws outside any operation, row or
    /// lookup that reports it more closely, such as while counting the inventory or listing
    /// objects, is reported as the engine's. What the CLI's own code throws passes as it is.
    /// </summary>
    internal static T Guard<T>(DocumentTable table, string doing, Func<T> run)
    {
        try
        {
            return run();
        }
        catch (Exception exception) when (exception is not CliException and not OperationCanceledException and not RowReadException
            && FromEngine(exception, table.Engine))
        {
            throw EngineDuring(doing, exception);
        }
    }

    /// <summary>
    /// The <c>ENGINE_ERROR</c> of a row the engine threw on while it was read; what the CLI's own
    /// code threw is thrown again as it was, with its own stack.
    /// </summary>
    internal static CliException RowRead(DocumentTable table, int? index, string at, RowReadException failure)
    {
        Exception thrown = failure.InnerException!;
        if (!FromEngine(thrown, table.Engine))
        {
            ExceptionDispatchInfo.Throw(thrown);
        }

        return Engine(index, at, failure.Row, thrown);
    }

    /// <summary>The <c>ENGINE_ERROR</c> of an engine call or row the engine refused, with its exception as it is.</summary>
    /// <param name="index">The operation's position, or null for a read.</param>
    /// <param name="at">The address of the object the call was made on, if any.</param>
    /// <param name="row">The row whose member was read or set, if any.</param>
    /// <param name="exception">The engine's exception.</param>
    internal static CliException Engine(int? index, string? at, string? row, Exception exception)
    {
        string type = exception.GetType().FullName ?? exception.GetType().Name;
        string place = (at, row) switch
        {
            (null, null) => string.Empty,
            (null, _) => $" row '{row}'",
            (_, null) => $" at {at}",
            _ => $" at {at}, row '{row}'",
        };
        string subject = index is null ? (place.Length == 0 ? "The engine call" : place.TrimStart() is var where && where.StartsWith("at ", StringComparison.Ordinal) ? $"At {where[3..]}" : $"Reading {where}") : $"ops[{index}]{place}";
        return CliException.Create(
            ErrorCodes.EngineError,
            $"{subject}: the engine threw {type}: {exception.Message}",
            new JsonObject
            {
                ["index"] = index,
                ["at"] = at,
                ["row"] = row,
                ["engineException"] = type,
                ["engineMessage"] = exception.Message,
            },
            innerException: exception);
    }

    /// <summary>The <c>ENGINE_ERROR</c> of an engine call no operation makes, such as the save of the edited document.</summary>
    internal static CliException EngineDuring(string doing, Exception exception) => EngineErrors.EngineFailed(doing, exception);

    /// <summary>An error an operation raised, restated with the operation's position and address.</summary>
    internal static CliException At(int index, string? at, CliException error)
    {
        JsonObject details = error.Details?.DeepClone().AsObject() ?? [];
        details["index"] = index;
        if (at is not null && details["at"] is null)
        {
            details["at"] = at;
        }

        return CliException.Create(error.Code, $"ops[{index}]: {error.Message}", details, error);
    }

    private static string Subject(int? index) => index is null ? "The operation document is invalid: " : $"ops[{index}]: ";
}
