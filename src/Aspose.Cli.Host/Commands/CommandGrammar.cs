using System.CommandLine;
using System.CommandLine.Completions;
using System.Globalization;
using System.Reflection;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Host.Commands;

internal static class CommandGrammar
{
    public static IReadOnlyList<CommandCapabilities> Describe(
        Command root,
        IReadOnlyDictionary<string, ProductCapabilities>? products = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        var result = new List<CommandCapabilities>();
        Visit(root, string.Empty, result, products);
        return result
            .OrderBy(static command => command.Path, StringComparer.Ordinal)
            .ToArray();
    }

    private static void Visit(
        Command command,
        string parent,
        ICollection<CommandCapabilities> result,
        IReadOnlyDictionary<string, ProductCapabilities>? products)
    {
        string path = parent.Length == 0
            ? command.Name
            : parent + " " + command.Name;
        result.Add(new CommandCapabilities
        {
            Path = path,
            Name = command.Name,
            Aliases = command.Aliases.Order(StringComparer.Ordinal).ToArray(),
            Description = command.Description,
            Hidden = command.Hidden,
            Options = command.Options
                .OrderBy(static option => option.Name, StringComparer.Ordinal)
                .Select(option => Describe(option, path, products))
                .ToArray(),
            Arguments = command.Arguments
                .Select(Describe)
                .ToArray(),
        });
        foreach (Command child in command.Subcommands)
        {
            Visit(child, path, result, products);
        }
    }

    private static CommandOptionCapabilities Describe(
        Option option,
        string commandPath,
        IReadOnlyDictionary<string, ProductCapabilities>? products)
    {
        ParameterMetadata metadata = option.GetParameterMetadata();
        bool secret = metadata.Secret;
        return new CommandOptionCapabilities
        {
            Name = option.Name,
            Aliases = option.Aliases.Order(StringComparer.Ordinal).ToArray(),
            Type = TypeName(option.ValueType),
            MinimumArity = option.Arity.MinimumNumberOfValues,
            MaximumArity = option.Arity.MaximumNumberOfValues,
            Required = option.Required,
            Recursive = option.Recursive,
            HasDefault = option.HasDefaultValue,
            Default = option.HasDefaultValue && !secret
                ? ReadDefault(option)
                : null,
            AllowedValues = ReadAllowedValues(
                option,
                commandPath,
                products),
            Secret = secret,
            ValueSource = metadata.ValueSource.ToContractName(),
            InputKind = metadata.InputKind.ToContractName(),
            Description = option.Description,
        };
    }

    private static CommandArgumentCapabilities Describe(Argument argument) =>
        new()
        {
            Name = argument.Name,
            Type = TypeName(argument.ValueType),
            MinimumArity = argument.Arity.MinimumNumberOfValues,
            MaximumArity = argument.Arity.MaximumNumberOfValues,
            Required = argument.Arity.MinimumNumberOfValues > 0,
            HasDefault = argument.HasDefaultValue,
            Default = argument.HasDefaultValue && !argument.GetParameterMetadata().Secret
                ? ReadDefault(argument)
                : null,
            AllowedValues = ReadAllowedValues(argument),
            InputKind = argument.GetParameterMetadata().InputKind.ToContractName(),
            ValueSource = argument.GetParameterMetadata().ValueSource.ToContractName(),
            Secret = argument.GetParameterMetadata().Secret,
            Description = argument.Description,
        };

    private static IReadOnlyList<string> ReadAllowedValues(
        IEnumerable<Func<CompletionContext, IEnumerable<CompletionItem>>> sources)
    {
        var values = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Func<CompletionContext, IEnumerable<CompletionItem>> source
            in sources)
        {
            try
            {
                foreach (CompletionItem item in source(CompletionContext.Empty))
                {
                    if (!string.IsNullOrWhiteSpace(item.InsertText))
                    {
                        values.Add(item.InsertText);
                    }
                }
            }
            catch (Exception)
            {
                // Context-dependent completion is not an allowed-value
                // declaration and is intentionally omitted.
            }
        }
        return values.ToArray();
    }

    private static IReadOnlyList<string> ReadAllowedValues(
        Option option,
        string commandPath,
        IReadOnlyDictionary<string, ProductCapabilities>? products)
    {
        IReadOnlyList<string> declared = ReadOptionCompletions(option);
        if (declared.Count > 0)
        {
            return declared;
        }

        string[] segments = commandPath.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length > 1
            && products is not null
            && products.TryGetValue(
                segments[0],
                out ProductCapabilities? product))
        {
            string verb = segments[^1];
            IReadOnlyList<string>? inferred = option.Name switch
            {
                "--to" when verb == "render" => product.RenderFormats,
                "--to" => product.ConvertFormats,
                "--view" when verb == "preview" => product.Preview?.Views,
                _ => null,
            };
            if (inferred is { Count: > 0 })
            {
                return inferred
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
            }
        }
        return ReadAllowedValues(
            option.CompletionSources.Concat(
                ReadOptionArgumentCompletionSources(option)));
    }

    private static IReadOnlyList<string> ReadOptionCompletions(Option option)
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

    private static IReadOnlyList<string> ReadAllowedValues(Argument argument)
    {
        try
        {
            return argument.GetCompletions(CompletionContext.Empty)
                .Select(static item => item.InsertText)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value!)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception)
        {
            return ReadAllowedValues(argument.CompletionSources);
        }
    }

    private static IEnumerable<
        Func<CompletionContext, IEnumerable<CompletionItem>>>
        ReadOptionArgumentCompletionSources(Option option)
    {
        PropertyInfo? property = typeof(Option).GetProperty(
            "Argument",
            BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.Instance);
        return property?.GetValue(option) is Argument argument
            ? argument.CompletionSources
            : [];
    }

    private static string? ReadDefault(object symbol)
    {
        PropertyInfo? property = symbol.GetType().GetProperty(
            "DefaultValueFactory",
            BindingFlags.Public | BindingFlags.Instance);
        if (property?.GetValue(symbol) is not Delegate factory)
        {
            return null;
        }
        try
        {
            object? value = factory.DynamicInvoke([null]);
            return value switch
            {
                null => null,
                bool boolean => boolean ? "true" : "false",
                IFormattable formattable =>
                    formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString(),
            };
        }
        catch (Exception)
        {
            return "<context-dependent>";
        }
    }

    private static string TypeName(Type type)
    {
        Type effective = Nullable.GetUnderlyingType(type) ?? type;
        if (effective.IsArray)
        {
            return TypeName(effective.GetElementType()!) + "[]";
        }
        return effective == typeof(string)
            ? "string"
            : effective == typeof(bool)
                ? "boolean"
                : effective == typeof(int)
                    ? "integer"
                    : effective == typeof(long)
                        ? "long"
                        : effective == typeof(double)
                            ? "number"
                            : effective.Name;
    }
}
