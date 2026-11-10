using System.CommandLine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Paths;

namespace Aspose.Cli.Host.Invocation;

/// <summary>Admits only declared file sources before product activation.</summary>
internal static class ProductInputAdmission
{
    public static void Admit(ParseResult parseResult, GlobalValues globals, ResourceBudgetLedger budgets)
    {
        ArgumentNullException.ThrowIfNull(parseResult);
        ArgumentNullException.ThrowIfNull(globals);
        ArgumentNullException.ThrowIfNull(budgets);
        var paths = new PathResolver(globals.WorkDir ?? Directory.GetCurrentDirectory());
        foreach (ParsedParameter parameter in parseResult.DeclaredParameters())
        {
            if (parameter.Metadata.InputKind == InputKind.None)
            {
                continue;
            }
            foreach (string value in parameter.TextValues())
            {
                if (parameter.Metadata.InputKind == InputKind.JsonSource
                    && JsonInputSource.Classify(value, parameter.Symbol.Name) != JsonSourceKind.File)
                {
                    continue;
                }
                try
                {
                    budgets.AdmitFile(paths.ResolveInput(value));
                }
                catch (CliException missing)
                    when (missing.Code == ErrorCodes.FileNotFound && parameter.Metadata.InputKind == InputKind.JsonSource)
                {
                    // The missing file is the option's JSON document, not an input.
                    throw JsonInputSource.NotFound(paths.ResolveOutput(value), parameter.Symbol.Name);
                }
            }
        }
    }
}
