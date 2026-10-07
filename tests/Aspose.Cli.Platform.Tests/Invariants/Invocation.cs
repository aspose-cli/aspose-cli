using Aspose.Cli.TestKit.Scenarios;

namespace Aspose.Cli.Platform.Tests.Invariants;

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
    /// product's valid operation document and a create its source text.
    /// </summary>
    /// <param name="command">The command to invoke.</param>
    /// <param name="omit">An option the caller supplies itself.</param>
    /// <exception cref="InvalidOperationException">A required option has no value rule.</exception>
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
        foreach (CliOption option in command.Options.Where(option => option.Name != omit && (option.Required || IsImplied(command, option))))
        {
            invocation.Fill(command, product, extension, option);
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

    // Options that no single declaration requires, but that the command needs to run: an edit
    // needs operations, a create something to create from, an extract a directory for files and
    // a split a way to split.
    private static bool IsImplied(CliCommand command, CliOption option) =>
        (command.Verb, option.Name) is ("edit", "--ops") or ("extract", "--out-dir") or ("split", "--every")
        || (command.Verb == "create" && option.Name == command.Options
            .Select(static candidate => candidate.Name)
            .FirstOrDefault(static name => name is "--markdown" or "--from-markdown" or "--from-text"));

    private void Fill(CliCommand command, string product, string extension, CliOption option)
    {
        switch (option.Name)
        {
            case "--ops":
                Files["ops.json"] = new ScenarioFile { Fixture = $"{product}.ops" };
                Add(option.Name, "ops.json");
                return;
            case "--markdown" or "--from-markdown":
                Files["source.md"] = new ScenarioFile { Fixture = "markdown" };
                Add(option.Name, "source.md");
                return;
            case "--from-text":
                Files["source.txt"] = new ScenarioFile { Fixture = "text" };
                Add(option.Name, "source.txt");
                return;
            case "--pattern":
                Add(option.Name, "Hello");
                return;
            case "--out":
                Add(option.Name, "output." + extension);
                return;
            case "--every":
                Add(option.Name, "1");
                return;
            case "--out-dir":
                Add(option.Name, "output");
                return;
            case "--certificate":
                Files["certificate.pfx"] = new ScenarioFile { Fixture = "certificate.pfx" };
                Add(option.Name, "certificate.pfx");
                return;
            case "--certificate-password-env":
                Env[CertificateVariable] = ScenarioFixtures.Password;
                Add(option.Name, CertificateVariable);
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
