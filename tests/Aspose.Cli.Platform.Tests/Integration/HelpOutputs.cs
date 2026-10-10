using System.Collections.Concurrent;
using Aspose.Cli.TestKit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// The <c>--help</c> output of each command path, run once per test process. Help does not
/// depend on the working directory or the tests that read it, and every tree-wide help test
/// reads most of the same paths, so one run of each path serves them all. Paths are relative to
/// the root command; the empty path is the root's own help.
/// </summary>
internal static class HelpOutputs
{
    private static readonly Lazy<TempWorkspace> Workspace = new(static () =>
    {
        var workspace = new TempWorkspace();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => workspace.Dispose();
        return workspace;
    });

    private static readonly ConcurrentDictionary<string, Lazy<CliResult>> Results = new(StringComparer.Ordinal);

    /// <summary>The result of <c>aspose-cli &lt;words&gt; --help</c>.</summary>
    public static CliResult Of(IReadOnlyList<string> words) =>
        Results.GetOrAdd(string.Join(' ', words), _ => new Lazy<CliResult>(() => Workspace.Value.Run([.. words, "--help"]))).Value;

    /// <summary>Runs the help of every path that has not run yet, at the same time.</summary>
    public static void Prefetch(IEnumerable<IReadOnlyList<string>> paths) =>
        Parallel.ForEach(paths, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, static words => Of(words));
}
