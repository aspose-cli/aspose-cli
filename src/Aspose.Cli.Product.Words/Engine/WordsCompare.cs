using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Tables;
using Aspose.Words;
using Aspose.Words.Comparing;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary><c>words compare</c>: Document.Compare of a copy of the original with the changed document, saved as the redline.</summary>
internal static class WordsCompare
{
    internal static WordsCompareResult Run(WordsSession session, WordsCompareRequest request)
    {
        LicenseState state = session.Outputs.License;
        var log = new EngineWarningLog();
        using LoadedDocument left = session.Loader.Open(request.Left, request.LeftPassword, log);
        using LoadedDocument right = session.Loader.Open(request.Right, request.RightPassword, log);
        Document compared = left.Document.Clone();
        var options = new CompareOptions();
        if (request.Granularity is { } granularity)
        {
            options.Granularity = Enum.GetNames<Granularity>().Contains(granularity, StringComparer.Ordinal)
                ? Enum.Parse<Granularity>(granularity)
                : throw CliErrors.OptionInvalid("--granularity", $"'{granularity}' is no member of Granularity; it is one of: {string.Join(", ", Enum.GetNames<Granularity>())}");
        }

        foreach (string ignored in request.Ignore)
        {
            WordsCompareOptions.Set(options, ignored);
        }

        compared.Compare(right.Document, request.Author, request.DateTime, options);
        OutputProtection protection = OutputProtection.For(request.Output.Format, encrypt: null, left.Protection, request.LeftPassword);
        PublishedFile written = session.Outputs.Write(request.Output.Path, request.Output.Format, request.Output.Overwrite, temp =>
        {
            log.During = "save";
            compared.Save(temp, WordsEngineFormats.Options(request.Output.Format.Id, protection.Password));
            left.Resources.ThrowIfFailed();
        }, encrypted: protection.Encrypted);
        return new WordsCompareResult
        {
            Left = FileObjects.Input(request.Left, left.FormatId, left.Protection.Encrypted),
            Right = FileObjects.Input(request.Right, right.FormatId, right.Protection.Encrypted),
            Output = FileObjects.Output(written, request.Output.Format, encrypted: protection.Encrypted),
            RevisionCount = compared.Revisions.Count,
            EngineWarnings = log.Warnings.Count == 0 ? null : log.Warnings,
            License = EnvelopeParts.License(state),
            Warnings = EnvelopeParts.CombineWarnings(left.Warnings, right.Warnings),
        };
    }
}

/// <summary>The CompareOptions members compare sets to true, by the option that names each.</summary>
internal static class WordsCompareOptions
{
    private static IReadOnlyList<(string Option, Action<CompareOptions> Set)> All { get; } =
    [
        ("--ignore-formatting", static o => o.IgnoreFormatting = true),
        ("--ignore-case-changes", static o => o.IgnoreCaseChanges = true),
        ("--ignore-comments", static o => o.IgnoreComments = true),
        ("--ignore-tables", static o => o.IgnoreTables = true),
        ("--ignore-fields", static o => o.IgnoreFields = true),
        ("--ignore-footnotes", static o => o.IgnoreFootnotes = true),
        ("--ignore-textboxes", static o => o.IgnoreTextboxes = true),
        ("--ignore-headers-and-footers", static o => o.IgnoreHeadersAndFooters = true),
    ];

    internal static void Set(CompareOptions options, string option) =>
        All.First(entry => entry.Option == option).Set(options);
}
