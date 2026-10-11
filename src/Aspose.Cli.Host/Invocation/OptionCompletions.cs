using System.CommandLine;
using System.CommandLine.Completions;

namespace Aspose.Cli.Host.Invocation;

/// <summary>
/// The values an option or argument declares through its completions, read for capabilities,
/// help and the suggestions for a value the parser refused.
/// </summary>
internal static class OptionCompletions
{
    private static readonly string[] BooleanValues = ["false", "true"];

    /// <summary>The option's completion values, in ordinal order; none when they cannot be read.</summary>
    internal static IReadOnlyList<string> Read(Option option)
    {
        // The parser completes a boolean with bool.ToString(); the contract spells it in lowercase.
        if (option.ValueType == typeof(bool) || option.ValueType == typeof(bool?))
        {
            return BooleanValues;
        }

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
