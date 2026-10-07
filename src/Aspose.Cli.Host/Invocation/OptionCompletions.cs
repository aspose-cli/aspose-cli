using System.CommandLine;
using System.CommandLine.Completions;

namespace Aspose.Cli.Host.Invocation;

/// <summary>
/// The values an option or argument declares through its completions, read for capabilities,
/// help and the suggestions for a value the parser refused.
/// </summary>
internal static class OptionCompletions
{
    /// <summary>The option's completion values, in ordinal order; none when they cannot be read.</summary>
    internal static IReadOnlyList<string> Read(Option option)
    {
        try
        {
            return Values(option.GetCompletions(CompletionContext.Empty));
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>The argument's completion values, in ordinal order; none when they cannot be read.</summary>
    internal static IReadOnlyList<string> Read(Argument argument)
    {
        try
        {
            return Values(argument.GetCompletions(CompletionContext.Empty));
        }
        catch (Exception)
        {
            return Values(argument.CompletionSources.SelectMany(static source =>
            {
                try
                {
                    return source(CompletionContext.Empty).ToArray();
                }
                catch (Exception)
                {
                    // Context-dependent completion is not an allowed-value declaration.
                    return [];
                }
            }));
        }
    }

    private static string[] Values(IEnumerable<CompletionItem> items) =>
        [.. items
            .Select(static item => item.InsertText)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
}
