using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.IO;

/// <summary>
/// Bounds the size of an input document the engine will load into memory. A
/// pathologically large file fails fast with a clear, actionable error instead of
/// exhausting memory. The invocation ledger's <see cref="ResourceBudgetKinds.InputBytes"/>
/// limit is the only source of the budget; the host sets it from
/// <c>--max-input-bytes</c> or <see cref="BudgetVariable"/>.
/// </summary>
public static class InputSizeGuard
{
    /// <summary>Environment variable the host reads for the default byte budget.</summary>
    public const string BudgetVariable = "ASPOSE_CLI_MAX_FILE_BYTES";

    /// <summary>Default budget: 1 GiB.</summary>
    public const long DefaultMaxBytes = ResourceBudgetDefaults.DefaultInputBytes;

    /// <summary>Hard safety maximum: 4 GiB.</summary>
    public const long MaximumBytes = ResourceBudgetDefaults.MaximumInputBytes;

    /// <summary>
    /// Resolves the host's byte budget from <paramref name="readEnvironment"/>, falling
    /// back to <see cref="DefaultMaxBytes"/> when unset or not a valid positive integer.
    /// </summary>
    public static long ResolveMaxBytes(Func<string, string?> readEnvironment)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        string? raw = readEnvironment(BudgetVariable);
        return long.TryParse(raw, out long value)
            && value > 0
            && value <= MaximumBytes
            ? value
            : DefaultMaxBytes;
    }

    /// <summary>
    /// Admits the file at <paramref name="path"/> against the ledger's input-byte limit,
    /// or verifies that an already admitted file has not changed since.
    /// </summary>
    /// <exception cref="CliException">
    /// <c>FILE_TOO_LARGE</c> above the limit; <c>INPUT_CHANGED</c> after admission.
    /// </exception>
    public static void Ensure(ResourceBudgetLedger resourceBudgets, string path)
    {
        ArgumentNullException.ThrowIfNull(resourceBudgets);
        ArgumentException.ThrowIfNullOrEmpty(path);
        resourceBudgets.VerifyAdmission(path);
    }

    internal static FileInfo ReadInfo(string path)
    {
        try
        {
            var info = new FileInfo(path);
            _ = info.Length;
            return info;
        }
        catch (Exception exception) when (
            exception is FileNotFoundException or DirectoryNotFoundException)
        {
            throw CliErrors.FileNotFound(path);
        }
        catch (UnauthorizedAccessException)
        {
            throw CliErrors.FileAccessDenied(path);
        }
    }
}
