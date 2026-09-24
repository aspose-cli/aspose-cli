using System.CommandLine;
using System.CommandLine.Parsing;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Host.Invocation;

/// <summary>Declared parameter values from the actual parser result, including aliases and defaults.</summary>
internal sealed record ParsedParameter(Symbol Symbol, ParameterMetadata Metadata, object? Value);

internal static class ParsedParameterExtensions
{
    public static IEnumerable<ParsedParameter> DeclaredParameters(this ParseResult parse)
    {
        ArgumentNullException.ThrowIfNull(parse);
        return Visit(parse.RootCommandResult);
    }

    private static IEnumerable<ParsedParameter> Visit(CommandResult command)
    {
        foreach (SymbolResult child in command.Children)
        {
            if (child is CommandResult nested)
            {
                foreach (ParsedParameter parameter in Visit(nested)) { yield return parameter; }
            }
            else if (child is ArgumentResult argument)
            {
                yield return new ParsedParameter(argument.Argument,
                    argument.Argument.GetParameterMetadata(), argument.GetValueOrDefault<object?>());
            }
            else if (child is OptionResult option)
            {
                yield return new ParsedParameter(option.Option,
                    option.Option.GetParameterMetadata(), option.GetValueOrDefault<object?>());
            }
        }
    }

    public static IEnumerable<string> TextValues(this ParsedParameter parameter) => parameter.Value switch
    {
        string value => [value],
        string[] values => values,
        _ => [],
    };
}
