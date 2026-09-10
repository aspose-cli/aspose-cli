using System.Text.RegularExpressions;

namespace Aspose.Cli.Host.Invocation;

/// <summary>Removes common secret and user-path shapes from private diagnostics.</summary>
internal static partial class DiagnosticRedactor
{
    public static string Redact(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string redacted = WindowsPath().Replace(value, "<path>");
        redacted = UnixSourcePath().Replace(redacted, " in <path>");
        redacted = SensitiveAssignment().Replace(
            redacted,
            match => match.Groups[1].Value + "=<redacted>");
        return redacted;
    }

    [GeneratedRegex(
        @"(?<![A-Za-z0-9_])(?:[A-Za-z]:\\|\\\\)[^""'\r\n]*?(?=(?:\s+at\s+|\s+inner=|$))",
        RegexOptions.CultureInvariant)]
    private static partial Regex WindowsPath();

    [GeneratedRegex(
        @"\s+in\s+/(?:[^:\r\n]+)(?=:\w|\s+inner=|$)",
        RegexOptions.CultureInvariant)]
    private static partial Regex UnixSourcePath();

    [GeneratedRegex(
        @"(?i)\b(password|token|capability|csrf|cookie|authorization|license-content)\s*=\s*[^\s,;]+",
        RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveAssignment();
}
