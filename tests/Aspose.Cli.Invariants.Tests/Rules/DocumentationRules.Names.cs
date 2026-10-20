using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>
/// C-R38: the names that error messages, hints, help, the descriptions the schemas publish
/// (documentation comments), the Skills, the README and <c>KNOWN-ISSUES.md</c> write must exist
/// in this build, all read by one reader (DocumentationRules.Sources.cs). The same reading of the
/// sources finds the public text that names a hidden option or command (C-R04).
/// <para>
/// How a text writes a name so that this check reads it: an option as <c>--name</c>, an
/// operation as its <c>snake_case</c> name and a code as its <c>UPPER_SNAKE</c> name, anywhere in
/// the text; a command after <c>aspose-cli</c> (<c>aspose-cli pdf inspect --detail outline</c>),
/// in quotes (<c>'pdf read --content text'</c>, also with backquotes or double quotes, and in a
/// documentation comment as <c>&lt;c&gt;pdf read --content text&lt;/c&gt;</c>), or as a whole text, such
/// as a help example or a Markdown code span (<c>cells convert book.csv --to xlsx</c>). Its path
/// must be a command, the options written after it that command's or global ones, and the value
/// of an option that lists its values one of them. An operation written as JSON names its
/// fields, which must be that operation's (DocumentationRules.Fields.cs), and so does prose that
/// writes a field after its operation's name (<c>import_range.from</c>). A product command
/// written bare in a sentence (<c>use slides render for</c>) is reported, since quoting is the
/// convention, and its path is still checked: after a product id the next words are command
/// words, so <c>pdf frobnicate</c> has no command; after a product id that prose uses as a noun
/// (<see cref="NounProducts"/>) only a word that names a command somewhere starts a path. A C#
/// text that is exactly one name declares it rather than naming it
/// (<c>[Operation("set_values")]</c>) and is not read; a Markdown code span of one name names it.
/// In a product's sources and Skill an operation name must be that product's, so the name a
/// product declares mistaken (<c>set_style</c> in Cells) is reported there even where another
/// product has an operation of that name.
/// </para>
/// </summary>
internal static partial class ContractRules
{
    // rule-exemptions: begin
    /// <summary>Words of the sources written like a code, operation or option that name something else, with why.</summary>
    private static readonly Dictionary<string, string> NotNames = new(StringComparer.Ordinal)
    {
        ["build_property"] = "the MSBuild property prefix an analyzer reads",
        ["field_1"] = "a name Aspose.Pdf gives a form field it creates, in a KNOWN-ISSUES.md entry",
        ["HTTPS_PROXY"] = "the environment variable that sets the proxy of release downloads",
        ["index_files"] = "the folder Excel's HTML export writes beside the page",
        ["PDF_A_2B"] = "the Aspose.Pdf conformance name that pdfa-2b maps to",
        ["REG_BINARY"] = "a Windows registry value type, in a KNOWN-ISSUES.md entry",
        ["REG_DWORD"] = "a Windows registry value type, in a KNOWN-ISSUES.md entry",
        ["SCREAMING_SNAKE_CASE"] = "the name of a naming style",
        ["shift_jis"] = "a text encoding name that cells convert --encoding accepts",
        ["--certificate-password"] = "the prefix of the --certificate-password-env and -stdin option family, as a parameter describes it",
    };

    /// <summary>
    /// Product ids that prose also uses as plain nouns ("table cells", "the slides"): after one of
    /// them only a command word (<see cref="CommandWords"/>) starts a command path. After any
    /// other product id every lowercase word does, so a renamed or removed command is reported.
    /// </summary>
    private static readonly Dictionary<string, string> NounProducts = new(StringComparer.Ordinal)
    {
        ["cells"] = "worksheet cells",
        ["slides"] = "the slides of a deck",
        ["words"] = "words of text",
    };

    /// <summary>Text that reads like a command, after a noun product id or as a whole C# text, but is prose, with where.</summary>
    private static readonly Dictionary<string, string> ProsePhrases = new(StringComparer.Ordinal)
    {
        ["app stop"] = "\"app stop requested\", a line the app service writes to its log",
        ["cells read"] = "\"table cells read it before the text\" in a Words result description",
        ["cells stop"] = "\"the listed cells stop at the diff budget\" in a Cells compare result description",
    };
    // rule-exemptions: end

    /// <summary>The kinds of name the reading recognizes; each must be met somewhere, or its rule stopped matching.</summary>
    private static readonly string[] NameKinds = ["command", "option", "value", "argument", "code", "operation", "operation json", "field"];

    private static IEnumerable<InvariantCase> R38(CliCatalog catalog)
    {
        var names = new SourceNames(catalog, MistakenNames.Current);
        string sources = Path.Combine(RepositoryPaths.Root, "src");
        string[] projects = [.. Directory.GetDirectories(sources).Order(StringComparer.Ordinal)];
        var documents = new List<WrittenFiles>();
        foreach (string project in projects)
        {
            string subject = "src/" + Path.GetFileName(project);
            yield return Check("C-R38", subject, () => names.Of(project).Names);
            yield return Check("C-R38", subject + " bare commands", () => names.Of(project).Bare);
            yield return Check("C-R04", subject + " hidden names", () => names.Of(project).Hidden);
            string skills = Path.Combine(project, "Skills");
            if (Directory.Exists(skills))
            {
                documents.Add(new WrittenFiles(subject + "/Skills", skills, ProductOf(project)));
            }
        }
        documents.Add(new WrittenFiles("README.md", Path.Combine(RepositoryPaths.Root, "README.md"), null));
        documents.Add(new WrittenFiles("KNOWN-ISSUES.md", Path.Combine(RepositoryPaths.Root, "KNOWN-ISSUES.md"), null));
        foreach (WrittenFiles document in documents)
        {
            yield return Check("C-R38", document.Subject, () => names.Of(document).Names);
        }
        yield return Check("C-R38", "src reading", () =>
        {
            SourceFindings[] findings = [.. projects.Select(names.Of), .. documents.Select(names.Of)];
            HashSet<string> used = [.. findings.SelectMany(static finding => finding.Exempted)];
            (string Group, SourceFindings[] Findings)[] groups =
            [
                ("the C# sources", [.. projects.Select(names.Of)]),
                ("the Markdown files", [.. documents.Select(names.Of)]),
            ];
            string[] silent = [.. documents.Where(document => names.Of(document).Kinds.Length == 0).Select(static document => document.Subject)];
            return NotNames.Keys.Concat(ProsePhrases.Keys).Where(name => !used.Contains(name)).Order(StringComparer.Ordinal)
                .Select(static name => $"'{name}' is exempted as no name, but no source writes it any more; delete the exemption")
                .Concat(groups.SelectMany(group => NameKinds
                    .Where(kind => !group.Findings.Any(finding => finding.Kinds.Contains(kind, StringComparer.Ordinal)))
                    .Select(kind => $"{group.Group} name no {kind} that the reading recognizes, so it checks nothing there; its rules stopped matching")))
                .Concat(silent.Select(static subject => $"the reading names nothing in {subject}, so it checks nothing there"))
                .Concat(MistakenReading(catalog, MistakenNames.Current));
        });
        yield return Check("C-R38", "written mistakes", () => WrittenMistakes(names));
        yield return Check("C-R04", "Skills and README hidden names", names.HiddenInDocuments);
        yield return HiddenInHintCase(catalog);
    }

    /// <summary>The files of a directory by pattern, bin and obj left out, in order, with their path relative to it.</summary>
    private static IEnumerable<(string File, string Where)> SourceFiles(string directory, string pattern) =>
        Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories)
            .Select(file => (File: file, Where: Path.GetRelativePath(directory, file).Replace('\\', '/')))
            .Where(static file => !file.Where.Split('/').Any(static part => part is "bin" or "obj"))
            .OrderBy(static file => file.File, StringComparer.Ordinal);

    /// <summary>The product a source project belongs to, such as <c>cells</c>, or null for the platform.</summary>
    private static string? ProductOf(string project) =>
        Path.GetFileName(project) is { } name && name.StartsWith(ProductProject, StringComparison.Ordinal)
            ? name[ProductProject.Length..].ToLowerInvariant()
            : null;

    /// <summary>
    /// Whether the table of mistaken names reads the sources right: every declaration placed, some
    /// of each kind read, each operation or field one its product's ops schema has, and each
    /// command one this build has. A command's mistaken names are also typed once each: the CLI
    /// must suggest exactly the commands the table reads them as standing for, so a name read as
    /// belonging to another command that exists fails too.
    /// </summary>
    private static IEnumerable<string> MistakenReading(CliCatalog catalog, MistakenNames mistaken)
    {
        foreach (string problem in mistaken.Problems)
        {
            yield return problem;
        }
        foreach (MistakenKind kind in Enum.GetValues<MistakenKind>().Where(kind => !mistaken.Names.Any(name => name.Kind == kind)))
        {
            yield return $"the reading finds no mistaken name of a {kind.ToString().ToLowerInvariant()}, so it stopped matching";
        }
        // A mistaken name is typed where the command it stands for is: under that command's parent,
        // a product, a group or the top level.
        foreach (IGrouping<(string Parent, string Name), MistakenName> typed in mistaken.Names
            .Where(static name => name.Kind == MistakenKind.Command)
            .GroupBy(static name => (MistakenNames.ParentCommand(name.Meant), name.Name)))
        {
            string[] meant = [.. typed.Select(static name => name.Meant).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
            using var workspace = new TempWorkspace();
            string typedPath = typed.Key.Parent.Length == 0 ? typed.Key.Name : $"{typed.Key.Parent} {typed.Key.Name}";
            CliResult result = workspace.Run([.. typedPath.Split(' '), "--output-format", "json"]);
            string[] suggested = JsonNode.Parse(result.StdErr.Length > 0 ? result.StdErr : result.StdOut)?["error"]?["details"]?["suggestions"] is JsonArray suggestions
                ? [.. suggestions.Select(static suggestion => suggestion!.GetValue<string>())]
                : [];
            if (!suggested.Take(meant.Length).Order(StringComparer.Ordinal).SequenceEqual(meant, StringComparer.Ordinal))
            {
                yield return $"'aspose-cli {typedPath}' suggests [{string.Join(", ", suggested)}] first, but the table reads it as standing for [{string.Join(", ", meant)}]";
            }
        }
        var fields = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (MistakenName name in mistaken.Names)
        {
            if (name.Kind == MistakenKind.Command)
            {
                if (!catalog.Commands.Any(command => command.Words == name.Meant))
                {
                    yield return $"{name.Where}: MistakenFor = [\"{name.Name}\"] reads as standing for the command '{name.Meant}', which this build does not have";
                }
                continue;
            }
            CliProduct? product = catalog.Products.FirstOrDefault(product => product.Id == name.Product);
            if (name.Kind == MistakenKind.Format)
            {
                // A mistaken format name suggests a format the product converts to and is never one itself.
                if (product is null || !product.ConvertFormats.Contains(name.Meant, StringComparer.Ordinal)
                    || product.ConvertFormats.Contains(name.Name, StringComparer.OrdinalIgnoreCase))
                {
                    yield return $"{name.Where}: MistakenFor = [\"{name.Name}\"] reads as standing for the format '{name.Meant}', which is not a convert format of {name.Product}, or is itself one";
                }
                continue;
            }
            if (product?.OpsSchemaId is null)
            {
                yield return $"{name.Where}: [MistakenFor(\"{name.Name}\")] is in no product with operations";
                continue;
            }
            bool known = name.IsOperation
                ? product.Ops.Contains(name.Meant, StringComparer.Ordinal)
                : (fields.TryGetValue(product.Id, out HashSet<string>? names) ? names : fields[product.Id] = SchemaFieldNames(product)).Contains(name.Meant);
            if (!known)
            {
                yield return $"{name.Where}: [MistakenFor(\"{name.Name}\")] reads as standing for the {(name.IsOperation ? "operation" : "field")} '{name.Meant}', which the {product.Id} ops schema does not have";
            }
        }
    }

    /// <summary>Every property name of a product's ops schema.</summary>
    private static HashSet<string> SchemaFieldNames(CliProduct product) =>
        [.. new SchemaSubject(product.OpsSchemaId!, null).Properties().Select(static property => property.Name)];

    /// <summary>
    /// The reading itself, on texts that write a mistake and on the same texts corrected, in a C#
    /// source and in Markdown: each mistaken text must report its problem and each corrected one
    /// nothing. A rule that stopped reporting one of these fails here.
    /// </summary>
    private static IEnumerable<string> WrittenMistakes(SourceNames names)
    {
        (string? Product, string Wrong, string Right, string Problem)[] samples =
        [
            ("slides", """{"op":"set_slide_size","size":{"preset":"16x9"}}""", """{"op":"set_slide_size","slideSize":{"preset":"16x9"}}""",
                "'set_slide_size' has no field 'size'; it is a mistaken name of 'slideSize'"),
            ("slides", """{"op":"set_body","slide":1,"paragraphs":[{"text":"a","level":2}]}""", """{"op":"set_body","slide":1,"paragraphs":[{"text":"a","list":{"level":2}}]}""",
                "'set_body' has no field 'paragraphs.level'; it is a mistaken name of 'list'"),
            ("cells", """{"op":"set_style","sheet":"S","range":"A1"}""", """{"op":"format_range","sheet":"S","range":"A1"}""",
                "'set_style' is no operation of cells; it is a mistaken name of 'format_range'"),
            (null, "aspose-cli slides read deck.pptx --scope shapes", "aspose-cli slides read deck.pptx --content shapes",
                "'slides read' has no option '--scope'"),
            (null, "aspose-cli pdf inspect report.pdf --detail outlines", "aspose-cli pdf inspect report.pdf --detail outline",
                "'pdf inspect --detail' has no value 'outlines'"),
            ("slides", """{"op":"set_slide_size", ..., "size":{"preset":"16x9"}}""", """{"op":"set_slide_size", ..., "slideSize":{"preset":"16x9"}}""",
                "'set_slide_size' has no field 'size'"),
            (null, "slides query deck.pptx", "slides read deck.pptx", "'slides' has no command 'query'"),
            (null, "license frobnicate", "license status", "'license' has no command 'frobnicate'"),
            (null, "docs licencing", "docs licensing", "'docs' has no topic 'licencing'"),
            (null, "schema v2/cells/opz", "schema v2/cells/ops", "'schema' has no schema 'v2/cells/opz'"),
            (null, "capabilities --sumary", "capabilities --summary", "'capabilities' has no option '--sumary'"),
            (null, "cells inspect book.xlsx --version", "cells inspect book.xlsx --verbose", "'cells inspect' has no option '--version'"),
            (null, "license status extra", "license status --product pdf", "'license status' has no command 'extra'"),
            (null, "words merge a.docx", "words edit a.docx --ops ops.json --in-place", "'words' has no command 'merge'; it is a mistaken name of 'words edit'"),
        ];
        // A field named in prose after its operation, which is no JSON and no command, so it has no
        // fenced form.
        (string? Product, string Wrong, string Right, string Problem)[] prose =
        [
            ("cells", "import_sheet.sheet", "import_sheet.sourceSheet",
                "'import_sheet' has no field 'sheet'; it is a mistaken name of 'sourceSheet'"),
            ("slides", "set_body.paragraphs.level", "set_body.paragraphs.list.level",
                "'set_body' has no field 'paragraphs.level'; it is a mistaken name of 'list'"),
        ];
        // A declaration added to a product's sources joins the table: on an operation and on a
        // field as an attribute, on a command as the initializer of its definition. A method named
        // MistakenFor, declared or called, declares nothing.
        const string Declared = """
            /// <summary>An operation.</summary>
            [Operation("set_sample")]
            [MistakenFor("old_sample")]
            public sealed record SetSampleOp : SampleOp
            {
                /// <summary>A field.</summary>
                [ReadBack("result.schema.json#/$defs/sample/properties/newName"), MistakenFor("oldName", "otherName")]
                public required string NewName { get; init; }
            }

            internal static class LookupCommand
            {
                public static CommandDefinition<LookupRequest, LookupResult> Create() =>
                    new(
                        "lookup",
                        "Look a value up.",
                        new CommandTraits(),
                        [],
                        static (parse, standard) => new LookupRequest(),
                        Table)
                    {
                        MistakenFor = ["find", "seek"],
                    };

                public static IReadOnlyList<string> MistakenFor(this Command command) => command.MistakenFor();
            }

            internal static class SheetCommands
            {
                public static Command Create()
                {
                    var sheet = new Command("sheet", "Work with sheets.");
                    var list = new CommandDefinition<ListRequest, ListResult>("list", "List the sheets.", new CommandTraits(), [], Bind, Table)
                    {
                        MistakenFor = ["ls"],
                    };
                    sheet.Subcommands.Add(list.Create());
                    return sheet;
                }
            }
            """;
        // A platform command is named by its path from the top level, through the groups that add it.
        const string Platform = """
            internal static class ToolCommandGroup
            {
                public static Command Create()
                {
                    var tool = new Command("tool", "Run tools.");
                    tool.Subcommands.Add(CreateRun());
                    return tool;
                }

                private static Command CreateRun() =>
                    new CommandDefinition<RunRequest, RunResult>("run", "Run a tool.", new CommandTraits(), [], Bind, Table)
                    {
                        MistakenFor = ["exec"],
                    }.Create();
            }
            """;
        var declarationProblems = new List<string>();
        string[] declared = [.. MistakenNames.ReadFile("sample", "Sample.cs", Declared, declarationProblems)
            .Concat(MistakenNames.ReadFile(null, "Platform.cs", Platform, declarationProblems))
            .Select(static name => $"{name.Name}>{name.Meant}:{name.Kind.ToString().ToLowerInvariant()}")];
        string[] expected =
        [
            "old_sample>set_sample:operation", "oldName>newName:field", "otherName>newName:field",
            "find>sample lookup:command", "seek>sample lookup:command", "ls>sample sheet list:command",
            "exec>tool run:command",
        ];
        if (!declared.SequenceEqual(expected, StringComparer.Ordinal) || declarationProblems.Count > 0)
        {
            yield return $"the [MistakenFor] reading of a sample reads [{string.Join(", ", declared)}] with [{string.Join("; ", declarationProblems)}], not [{string.Join(", ", expected)}]";
        }

        foreach ((string? product, string wrong, string right, string problem, bool fenced) in samples
            .Select(static sample => (sample.Product, sample.Wrong, sample.Right, sample.Problem, true))
            .Concat(prose.Select(static sample => (sample.Product, sample.Wrong, sample.Right, sample.Problem, false))))
        {
            // In a C# text, in a Markdown code span, and in a fenced block: JSON in a json block, a
            // command in a block labeled text, which is read because it writes aspose-cli.
            foreach (string way in fenced ? ["C# text", "Markdown code span", "Markdown fenced block"] : new[] { "C# text", "Markdown code span" })
            {
                bool markdown = way != "C# text";
                string Written(string text) => way switch
                {
                    "C# text" => $"Use '{text}' here.",
                    "Markdown code span" => $"Use `{text}` here.",
                    _ when text.StartsWith('{') => $"Use\n\n```json\n{{\"ops\":[{text}]}}\n```\n",
                    _ => $"Use\n\n```text\n{(text.StartsWith("aspose-cli", StringComparison.Ordinal) ? text : "aspose-cli " + text)}\n```\n",
                };
                string[] found = names.CheckSample(Written(wrong), product, markdown);
                if (!found.Any(finding => finding.Contains(problem, StringComparison.Ordinal)))
                {
                    yield return $"{way} \"{Written(wrong)}\" does not report \"{problem}\" but [{string.Join("; ", found)}]";
                }
                if (names.CheckSample(Written(right), product, markdown) is { Length: > 0 } corrected)
                {
                    yield return $"{way} \"{Written(right)}\" reports [{string.Join("; ", corrected)}]";
                }
            }
        }
    }

    /// <summary>
    /// A <c>cells read</c> over its cell budget, whose follow-up a source composes from constants
    /// that the reading of the sources cannot see: it succeeds with its first window and a
    /// window.next, and no message or hint it prints names a hidden option; only the generated
    /// window.next carries one.
    /// </summary>
    private static InvariantCase HiddenInHintCase(CliCatalog catalog) =>
        Check("C-R04", "cells read budget hint", () =>
        {
            string[] hidden = [.. HiddenOptions(catalog)];
            using var workspace = new TempWorkspace();
            File.WriteAllBytes(workspace.File("book.xlsx"), ScenarioFixtures.Read("cells.xlsx"));
            CliResult result = workspace.Run("cells", "read", "book.xlsx", "--range", "A1:Z100", "--max-cells", "1", "--output-format", "json");
            if (result.ExitCode != 0 || JsonNode.Parse(result.StdOut.Length > 0 ? result.StdOut : result.StdErr) is not { } document
                || document["error"] is not null)
            {
                return [$"a read over its budget failed with exit {result.ExitCode}: {result.StdErr.Trim()}"];
            }

            if (document["window"]?["next"] is null)
            {
                return ["a read over its budget gave no window.next"];
            }

            string text = string.Join(' ', (document["warnings"]?.AsArray() ?? [])
                .Select(static warning => $"{warning?["message"]} {warning?["hint"]}"));
            return hidden.Where(name => OptionMention(name).IsMatch(text))
                .Select(name => $"a warning of the read names the hidden option {name}");
        }, shard: "cells");

    /// <summary>The options that every command hides: a name some command shows is public.</summary>
    private static IEnumerable<string> HiddenOptions(CliCatalog catalog)
    {
        HashSet<string> shown = [.. catalog.Commands.SelectMany(static command => command.Options).Where(static option => !option.Hidden).Select(static option => option.Name)];
        return catalog.Commands.SelectMany(static command => command.Options)
            .Where(option => option.Hidden && !shown.Contains(option.Name))
            .Select(static option => option.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
    }

    private static Regex OptionMention(string name) => new($@"(?<![\w-]){Regex.Escape(name)}(?![\w-])", RegexOptions.CultureInvariant);

    /// <summary>What the reading of one project or document set found.</summary>
    private sealed record SourceFindings(string[] Names, string[] Bare, string[] Hidden, string[] Exempted, string[] Kinds);

    /// <summary>
    /// Markdown files read as one subject: a Skill directory or one file, and the product whose
    /// operations its JSON names (null for the platform Skill, the README and <c>KNOWN-ISSUES.md</c>).
    /// </summary>
    private sealed record WrittenFiles(string Subject, string Path, string? Product);

    /// <summary>The names of this build and the reading of each source project and document against them.</summary>
    private sealed partial class SourceNames
    {
        private readonly CliCatalog _catalog;
        private readonly MistakenNames _mistaken;
        private readonly OperationFields _fields;
        private readonly HashSet<string> _options;
        private readonly HashSet<string> _codes;
        private readonly HashSet<string> _operations;
        private readonly Dictionary<string, HashSet<string>> _commandOptions;
        private readonly HashSet<string> _globalOptions;
        private readonly Dictionary<string, HashSet<string>> _recursiveOptions;
        private readonly HashSet<string> _globalValueOptions;
        private readonly Regex _quoted;
        private readonly Regex _bare;
        private readonly Regex _hidden;
        private readonly string[] _products;
        private readonly HashSet<string> _commandWords;
        private readonly HashSet<string> _schemas;
        private readonly Lazy<HashSet<string>> _topics = new(static () =>
        {
            using var workspace = new TempWorkspace();
            CliResult docs = workspace.Run("docs");
            return docs.ExitCode == 0
                ? [.. docs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)]
                : throw new InvalidOperationException("docs failed: " + docs.StdErr);
        });
        private readonly ConcurrentDictionary<string, Lazy<SourceFindings>> _findings = new(StringComparer.Ordinal);

        public SourceNames(CliCatalog catalog, MistakenNames mistaken)
        {
            _catalog = catalog;
            _mistaken = mistaken;
            _fields = new OperationFields(catalog);
            JsonArray commands = catalog.Document["commands"]!.AsArray();
            _commandOptions = commands.ToDictionary(
                static command => Words(command!["path"]!.GetValue<string>()),
                static command => new HashSet<string>(command!["options"]!.AsArray().SelectMany(NamesOf), StringComparer.Ordinal),
                StringComparer.Ordinal);
            _options = [.. _commandOptions.Values.SelectMany(static options => options)];
            _globalOptions = _commandOptions[string.Empty];
            _recursiveOptions = commands.ToDictionary(
                static command => Words(command!["path"]!.GetValue<string>()),
                static command => new HashSet<string>(
                    command!["options"]!.AsArray().Where(static option => option!["recursive"]!.GetValue<bool>()).SelectMany(NamesOf),
                    StringComparer.Ordinal),
                StringComparer.Ordinal);
            _globalValueOptions =
            [
                .. catalog.Command(string.Empty).Options
                    .Where(static option => option.Type is not ("boolean" or "Void"))
                    .Select(static option => option.Name),
            ];
            _codes =
            [
                .. catalog.Diagnostics.Keys,
                .. catalog.Products.SelectMany(product => ReviewCodes(catalog, product.Id)),
            ];
            _operations =
            [
                .. catalog.Document["products"]!.AsArray()
                    .SelectMany(static product => product!["operations"]?.AsArray() ?? [])
                    .SelectMany(static operation => operation!["ops"]!.AsArray().Select(static op => op!.GetValue<string>())),
            ];
            string roots = string.Join('|', catalog.Command(string.Empty).Subcommands.Select(Regex.Escape));
            _products = [.. catalog.Products.Select(static product => product.Id)];
            _schemas = [.. PublishedSchema.Ids(catalog)];
            _commandWords = [.. CommandTree.ProductVerbs, .. catalog.Commands.SelectMany(static command => command.Tokens)];
            string products = string.Join('|', _products.Select(Regex.Escape));
            _bare = new Regex($@"(?<![\w./\\-])(?<product>{products}) (?<verb>[a-z]+)(?: (?<next>[a-z]+))?\b", RegexOptions.CultureInvariant);
            // Inside one kind of quote, the other kinds may quote a value: 'cells render --sheet "Sales"'.
            _quoted = new Regex(
                string.Join('|', "'`\"".Select(quote => $@"{quote}(?<command>(?:{roots})(?: [^{quote}\n]*?)?){quote}")),
                RegexOptions.CultureInvariant);
            // Capabilities lists no hidden command; a hidden command's name starts with '__' (C-R04).
            string[] hidden = [.. HiddenOptions(catalog).Select(Regex.Escape), "__[a-z][a-z0-9-]*"];
            _hidden = new Regex($@"(?<![\w-])(?:{string.Join('|', hidden)})(?![\w-])", RegexOptions.CultureInvariant);
        }

        private static IEnumerable<string> NamesOf(JsonNode? option) =>
            option!["aliases"]!.AsArray().Select(static alias => alias!.GetValue<string>()).Append(option["name"]!.GetValue<string>());

        private static string Words(string path) =>
            path.Contains(' ', StringComparison.Ordinal) ? path[(path.IndexOf(' ', StringComparison.Ordinal) + 1)..] : string.Empty;

        /// <summary>The reading of one source project's C# files.</summary>
        public SourceFindings Of(string project) =>
            _findings.GetOrAdd(project, key => new Lazy<SourceFindings>(() => Read(
                SourceFiles(key, "*.cs"), ProductOf(key), declares: true))).Value;

        /// <summary>The reading of a set of Markdown files.</summary>
        public SourceFindings Of(WrittenFiles document) =>
            _findings.GetOrAdd(document.Path, key => new Lazy<SourceFindings>(() => Read(
                Directory.Exists(key) ? SourceFiles(key, "*.md") : [(key, Path.GetFileName(key))], document.Product, declares: false))).Value;

        /// <summary>The problems of one text, read as a C# text or as Markdown, for <see cref="WrittenMistakes"/>.</summary>
        public string[] CheckSample(string text, string? product, bool markdown)
        {
            var exempted = new HashSet<string>(StringComparer.Ordinal);
            var kinds = new HashSet<string>(StringComparer.Ordinal);
            List<SourceText> texts = markdown ? ReadMarkdown(text) : [new SourceText(1, text)];
            return [.. texts.SelectMany(written => CheckText(written.Text, product, markdown, exempted, kinds, out _))];
        }

        /// <summary>
        /// Reads files with the one reader of their kind. Each finding is reported once per file,
        /// without its line, so an edit elsewhere in the file leaves the text alone. In C#, where
        /// a text that is exactly one name declares it, such a text is not read
        /// (<paramref name="declares"/>); Markdown declares nothing, so a code span of one name is read.
        /// </summary>
        private SourceFindings Read(IEnumerable<(string File, string Where)> files, string? product, bool declares)
        {
            var problems = new SortedSet<string>(StringComparer.Ordinal);
            var bare = new SortedSet<string>(StringComparer.Ordinal);
            var hidden = new SortedSet<string>(StringComparer.Ordinal);
            var exempted = new HashSet<string>(StringComparer.Ordinal);
            var kinds = new HashSet<string>(StringComparer.Ordinal);
            foreach ((string file, string where) in files)
            {
                foreach (SourceText text in ReadTexts(file))
                {
                    string trimmed = text.Text.Trim();
                    if (declares && (UpperSnake().IsMatch(trimmed) || LowerSnake().IsMatch(trimmed) || OptionName().IsMatch(trimmed)
                        || _hidden.Match(trimmed) is { Success: true } whole && whole.Length == trimmed.Length))
                    {
                        // A text that is exactly one name declares it.
                        continue;
                    }
                    problems.UnionWith(CheckText(text.Text, product, !declares, exempted, kinds, out List<string> written).Select(problem => $"{where}: {problem}"));
                    bare.UnionWith(written.Select(command => $"{where}: '{command}' is written bare; quote it or write aspose-cli before it"));
                    hidden.UnionWith(_hidden.Matches(text.Text).Select(match => $"{where}: names the hidden {match.Value}"));
                }
            }
            return new SourceFindings([.. problems], [.. bare], [.. hidden], [.. exempted], [.. kinds]);
        }

        /// <summary>The hidden options and commands that the Skills and the README name.</summary>
        public IEnumerable<string> HiddenInDocuments()
        {
            IEnumerable<string> skills = Directory.GetDirectories(Path.Combine(RepositoryPaths.Root, "src"))
                .Select(static project => Path.Combine(project, "Skills"))
                .Where(Directory.Exists)
                .SelectMany(static skills => Directory.EnumerateFiles(skills, "*.md", SearchOption.AllDirectories));
            foreach (string file in skills.Append(Path.Combine(RepositoryPaths.Root, "README.md")).Order(StringComparer.Ordinal))
            {
                string where = Path.GetRelativePath(RepositoryPaths.Root, file).Replace('\\', '/');
                foreach (string line in File.ReadLines(file))
                {
                    foreach (Match match in _hidden.Matches(line))
                    {
                        yield return $"{where}: names the hidden {match.Value}";
                    }
                }
            }
        }

        /// <summary>
        /// The problems of one text: its commands, options and their values, codes, operations,
        /// and the operations it writes as JSON with their fields; <paramref name="product"/>
        /// is the product whose sources or Skill hold it, which an operation name must belong to.
        /// </summary>
        private List<string> CheckText(string text, string? product, bool markdown, HashSet<string> exempted, HashSet<string> kinds, out List<string> bare)
        {
            var problems = new List<string>();
            bare = [];

            // Commands: their paths are checked against the tree, and their options against the command.
            var spans = new List<(int Start, int End)>();
            foreach (Match match in Prefixed().Matches(text))
            {
                Group command = match.Groups["command"];
                spans.Add((match.Index, command.Index + command.Length));
                problems.AddRange(CheckCommand(command.Value, exempted, kinds));
            }
            foreach (Match match in _quoted.Matches(text))
            {
                Group command = match.Groups["command"];
                if (!Inside(spans, command.Index))
                {
                    spans.Add((match.Index, match.Index + match.Length));
                    problems.AddRange(CheckCommand(command.Value, exempted, kinds));
                }
            }
            string trimmed = text.Trim();
            string[] words = trimmed.Split(' ', 3);
            bool root = words.Length >= 2 && _catalog.Command(string.Empty).Subcommands.Contains(words[0]);
            if (spans.Count == 0 && words.Length >= 2
                && ((markdown && root)
                    || (LowerWord().IsMatch(words[1])
                        && (StartsCommand(words[0], words[1], exempted)
                            || (root && _catalog.Commands.Any(command => command.Words == $"{words[0]} {words[1]}")
                                && !Prose($"{words[0]} {words[1]}", exempted))))))
            {
                // A whole text that starts with a command, such as a help example; in Markdown, a
                // code span or block that starts with a top-level command is one, whatever follows.
                spans.Add((0, text.Length));
                problems.AddRange(CheckCommand(trimmed, exempted, kinds));
            }
            if (spans.Count > 0)
            {
                kinds.Add("command");
            }
            foreach (Match match in _bare.Matches(text))
            {
                // After a product name, a lowercase word is a command word: the path must exist.
                string id = match.Groups["product"].Value;
                string verb = match.Groups["verb"].Value;
                if (!Inside(spans, match.Index) && StartsCommand(id, verb, exempted))
                {
                    bare.Add($"{id} {verb}");
                    if (_catalog.Commands.FirstOrDefault(command => command.Words == $"{id} {verb}") is not { } command)
                    {
                        problems.Add(NoCommand(id, verb));
                    }
                    else if (command.Subcommands.Count > 0 && match.Groups["next"] is { Success: true } next
                        && !command.Subcommands.Contains(next.Value))
                    {
                        problems.Add($"'{command.Words}' has no command '{next.Value}'");
                    }
                }
            }

            foreach (Match match in OptionToken().Matches(text))
            {
                string name = match.Groups["name"].Value;
                kinds.Add("option");
                if (!Inside(spans, match.Index) && !Known(name, _options, exempted))
                {
                    problems.Add($"'{name}' is no option of any command");
                }
            }
            List<(int Start, int End)> variables = [.. EnvironmentValue().Matches(text)
                .Select(static match => (match.Groups["name"].Index, match.Groups["name"].Index + match.Groups["name"].Length))];
            foreach (Match match in UpperToken().Matches(text))
            {
                kinds.Add("code");
                if (!match.Value.StartsWith("ASPOSE_", StringComparison.Ordinal) && !Inside(variables, match.Index) && !Known(match.Value, _codes, exempted))
                {
                    problems.Add($"'{match.Value}' is no diagnostic or review check");
                }
            }
            foreach (Match match in LowerToken().Matches(text))
            {
                kinds.Add("operation");
                string? meant = _mistaken.Operation(product, match.Value);
                if (!Known(match.Value, _operations, exempted))
                {
                    problems.Add(meant is null
                        ? $"'{match.Value}' is no operation of any product"
                        : $"'{match.Value}' is no operation of any product; it is a mistaken name of '{meant}'");
                }
                else if (product is not null && meant is not null && !_catalog.Product(product).Ops.Contains(match.Value, StringComparer.Ordinal))
                {
                    // Another product's operation, but the one this product's callers write by mistake.
                    problems.Add($"'{match.Value}' is no operation of {product}; it is a mistaken name of '{meant}'");
                }
            }
            problems.AddRange(CheckOperationJson(text, product, _fields, _mistaken, kinds));
            problems.AddRange(CheckDottedFields(text, product, _fields, _mistaken, kinds));
            return problems;
        }

        /// <summary>Whether a text that starts like a command is prose (<see cref="ProsePhrases"/>), noting the exemption as used.</summary>
        private static bool Prose(string start, HashSet<string> exempted)
        {
            if (!ProsePhrases.ContainsKey(start))
            {
                return false;
            }
            exempted.Add(start);
            return true;
        }

        /// <summary>That <paramref name="parent"/> has no command <paramref name="token"/>, and the commands a product declares it mistaken for.</summary>
        private string NoCommand(string parent, string token) =>
            _mistaken.Commands(parent, token).ToArray() is { Length: > 0 } meant
                ? $"'{parent}' has no command '{token}'; it is a mistaken name of '{string.Join("' or '", meant)}'"
                : $"'{parent}' has no command '{token}'";

        /// <summary>Whether a product id and the word after it start a command path.</summary>
        private bool StartsCommand(string product, string word, HashSet<string> exempted)
        {
            if (!_products.Contains(product))
            {
                return false;
            }
            if (!NounProducts.ContainsKey(product))
            {
                return true;
            }
            if (!_commandWords.Contains(word))
            {
                return false;
            }
            return !Prose($"{product} {word}", exempted);
        }

        private static bool Known(string name, HashSet<string> names, HashSet<string> exempted)
        {
            if (names.Contains(name))
            {
                return true;
            }
            if (NotNames.ContainsKey(name))
            {
                exempted.Add(name);
                return true;
            }
            return false;
        }

        private static bool Inside(List<(int Start, int End)> spans, int index) =>
            spans.Any(span => index >= span.Start && index < span.End);

        /// <summary>
        /// Walks a written command down the tree: global options and their values may come first,
        /// then each word must name a subcommand until a leaf, whose options are its own or global.
        /// A clause or sentence ends the command. A placeholder or interpolated word where a
        /// command word belongs stops the walk, and the options after it need only exist
        /// somewhere; so does a word after the bare executable name, which is prose (<c>the
        /// aspose-cli executable</c>).
        /// </summary>
        private IEnumerable<string> CheckCommand(string written, HashSet<string> exempted, HashSet<string> kinds)
        {
            var tokens = new List<string>();
            foreach (string word in Word().Matches(written).Select(static match => match.Value))
            {
                if (word.StartsWith('\'') && word.Length > 1)
                {
                    // A single-quoted value, such as the JSON of --ops, is one word of no name.
                    tokens.Add(Hole);
                    continue;
                }
                string token = word.TrimEnd('.', ',', ';', ':', ')');
                if (token.Length > 0)
                {
                    tokens.Add(token);
                }
                if (token.Length != word.Length)
                {
                    break;
                }
            }
            CliCommand command = _catalog.Command(string.Empty);
            bool resolved = true;
            int index = 0;
            for (; index < tokens.Count; index++)
            {
                string token = tokens[index];
                if (token.StartsWith('-'))
                {
                    string name = token.Split('=', 2)[0];
                    if (command.Words.Length == 0 && _globalOptions.Contains(name))
                    {
                        if (CheckValue(command, tokens, index, kinds) is { } problem)
                        {
                            yield return problem;
                        }
                        index += _globalValueOptions.Contains(name) && !token.Contains('=', StringComparison.Ordinal) ? 1 : 0;
                        continue;
                    }
                    break;
                }
                if (IsPlaceholder(token))
                {
                    resolved = command.Subcommands.Count == 0;
                    break;
                }
                string words = command.Words.Length == 0 ? token : $"{command.Words} {token}";
                CliCommand? next = _catalog.Commands.FirstOrDefault(candidate => candidate.Words == words);
                if (next is null)
                {
                    if (command.Subcommands.Count > 0)
                    {
                        resolved = false;
                        if (command.Words.Length > 0)
                        {
                            yield return NoCommand(command.Words, token);
                        }
                    }
                    else if (command.Words.Length > 0 && command.Arguments.Count == 0)
                    {
                        // A command that takes no argument has nothing a word after it could be.
                        yield return NoCommand(command.Words, token);
                    }
                    break;
                }
                command = next;
            }
            // A command takes its own options and the recursive ones of the commands above it.
            string[] path = command.Tokens;
            HashSet<string> options = resolved
                ? [.. _commandOptions[command.Words], .. Enumerable.Range(0, path.Length).SelectMany(depth => _recursiveOptions[string.Join(' ', path[..depth])])]
                : _options;
            bool positional = false;
            for (; index < tokens.Count; index++)
            {
                if (resolved && !positional && !tokens[index].StartsWith('-'))
                {
                    // The first argument of docs and schema names a topic or a schema.
                    positional = true;
                    if (CheckArgument(command, tokens[index], kinds) is { } unknown)
                    {
                        yield return unknown;
                    }
                }
                else if (tokens[index].StartsWith('-') && !tokens[index].Contains('=', StringComparison.Ordinal)
                    && (command.Option(tokens[index]) ?? _catalog.Command(string.Empty).Option(tokens[index])) is { Type: not ("boolean" or "Void") })
                {
                    // An option's value is no argument.
                    if (CheckValue(command, tokens, index, kinds) is { } wrong)
                    {
                        yield return wrong;
                    }
                    index++;
                    continue;
                }
                Match option = OptionName().Match(tokens[index].Split('=', 2)[0]);
                if (option.Success && !Known(option.Value, options, exempted))
                {
                    yield return resolved
                        ? $"'{(command.Words.Length == 0 ? "aspose-cli" : command.Words)}' has no option '{option.Value}'"
                        : $"'{option.Value}' is no option of any command";
                }
                else if (option.Success && resolved && CheckValue(command, tokens, index, kinds) is { } problem)
                {
                    yield return problem;
                }
            }
        }

        /// <summary>
        /// The value written for the option at <paramref name="index"/>, after <c>=</c> or as the
        /// next word, must be one the option allows when it lists its values (<c>--detail
        /// outline</c>); a placeholder, a variable or a quoted value is not judged, and only the
        /// first value of a repeatable option is, since the words after it may be arguments.
        /// </summary>
        private string? CheckArgument(CliCommand command, string token, HashSet<string> kinds)
        {
            (HashSet<string>? names, string what) = command.Words switch
            {
                "docs" => (_topics.Value, "topic"),
                "schema" => (_schemas, "schema"),
                _ => ((HashSet<string>?)null, string.Empty),
            };
            if (names is null || IsPlaceholder(token) || !ArgumentName().IsMatch(token))
            {
                return null;
            }
            kinds.Add("argument");
            return names.Contains(token) ? null : $"'{command.Words}' has no {what} '{token}'";
        }

        private string? CheckValue(CliCommand command, List<string> tokens, int index, HashSet<string> kinds)
        {
            string[] parts = tokens[index].Split('=', 2);
            CliOption? option = command.Option(parts[0]) ?? _catalog.Command(string.Empty).Option(parts[0]);
            if (option is null || option.Type == "boolean" || option.AllowedValues.Count == 0)
            {
                return null;
            }
            string? value = parts.Length == 2 ? parts[1] : index + 1 < tokens.Count ? tokens[index + 1] : null;
            if (value is null || value.Length == 0 || value.StartsWith('-') || IsPlaceholder(value) || value.IndexOfAny(['$', '{', '"', '\'', '%', '*', '|']) >= 0)
            {
                return null;
            }
            kinds.Add("value");
            return option.AllowedValues.Contains(value, StringComparer.Ordinal)
                ? null
                : $"'{(command.Words.Length == 0 ? "aspose-cli" : command.Words)} {option.Name}' has no value '{value}'";
        }

        private static bool IsPlaceholder(string token) =>
            token.StartsWith('<') || token.Contains(Hole, StringComparison.Ordinal) || token.StartsWith("...", StringComparison.Ordinal);

        /// <summary>A command after the executable name, to the end of its quote; a single-quoted value inside it, such as the JSON of <c>--ops</c>, does not end it.</summary>
        [GeneratedRegex(@"(?<![\w-])aspose-cli(?<command>(?: (?:'[^'\n]*'(?=\s|$)|[^\s'`]+))*)")]
        private static partial Regex Prefixed();

        /// <summary>A word of a written command: a single-quoted value that a space or the end follows, or a run of non-space.</summary>
        [GeneratedRegex(@"'[^'\n]*'(?=\s|$)|\S+")]
        private static partial Regex Word();

        /// <summary>An option name, unless an interpolation or a placeholder completes it (<c>--all-{part}s</c>).</summary>
        [GeneratedRegex(@"(?<![\w-])(?<name>--[a-z][a-z0-9-]*)(?![a-z0-9-]|[…{])")]
        private static partial Regex OptionToken();

        /// <summary>A word that names a topic or schema rather than standing for one (no placeholder or file pattern).</summary>
        [GeneratedRegex(@"^[A-Za-z0-9][\w./-]*$")]
        private static partial Regex ArgumentName();

        [GeneratedRegex(@"^[a-z]+$")]
        private static partial Regex LowerWord();

        [GeneratedRegex(@"^--[a-z][a-z0-9-]*$")]
        private static partial Regex OptionName();

        [GeneratedRegex(@"(?<![\w-])[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)+(?![\w-])")]
        private static partial Regex UpperToken();

        /// <summary>An operation name: not part of a path, a file name (<c>eu_raw.xlsx</c>) or a template field (<c>{{contract_no}}</c>).</summary>
        [GeneratedRegex(@"(?<![\w./\\{-])[a-z][a-z0-9]*(?:_[a-z0-9]+)+(?![\w-]|\.\w)")]
        private static partial Regex LowerToken();

        /// <summary>The environment variable an option such as <c>--encrypt-env</c> names, which is no code.</summary>
        [GeneratedRegex(@"(?<![\w-])--[a-z][a-z0-9-]*-env[ =](?<name>[A-Z][A-Z0-9_]*)")]
        private static partial Regex EnvironmentValue();

        [GeneratedRegex(@"^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)+$")]
        private static partial Regex UpperSnake();

    }

    /// <summary>A whole snake_case name, such as an operation's.</summary>
    [GeneratedRegex(@"^[a-z][a-z0-9]*(?:_[a-z0-9]+)+$")]
    private static partial Regex LowerSnake();
}
