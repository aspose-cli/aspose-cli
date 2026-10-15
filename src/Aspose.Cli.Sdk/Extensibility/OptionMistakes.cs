using System.CommandLine;
using System.Runtime.CompilerServices;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>
/// The names an option declares a caller may mistake for it, such as <c>--text</c> for a search's
/// <c>--pattern</c>, and whether a caller may give its value as a bare argument instead. They name
/// no option: the parser refuses them, and the error suggests the option that declares them first.
/// </summary>
public static class OptionMistakes
{
    private static readonly ConditionalWeakTable<Option, Declaration> Declared = [];

    /// <summary>
    /// Declares the option names <paramref name="option"/> is mistaken for, each written with its
    /// dashes, such as <c>--text</c>.
    /// </summary>
    /// <param name="option">The option a caller means.</param>
    /// <param name="names">The option names written for it.</param>
    public static T MistakenFor<T>(this T option, params string[] names) where T : Option
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(names);
        foreach (string name in names)
        {
            if (string.IsNullOrWhiteSpace(name) || !name.StartsWith('-') || name == option.Name || option.Aliases.Contains(name))
            {
                throw new InvalidOperationException($"Option '{option.Name}' is mistaken for '{name}', which is blank, not an option name or its own name.");
            }
        }

        Declaration current = Declared.TryGetValue(option, out Declaration? declared) ? declared : new([], false);
        Declared.AddOrUpdate(option, current with { Names = [.. current.Names, .. names] });
        return option;
    }

    /// <summary>
    /// Declares that a caller may give the value of the required <paramref name="option"/> as a
    /// bare argument, such as <c>search deck.pptx speaker</c> for <c>--pattern speaker</c>: a
    /// stray argument beside the missing option then suggests it.
    /// </summary>
    /// <param name="option">The required option a bare argument stands for.</param>
    public static T MistakenForArgument<T>(this T option) where T : Option
    {
        ArgumentNullException.ThrowIfNull(option);
        if (!option.Required)
        {
            throw new InvalidOperationException($"Option '{option.Name}' is not required, so a stray argument cannot stand for it.");
        }

        Declaration current = Declared.TryGetValue(option, out Declaration? declared) ? declared : new([], false);
        Declared.AddOrUpdate(option, current with { Argument = true });
        return option;
    }

    /// <summary>The option names a caller may mistake for <paramref name="option"/>; empty when it declares none.</summary>
    /// <param name="option">The option to inspect.</param>
    public static IReadOnlyList<string> MistakenNames(this Option option)
    {
        ArgumentNullException.ThrowIfNull(option);
        return Declared.TryGetValue(option, out Declaration? declared) ? declared.Names : [];
    }

    /// <summary>Whether a caller may give the value of <paramref name="option"/> as a bare argument.</summary>
    /// <param name="option">The option to inspect.</param>
    public static bool IsMistakenForArgument(this Option option)
    {
        ArgumentNullException.ThrowIfNull(option);
        return Declared.TryGetValue(option, out Declaration? declared) && declared.Argument;
    }

    private sealed record Declaration(IReadOnlyList<string> Names, bool Argument);
}
