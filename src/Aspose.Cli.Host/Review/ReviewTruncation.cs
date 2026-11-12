using System.Globalization;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Host.Review;

/// <summary>
/// The one finding, the same for every product, of a review whose limits left parts of its view
/// without an image (<see cref="ReviewChecks.ReviewTruncated"/>).
/// </summary>
/// <param name="Parts">What the view's parts are, as people count them, such as <c>pages</c>.</param>
/// <param name="Again">The review again with <c>--max-items</c> at the given count, in a new directory; null when none can be named.</param>
internal sealed record ReviewTruncation(string Parts, Func<int, ContinuationCommand?>? Again)
{
    /// <summary>
    /// How many parts the review rendered of how many, which it left out, the limit that stopped
    /// it and the next step; null when it rendered every part.
    /// </summary>
    public ReviewFinding? Finding(string productId, ViewManifest manifest, int maxItems, LocalServiceResourceLimits limits)
    {
        int rendered = manifest.Parts.Count;
        int total = manifest.TotalPartCount;
        if (rendered >= total)
        {
            return null;
        }

        string left = rendered == 0
            ? "it has no image of any of them and did not check them"
            : Text($"it has no image of the {total - rendered} after the first {rendered} ({(rendered + 1 == total ? $"{total}" : $"{rendered + 1}-{total}")} in the view's order) and did not check them");
        // The rendering stops before the bound only when the snapshot has no room for the next part.
        bool budget = rendered < Math.Min(total, maxItems);
        // Each part is one file, beside the view's view.json.
        int most = Math.Min(ReviewEvidenceWriter.MaximumMaxItems, limits.MaximumSnapshotFiles - 1);
        string message = budget
            ? Text($"The review rendered {rendered} of {total} {Parts}: the preview snapshot budget, {ViewArtifactLimits.SnapshotBytesVariable}, now {limits.MaximumSnapshotBytes} bytes, had no room for the {(rendered == 0 ? "first" : "next")}; {left}.")
            : Text($"The review rendered {rendered} of {total} {Parts}, the most --max-items allows; {left}.");
        string hint = budget
            ? $"Raise {ViewArtifactLimits.SnapshotBytesVariable} and review again in a new directory{(rendered == 0 ? "" : ", or split the document and review each part")}."
            : total > most
                ? Text($"One review renders at most {most} {Parts} ({(most == ReviewEvidenceWriter.MaximumMaxItems ? "the most --max-items takes" : $"{ViewArtifactLimits.FilesVariable}, now {limits.MaximumSnapshotFiles}, counts view.json too")}): split the document into parts of at most {most} {Parts} and review each part in a new directory.")
                : Again?.Invoke(total) is { } again
                    ? Text($"Review all {total} {Parts} in a new directory: {again.Quoted()}.")
                    : Text($"Run the review again with --max-items {total} and a new --out directory.");
        return ReviewChecks.ReviewTruncated(productId).Finding(message, null, hint) with
        {
            // The parts left out have no rendering: the view's entry lists what was rendered.
            Evidence = ["artifacts/" + ReviewEvidenceWriter.ViewManifestFile],
        };
    }

    private static string Text(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
