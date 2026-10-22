using System.Security.Cryptography;
using System.Text;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using SkiaSharp;
using Xunit;

namespace Aspose.Cli.Invariants.Tests.Isolation;

/// <summary>
/// One isolation case: a command opens a sample document of one format, built for one kind of
/// references. <see cref="SourceOption"/> names the option a <c>create</c> reads the sample with;
/// <see cref="ProductOption"/> says a platform command names the product because no route
/// assigns the format to one.
/// </summary>
internal sealed record IsolationCase(
    string Id,
    string Product,
    string Format,
    ReferenceKind Kind,
    CliCommand Command,
    string? SourceOption,
    bool ProductOption);

/// <summary>
/// The isolation matrix (SECURITY.md, Network): for every product, every format it opens and
/// every command that opens one, generated from the live <c>capabilities</c>. A case opens a sample
/// that references a loopback HTTP server (<see cref="ReferenceKind.Network"/>,
/// <see cref="ReferenceKind.Decoy"/>) or sentinel files outside its folder
/// (<see cref="ReferenceKind.Files"/>, and through the administrative share,
/// <see cref="ReferenceKind.Unc"/>), and must leave the server unasked and every output free of
/// the sentinels. The <see cref="ReferenceKind.Control"/> of each format references only files of
/// its own folder the same ways and must succeed and keep them, so the forms the matrix uses are
/// ones the product follows where it is allowed to, and an inside resource is never reported.
/// </summary>
internal static class IsolationCases
{
    /// <summary>
    /// The file options of <c>create</c> commands and the formats each reads, null for a template,
    /// which is any format the product reads. A create option missing here fails
    /// <c>IsolationMatrixTests</c>, so a new source is classified before it ships.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]?> SourceFormats = new Dictionary<string, string[]?>(StringComparer.Ordinal)
    {
        ["--from-html"] = ["html"],
        ["--from-markdown"] = ["md"],
        ["--from-images"] = ["svg"],
        ["--from-text"] = ["txt"],
        ["--template"] = null,
    };

    /// <summary>
    /// The unmarked sample that every run checks: the formats with known reference handling of
    /// their own (Cells HTML and ODS, PDF HTML import) and each other product's own format, each
    /// rendered or created from, against the outside sentinels and as a control.
    /// </summary>
    private static readonly (string Product, string Format)[] SampleRows =
        [("cells", "html"), ("cells", "ods"), ("pdf", "html"), ("slides", "pptx"), ("words", "docx")];

    private static readonly Lazy<IReadOnlyDictionary<string, IsolationCase>> Generated = new(Generate);

    /// <summary>
    /// The controls whose drawing cannot show their resource, by design, with the decision that
    /// removes it. The control still must succeed and keep every sentinel out; only the check that
    /// its picture holds the resource is not applicable.
    /// </summary>
    private static readonly Dictionary<(string Product, string Format), string> KeepsNoResource = new()
    {
        // The only form a Cells ODS shows is an SVG picture naming the file; a draw:image link
        // was never followed, master included.
        [("cells", "ods")] = "CELLS-SVG-EGRESS: an SVG picture keeps only its own content, so the same-folder image it names is removed.",
        // img, inline SVG and data SVG were the forms master showed; CSS backgrounds and style
        // sheets never showed in MHTML.
        [("cells", "mhtml")] = "Cells drops every image of HTML and MHTML input (IMAGES_SKIPPED), an approved decision.",
    };

    /// <summary>Every case by id.</summary>
    public static IReadOnlyDictionary<string, IsolationCase> All => Generated.Value;

    public static TheoryData<string> Sample => [.. All.Values.Where(IsSample).Select(static item => item.Id)];

    public static TheoryData<string> Exhaustive => [.. All.Values.Where(static item => !IsSample(item)).Select(static item => item.Id)];

    private static bool IsSample(IsolationCase item) =>
        item.Kind != ReferenceKind.Unc
        && SampleRows.Contains((item.Product, item.Format))
        && item.Command.Verb is "render" or "create";

    /// <summary>The formats each product opens: the formats it reads and those its create sources read.</summary>
    public static IEnumerable<(CliProduct Product, string Format)> Formats(CliCatalog catalog) =>
        catalog.Products.SelectMany(product => product.LoadFormats
            .Concat(catalog.Commands.Where(command => command.Product == product.Id && command.Verb == "create")
                .SelectMany(static command => command.Options.Where(static option => option.InputKind == "file"))
                .SelectMany(option => SourceFormats.GetValueOrDefault(option.Name) ?? []))
            .Distinct(StringComparer.Ordinal)
            .Select(format => (product, format)));

    private static Dictionary<string, IsolationCase> Generate()
    {
        CliCatalog catalog = CliCatalog.Current;
        var cases = new Dictionary<string, IsolationCase>(StringComparer.Ordinal);
        foreach ((CliProduct product, string format) in Formats(catalog))
        {
            if (!IsolationDocuments.Families.ContainsKey(format))
            {
                continue;
            }
            var commands = new List<(CliCommand Command, string? Source, bool ProductOption)>();
            bool reads = product.LoadFormats.Contains(format, StringComparer.Ordinal);
            foreach (CliCommand command in catalog.Commands.Where(static command => command.Arguments.Any(static argument => argument.Required && argument.InputKind == "file")))
            {
                if (command.Product == product.Id && reads)
                {
                    commands.Add((command, null, false));
                }
                else if (command.Product is null && reads)
                {
                    // A platform command routes the input by its extension; one no route assigns
                    // needs the product named, so a command without --product cannot open it.
                    if (catalog.Routes.TryGetValue("." + IsolationDocuments.Extension(format), out string? routed))
                    {
                        if (routed == product.Id)
                        {
                            commands.Add((command, null, false));
                        }
                    }
                    else if (command.Has("--product"))
                    {
                        commands.Add((command, null, true));
                    }
                }
            }
            foreach (CliCommand command in catalog.Commands.Where(command => command.Product == product.Id && command.Verb == "create"))
            {
                foreach (CliOption option in command.Options.Where(static option => option.InputKind == "file"))
                {
                    string[]? formats = SourceFormats.GetValueOrDefault(option.Name);
                    if (formats?.Contains(format, StringComparer.Ordinal) ?? (SourceFormats.ContainsKey(option.Name) && reads))
                    {
                        commands.Add((command, option.Name, false));
                    }
                }
            }
            foreach (ReferenceKind kind in Enum.GetValues<ReferenceKind>())
            {
                foreach ((CliCommand command, string? source, bool productOption) in commands)
                {
                    if (kind == ReferenceKind.Decoy && !IsolationDocuments.HoldsDecoy(format))
                    {
                        continue;
                    }
                    if (kind == ReferenceKind.Unc && command.Verb is not ("render" or "create"))
                    {
                        // A UNC reference takes the path every other command's loader takes; the
                        // commands that draw the document show whether it was read.
                        continue;
                    }
                    string id = $"{product.Id}.{format}.{kind.ToString().ToLowerInvariant()}: {command}{(source is null ? string.Empty : " " + source)}";
                    cases[id] = new IsolationCase(id, product.Id, format, kind, command, source, productOption);
                }
            }
        }
        return cases;
    }

    /// <summary>Runs one case in a new workspace and asserts its guarantee.</summary>
    public static async Task Verify(string id)
    {
        IsolationCase item = All[id];
        using var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);
        var layout = new IsolationLayout(workspace.Path);
        await using var server = new ResourceHttpServer();
        References references = ReferencesOf(item, layout, server.Url);
        // The command runs in the input folder, as the Skills direct: a relative reference then
        // means the same from the document and from the working directory.
        ScenarioLicense.Project(layout.Input);
        string input = Path.GetFileName(IsolationDocuments.Write(item.Product, item.Format, references, layout));
        Invocation invocation = InvocationOf(item, input);
        foreach ((string relative, ScenarioFile file) in invocation.Files)
        {
            string target = Path.Combine(layout.Input, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllBytes(target, file.Copy is { } copy ? File.ReadAllBytes(Path.Combine(layout.Input, copy))
                : file.Fixture is { } fixture ? ScenarioFixtures.Read(fixture) : Encoding.UTF8.GetBytes(file.Text!));
        }
        Dictionary<string, string> before = Snapshot(workspace.Path);
        string[] args = [.. invocation.Args, "--output-format", "json"];
        var process = new CliProcess(CliRunner.ExecutablePath, CliEnvironment.Evaluation(
            Path.GetDirectoryName(workspace.ConfigDirectory)!,
            invocation.Env.ToDictionary(static pair => pair.Key, static pair => (string?)pair.Value)));
        CliResult result = process.Run(layout.Input, string.Empty, args);
        string[] written = [.. Snapshot(workspace.Path).Where(file => !before.TryGetValue(file.Key, out string? hash) || hash != file.Value).Select(static file => file.Key)];

        string Describe(string problem) =>
            $"{problem}{Environment.NewLine}case: {id}{Environment.NewLine}command: aspose-cli {string.Join(' ', args)}{Environment.NewLine}"
            + $"exit {result.ExitCode}; written: {string.Join(", ", written)}{Environment.NewLine}{Truncate(result.StdOut + result.StdErr)}";

        List<string> leaks = Leaks(workspace.Path, written, result, IsolationLayout.SentinelColour, IsolationLayout.Marker);
        Assert.True(server.RequestCount == 0, Describe($"The command asked the loopback server for: {string.Join("; ", server.Requests)}"));
        Assert.True(leaks.Count == 0, Describe($"An output holds a sentinel from outside the input folder:{Environment.NewLine}{string.Join(Environment.NewLine, leaks)}"));
        if (item.Kind != ReferenceKind.Control)
        {
            return;
        }
        // A partial result (exit 8) is a success whose output says what it could not verify, such
        // as the resource coverage of an MHTML archive.
        Assert.True(result.ExitCode is 0 or 8, Describe("The control, which references only files of its own folder, did not succeed."));
        // A command that draws the document shows whether it kept the resource; another, such as an
        // extraction of embedded media, writes pictures that never held it.
        string[] pictures = item.Command.Verb is "render" or "review" && !KeepsNoResource.ContainsKey((item.Product, item.Format))
            ? [.. written.Where(static file => Path.GetExtension(file).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg")]
            : [];
        if (pictures.Length > 0)
        {
            Assert.True(pictures.Any(file => OutputScan.Pixels(File.ReadAllBytes(workspace.File(file)), IsolationLayout.ControlColour) >= 16),
                Describe("No picture the control drew holds the resource of its own folder that it references."));
        }
    }

    private static References ReferencesOf(IsolationCase item, IsolationLayout layout, string serverUrl)
    {
        int depth = IsolationDocuments.PackageDepth(item.Format);
        if (item.Kind != ReferenceKind.Unc)
        {
            return References.For(item.Kind, layout, serverUrl, depth);
        }
        (string? image, string? unreachable) = layout.Unc("sentinel.png");
        Assert.SkipWhen(image is null, unreachable ?? "The administrative share is not reachable.");
        return References.For(item.Kind, layout, serverUrl, depth, image, layout.Unc("sentinel.css").Path, layout.Unc("sentinel.html").Path);
    }

    /// <summary>
    /// The command's smallest valid invocation (<see cref="Invocation.For"/>) reading the sample: in
    /// place of its first input, or as the value of its create source option, which replaces the
    /// source the invocation would otherwise give (a template goes with it instead).
    /// </summary>
    private static Invocation InvocationOf(IsolationCase item, string input)
    {
        if (item.SourceOption is { } option)
        {
            if (option == "--template")
            {
                return Invocation.For(item.Command).Add(option, input);
            }
            string? implied = Invocation.ImpliedByVerb
                .Where(rule => rule.Verb == item.Command.Verb && item.Command.Has(rule.Option))
                .Select(static rule => rule.Option)
                .FirstOrDefault();
            return Invocation.For(item.Command, implied).Add(option, input);
        }
        Invocation invocation = Invocation.For(item.Command);
        if (item.Command.Has("--in-place") && item.Command.Has("--out"))
        {
            // An edit keeps its input's format without --out, and not every format it reads is one it
            // writes; the product's own format always is.
            invocation.Add("--out", "edited." + ScenarioFixtures.PrimaryFormat(item.Product));
        }
        // Every input document is the sample: a second one, as a comparison reads, is a copy of it.
        for (int index = 0; index < invocation.Inputs.Count; index++)
        {
            string original = invocation.Inputs[index];
            string replacement = index == 0 ? input : $"{Path.GetFileNameWithoutExtension(input)}-{index + 1}{Path.GetExtension(input)}";
            invocation.Replace(original, replacement);
            invocation.Files.Remove(original);
            if (index > 0)
            {
                invocation.Files[replacement] = new ScenarioFile { Copy = input };
            }
        }
        if (item.ProductOption)
        {
            invocation.Add("--product", item.Product);
        }
        return invocation;
    }

    /// <summary>Where the colour or text reached a written file or the command's output.</summary>
    private static List<string> Leaks(string root, IEnumerable<string> written, CliResult result, SKColor colour, string text)
    {
        var leaks = new List<string>();
        foreach (string file in written)
        {
            leaks.AddRange(OutputScan.Find(file, File.ReadAllBytes(Path.Combine(root, file)), colour, text));
        }
        leaks.AddRange(OutputScan.Find("stdout", Encoding.UTF8.GetBytes(result.StdOut), colour, text));
        leaks.AddRange(OutputScan.Find("stderr", Encoding.UTF8.GetBytes(result.StdErr), colour, text));
        return leaks;
    }

    /// <summary>The hash of every file below the root but the sentinels and the project license, by relative path.</summary>
    private static Dictionary<string, string> Snapshot(string root)
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in Directory.EnumerateFiles(root, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }))
        {
            string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (relative.StartsWith(IsolationLayout.OutsideFolder + "/", StringComparison.OrdinalIgnoreCase)
                || relative.StartsWith(ScenarioLicense.ProjectDirectory + "/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            files[relative] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        }
        return files;
    }

    private static string Truncate(string text) => text.Length <= 3000 ? text : text[..3000] + " ...";
}
