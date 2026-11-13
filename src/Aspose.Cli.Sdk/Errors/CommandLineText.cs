using System.Text;

namespace Aspose.Cli.Sdk.Errors;

/// <summary>
/// A command line of this executable as text an agent can run verbatim: every token that a POSIX
/// shell, PowerShell or cmd would otherwise split or expand is quoted.
/// </summary>
internal static class CommandLineText
{
    /// <summary>
    /// Leaves a token bare when it holds only characters no common shell treats specially;
    /// otherwise wraps it in double quotes and escapes the characters that stay special there.
    /// </summary>
    internal static string Quote(string value)
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
