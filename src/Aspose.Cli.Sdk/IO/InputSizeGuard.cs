using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.IO;

/// <summary>
/// Bounds the size of an input workbook the engine will load into memory. The
/// CLI keeps output budgetable — windowed reads, capped previews — and this
/// extends the same discipline to input: a pathologically large file fails fast
/// with a clear, actionable error instead of exhausting memory. The budget is a
/// generous default that agents in constrained environments can lower via the
/// environment.
/// </summary>
public static class InputSizeGuard
{
    /// <summary>Environment variable overriding the byte budget.</summary>
    public const string BudgetVariable = "ASPOSE_CLI_MAX_FILE_BYTES";

    /// <summary>Default budget: 1 GiB.</summary>
    public const long DefaultMaxBytes = ResourceBudgetDefaults.DefaultInputBytes;

    /// <summary>Hard safety maximum: 4 GiB.</summary>
    public const long MaximumBytes = ResourceBudgetDefaults.MaximumInputBytes;

    /// <summary>
    /// Resolves the byte budget from <paramref name="readEnvironment"/>, falling
    /// back to <see cref="DefaultMaxBytes"/> when unset or not a positive integer.
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
    /// Throws <see cref="CliErrors.FileTooLarge"/> when the file at
    /// <paramref name="path"/> is larger than <paramref name="maxBytes"/>.
    /// </summary>
    public static void Ensure(
        ResourceBudgetLedger resourceBudgets,
        string path,
        long maxBytes)
    {
        ArgumentNullException.ThrowIfNull(resourceBudgets);
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (maxBytes <= 0 || maxBytes > MaximumBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxBytes),
                $"Input byte limit must be between 1 and {MaximumBytes}.");
        }

        resourceBudgets.VerifyAdmission(path);
        long size = ReadInfo(path).Length;

        if (size > maxBytes)
        {
            throw CliErrors.FileTooLarge(size, maxBytes);
        }
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
