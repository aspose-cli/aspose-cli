using System.Globalization;
using Aspose.Cli.Sdk.Errors;

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

    /// <summary>Starts a command from its path, such as <c>product query parts</c>.</summary>
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
        _tokens.Add(CommandLineText.Quote(value));
        return this;
    }

    /// <summary>
    /// Appends a placeholder the caller replaces, such as <c>&lt;that file&gt;</c> for
    /// <paramref name="name"/> <c>that file</c>, as written: lower-case words a hint's reader, and
    /// its check, fill in.
    /// </summary>
    public ContinuationCommand Placeholder(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!name.All(static character => char.IsAsciiLetterLower(character) || character is ' ' or '-'))
        {
            throw new ArgumentException("A placeholder is lower-case words.", nameof(name));
        }

        _tokens.Add($"<{name}>");
        return this;
    }

    /// <summary>Appends an option with a text value.</summary>
    public ContinuationCommand Option(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        _tokens.Add(name);
        _tokens.Add(CommandLineText.Quote(value));
        return this;
    }

    /// <summary>Appends an option with an integer value.</summary>
    public ContinuationCommand Option(string name, long value) =>
        Option(name, value.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Appends an option whose value is a JSON document, such as an operation document, in
    /// single quotes, which a POSIX shell and PowerShell both pass as written, as they do not the
    /// escaped double quotes of any other value; a document that holds a single quote or a
    /// backtick is quoted as any other value is.
    /// </summary>
    public ContinuationCommand Document(string name, string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(json);
        if (json.Contains('\'', StringComparison.Ordinal) || json.Contains('`', StringComparison.Ordinal))
        {
            return Option(name, json);
        }

        _tokens.Add(name);
        _tokens.Add($"'{json}'");
        return this;
    }

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

    /// <summary>Returns the command line, ending with <c>--output-format json</c>.</summary>
    public override string ToString() => string.Join(' ', _tokens.Append(GlobalOptionNames.OutputFormat).Append("json"));

    /// <summary>
    /// The command line as a hint's prose quotes it: in single quotes, or in backticks when it
    /// holds single quotes of its own, as a <see cref="Document"/> value does.
    /// </summary>
    public string Quoted()
    {
        string line = ToString();
        return line.Contains('\'', StringComparison.Ordinal) ? $"`{line}`" : $"'{line}'";
    }

    /// <summary>Command lines a hint runs one after the other, each <see cref="Quoted"/>, as in <c>'a', then 'b'</c>.</summary>
    public static string Steps(IEnumerable<ContinuationCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        return string.Join(", then ", commands.Select(static command => command.Quoted()));
    }
}
