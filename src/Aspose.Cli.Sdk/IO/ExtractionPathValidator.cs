using System.Text;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.IO;

/// <summary>Normalizes extraction paths and rejects traversal, devices, and links.</summary>
internal static class ExtractionPathValidator
{
    internal static string NormalizeRelativePath(string suggestedPath, bool flatten)
    {
        ArgumentException.ThrowIfNullOrEmpty(suggestedPath);
        string normalized = suggestedPath.Normalize(NormalizationForm.FormC)
            .Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized)
            || normalized.StartsWith("/", StringComparison.Ordinal)
            || Path.IsPathRooted(normalized))
        {
            throw Refused($"unsafe extraction name '{suggestedPath}'");
        }

        string[] segments = normalized.Split('/', StringSplitOptions.None);
        if (segments.Any(static segment => IsUnsafeSegment(segment)))
        {
            throw Refused($"unsafe extraction name '{suggestedPath}'");
        }
        if (flatten)
        {
            segments = [segments[^1]];
        }
        return Path.Combine(segments);
    }

    internal static void EnsureNoLinks(string path)
    {
        string? current = Path.GetFullPath(path);
        while (current is not null)
        {
            FileAttributes? attributes =
                FilePublicationOwnedDelete.TryGetAttributesNoFollow(current);
            if (attributes?.HasFlag(FileAttributes.ReparsePoint) == true)
            {
                throw Refused(
                    $"unsafe extraction path contains a link or reparse point: '{path}'");
            }

            string? parent = Path.GetDirectoryName(current);
            if (string.Equals(parent, current, StringComparison.Ordinal))
            {
                break;
            }
            current = parent;
        }
    }

    internal static CliException Refused(string reason) => new(
        ErrorCodes.ExtractBudgetExceeded,
        $"Extraction was refused: {reason}.",
        hint: "Narrow the extraction or raise the documented item/byte budget only for trusted content.");

    internal static bool IsUnsafeSegment(string segment)
    {
        if (segment.Length == 0
            || segment is "." or ".."
            || segment.EndsWith(' ')
            || segment.EndsWith('.')
            || segment.Contains(':')
            || segment.IndexOfAny(['<', '>', '"', '|', '?', '*']) >= 0
            || segment.Any(static character => char.IsControl(character))
            || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return true;
        }

        int extension = segment.IndexOf('.');
        string device = (extension < 0 ? segment : segment[..extension]).TrimEnd(' ').ToUpperInvariant();
        return device is "CON" or "PRN" or "AUX" or "NUL"
            || (device.Length == 4
                && (device.StartsWith("COM", StringComparison.Ordinal)
                    || device.StartsWith("LPT", StringComparison.Ordinal))
                && device[3] is >= '1' and <= '9' or '\u00B9' or '\u00B2' or '\u00B3');
    }
}
