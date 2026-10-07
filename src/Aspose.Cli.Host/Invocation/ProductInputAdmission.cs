using System.CommandLine;
using Aspose.Cli.Sdk.IO;

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
                budgets.AdmitFile(paths.ResolveInput(value));
            }
        }
    }
}
