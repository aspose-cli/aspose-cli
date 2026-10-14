using Aspose.Cli.TestKit.Scenarios;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>
/// A command line under construction with the seed files and environment it needs. <see cref="For"/>
/// builds the smallest valid invocation of a command from its catalog entry, so a case only adds
/// the one thing it tests.
/// </summary>
internal sealed class Invocation
{
    /// <summary>The variable that carries the signing certificate's password.</summary>
    public const string CertificateVariable = "INVARIANT_CERTIFICATE_PASSWORD";

    public Invocation(IEnumerable<string> args) => Args = [.. args];

    public List<string> Args { get; }

    public Dictionary<string, ScenarioFile> Files { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> Env { get; } = new(StringComparer.Ordinal);

    /// <summary>The input documents in argument order.</summary>
    public List<string> Inputs { get; } = [];

    /// <summary>The first input document, when the command reads one.</summary>
    public string? Input => Inputs.Count > 0 ? Inputs[0] : null;

    /// <summary>
    /// The command with every required argument and option filled: input files are the product's
    /// fixture document (a platform command reads the first product's), a path the command
    /// creates has the product's own extension, an option with allowed values takes the product's
    /// own format when it allows it (a convert keeps its format) and the first value otherwise,
    /// and the few required free-form options take a fixed valid value. An edit gets the
    /// product's valid operation document and a create its source text. An <c>open</c> command,
    /// which opens a browser by default, gets <see cref="NoOpen"/>.
    /// </summary>
    /// <param name="command">The command to invoke.</param>
    /// <param name="omit">An option the caller supplies itself.</param>
    /// <exception cref="InvalidOperationException">
    /// A required option has no value rule, or an <c>open</c> command has no <see cref="NoOpen"/>.
    /// </exception>
    public static Invocation For(CliCommand command, string? omit = null)
    {
        string product = command.Product ?? ScenarioFixtures.Products.First();
        string extension = ScenarioFixtures.PrimaryFormat(product);
        var invocation = new Invocation(command.Tokens);
        string[] names = ["input", "second"];
        int inputs = 0;
        foreach (CliArgument argument in command.Arguments.Where(static argument => argument.Required))
        {
            if (argument.InputKind != "file")
            {
                invocation.Args.Add("created." + extension);
                continue;
            }
            int count = argument.MaximumArity > 1 ? 2 : 1;
            for (int index = 0; index < count && inputs < names.Length; index++)
            {
                string file = $"{names[inputs++]}.{extension}";
                invocation.Files[file] = new ScenarioFile { Fixture = $"{product}.{extension}" };
                invocation.Inputs.Add(file);
                invocation.Args.Add(file);
            }
        }
        foreach (CliOption option in command.Options.Where(option => option.Name != omit && (option.Required || IsImplied(command, option, omit))))
        {
            invocation.Fill(command, product, extension, option);
        }
        if (command.Verb == OpenVerb)
        {
            // A test never opens the user's browser; an open command without the option fails
            // rather than opening one.
            invocation.Args.Add(command.Has(NoOpen)
                ? NoOpen
                : throw new InvalidOperationException($"{command} opens a browser and has no {NoOpen}."));
        }
        return invocation;
    }

    /// <summary>Appends arguments.</summary>
    public Invocation Add(params string[] args)
    {
        Args.AddRange(args);
        return this;
    }

    /// <summary>Replaces an argument, such as an input path.</summary>
    public void Replace(string value, string replacement)
    {
        int index = Args.IndexOf(value);
        Args[index] = replacement;
    }

    /// <summary>
    /// The options that no declaration requires but that a command of a verb needs to run, each
    /// with the file it reads (null for a value rule of <see cref="Fill"/>): a convert without
    /// --out names the format it writes, and a create from a source reads the first of these it
    /// has. Each must name an option some command has (<see cref="ImpliedRulesMatchTheCatalog"/>),
    /// so a renamed option fails rather than leaving its commands without it.
    /// </summary>
    internal static readonly (string Verb, string Option, string? File, string? Fixture)[] ImpliedByVerb =
    [
        ("convert", "--to", null, null),
        ("create", "--from-markdown", "source.md", "markdown"),
        ("create", "--from-text", "source.txt", "text"),
    ];

    /// <summary>
    /// Whether a command needs an option that it does not declare required. From the command tree:
    /// an operations document (input kind <c>json-source</c>), and the environment variable of the
    /// password of a required file input (<c>--certificate</c> and its
    /// <c>--certificate-password-env</c>) unless the case passes the password another way
    /// (<paramref name="omit"/>). By verb: <see cref="ImpliedByVerb"/>, where a create takes only
    /// its first source.
    /// </summary>
    private static bool IsImplied(CliCommand command, CliOption option, string? omit)
    {
        if (option.InputKind == JsonSource)
        {
            return true;
        }
        if (PasswordOf(command, option) is { } secured)
        {
            return omit?.StartsWith(secured.Name + "-password", StringComparison.Ordinal) != true;
        }
        return option.Name == ImpliedByVerb
            .Where(rule => rule.Verb == command.Verb && command.Has(rule.Option))
            .Select(static rule => rule.Option)
            .FirstOrDefault();
    }

    /// <summary>The verb of the commands that open a browser by default.</summary>
    private const string OpenVerb = "open";

    /// <summary>The flag of an <see cref="OpenVerb"/> command that keeps the browser closed.</summary>
    internal const string NoOpen = "--no-open";

    /// <summary>The input kind of an option whose value is an operations document.</summary>
    private const string JsonSource = "json-source";

    /// <summary>The required file input whose password <paramref name="option"/> names the variable of, or null.</summary>
    private static CliOption? PasswordOf(CliCommand command, CliOption option) =>
        option.ValueSource == "environment-variable-name"
            ? command.Options.FirstOrDefault(input => input.Required && input.InputKind == "file" && option.Name == input.Name + "-password-env")
            : null;

    /// <summary>
    /// The rules of <see cref="ImpliedByVerb"/> that no command of the catalog matches, the
    /// create commands with file sources none of them names, and the open commands without
    /// <see cref="NoOpen"/>: each means an option was renamed.
    /// </summary>
    internal static IEnumerable<string> ImpliedRulesMatchTheCatalog(IReadOnlyList<CliCommand> commands)
    {
        foreach ((string verb, string option, _, _) in ImpliedByVerb)
        {
            if (!commands.Any(command => command.Verb == verb && command.Has(option)))
            {
                yield return $"no {verb} command has {option}, which the invocation rules imply";
            }
        }
        foreach (CliCommand command in commands.Where(static command => command.Arguments.Any(static argument => argument.Required && argument.InputKind != "file")
            && command.Options.Any(static option => option.InputKind == "file" && !option.Required)))
        {
            if (!ImpliedByVerb.Any(rule => rule.Verb == command.Verb && command.Has(rule.Option)))
            {
                yield return $"{command} creates its argument from file options none of the invocation rules names";
            }
        }
        if (!commands.Any(static command => command.Verb == OpenVerb))
        {
            yield return $"no command has the verb {OpenVerb}, whose commands the invocation keeps from opening a browser";
        }
        foreach (CliCommand command in commands.Where(static command => command.Verb == OpenVerb && !command.Has(NoOpen)))
        {
            yield return $"{command} opens a browser and has no {NoOpen}";
        }
    }

    private void Fill(CliCommand command, string product, string extension, CliOption option)
    {
        if (option.InputKind == JsonSource)
        {
            Files["ops.json"] = new ScenarioFile { Fixture = $"{product}.ops" };
            Add(option.Name, "ops.json");
            return;
        }
        if (PasswordOf(command, option) is not null)
        {
            Env[CertificateVariable] = ScenarioFixtures.Password;
            Add(option.Name, CertificateVariable);
            return;
        }
        if (ImpliedByVerb.FirstOrDefault(rule => rule.Verb == command.Verb && rule.Option == option.Name) is { File: { } file, Fixture: { } fixture })
        {
            Files[file] = new ScenarioFile { Fixture = fixture };
            Add(option.Name, file);
            return;
        }
        switch (option.Name)
        {
            case "--pattern":
                Add(option.Name, "Hello");
                return;
            case "--out":
                Add(option.Name, "output." + extension);
                return;
            case "--out-dir":
                Add(option.Name, "output");
                return;
            case "--certificate":
                Files["certificate.pfx"] = new ScenarioFile { Fixture = "certificate.pfx" };
                Add(option.Name, "certificate.pfx");
                return;
        }
        if (option.AllowedValues.Contains("pages"))
        {
            // A split by pages runs on every fixture, which has no bookmarks or headings.
            Add(option.Name, "pages");
            return;
        }
        if (option.AllowedValues.Count > 0)
        {
            Add(option.Name, option.AllowedValues.Contains(extension) ? extension : option.AllowedValues[0]);
            return;
        }
        throw new InvalidOperationException($"No value rule for the required option {option.Name} of {command}.");
    }
}
