using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>Provides stateless result, warning and text helpers shared by Words services.</summary>
internal static class WordsEngineSupport
{
    internal static IReadOnlyList<Warning>? RemoteWarning(int blocked) =>
        blocked == 0 ? null :
        [new Warning { Code = WarningCodes.RemoteResourcesBlocked, AffectsCompleteness = true, Message = $"{blocked} external resource(s) were blocked.", Hint = "Use guarded local resources beside the document, or a separately verified local cache." }];

    internal static IReadOnlyList<Warning>? InputWarnings(LoadedDocument loaded)
    {
        var warnings = new List<Warning>();
        if (loaded.RemoteResourcesBlocked > 0)
        {
            warnings.Add(RemoteWarning(loaded.RemoteResourcesBlocked)![0]);
        }

        if (loaded.EvaluationInputTruncated)
        {
            warnings.Add(new Warning
            {
                Code = WarningCodes.EvalInputTruncated,
                Message = "Aspose.Words evaluation mode truncated the input document while loading it.",
                Hint = "Do not treat this projection or output as complete; apply a license and retry.",
            });
        }

        return warnings.Count == 0 ? null : warnings;
    }

    internal static IReadOnlyList<Warning>? OutputWarnings(LicenseState state, LoadedDocument loaded, string format)
    {
        var extra = new List<Warning>();
        extra.AddRange(InputWarnings(loaded) ?? []);

        if (format is "txt" or "md" or "html" or "html-fixed")
        {
            extra.Add(new Warning { Code = WarningCodes.LossyConversion, Message = $"Conversion to {format} cannot preserve every Word feature.", Hint = "Keep a DOCX copy when styles, headers, fields or revisions matter." });
        }

        if (loaded.Format.HasMacros && format is not "docm" and not "dotm")
        {
            extra.Add(new Warning { Code = WordsDiagnostics.MacrosDropped, Message = "The source contains macros which the target format does not preserve.", Hint = "Convert to docm/dotm to preserve macros." });
        }

        return Combine(EnvelopeParts.OutputWarnings(state), extra);
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
        return Combine(outputWarnings, warnings);
    }

    internal static IReadOnlyList<Warning>? Combine(IReadOnlyList<Warning>? first, IReadOnlyList<Warning>? second)
    {
        if (first is null or { Count: 0 })
        {
            return second is null or { Count: 0 } ? null : second;
        }

        if (second is null or { Count: 0 })
        {
            return first;
        }

        return [.. first, .. second];
    }

    internal static OutputInfo BuildOutput(string path, string format, long size) =>
        new() { Path = path, Format = format, SizeBytes = size };

    internal static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..length] + "\u2026";
}
