using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.Architecture.Tests;
using Aspose.Cli.TestKit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// Building the catalog and the command tree, printing help and describing capabilities are
/// pure: they never load a document engine, so a definition cannot touch an engine type and
/// help stays fast. Each CLI process runs with a startup hook that records every assembly the
/// process holds before its entry point and every one it loads afterwards; the controls show
/// that the hook sees each engine a real command loads.
/// </summary>
[Category(TestCategory.Slow)]
public sealed class EngineAssemblyLoadingTests : IDisposable
{
    private const string LogVariable = "ENGINE_LOAD_PROBE_LOG";

    private static readonly Regex EngineAssembly = new(
        @"^Aspose\.(Pdf|Cells|Words|Slides)(\..+)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private const string HookSource = """
        using System;
        using System.IO;
        using System.Reflection;

        internal static class StartupHook
        {
            private static readonly object Gate = new();

            public static void Initialize()
            {
                string path = Environment.GetEnvironmentVariable("ENGINE_LOAD_PROBE_LOG");
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }
                AppDomain.CurrentDomain.AssemblyLoad += (_, loaded) => Record(path, loaded.LoadedAssembly);
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Record(path, assembly);
                }
            }

            private static void Record(string path, Assembly assembly)
            {
                lock (Gate)
                {
                    File.AppendAllText(path, assembly.GetName().Name + Environment.NewLine);
                }
            }
        }
        """;

    private readonly TempWorkspace _workspace = new();
    private readonly TempDirectory _probe = new();
    private readonly string _hook;
    private int _runs;

    public EngineAssemblyLoadingTests() => _hook = CompileHook(_probe.File("EngineLoadProbe.dll"));

    public void Dispose()
    {
        _workspace.Dispose();
        _probe.Dispose();
    }

    [Fact]
    public void CatalogCommandTreeHelpAndCapabilities_LoadNoEngine()
    {
        var offenders = new List<string>();
        (CliResult capabilities, _) = Probe(offenders, "capabilities", "--output", "json");
        Assert.True(capabilities.ExitCode == 0, capabilities.StdErr);
        JsonNode document = JsonNode.Parse(capabilities.StdOut)!;

        string[][] paths = document["commands"]!.AsArray()
            .Select(static command => command!["path"]!.GetValue<string>()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Skip(1)
                .ToArray())
            .ToArray();
        Assert.Contains(paths, static path => path.Length == 0);
        Assert.Contains(paths, static path => path.Length >= 2);
        foreach (string[] path in paths)
        {
            (CliResult help, _) = Probe(offenders, [.. path, "--help"]);
            Assert.True(help.ExitCode == 0, $"{string.Join(' ', path)} --help exited {help.ExitCode}: {help.StdErr}");
        }

        string[] products = document["products"]!.AsArray()
            .Select(static product => product!["id"]!.GetValue<string>())
            .ToArray();
        Assert.NotEmpty(products);
        Probe(offenders, "capabilities", "--summary", "--output", "json");
        foreach (string product in products)
        {
            (CliResult described, _) = Probe(offenders, "capabilities", product, "--output", "json");
            Assert.True(described.ExitCode == 0, described.StdErr);
        }
        Probe(offenders, "--version", "--output", "json");

        Assert.True(
            offenders.Count == 0,
            "These commands loaded a document engine while only building the catalog, the command tree, "
            + "help or capabilities:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>A command that opens a document loads its engine, and the hook reports it.</summary>
    [Theory]
    [InlineData("Aspose.PDF", new[] { "pdf", "create", "control.pdf", "--from-text", "control.txt" })]
    [InlineData("Aspose.Cells", new[] { "cells", "create", "control.xlsx" })]
    [InlineData("Aspose.Words", new[] { "words", "create", "control.docx", "--blank" })]
    [InlineData("Aspose.Slides", new[] { "slides", "create", "control.pptx" })]
    public void Control_ACommandThatWritesADocumentLoadsItsEngine(string engine, string[] args)
    {
        File.WriteAllText(_workspace.File("control.txt"), "Control");
        var offenders = new List<string>();

        (CliResult result, string[] loaded) = Probe(offenders, [.. args, "--output", "json"]);

        Assert.True(result.ExitCode == 0, $"{string.Join(' ', args)} exited {result.ExitCode}: {result.StdErr}");
        Assert.Contains(engine, loaded, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(offenders, offender => offender.Contains(engine, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Runs one command under the hook and records it in <paramref name="offenders"/> when it
    /// loaded an engine; returns its result and every assembly it held.
    /// </summary>
    private (CliResult Result, string[] Loaded) Probe(List<string> offenders, params string[] args)
    {
        string log = _probe.File($"run-{Interlocked.Increment(ref _runs)}.log");
        string? inherited = Environment.GetEnvironmentVariable("DOTNET_STARTUP_HOOKS");
        var variables = new Dictionary<string, string?>
        {
            ["DOTNET_STARTUP_HOOKS"] = string.IsNullOrEmpty(inherited) ? _hook : inherited + Path.PathSeparator + _hook,
            [LogVariable] = log,
        };

        CliResult result = _workspace.RunWithEnv(variables, args);

        Assert.True(File.Exists(log), $"The startup hook recorded nothing for: {string.Join(' ', args)}");
        string[] loaded = File.ReadAllLines(log)
            .Where(static line => line.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.Contains("Aspose.Cli.Host", loaded);
        string[] engines = loaded.Where(static name => EngineAssembly.IsMatch(name)).Order(StringComparer.Ordinal).ToArray();
        if (engines.Length > 0)
        {
            offenders.Add($"{string.Join(' ', args)}: {string.Join(", ", engines)}");
        }
        return (result, loaded);
    }

    private static string CompileHook(string path)
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            Path.GetFileNameWithoutExtension(path),
            [CSharpSyntaxTree.ParseText(HookSource)],
            RoslynTestSupport.PlatformReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Disable));
        EmitResult result = compilation.Emit(path);
        Assert.True(
            result.Success,
            string.Join(Environment.NewLine, result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
        return path;
    }
}
