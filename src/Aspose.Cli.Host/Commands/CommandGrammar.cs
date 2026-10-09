using System.CommandLine;
using System.Globalization;
using System.Reflection;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Host.Commands;

/// <summary>A described command and the product whose command root it sits under, if any.</summary>
/// <param name="Command">The command as capabilities lists it, with its path from the executable.</param>
/// <param name="Product">
/// The product whose command root declares this command's owner where the tree is assembled;
/// <see langword="null"/> for a host command.
/// </param>
internal sealed record OwnedCommand(CommandCapabilities Command, string? Product);

internal static class CommandGrammar
{
    /// <summary>
    /// Describes every command under <paramref name="root"/>, in ordinal path order, with the
    /// product that owns it: the one a command root names in its invocation policy.
    /// </summary>
    public static IReadOnlyList<OwnedCommand> Describe(Command root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var result = new List<OwnedCommand>();
        Visit(root, string.Empty, owner: null, result);
        return result
            .OrderBy(static command => command.Command.Path, StringComparer.Ordinal)
            .ToArray();
    }

    private static void Visit(
        Command command,
        string parent,
        string? owner,
        ICollection<OwnedCommand> result)
    {
        string path = parent.Length == 0
            ? command.Name
            : parent + " " + command.Name;
        owner = command.Policy().ProductId ?? owner;
        result.Add(new OwnedCommand(new CommandCapabilities
        {
            Path = path,
            Name = command.Name,
            Aliases = command.Aliases.Order(StringComparer.Ordinal).ToArray(),
            Description = command.Description,
            Hidden = command.Hidden,
            Options = command.Options
                .OrderBy(static option => option.Name, StringComparer.Ordinal)
                .Select(Describe)
                .ToArray(),
            Arguments = command.Arguments
                .Select(Describe)
                .ToArray(),
        }, owner));
        foreach (Command child in command.Subcommands)
        {
            Visit(child, path, owner, result);
        }
    }

    private static CommandOptionCapabilities Describe(Option option)
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
            Hidden = option.Hidden,
            HasDefault = option.HasDefaultValue,
            Default = option.HasDefaultValue && !secret
                ? ReadDefault(option)
                : null,
            AllowedValues = OptionCompletions.Read(option),
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
            AllowedValues = OptionCompletions.Read(argument),
            InputKind = argument.GetParameterMetadata().InputKind.ToContractName(),
            ValueSource = argument.GetParameterMetadata().ValueSource.ToContractName(),
            Secret = argument.GetParameterMetadata().Secret,
            Description = argument.Description,
        };

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
