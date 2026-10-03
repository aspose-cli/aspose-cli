using System.CommandLine;
using System.CommandLine.Completions;

namespace Aspose.Cli.Host.Invocation;

/// <summary>The values an option declares through its completions, read once for capabilities and help.</summary>
internal static class OptionCompletions
{
    /// <summary>The option's completion values, in ordinal order; none when they cannot be read.</summary>
    internal static IReadOnlyList<string> Read(Option option)
    {
        try
        {
            return option.GetCompletions(CompletionContext.Empty)
                .Select(static item => item.InsertText)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value!)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception)
        {
            return [];
        }
    }
}
