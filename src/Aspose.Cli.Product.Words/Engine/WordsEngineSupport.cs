using System.Globalization;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Words;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>Provides stateless result, warning and text helpers shared by Words services.</summary>
internal static class WordsEngineSupport
{
    internal static IReadOnlyList<Warning>? InputWarnings(LoadedDocument loaded)
    {
        var warnings = new List<Warning>();
        if (LocalDocumentResourceLoader.OmissionWarning(loaded.RemoteResourcesBlocked) is { } omitted)
        {
            warnings.Add(omitted);
        }

        if (loaded.EvaluationInputTruncated)
        {
            warnings.Add(EvaluationTruncated);
        }

        return warnings.Count == 0 ? null : warnings;
    }

    /// <summary>Evaluation mode cut short an input or imported document.</summary>
    internal static Warning EvaluationTruncated { get; } = new()
    {
        Code = WarningCodes.EvalInputTruncated,
        Message = "Aspose.Words evaluation mode truncated an input document while loading it.",
        Hint = "Do not treat this projection or output as complete; apply a license and retry.",
    };

    /// <summary>
    /// The warnings for writing a loaded document to a format. A page render passes
    /// <paramref name="rendered"/>: an image shows the document as it looks and is never a copy
    /// of it, so it is not told that it drops the source's revisions.
    /// </summary>
    internal static IReadOnlyList<Warning>? OutputWarnings(LicenseState state, LoadedDocument loaded, string format, bool rendered = false)
    {
        var extra = new List<Warning>();
        extra.AddRange(InputWarnings(loaded) ?? []);

        if (!rendered)
        {
            extra.AddRange(ConversionWarnings(loaded.Document, format));
        }

        if (KeepsRevisions(loaded.Document, format))
        {
            extra.Add(TrackedChangesPresent);
        }

        if (loaded.Format.HasMacros && format is not "docm" and not "dotm")
        {
            extra.Add(new Warning { Code = WordsDiagnostics.MacrosDropped, Message = "The source contains macros which the target format does not preserve.", Hint = "Convert to docm/dotm to preserve macros." });
        }

        return EnvelopeParts.CombineWarnings(EnvelopeParts.OutputWarnings(state), extra);
    }

    /// <summary>Discloses the revisions an output that stores them still contains.</summary>
    internal static Warning TrackedChangesPresent { get; } = new()
    {
        Code = WordsDiagnostics.TrackedChangesPresent,
        Message = "The input has tracked changes, and the output still contains tracked changes.",
        Hint = "Disclose them and accept or reject only when explicitly requested.",
    };

    /// <summary>Whether a document saved in a format keeps its revisions as revisions.</summary>
    internal static bool KeepsRevisions(Document document, string format) =>
        document.Revisions.Count > 0 && WordsFormats.RevisionIds.Contains(format, StringComparer.Ordinal);

    /// <summary>
    /// The warnings for saving a document to a format that cannot hold every Word feature: one
    /// for the format; one for revisions the format cannot store; and for plain text and
    /// Markdown one for each kind of text the SDK mixes into the body (WORDS-TEXT-COMMENTS,
    /// WORDS-TEXT-DELETIONS), which also stands for the revisions such an output drops.
    /// </summary>
    internal static IEnumerable<Warning> ConversionWarnings(Document document, string format)
    {
        bool text = format is "txt" or "md";
        if (format is "txt" or "md" or "html" or "html-fixed")
        {
            yield return new Warning { Code = WarningCodes.LossyConversion, Message = $"Conversion to {format} cannot preserve every Word feature.", Hint = "Keep a DOCX copy when styles, headers, fields or revisions matter." };
        }

        bool revised = document.Revisions.Count > 0;
        bool mixed = text && document.Revisions.Cast<Revision>()
            .Any(static revision => revision.RevisionType is RevisionType.Deletion or RevisionType.Moving);
        if (revised && !mixed && !WordsFormats.RevisionIds.Contains(format, StringComparer.Ordinal))
        {
            yield return new Warning
            {
                Code = WarningCodes.LossyConversion,
                Message = $"The {format} output cannot keep tracked changes as revisions that can be accepted or rejected.",
                Hint = "Disclose the tracked changes; keep a DOCX, RTF or ODT copy to keep them reviewable.",
            };
        }

        if (!text)
        {
            yield break;
        }

        int comments = document.GetChildNodes(NodeType.Comment, true).Count;
        if (comments > 0)
        {
            yield return new Warning
            {
                Code = WarningCodes.LossyConversion,
                Message = string.Create(CultureInfo.InvariantCulture, $"The {format} output writes the text of {comments} comment(s) into the body text, where it reads as document text."),
                Hint = $"To leave the comments out, apply remove_comments with 'aspose-cli words edit' and a .{format} --out; keep a DOCX copy to keep them.",
            };
        }

        if (mixed)
        {
            yield return new Warning
            {
                Code = WarningCodes.LossyConversion,
                Message = $"The {format} output writes the deleted and moved-from text of tracked changes beside the text that replaces it, so it reads as neither the original nor the revised document.",
                Hint = $"Disclose the revisions; when the user decides, accept or reject them with 'aspose-cli words edit' and a .{format} --out, or keep a DOCX copy.",
            };
        }
    }

    internal static IReadOnlyList<Warning>? CompareWarnings(
        LicenseState state,
        LoadedDocument left,
        LoadedDocument right,
        bool producedOutput)
    {
        var warnings = new List<Warning>();
        warnings.AddRange(InputWarnings(left) ?? []);
        warnings.AddRange(InputWarnings(right) ?? []);
        IReadOnlyList<Warning>? outputWarnings = producedOutput
            ? EnvelopeParts.OutputWarnings(state)
            : null;
        return EnvelopeParts.CombineWarnings(outputWarnings, warnings);
    }

    internal static OutputInfo BuildOutput(string path, string format, long size) =>
        new() { Path = path, Format = format, SizeBytes = size };

    internal static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..length] + "\u2026";
}
