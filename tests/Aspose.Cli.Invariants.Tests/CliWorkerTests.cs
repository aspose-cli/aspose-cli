using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>
/// The invariants run their command lines in warm CLI workers (<see cref="CliWorkers"/>); set
/// <c>ASPOSE_CLI_TEST_WORKERS=0</c> to run each as a child process instead.
/// </summary>
internal static class CliWorkerMode
{
    [ModuleInitializer]
    internal static void Enable()
    {
        // This assembly is also a startup hook of the CLI in a test (HookProbe), where the test kit
        // cannot load; only the test process runs workers.
        if (Assembly.GetEntryAssembly()?.GetName().Name != Aspose.Cli.Sdk.DistributionInfo.CommandName)
        {
            EnableWorkers();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void EnableWorkers() => CliWorkers.Enable();
}

/// <summary>
/// A worker runs a command line as a child process does: the same exit code, stdout and stderr
/// for each product's commands, their dry runs, errors and help, an encrypted input and an
/// evaluation workspace; and a real child process keeps the result on stdout and diagnostics on
/// stderr.
/// </summary>
public sealed class CliWorkerTests
{
    private const string SecretVariable = "INVARIANT_SECRET";

    public static TheoryData<string, bool> Lanes()
    {
        var data = new TheoryData<string, bool>();
        foreach (string product in ScenarioFixtures.Products)
        {
            data.Add(product, true);
            data.Add(product, false);
        }
        return data;
    }

    /// <summary>
    /// Each of a product's commands, in a workspace with the run's project license or in one
    /// without a license, runs in a worker with the exit code, stdout and stderr of a child
    /// process, once the workspaces' paths are named alike. Text is compared exactly; in a JSON
    /// document only a value that two child processes write differently, such as the fingerprint
    /// of a document that records when it was saved, may differ, and the size and fingerprint of
    /// an output whose content is the child process's in other bytes, such as an xlsx whose
    /// package relationships Aspose.Cells wrote in another order.
    /// </summary>
    [Theory]
    [Category(TestCategory.Slow)]
    [MemberData(nameof(Lanes))]
    public void AWorkerRun_MatchesAChildProcessRun(string product, bool projectLicense)
    {
        Assert.SkipUnless(CliWorkers.Enabled, $"{CliWorkers.CountVariable}=0 runs every command as a child process.");
        (string Name, Invocation Invocation)[] cases = [.. Cases(product, projectLicense)];
        var differences = new List<string>();
        foreach ((string name, Invocation invocation) in cases)
        {
            Run worker = Execute(invocation, projectLicense);
            Assert.True(worker.InWorker, $"{name} did not run in a worker.");
            Run child;
            Run again;
            using (CliWorkers.ChildProcesses())
            {
                child = Execute(invocation, projectLicense);
                if (child.StdOut.Contains("\"output", StringComparison.Ordinal))
                {
                    // A document records when it was saved, to the second: a second apart, two
                    // saves show which values of a result follow from that.
                    Thread.Sleep(TimeSpan.FromSeconds(1.1));
                }
                again = Execute(invocation, projectLicense);
            }
            Assert.False(child.InWorker || again.InWorker, $"{name} ran in a worker, not a child process.");
            if (!Matches(worker, child, again))
            {
                differences.Add($"{name}:{Environment.NewLine}worker: {worker}{Environment.NewLine}child:  {child}");
            }
        }
        TestContext.Current.TestOutputHelper?.WriteLine($"{cases.Length} {product} cases, {(projectLicense ? "project license" : "no license")}, {differences.Count} differ.");
        Assert.True(cases.Length >= (projectLicense ? 30 : 8), $"Only {cases.Length} cases for {product}.");
        Assert.True(differences.Count == 0, $"{differences.Count} of {cases.Length} runs differ:{Environment.NewLine}" + string.Join(Environment.NewLine, differences));
    }

    /// <summary>Whether a worker run matches two child process runs of the same invocation.</summary>
    private static bool Matches(Run worker, Run child, Run again)
    {
        string? Content(JsonNode? fingerprint) =>
            fingerprint is JsonObject value && value["sha256"] is JsonValue sha256 && sha256.TryGetValue(out string? hash)
                ? new[] { worker, child, again }.Select(run => run.Contents.GetValueOrDefault(hash)).FirstOrDefault(static content => content is not null)
                : null;
        bool SameContent(JsonNode? one, JsonNode? other) =>
            !JsonNode.DeepEquals(one, other) && Content(one) is { } content && content == Content(other);
        return worker.ExitCode == child.ExitCode
            && Same(worker.StdOut, child.StdOut, again.StdOut, SameContent)
            && Same(worker.StdErr, child.StdErr, again.StdErr, SameContent);
    }

    /// <summary>
    /// Whether a worker wrote what a child process wrote: the same text; or, where two child
    /// processes wrote different JSON documents, the same document except at the values that
    /// differ between the two; or the same text except for the size and fingerprint of a file
    /// with the same content as the child process's file.
    /// </summary>
    private static bool Same(string worker, string child, string again, Func<JsonNode?, JsonNode?, bool> sameContent)
    {
        if (worker == child && child == again)
        {
            return true;
        }
        try
        {
            return Same(JsonNode.Parse(worker), JsonNode.Parse(child), JsonNode.Parse(again), sameContent)
                && (child != again || Shape(worker) == Shape(child));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>A JSON text without its numbers and SHA-256 values, which the documents compare.</summary>
    private static string Shape(string json) => Regex.Replace(json, "[0-9a-f]{64}|[0-9]+", "#");

    private static bool Same(JsonNode? worker, JsonNode? child, JsonNode? again, Func<JsonNode?, JsonNode?, bool> sameContent)
    {
        if (JsonNode.DeepEquals(child, again) && JsonNode.DeepEquals(worker, child))
        {
            return true;
        }
        if ((worker, child, again) is (JsonObject file, JsonObject first, JsonObject second)
            && (!JsonNode.DeepEquals(first["fingerprint"], second["fingerprint"]) || sameContent(file["fingerprint"], first["fingerprint"])))
        {
            // A file saved at another time, or with the same content in other bytes: its size goes
            // with its fingerprint, even when two saves happen to have the same size.
            return Same(Without(file, FileVolatiles), Without(first, FileVolatiles), Without(second, FileVolatiles), sameContent)
                && FileVolatiles.All(key => file[key]?.GetValueKind() == first[key]?.GetValueKind());
        }
        return (worker, child, again) switch
        {
            (JsonObject w, JsonObject c, JsonObject a) =>
                c.Select(static pair => pair.Key).Order(StringComparer.Ordinal).SequenceEqual(a.Select(static pair => pair.Key).Order(StringComparer.Ordinal))
                && c.Select(static pair => pair.Key).Order(StringComparer.Ordinal).SequenceEqual(w.Select(static pair => pair.Key).Order(StringComparer.Ordinal))
                && c.All(pair => Same(w[pair.Key], pair.Value, a[pair.Key], sameContent)),
            (JsonArray w, JsonArray c, JsonArray a) =>
                c.Count == a.Count && c.Count == w.Count && c.Select((item, index) => Same(w[index], item, a[index], sameContent)).All(static same => same),
            (JsonValue w, JsonValue c, JsonValue a) =>
                !JsonNode.DeepEquals(c, a) && w.GetValueKind() == c.GetValueKind() && c.GetValueKind() == a.GetValueKind(),
            _ => false,
        };
    }

    /// <summary>
    /// The cases of a product: each command's smallest valid invocation; with a project license
    /// also its dry run, an unknown option, a missing input, its help, and its encrypted input
    /// opened with the right and a wrong password.
    /// </summary>
    private static IEnumerable<(string Name, Invocation Invocation)> Cases(string product, bool projectLicense)
    {
        string extension = ScenarioFixtures.PrimaryFormat(product);
        foreach (CliCommand command in CliCatalog.Current.Commands.Where(command => command.Product == product && command.Arguments.Count + command.Options.Count > 0))
        {
            Invocation invocation;
            try
            {
                invocation = Invocation.For(command);
            }
            catch (InvalidOperationException)
            {
                continue;
            }
            yield return ($"{command}", invocation);
            if (!projectLicense)
            {
                continue;
            }
            if (command.Has("--dry-run"))
            {
                yield return ($"{command} --dry-run", Invocation.For(command).Add("--dry-run"));
            }
            yield return ($"{command} --no-such-option", Invocation.For(command).Add("--no-such-option"));
            if (invocation.Input is { } input)
            {
                Invocation missing = Invocation.For(command);
                missing.Replace(input, "missing." + extension);
                yield return ($"{command} missing input", missing);
            }
            yield return ($"{command} --help", new Invocation([.. command.Tokens, "--help"]));
            if (command.Verb == "inspect" && command.Has("--password-env"))
            {
                foreach ((string label, string password) in new[] { ("password", ScenarioFixtures.Password), ("wrong password", "not-the-password") })
                {
                    var secret = new Invocation([.. command.Tokens, "secret." + extension, "--password-env", SecretVariable]);
                    secret.Files["secret." + extension] = new ScenarioFile { Fixture = $"{product}.encrypted" };
                    secret.Env[SecretVariable] = password;
                    yield return ($"{command} encrypted input, {label}", secret);
                }
            }
        }
    }

    /// <summary>The members of a file object that follow from when the file was saved.</summary>
    private static readonly string[] FileVolatiles = ["sizeBytes", "fingerprint"];

    private static JsonObject Without(JsonObject value, string[] keys)
    {
        var copy = (JsonObject)value.DeepClone();
        foreach (string key in keys)
        {
            copy.Remove(key);
        }
        return copy;
    }

    /// <summary>
    /// What one run wrote, with the workspace paths named, whether a worker ran it, and the
    /// content of each file its workspace held afterwards, by the file's SHA-256.
    /// </summary>
    private sealed record Run(int ExitCode, string StdOut, string StdErr, bool InWorker, IReadOnlyDictionary<string, string> Contents)
    {
        public override string ToString() =>
            $"exit {ExitCode}; stdout {JsonSerializer.Serialize(StdOut)}; stderr {JsonSerializer.Serialize(StdErr)}";
    }

    /// <summary>Runs an invocation with JSON output in a new workspace.</summary>
    private static Run Execute(Invocation invocation, bool projectLicense)
    {
        using var workspace = new TempWorkspace();
        if (projectLicense)
        {
            ScenarioLicense.Project(workspace.Path);
        }
        foreach ((string relative, ScenarioFile file) in invocation.Files)
        {
            string target = workspace.File(relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllBytes(target, file.Fixture is { } fixture ? ScenarioFixtures.Read(fixture) : Encoding.UTF8.GetBytes(file.Text!));
        }
        string config = Path.GetDirectoryName(workspace.ConfigDirectory)!;
        string[] args = invocation.Args.Contains("--help") ? [.. invocation.Args] : [.. invocation.Args, "--output-format", "json"];
        var process = new CliProcess(CliRunner.ExecutablePath, CliEnvironment.Evaluation(config, invocation.Env.ToDictionary(static pair => pair.Key, static pair => (string?)pair.Value)));
        int before = CliWorkers.ThreadRuns;
        CliResult result = process.Run(workspace.Path, string.Empty, args);
        bool inWorker = CliWorkers.ThreadRuns != before;
        string Named(string text) => text
            .Replace(workspace.Path.Replace("\\", "\\\\", StringComparison.Ordinal), "<workspace>", StringComparison.OrdinalIgnoreCase)
            .Replace(workspace.Path, "<workspace>", StringComparison.OrdinalIgnoreCase)
            .Replace(config.Replace("\\", "\\\\", StringComparison.Ordinal), "<config>", StringComparison.OrdinalIgnoreCase)
            .Replace(config, "<config>", StringComparison.OrdinalIgnoreCase);
        var contents = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string path in Directory.EnumerateFiles(workspace.Path, "*", SearchOption.AllDirectories))
        {
            byte[] bytes = File.ReadAllBytes(path);
            contents[Convert.ToHexStringLower(SHA256.HashData(bytes))] = Content(bytes);
        }
        return new Run(result.ExitCode, Named(result.StdOut), Named(result.StdErr), inWorker, contents);
    }

    /// <summary>
    /// The content of a file: its bytes, or, for a ZIP package, its entries in their order with
    /// the relationships of each relationship part in the order of their ids, which a package
    /// does not give a meaning.
    /// </summary>
    private static string Content(byte[] bytes)
    {
        var content = new StringBuilder();
        try
        {
            using var package = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            foreach (ZipArchiveEntry entry in package.Entries)
            {
                using Stream stream = entry.Open();
                content.Append(entry.FullName).Append('\n');
                if (entry.FullName.EndsWith(".rels", StringComparison.Ordinal))
                {
                    XDocument relationships = XDocument.Load(stream);
                    content.Append(relationships.Declaration).Append(relationships.Root!.Name)
                        .AppendJoin(string.Empty, relationships.Root.Attributes())
                        .AppendJoin(string.Empty, relationships.Root.Nodes().OrderBy(static node => (string?)(node as XElement)?.Attribute("Id"), StringComparer.Ordinal));
                }
                else
                {
                    content.Append(Convert.ToHexStringLower(SHA256.HashData(stream)));
                }
                content.Append('\n');
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or System.Xml.XmlException)
        {
            return Convert.ToHexStringLower(SHA256.HashData(bytes));
        }
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content.ToString())));
    }

    /// <summary>
    /// A worker's output whose package relationships are in another order than the child
    /// processes' matches, though its fingerprint and size differ and the two child processes
    /// happened to write the same bytes: Aspose.Cells orders the relationships of an xlsx anew on
    /// every save.
    /// </summary>
    [Fact]
    public void AWorkerOutput_WithReorderedRelationships_Matches()
    {
        Run child = Sample(Output(7092, Hash('a')), (Hash('a'), "book"));

        Assert.True(Matches(Sample(Output(7096, Hash('b')), (Hash('b'), "book")), child, child));
    }

    /// <summary>
    /// A worker run that differs from two equal child process runs in its exit code, the size,
    /// format or content of its output, or its text does not match.
    /// </summary>
    [Fact]
    public void AWorkerRun_ThatDiffers_DoesNotMatch()
    {
        Run child = Sample(Output(7092, Hash('a')), (Hash('a'), "book"));

        Assert.True(Matches(child, child, child));
        Assert.False(Matches(child with { ExitCode = 1 }, child, child));
        Assert.False(Matches(Sample(Output(7096, Hash('a')), (Hash('a'), "book")), child, child));
        Assert.False(Matches(Sample(Output(7092, Hash('b')), (Hash('b'), "another book")), child, child));
        Assert.False(Matches(Sample(Output(7092, Hash('b'))), child, child));
        Assert.False(Matches(Sample(Output(7096, Hash('b'), "xlsm"), (Hash('b'), "book")), child, child));
        Assert.False(Matches(Sample(Output(7096, Hash('b')).Replace(",", ", ", StringComparison.Ordinal), (Hash('b'), "book")), child, child));
        Assert.False(Matches(child with { StdErr = "warning" }, child, child));
    }

    /// <summary>
    /// Where two child processes wrote different outputs, a worker's output may differ from them
    /// only in its size and fingerprint.
    /// </summary>
    [Fact]
    public void AWorkerOutput_SavedAtAnotherTime_DiffersOnlyInItsSizeAndFingerprint()
    {
        Run child = Sample(Output(7092, Hash('a')), (Hash('a'), "book at 1"));
        Run again = Sample(Output(7093, Hash('c')), (Hash('c'), "book at 2"));

        Assert.True(Matches(Sample(Output(7094, Hash('d')), (Hash('d'), "book at 3")), child, again));
        Assert.False(Matches(Sample(Output(7094, Hash('d'), "xlsm"), (Hash('d'), "book at 3")), child, again));
    }

    private static string Output(long sizeBytes, string sha256, string format = "xlsx") =>
        $$$"""{"outputs":[{"path":"<workspace>/out.xlsx","format":"{{{format}}}","sizeBytes":{{{sizeBytes}}},"fingerprint":{"sha256":"{{{sha256}}}"}}]}""";

    private static string Hash(char character) => new(character, 64);

    private static Run Sample(string stdOut, params (string Sha256, string Content)[] contents) =>
        new(0, stdOut, string.Empty, false, contents.ToDictionary(static pair => pair.Sha256, static pair => pair.Content));

    /// <summary>
    /// A worker keeps the startup hooks of the run, such as the culture hook of
    /// <c>scripts/test.ps1 -CiLike</c>: the hook runs in the worker, the culture it sets holds in
    /// the commands and after them, and a command, with the processes it starts, sees the run's
    /// hooks variable unchanged.
    /// </summary>
    [Fact]
    public void AWorker_KeepsTheStartupHooksOfTheRun()
    {
        using var directory = new TempDirectory();
        string probe = directory.File("probe.txt");
        var variables = new Dictionary<string, string?>
        {
            ["DOTNET_STARTUP_HOOKS"] = typeof(HookProbe).Assembly.Location,
            [HookProbe.FileVariable] = probe,
        };
        var environment = new ProcessStartInfo().Environment;
        CliEnvironment.Evaluation(directory.Path, variables).Apply(environment);
        using (CliWorker worker = CliWorker.Start(CliRunner.ExecutablePath, "probe", variables))
        {
            Assert.Equal(0, worker.Run(directory.Path, ["--version"], environment, TimeSpan.FromSeconds(120)).ExitCode);
            foreach (string missing in new[] { "missing.docx", "absent.docx" })
            {
                Assert.NotEqual(0, worker.Run(directory.Path, ["words", "inspect", missing, "--output-format", "json"], environment, TimeSpan.FromSeconds(120)).ExitCode);
            }
        }

        Assert.True(File.Exists(probe), "The run's startup hook did not run in the worker.");
        string[] lines = File.ReadAllLines(probe);
        string expected = $" {HookProbe.Culture} {HookProbe.Culture} {typeof(HookProbe).Assembly.Location}";
        Assert.Contains(lines, static line => line.StartsWith("load ", StringComparison.Ordinal));
        Assert.True(lines.Count(static line => line.StartsWith("error ", StringComparison.Ordinal)) >= 2, string.Join(Environment.NewLine, lines));
        Assert.All(lines, line => Assert.EndsWith(expected, line, StringComparison.Ordinal));
    }

    /// <summary>A real child process that fails writes nothing to stdout and one error envelope to stderr.</summary>
    [Fact]
    public void AFailedChildProcess_WritesOnlyTheErrorEnvelopeToStderr()
    {
        using var workspace = new TempWorkspace();
        using (CliWorkers.ChildProcesses())
        {
            int before = CliWorkers.ThreadRuns;
            CliResult result = workspace.Run("cells", "inspect", "missing.xlsx", "--output-format", "json");

            Assert.Equal(before, CliWorkers.ThreadRuns);
            Assert.NotEqual(0, result.ExitCode);
            Assert.Equal(string.Empty, result.StdOut);
            Assert.Equal("FILE_NOT_FOUND", JsonNode.Parse(result.StdErr)!["error"]!["code"]!.GetValue<string>());
        }
    }

    /// <summary>
    /// A real child process that succeeds writes its one result document to stdout and only
    /// diagnostics to stderr: nothing with <c>--quiet</c>, and JSON lines of diagnostics with
    /// <c>--verbose</c>.
    /// </summary>
    [Fact]
    public void ASucceededChildProcess_WritesTheResultToStdoutAndOnlyDiagnosticsToStderr()
    {
        using var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);
        File.WriteAllBytes(workspace.File("book.xlsx"), ScenarioFixtures.Read("cells.xlsx"));
        using (CliWorkers.ChildProcesses())
        {
            int before = CliWorkers.ThreadRuns;
            CliResult quiet = workspace.Run("cells", "inspect", "book.xlsx", "--quiet", "--output-format", "json");
            CliResult verbose = workspace.Run("cells", "inspect", "book.xlsx", "--verbose", "--output-format", "json");

            Assert.Equal(before, CliWorkers.ThreadRuns);
            Assert.Equal(0, quiet.ExitCode);
            Assert.NotNull(JsonNode.Parse(quiet.StdOut)!["schema"]);
            Assert.Equal(string.Empty, quiet.StdErr);
            Assert.Equal(0, verbose.ExitCode);
            Assert.Equal(JsonNode.Parse(quiet.StdOut)!["schema"]!.GetValue<string>(), JsonNode.Parse(verbose.StdOut)!["schema"]!.GetValue<string>());
            string[] diagnostics = verbose.StdErr.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Assert.NotEmpty(diagnostics);
            Assert.All(diagnostics, static line => Assert.Null(JsonNode.Parse(line)!["schema"]));
        }
    }
}
