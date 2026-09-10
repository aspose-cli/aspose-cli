using Aspose.Cells;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Translates engine and IO exceptions into the stable error taxonomy. This
/// is a load-bearing part of the anti-corruption layer: engine exception
/// shapes may change between monthly SDK releases, but the codes emitted here
/// never do.
/// </summary>
internal static class ErrorTranslator
{
    /// <summary>Translates a failure while opening a workbook.</summary>
    public static CliException TranslateLoad(Exception exception, string path, bool passwordProvided) =>
        exception switch
        {
            CellsException cells when IsPasswordError(cells) =>
                passwordProvided ? CliErrors.PasswordInvalid(path) : CliErrors.PasswordRequired(path),
            CellsException cells => CellsErrors.FileCorrupt(path, Summarize(cells.Message)),
            FileNotFoundException => CliErrors.FileNotFound(path),
            IOException io when IsSharingViolation(io) => CliErrors.FileLocked(path),
            IOException io => CellsErrors.FileCorrupt(path, Summarize(io.Message)),
            UnauthorizedAccessException => CliErrors.FileAccessDenied(path),
            // We are here only because opening the file FAILED, and the try that
            // calls this wraps engine calls alone (new Workbook / AutoFitColumns) —
            // there is no CLI code left to blame. So any other exception is the
            // engine choking on the file's bytes (a pathological xls can make the
            // reader throw a raw ArgumentOutOfRangeException, not a CellsException),
            // which is a FILE_CORRUPT the user can act on — never an INTERNAL
            // "report a bug", which would send them chasing a defect that isn't ours.
            _ => CellsErrors.FileCorrupt(path, Summarize(exception.Message)),
        };

    /// <summary>
    /// Detects Windows sharing/lock violations (ERROR_SHARING_VIOLATION = 32,
    /// ERROR_LOCK_VIOLATION = 33), e.g. a workbook opened in Excel.
    /// </summary>
    internal static bool IsSharingViolation(IOException exception) =>
        (exception.HResult & 0xFFFF) is 32 or 33;

    // Prefer the typed code; keep the message check as a fallback in case a
    // future SDK build reports an encrypted-file failure under another code.
    // Permission covers the "Permission is required to open this file." failure
    // an encrypted workbook throws when opened without (or with a wrong)
    // password — reporting that as FILE_CORRUPT sent the user to check a file
    // that is perfectly fine and just needs --password.
    private static bool IsPasswordError(CellsException exception) =>
        exception.Code is ExceptionType.IncorrectPassword or ExceptionType.Permission
        || exception.Message.Contains("password", StringComparison.OrdinalIgnoreCase);

    private static string Summarize(string message)
    {
        // Engine messages can be multi-line; the first line carries the fact.
        string firstLine = message.AsSpan().TrimStart().ToString();
        int newline = firstLine.IndexOfAny(['\r', '\n']);
        return newline > 0 ? firstLine[..newline].TrimEnd() : firstLine.TrimEnd();
    }
}
