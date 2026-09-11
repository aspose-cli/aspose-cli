using System.CommandLine;
using System.Runtime.CompilerServices;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>The input role declared by the symbol's owner.</summary>
public enum InputKind { None, File, JsonSource }

/// <summary>How the symbol supplies its value to the owning operation.</summary>
public enum ParameterValueSource { CommandLine, EnvironmentVariableName, StandardInput }

/// <summary>Immutable semantics attached to one argument or option.</summary>
public sealed record ParameterMetadata(
    InputKind InputKind,
    ParameterValueSource ValueSource = ParameterValueSource.CommandLine,
    bool Secret = false);

/// <summary>Declares and validates parameter roles without interpreting names or tokens.</summary>
public static class ParameterMetadataExtensions
{
    private static readonly ConditionalWeakTable<Symbol, ParameterMetadata> Declarations = new();
    private static readonly ParameterMetadata ScalarDefault = new(InputKind.None);

    public static T WithInput<T>(
        this T symbol,
        InputKind inputKind,
        ParameterValueSource valueSource = ParameterValueSource.CommandLine,
        bool secret = false) where T : Symbol
    {
        ArgumentNullException.ThrowIfNull(symbol);
        var metadata = new ParameterMetadata(inputKind, valueSource, secret);
        Validate(symbol, metadata);
        try
        {
            Declarations.Add(symbol, metadata);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException($"Parameter '{symbol.Name}' is declared more than once.", exception);
        }
        return symbol;
    }

    public static ParameterMetadata GetParameterMetadata(this Symbol symbol)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        if (Declarations.TryGetValue(symbol, out ParameterMetadata? metadata))
        {
            return metadata;
        }
        Type type = ValueType(symbol);
        if (type == typeof(string) || type == typeof(string[]))
        {
            throw new InvalidOperationException($"String parameter '{symbol.Name}' has no input declaration.");
        }
        Type effective = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(void) && symbol is Option { Arity.MaximumNumberOfValues: 0 })
        {
            return ScalarDefault;
        }
        if (effective.IsEnum || Type.GetTypeCode(effective) is
            TypeCode.Boolean or TypeCode.Byte or TypeCode.SByte
            or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32
            or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal)
        {
            return ScalarDefault;
        }
        throw new InvalidOperationException($"Parameter '{symbol.Name}' has no declaration for type '{type.Name}'.");
    }

    public static void ValidateParameters(this Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        foreach (Argument argument in command.Arguments)
        {
            _ = argument.GetParameterMetadata();
        }
        foreach (Option option in command.Options)
        {
            _ = option.GetParameterMetadata();
        }
        foreach (Command child in command.Subcommands)
        {
            child.ValidateParameters();
        }
    }

    private static Type ValueType(Symbol symbol) => symbol switch
    {
        Argument argument => argument.ValueType,
        Option option => option.ValueType,
        _ => throw new ArgumentException("Only arguments and options have parameter semantics.", nameof(symbol)),
    };

    private static void Validate(Symbol symbol, ParameterMetadata metadata)
    {
        Type type = ValueType(symbol);
        bool text = type == typeof(string) || type == typeof(string[]);
        if (!Enum.IsDefined(metadata.InputKind) || !Enum.IsDefined(metadata.ValueSource)
            || (metadata.InputKind == InputKind.File && !text)
            || (metadata.InputKind == InputKind.JsonSource && type != typeof(string))
            || (metadata.ValueSource != ParameterValueSource.CommandLine && metadata.InputKind != InputKind.None)
            || (metadata.ValueSource == ParameterValueSource.EnvironmentVariableName && type != typeof(string))
            || (metadata.ValueSource == ParameterValueSource.StandardInput && type != typeof(bool)))
        {
            throw new InvalidOperationException($"Parameter '{symbol.Name}' has an invalid type or value-source declaration.");
        }
    }

    public static string ToContractName(this InputKind kind) => kind switch
    {
        InputKind.None => "none",
        InputKind.File => "file",
        InputKind.JsonSource => "json-source",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static string ToContractName(this ParameterValueSource source) => source switch
    {
        ParameterValueSource.CommandLine => "command-line",
        ParameterValueSource.EnvironmentVariableName => "environment-variable-name",
        ParameterValueSource.StandardInput => "stdin",
        _ => throw new ArgumentOutOfRangeException(nameof(source)),
    };
}
