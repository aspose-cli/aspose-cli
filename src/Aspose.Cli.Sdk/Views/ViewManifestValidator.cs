using System.Text.RegularExpressions;

namespace Aspose.Cli.Sdk.Views;

/// <summary>
/// The structural rules every rendered view manifest must satisfy before a
/// host publishes it. Products, static review and live display share them.
/// </summary>
public static partial class ViewManifestValidator
{
    /// <summary>
    /// Validates part identity, file names, kinds, layout sizes and element
    /// bounds; throws <see cref="InvalidOperationException"/> naming the first violation.
    /// </summary>
    public static void Validate(ViewManifest manifest, int maxPartCount)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.Parts.Count > maxPartCount || manifest.TotalPartCount < manifest.Parts.Count)
        {
            throw Invalid(
                $"reported {manifest.Parts.Count} of {manifest.TotalPartCount} parts with a bound of {maxPartCount}");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ViewPart part in manifest.Parts)
        {
            if (string.IsNullOrWhiteSpace(part.Id) || !ids.Add(part.Id))
            {
                throw Invalid($"part id '{part.Id}' is empty or repeated");
            }
            if (!IsSafeFile(part.File) || !files.Add(part.File))
            {
                throw Invalid($"part file '{part.File}' is not a unique safe relative path");
            }
            bool image = part.Kind == ViewPartKinds.Image;
            if (!image && part.Kind != ViewPartKinds.Html)
            {
                throw Invalid($"part '{part.Id}' has unknown kind '{part.Kind}'");
            }
            if (image && (part.Width is not > 0 || part.Height is not > 0))
            {
                throw Invalid($"image part '{part.Id}' has no positive layout size");
            }
            foreach (ViewElement element in part.Elements ?? [])
            {
                ViewBox box = element.Box;
                if (!double.IsFinite(box.X) || !double.IsFinite(box.Y)
                    || !double.IsFinite(box.Width) || !double.IsFinite(box.Height)
                    || box.Width < 0 || box.Height < 0
                    || string.IsNullOrEmpty(element.Kind) || string.IsNullOrEmpty(element.Digest))
                {
                    throw Invalid($"part '{part.Id}' has an invalid element");
                }
            }
        }
    }

    /// <summary>Whether a part file is a forward-slash path of plain ASCII segments.</summary>
    public static bool IsSafeFile(string file) =>
        !string.IsNullOrEmpty(file) && SafeFile().IsMatch(file);

    private static InvalidOperationException Invalid(string reason) =>
        new($"The view manifest is invalid: {reason}.");

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]*(/[A-Za-z0-9][A-Za-z0-9._-]*)*$")]
    private static partial Regex SafeFile();
}
