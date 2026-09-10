using System.CommandLine;
using System.CommandLine.Parsing;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Host.Invocation;

/// <summary>
/// Conservative pre-runtime admission of existing user-controlled paths.
/// Engine loaders repeat the check when they open the file, closing the
/// metadata-to-load growth window through the shared invocation ledger.
/// </summary>
internal static class ProductInputAdmission
{
    private static readonly HashSet<string> OutputOptions =
        new(StringComparer.Ordinal)
        {
            "--out",
            "-o",
            "--out-dir",
            "--output-dir",
            "--backup",
            "--verify-dir",
            "--log",
        };

    public static void Admit(
        ParseResult parseResult,
        GlobalValues globals,
        ResourceBudgetLedger budgets)
    {
        ArgumentNullException.ThrowIfNull(parseResult);
        ArgumentNullException.ThrowIfNull(globals);
        ArgumentNullException.ThrowIfNull(budgets);

        string workDirectory = globals.WorkDir is null
            ? Directory.GetCurrentDirectory()
            : Path.GetFullPath(globals.WorkDir);
        IReadOnlyList<Token> tokens = parseResult.Tokens;
        bool firstPositionalIsOutput = string.Equals(
            parseResult.CommandResult.Command.Name,
            "new",
            StringComparison.Ordinal);
        int positionalIndex = 0;
        for (int index = 0; index < tokens.Count; index++)
        {
            Token token = tokens[index];
            if (token.Type != TokenType.Argument
                || string.IsNullOrWhiteSpace(token.Value)
                || token.Value == "-"
                || IsInlineDocument(token.Value)
                || IsOutputValue(tokens, index))
            {
                continue;
            }

            if (firstPositionalIsOutput
                && !IsOutputValue(tokens, index)
                && positionalIndex++ == 0)
            {
                continue;
            }

            string full;
            try
            {
                full = Path.GetFullPath(token.Value, workDirectory);
            }
            catch (Exception exception) when (
                exception is ArgumentException
                    or NotSupportedException
                    or PathTooLongException)
            {
                continue;
            }

            if (File.Exists(full))
            {
                budgets.AdmitFile(full);
            }
        }
    }

    private static bool IsOutputValue(
        IReadOnlyList<Token> tokens,
        int index) =>
        index > 0
        && tokens[index - 1].Type == TokenType.Option
        && OutputOptions.Contains(tokens[index - 1].Value);

    private static bool IsInlineDocument(string value)
    {
        string trimmed = value.TrimStart();
        return trimmed.StartsWith('{')
            || trimmed.StartsWith('[')
            || Uri.TryCreate(value, UriKind.Absolute, out _);
    }
}
