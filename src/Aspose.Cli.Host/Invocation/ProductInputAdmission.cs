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
        foreach (Token token in InputTokens(parseResult.RootCommandResult))
        {
            if (token.Type != TokenType.Argument
                || string.IsNullOrWhiteSpace(token.Value)
                || token.Value == "-"
                || IsInlineDocument(token.Value))
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

    private static IEnumerable<Token> InputTokens(CommandResult command)
    {
        foreach (SymbolResult child in command.Children)
        {
            if (child is CommandResult nested)
            {
                foreach (Token token in InputTokens(nested))
                {
                    yield return token;
                }
                continue;
            }

            if (child is OptionResult option && OutputOptions.Contains(option.Option.Name)
                || child is ArgumentResult argument
                    && command.Command.Name == "create"
                    && ReferenceEquals(command.Command.Arguments.FirstOrDefault(), argument.Argument))
            {
                continue;
            }

            foreach (Token token in child.Tokens)
            {
                yield return token;
            }
        }
    }

    private static bool IsInlineDocument(string value)
    {
        // A Windows drive-qualified path is also a valid absolute URI.
        if (Path.IsPathRooted(value))
        {
            return false;
        }
        string trimmed = value.TrimStart();
        return trimmed.StartsWith('{')
            || trimmed.StartsWith('[')
            || Uri.TryCreate(value, UriKind.Absolute, out _);
    }
}
