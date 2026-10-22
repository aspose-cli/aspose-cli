using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Invariants.Tests.Ownership;

/// <summary>What an ownership case points a command's output at.</summary>
internal enum OwnershipCheck
{
    /// <summary>The input itself, by the same path: the input file for <c>--out</c>, its folder for <c>--out-dir</c>.</summary>
    Same,

    /// <summary>The input by the same path in other letter case.</summary>
    Case,

    /// <summary>A hard link to the input file (files only: a folder has no hard links).</summary>
    HardLink,

    /// <summary>The input through a junction to its folder.</summary>
    Junction,

    /// <summary>The input by its 8.3 short name.</summary>
    ShortName,

    /// <summary>A folder that already holds a file the command never wrote; without <c>--overwrite</c>.</summary>
    Sentinel,

    /// <summary>
    /// The same folder that also holds a file of each name the command writes, so it looks like an
    /// earlier output; with <c>--overwrite</c>.
    /// </summary>
    SentinelOverwrite,

    /// <summary>
    /// A folder output over a folder that looks like an earlier output and holds the input, which
    /// the command reads through a junction to that folder; with <c>--overwrite</c>.
    /// </summary>
    InputInside,
}

/// <summary>
/// One ownership case: a command that writes files, in one variant (a value of its required
/// choice, such as <c>--what attachments</c>), checked one way.
/// </summary>
internal sealed record OwnershipCase(string Id, CliCommand Command, string? VariantOption, string? Variant, string Output, OwnershipCheck Check);

/// <summary>
/// The publication-ownership matrix (SECURITY.md, Files: no command writes over its own input
/// unless asked to edit in place, and publication replaces only what a command owns), generated
/// from the live <c>capabilities</c> for every command that writes by <c>--out</c>,
/// <c>--out-dir</c> or <c>--in-place</c>. A case points the output at the input through each alias
/// the file system has, or into a folder holding a file the command never wrote, and asserts that
/// the input and that file are unchanged afterwards, whatever the command answers.
/// </summary>
internal static class OwnershipCases
{
    /// <summary>The folder of the inputs; its space gives it an 8.3 short name where the volume makes them.</summary>
    public const string InputFolder = "input folder";

    /// <summary>The input file's stem for <see cref="OwnershipCheck.ShortName"/>, long enough to have an 8.3 short name.</summary>
    private const string LongStem = "input document";

    private const string SentinelName = "sentinel.txt";
    private const string SentinelText = "Owned by the caller, never by aspose-cli.";
    private const string Foreign = "A file aspose-cli did not write.";

    /// <summary>
    /// The unmarked sample that every run checks: a converter writing its own format, an extraction
    /// of attachments and the review evidence folder, each through a junction, by the same path and
    /// into a folder that looks like an earlier output, the review's also holding its input.
    /// </summary>
    private static readonly (string Command, string? Variant, OwnershipCheck Check)[] SampleCases =
    [
        ("words convert", null, OwnershipCheck.Junction),
        ("words convert", null, OwnershipCheck.Same),
        ("pdf extract", "attachments", OwnershipCheck.Same),
        ("pdf extract", "attachments", OwnershipCheck.Junction),
        ("review", null, OwnershipCheck.SentinelOverwrite),
        ("review", null, OwnershipCheck.InputInside),
    ];

    /// <summary>
    /// The commands whose <c>--out</c> names a folder rather than a file; capabilities does not say
    /// which, so every <c>--out</c> case checks its command against this list
    /// (<see cref="Verify"/>) and a command that changes kind fails until it is listed.
    /// </summary>
    private static readonly HashSet<string> FolderOutputs = new(StringComparer.Ordinal) { "review" };

    private static readonly Lazy<IReadOnlyDictionary<string, OwnershipCase>> Generated = new(Generate);
    private static readonly ConcurrentDictionary<string, Lazy<Probe>> Probes = new(StringComparer.Ordinal);
    private static readonly Lazy<byte[]> AttachmentFixture = new(BuildAttachmentFixture);

    public static IReadOnlyDictionary<string, OwnershipCase> All => Generated.Value;

    public static TheoryData<string> Sample => [.. All.Values.Where(IsSample).Select(static item => item.Id)];

    public static TheoryData<string> Exhaustive => [.. All.Values.Where(static item => !IsSample(item)).Select(static item => item.Id)];

    private static bool IsSample(OwnershipCase item) =>
        SampleCases.Contains((item.Command.Words, item.Variant, item.Check));

    private static Dictionary<string, OwnershipCase> Generate()
    {
        var cases = new Dictionary<string, OwnershipCase>(StringComparer.Ordinal);
        foreach (CliCommand command in CliCatalog.Current.Commands.Where(static command =>
            command.Arguments.Any(static argument => argument.Required && argument.InputKind == "file")))
        {
            string? output = new[] { "--out", "--out-dir" }.FirstOrDefault(command.Has);
            if (output is not null)
            {
                // A folder output writes several files whose kind a required choice decides, such
                // as --what; each value is a variant. A file output writes one kind.
                CliOption? choice = output == "--out-dir"
                    ? command.Options.FirstOrDefault(static option => option is { Required: true, AllowedValues.Count: > 0 } && option.Name != "--by")
                    : null;
                foreach (string? variant in choice?.AllowedValues.Cast<string?>() ?? [null])
                {
                    foreach (OwnershipCheck check in Enum.GetValues<OwnershipCheck>())
                    {
                        if (check == OwnershipCheck.HardLink && output == "--out-dir")
                        {
                            continue;
                        }
                        if (check == OwnershipCheck.InputInside && output == "--out" && !FolderOutputs.Contains(command.Words))
                        {
                            // A single file cannot hold its input.
                            continue;
                        }
                        string id = $"{command}{(variant is null ? string.Empty : $" {choice!.Name} {variant}")}: {output} {Name(check)}";
                        cases[id] = new OwnershipCase(id, command, choice?.Name, variant, output, check);
                    }
                }
            }
            if (command.Has("--in-place"))
            {
                string id = $"{command}: --in-place {Name(OwnershipCheck.Sentinel)}";
                cases[id] = new OwnershipCase(id, command, null, null, "--in-place", OwnershipCheck.Sentinel);
            }
        }
        return cases;
    }

    private static string Name(OwnershipCheck check) => check switch
    {
        OwnershipCheck.Same => "same path",
        OwnershipCheck.Case => "other case",
        OwnershipCheck.HardLink => "hard link",
        OwnershipCheck.Junction => "junction",
        OwnershipCheck.ShortName => "8.3 short name",
        OwnershipCheck.Sentinel => "beside a foreign file",
        OwnershipCheck.InputInside => "over a folder holding its input through a junction",
        _ => "over a foreign folder with --overwrite",
    };

    /// <summary>Runs one case in a new workspace and asserts its guarantee.</summary>
    public static void Verify(string id)
    {
        Requires.Windows();
        OwnershipCase item = All[id];
        Probe probe = ProbeOf(item);
        Assert.SkipWhen(probe.Unrunnable is not null, $"The command fails on its fixture, so no output can reach the input: {probe.Unrunnable}");
        if (item.Output == "--out" && probe.Written.Length > 0)
        {
            Assert.True(probe.FolderOutput == FolderOutputs.Contains(item.Command.Words),
                $"{item.Command} writes {(probe.FolderOutput ? "a folder" : "a file")} by --out; {(probe.FolderOutput ? "add it to" : "remove it from")} OwnershipCases.FolderOutputs.");
        }

        using var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);
        Invocation invocation = Build(item, workspace.Path, probe.InputName);
        string folder = workspace.File(InputFolder);
        string input = invocation.Input!;
        string inputPath = workspace.File(input);
        if (item.Check == OwnershipCheck.InputInside)
        {
            // The input lies in a folder that looks like an earlier output of the command and is
            // named through a junction to it; the command replaces that folder with --overwrite.
            Target(item with { Check = OwnershipCheck.SentinelOverwrite }, workspace, probe, input);
            string inside = workspace.File("out/" + Path.GetFileName(input));
            File.Move(inputPath, inside, overwrite: true);
            FileSystemLinks.CreateDirectoryLink(workspace.File("junction"), workspace.File("out"));
            string linked = "junction/" + Path.GetFileName(input);
            invocation.Replace(input, linked);
            input = "out/" + Path.GetFileName(input);
            inputPath = inside;
        }
        string inputHash = Hash(inputPath);

        string? target = item.Output == "--in-place" ? null
            : item.Check == OwnershipCheck.InputInside ? "out"
            : Target(item, workspace, probe, input);
        bool overwrite = item.Check != OwnershipCheck.Sentinel;
        if (target is not null)
        {
            invocation.Args.RemoveAll(static arg => arg == "--overwrite");
            int index = invocation.Args.IndexOf(item.Output);
            if (index >= 0)
            {
                invocation.Args.RemoveRange(index, 2);
            }
            invocation.Add(item.Output, target);
        }
        if (overwrite && item.Output != "--in-place")
        {
            invocation.Add("--overwrite");
        }
        if (item.Output == "--in-place")
        {
            invocation.Add("--in-place");
            File.WriteAllText(Path.Combine(folder, SentinelName), SentinelText);
        }

        Dictionary<string, string> before = Hashes(workspace.Path, InputFolder);
        string? sentinel = item.Check is OwnershipCheck.Sentinel or OwnershipCheck.SentinelOverwrite
            ? item.Output == "--in-place" ? Path.Combine(folder, SentinelName) : SentinelOf(target!, probe, workspace)
            : null;
        string[] args = [.. invocation.Args, "--output-format", "json"];
        var process = new CliProcess(CliRunner.ExecutablePath, CliEnvironment.Evaluation(
            Path.GetDirectoryName(workspace.ConfigDirectory)!,
            invocation.Env.ToDictionary(static pair => pair.Key, static pair => (string?)pair.Value)));
        CliResult result = process.Run(workspace.Path, string.Empty, args);

        string Describe(string problem) =>
            $"{problem}{Environment.NewLine}case: {id}{Environment.NewLine}command: aspose-cli {string.Join(' ', args)}{Environment.NewLine}"
            + $"exit {result.ExitCode}{Environment.NewLine}{Truncate(result.StdOut + result.StdErr)}";

        Assert.True(File.Exists(inputPath) && !File.GetAttributes(inputPath).HasFlag(FileAttributes.Directory),
            Describe($"The input {input} is gone."));
        Assert.True(item.Output == "--in-place" || inputHash == Hash(inputPath), Describe($"The input {input} was changed."));
        if (sentinel is not null)
        {
            Assert.True(File.Exists(sentinel) && File.ReadAllText(sentinel) == SentinelText,
                Describe($"The file {Path.GetRelativePath(workspace.Path, sentinel)}, which the command never wrote, was {(File.Exists(sentinel) ? "changed" : "removed")}."));
        }
        if (item.Output == "--in-place")
        {
            return;
        }
        // Every other file of the input folder, which the command never wrote, is unchanged too.
        string[] changed = [.. before.Where(file => !File.Exists(workspace.File(file.Key)) || Hash(workspace.File(file.Key)) != file.Value).Select(static file => file.Key)];
        Assert.True(changed.Length == 0, Describe($"Files of the input folder were changed or removed: {string.Join(", ", changed)}"));
    }

    /// <summary>
    /// Where the case points the output. An alias names the input file for a file output and the
    /// input folder for a folder output; a sentinel case names a new folder <c>out</c> that holds the
    /// sentinel and, with <c>--overwrite</c>, a foreign file of each name the command writes.
    /// </summary>
    private static string Target(OwnershipCase item, TempWorkspace workspace, Probe probe, string input)
    {
        bool folderOutput = item.Output == "--out-dir";
        string inputFile = Path.GetFileName(input);
        switch (item.Check)
        {
            case OwnershipCheck.Same:
                return folderOutput ? InputFolder : input;
            case OwnershipCheck.Case:
                return folderOutput ? InputFolder.ToUpperInvariant() : $"{InputFolder.ToUpperInvariant()}/{inputFile.ToUpperInvariant()}";
            case OwnershipCheck.HardLink:
                string link = "linked" + Path.GetExtension(inputFile);
                Assert.True(CreateHardLink(workspace.File(link), workspace.File(input), IntPtr.Zero), $"CreateHardLink failed with {Marshal.GetLastWin32Error()}.");
                return link;
            case OwnershipCheck.Junction:
                FileSystemLinks.CreateDirectoryLink(workspace.File("junction"), workspace.File(InputFolder));
                return folderOutput ? "junction" : $"junction/{inputFile}";
            case OwnershipCheck.ShortName:
                string full = folderOutput ? workspace.File(InputFolder) : workspace.File(input);
                string shortPath = ShortPath(full);
                Assert.SkipWhen(string.Equals(Path.GetFileName(shortPath), Path.GetFileName(full), StringComparison.OrdinalIgnoreCase),
                    $"The volume of {workspace.Path} makes no 8.3 short names, so {Path.GetFileName(full)} has none.");
                return shortPath;
            default:
                string outFolder = workspace.File("out");
                Directory.CreateDirectory(outFolder);
                if (folderOutput || probe.FolderOutput)
                {
                    if (item.Check == OwnershipCheck.SentinelOverwrite)
                    {
                        foreach (string name in probe.Written)
                        {
                            string path = Path.Combine(outFolder, name);
                            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                            File.WriteAllText(path, Foreign);
                        }
                    }
                    return "out";
                }
                if (item.Check == OwnershipCheck.SentinelOverwrite)
                {
                    File.WriteAllText(Path.Combine(outFolder, probe.OutputName), Foreign);
                }
                return "out/" + probe.OutputName;
        }
    }

    /// <summary>Writes the sentinel into the folder the output goes to and returns its path.</summary>
    private static string SentinelOf(string target, Probe probe, TempWorkspace workspace)
    {
        string folder = target == "out" ? workspace.File("out") : Path.GetDirectoryName(workspace.File(target))!;
        string sentinel = Path.Combine(folder, SentinelName);
        File.WriteAllText(sentinel, SentinelText);
        return sentinel;
    }

    /// <summary>
    /// The command's smallest valid invocation (<see cref="Invocation.For"/>) with its variant, its
    /// input documents moved into <see cref="InputFolder"/>, the first named
    /// <paramref name="inputName"/>, and every file written into the workspace.
    /// </summary>
    private static Invocation Build(OwnershipCase item, string root, string? inputName)
    {
        Invocation invocation = Invocation.For(item.Command);
        if (item.VariantOption is { } option)
        {
            invocation.Replace(invocation.Args[invocation.Args.IndexOf(option) + 1], item.Variant!);
            // A variant may need its own format named, as --what forms needs --form-format.
            if (item.Command.Option($"--{item.Variant!.TrimEnd('s')}-format") is { AllowedValues.Count: > 0 } format)
            {
                invocation.Add(format.Name, format.AllowedValues[0]);
            }
            if (item.Command.Product == "pdf" && item.Variant == "attachments")
            {
                invocation.Files[invocation.Input!] = new ScenarioFile { Text = AttachmentMarker };
            }
            else if (item.Command.Product == "slides")
            {
                invocation.Files[invocation.Input!] = new ScenarioFile { Fixture = "slides.media" };
            }
        }
        for (int index = 0; index < invocation.Inputs.Count; index++)
        {
            string original = invocation.Inputs[index];
            string name = index == 0 && inputName is not null ? inputName : original;
            if (index == 0 && item.Check == OwnershipCheck.ShortName && item.Output != "--out-dir")
            {
                name = LongStem + Path.GetExtension(original);
            }
            string moved = $"{InputFolder}/{name}";
            invocation.Files[moved] = invocation.Files[original];
            invocation.Files.Remove(original);
            invocation.Replace(original, moved);
            invocation.Inputs[index] = moved;
        }
        foreach ((string relative, ScenarioFile file) in invocation.Files)
        {
            string path = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, file.Text == AttachmentMarker ? AttachmentFixture.Value
                : file.Fixture is { } fixture ? ScenarioFixtures.Read(fixture) : Encoding.UTF8.GetBytes(file.Text!));
        }
        return invocation;
    }

    /// <summary>Stands for <see cref="AttachmentFixture"/> in an invocation's files.</summary>
    private const string AttachmentMarker = "<pdf fixture with an attachment named like its input>";

    /// <summary>
    /// The PDF fixture with one attachment named <c>input.pdf</c>, the name the invocation gives its
    /// input, so an extraction that names attachments after themselves writes over the input.
    /// </summary>
    private static byte[] BuildAttachmentFixture()
    {
        using var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);
        File.WriteAllBytes(workspace.File("source.pdf"), ScenarioFixtures.Read("pdf.pdf"));
        File.WriteAllBytes(workspace.File("attachment.pdf"), ScenarioFixtures.Read("pdf.pdf"));
        File.WriteAllText(workspace.File("ops.json"), """{"ops":[{"op":"add_attachment","path":"attachment.pdf","name":"input.pdf"}]}""");
        CliResult result = workspace.Run("pdf", "edit", "source.pdf", "--ops", "ops.json", "--out", "attached.pdf");
        return result.ExitCode == 0
            ? File.ReadAllBytes(workspace.File("attached.pdf"))
            : throw new InvalidOperationException($"Could not attach a file to the PDF fixture: exit {result.ExitCode}: {result.StdErr}{result.StdOut}");
    }

    /// <summary>
    /// What the command writes when it runs on its fixture into a fresh place: whether its output is
    /// a folder, the names it writes there (relative to it), the file name of a file output, and
    /// the name to give the input so that a folder output's first file of a format the product reads
    /// lands on it; or why it fails. A command that runs but writes nothing (an extraction from a
    /// fixture without that content) has no names, and its checks run all the same.
    /// </summary>
    private sealed record Probe(string? Unrunnable, bool FolderOutput, string[] Written, string OutputName, string? InputName);

    private static Probe ProbeOf(OwnershipCase item) =>
        Probes.GetOrAdd($"{item.Command.Words}|{item.Variant}|{item.Output}", _ => new Lazy<Probe>(() => RunProbe(item))).Value;

    private static Probe RunProbe(OwnershipCase item)
    {
        var probeCase = item with { Check = OwnershipCheck.Sentinel };
        if (item.Output == "--in-place")
        {
            return new Probe(null, false, [], string.Empty, null);
        }
        using var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);
        Invocation invocation = Build(probeCase, workspace.Path, null);
        int index = invocation.Args.IndexOf(item.Output);
        string extension = Path.GetExtension(invocation.Input!);
        if (index < 0)
        {
            // The output name a file output takes by default, so the probe writes the same format.
            Dictionary<string, string> before = Hashes(workspace.Path, string.Empty);
            CliResult first = Run(workspace, invocation);
            string? written = Hashes(workspace.Path, string.Empty).Keys.Where(key => !before.ContainsKey(key)).Order(StringComparer.Ordinal).FirstOrDefault();
            if (first.ExitCode is not (0 or 8))
            {
                return new Probe($"exit {first.ExitCode}: {Truncate(first.StdErr + first.StdOut)}", false, [], string.Empty, null);
            }
            // A command that writes a folder by default (review) takes a folder name, one that
            // writes a file takes that file's extension, and one that writes nothing without --out
            // (compare) writes its input's format.
            string? parent = written is null ? null : Path.GetDirectoryName(written)!.Replace('\\', '/');
            bool defaultFolder = parent is { Length: > 0 } && !string.Equals(parent, InputFolder, StringComparison.OrdinalIgnoreCase);
            extension = defaultFolder ? string.Empty : written is null ? extension : Path.GetExtension(written);
            invocation.Add(item.Output, "probe/output" + extension);
        }
        else
        {
            invocation.Args[index + 1] = item.Output == "--out-dir" ? "probe" : "probe/output" + Path.GetExtension(invocation.Args[index + 1]);
        }
        CliResult result = Run(workspace, invocation);
        string target = workspace.File(invocation.Args[invocation.Args.IndexOf(item.Output) + 1]);
        bool folder = Directory.Exists(target);
        string root = folder ? target : workspace.File("probe");
        string[] names = Directory.Exists(root)
            ? [.. Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(root, path).Replace('\\', '/')).Order(StringComparer.Ordinal)]
            : [];
        if (result.ExitCode is not (0 or 8))
        {
            return new Probe($"exit {result.ExitCode}: {Truncate(result.StdErr + result.StdOut)}", folder, names, string.Empty, null);
        }
        if (names.Length == 0)
        {
            // Nothing to extract from the fixture: the command still runs and may still replace or
            // clear the folder it is given, so every check runs, without a name to collide on.
            return new Probe(null, folder || item.Output == "--out-dir", [], Path.GetFileName(target), null);
        }
        CliProduct? product = item.Command.Product is { } id ? CliCatalog.Current.Product(id) : null;
        string? inputName = item.Output == "--out-dir"
            ? names.FirstOrDefault(name => !name.Contains('/', StringComparison.Ordinal)
                && product?.LoadFormats.Contains(Path.GetExtension(name).TrimStart('.'), StringComparer.OrdinalIgnoreCase) == true)
            : null;
        return new Probe(null, folder, names, Path.GetFileName(target), inputName);
    }

    private static CliResult Run(TempWorkspace workspace, Invocation invocation) =>
        new CliProcess(CliRunner.ExecutablePath, CliEnvironment.Evaluation(
                Path.GetDirectoryName(workspace.ConfigDirectory)!,
                invocation.Env.ToDictionary(static pair => pair.Key, static pair => (string?)pair.Value)))
            .Run(workspace.Path, string.Empty, [.. invocation.Args, "--output-format", "json"]);

    /// <summary>The hash of every file below a folder of the root, by path relative to the root, without following links.</summary>
    private static Dictionary<string, string> Hashes(string root, string folder)
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string start = Path.Combine(root, folder);
        foreach (string path in Directory.EnumerateFiles(start, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }))
        {
            string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (!relative.StartsWith(ScenarioLicense.ProjectDirectory + "/", StringComparison.OrdinalIgnoreCase))
            {
                files[relative] = Hash(path);
            }
        }
        return files;
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static string ShortPath(string path)
    {
        var buffer = new StringBuilder(1024);
        int length = GetShortPathName(path, buffer, buffer.Capacity);
        Assert.True(length > 0, $"GetShortPathName failed with {Marshal.GetLastWin32Error()}.");
        return buffer.ToString();
    }

    private static string Truncate(string text) => text.Length <= 3000 ? text : text[..3000] + " ...";

    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string name, string existing, IntPtr security);

    [DllImport("kernel32.dll", EntryPoint = "GetShortPathNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetShortPathName(string longPath, StringBuilder shortPath, int length);
}
