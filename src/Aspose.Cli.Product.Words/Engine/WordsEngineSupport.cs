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

    internal static IReadOnlyList<Warning>? OutputWarnings(LicenseState state, LoadedDocument loaded, string format)
    {
        var extra = new List<Warning>();
        extra.AddRange(InputWarnings(loaded) ?? []);

        extra.AddRange(ConversionWarnings(loaded.Document, format));

        if (loaded.Format.HasMacros && format is not "docm" and not "dotm")
        {
            extra.Add(new Warning { Code = WordsDiagnostics.MacrosDropped, Message = "The source contains macros which the target format does not preserve.", Hint = "Convert to docm/dotm to preserve macros." });
        }

        return EnvelopeParts.CombineWarnings(EnvelopeParts.OutputWarnings(state), extra);
    }

    /// <summary>
    /// The warnings for saving a document to a format that cannot hold every Word feature: one
    /// for the format, and for plain text and Markdown one for each kind of text the SDK mixes
    /// into the body (WORDS-TEXT-COMMENTS, WORDS-TEXT-DELETIONS).
    /// </summary>
    internal static IEnumerable<Warning> ConversionWarnings(Document document, string format)
    {
        if (format is not ("txt" or "md" or "html" or "html-fixed"))
        {
            yield break;
        }

        yield return new Warning { Code = WarningCodes.LossyConversion, Message = $"Conversion to {format} cannot preserve every Word feature.", Hint = "Keep a DOCX copy when styles, headers, fields or revisions matter." };
        if (format is not ("txt" or "md"))
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

        if (document.Revisions.Cast<Revision>().Any(static revision => revision.RevisionType is RevisionType.Deletion or RevisionType.Moving))
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
