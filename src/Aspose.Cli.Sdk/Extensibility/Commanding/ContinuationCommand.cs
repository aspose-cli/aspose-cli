using System.Globalization;
using System.Text;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// Builds the ready-to-run follow-up command a bounded read advertises in its <c>next</c>
/// field. The command names this executable, quotes every value that a POSIX shell,
/// PowerShell or cmd would otherwise split or expand, and always asks for JSON, so an
/// agent can run it verbatim instead of computing the next window itself.
/// </summary>
public sealed class ContinuationCommand
{
    private readonly List<string> _tokens = [DistributionInfo.CommandName];

    /// <summary>Starts a command from its path, such as <c>pdf query pages</c>.</summary>
    public ContinuationCommand(params string[] commandPath)
    {
        ArgumentNullException.ThrowIfNull(commandPath);
        if (commandPath.Length == 0)
        {
            throw new ArgumentException("A continuation needs a command path.", nameof(commandPath));
        }

        foreach (string segment in commandPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(segment);
            _tokens.Add(segment);
        }
    }

    /// <summary>Appends a positional argument.</summary>
    public ContinuationCommand Argument(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _tokens.Add(Quote(value));
        return this;
    }

    /// <summary>Appends an option with a text value.</summary>
    public ContinuationCommand Option(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        _tokens.Add(name);
        _tokens.Add(Quote(value));
        return this;
    }

    /// <summary>Appends an option with an integer value.</summary>
    public ContinuationCommand Option(string name, long value) =>
        Option(name, value.ToString(CultureInfo.InvariantCulture));

    /// <summary>Appends a switch when <paramref name="enabled"/> is true.</summary>
    public ContinuationCommand Flag(string name, bool enabled = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (enabled)
        {
            _tokens.Add(name);
        }

        return this;
    }

    /// <summary>Returns the command line, ending with <c>--output json</c>.</summary>
    public override string ToString() => string.Join(' ', _tokens.Append("--output").Append("json"));

    /// <summary>
    /// Leaves a token bare when it holds only characters no common shell treats specially;
    /// otherwise wraps it in double quotes and escapes the characters that stay special there.
    /// </summary>
    public static string Quote(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > 0 && value.All(IsSafe))
        {
            return value;
        }

        var quoted = new StringBuilder(value.Length + 2).Append('"');
        foreach (char character in value)
        {
            if (character is '"' or '$' or '`')
            {
                quoted.Append('\\');
            }

            quoted.Append(character);
        }

        return quoted.Append('"').ToString();
    }

    private static bool IsSafe(char character) =>
        char.IsAsciiLetterOrDigit(character)
        || character is '-' or '_' or '.' or '/' or ':' or ',' or '+' or '=';
}
