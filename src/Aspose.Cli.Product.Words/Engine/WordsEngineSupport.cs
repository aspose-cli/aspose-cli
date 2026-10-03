using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;

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

        if (LossyConversion(format) is { } lossy)
        {
            extra.Add(lossy);
        }

        if (loaded.Format.HasMacros && format is not "docm" and not "dotm")
        {
            extra.Add(new Warning { Code = WordsDiagnostics.MacrosDropped, Message = "The source contains macros which the target format does not preserve.", Hint = "Convert to docm/dotm to preserve macros." });
        }

        return EnvelopeParts.CombineWarnings(EnvelopeParts.OutputWarnings(state), extra);
    }

    /// <summary>The warning for an output format that cannot hold every Word feature, or null.</summary>
    internal static Warning? LossyConversion(string format) => format is "txt" or "md" or "html" or "html-fixed"
        ? new Warning { Code = WarningCodes.LossyConversion, Message = $"Conversion to {format} cannot preserve every Word feature.", Hint = "Keep a DOCX copy when styles, headers, fields or revisions matter." }
        : null;

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
