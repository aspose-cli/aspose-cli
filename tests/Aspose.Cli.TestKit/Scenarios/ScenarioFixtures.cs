using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Nodes;

namespace Aspose.Cli.TestKit.Scenarios;

/// <summary>
/// Named seed files for scenarios. Documents are built once per test process with the products'
/// own commands, under the run's license mode, and copied into each scenario workspace:
/// <list type="bullet">
/// <item><c>&lt;product&gt;.&lt;format&gt;</c>: a small document; the product's primary format
/// (<see cref="PrimaryFormat"/>) is created with its <c>create</c> command, any other format is
/// that document converted with its <c>convert --to</c>.</item>
/// <item><c>&lt;product&gt;.encrypted</c>: the product's primary-format document encrypted with
/// <see cref="Password"/>.</item>
/// <item><c>&lt;product&gt;.ops</c>: a valid one-operation document for the product's <c>edit --ops</c>.</item>
/// <item><c>markdown</c> and <c>text</c>: source text that holds <see cref="SampleText"/>.</item>
/// <item><c>certificate.pfx</c>: a self-signed signing certificate protected by <see cref="Password"/>.</item>
/// </list>
/// </summary>
public static class ScenarioFixtures
{
    /// <summary>Text every built document contains, for searches.</summary>
    public const string SampleText = "Hello invariant world";

    /// <summary>
    /// The password of every protected fixture: the encrypted documents and <c>certificate.pfx</c>.
    /// It is a secret, so the CLI must never print it.
    /// </summary>
    public const string Password = "Invariant-Secret-5f1c9e7a";

    private const string PasswordVariable = "FIXTURE_PASSWORD";

    private static readonly Dictionary<string, string> PrimaryFormats = new(StringComparer.Ordinal)
    {
        ["cells"] = "xlsx",
        ["pdf"] = "pdf",
        ["slides"] = "pptx",
        ["words"] = "docx",
    };

    private static readonly Dictionary<string, string> Operations = new(StringComparer.Ordinal)
    {
        ["cells"] = """{"ops":[{"op":"set_values","sheet":"Sheet1","range":"D1","values":[["edited"]]}]}""",
        ["pdf"] = """{"ops":[{"op":"set_metadata","title":"Edited"}]}""",
        ["slides"] = """{"ops":[{"op":"set_properties","title":"Edited"}]}""",
        ["words"] = """{"ops":[{"op":"set_properties","title":"Edited"}]}""",
    };

    private static readonly string Markdown =
        $"# {SampleText}\n\nThe first paragraph of the seed document.\n\n## Second section\n\n- One point\n- Another point\n";

    private static readonly string Text = $"{SampleText}\n\nThe second paragraph of the seed document.\n";

    private static readonly ConcurrentDictionary<string, Lazy<byte[]>> Built = new(StringComparer.Ordinal);

    /// <summary>The products that have document fixtures.</summary>
    public static IReadOnlyCollection<string> Products => PrimaryFormats.Keys;

    /// <summary>The format of a product's own fixture document, such as <c>xlsx</c>.</summary>
    public static string PrimaryFormat(string product) => PrimaryFormats[product];

    /// <summary>The bytes of a named fixture.</summary>
    /// <exception cref="ArgumentException">The name is not a fixture.</exception>
    public static byte[] Read(string name) =>
        Built.GetOrAdd(name, static key => new Lazy<byte[]>(() => Build(key))).Value;

    private static byte[] Build(string name)
    {
        switch (name)
        {
            case "markdown":
                return Encoding.UTF8.GetBytes(Markdown);
            case "text":
                return Encoding.UTF8.GetBytes(Text);
            case "certificate.pfx":
                return Certificate();
        }
        int dot = name.IndexOf('.', StringComparison.Ordinal);
        string product = dot > 0 ? name[..dot] : string.Empty;
        string format = dot > 0 ? name[(dot + 1)..] : string.Empty;
        if (!PrimaryFormats.TryGetValue(product, out string? primary) || format.Length == 0)
        {
            throw new ArgumentException($"'{name}' is not a scenario fixture.", nameof(name));
        }
        return format switch
        {
            "ops" => Encoding.UTF8.GetBytes(Operations[product]),
            "encrypted" => Encrypt(product, primary),
            _ when format == primary => CreateDocument(product, primary),
            _ => Convert(product, primary, format),
        };
    }

    private static byte[] CreateDocument(string product, string format)
    {
        using var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);
        File.WriteAllText(workspace.File("source.md"), Markdown);
        File.WriteAllText(workspace.File("source.txt"), Text);
        string file = "seed." + format;
        switch (product)
        {
            case "cells":
                Require(workspace.Run("cells", "create", file, "--output", "json"));
                File.WriteAllText(workspace.File("ops.json"),
                    $$"""{"ops":[{"op":"set_values","sheet":"Sheet1","range":"A1","values":[["Item","Amount"],["{{SampleText}}",1],["Second row",2]]}]}""");
                Require(workspace.Run("cells", "edit", file, "--ops", "ops.json", "--in-place", "--output", "json"));
                break;
            case "pdf":
                Require(workspace.Run("pdf", "create", file, "--from-text", "source.txt", "--output", "json"));
                break;
            case "slides":
                Require(workspace.Run("slides", "create", file, "--from-markdown", "source.md", "--output", "json"));
                break;
            case "words":
                Require(workspace.Run("words", "create", file, "--markdown", "source.md", "--output", "json"));
                break;
        }
        return File.ReadAllBytes(workspace.File(file));
    }

    private static byte[] Convert(string product, string primary, string format)
    {
        using var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);
        string source = "seed." + primary;
        File.WriteAllBytes(workspace.File(source), Read($"{product}.{primary}"));
        JsonNode result = Require(workspace.Run(product, "convert", source, "--to", format, "--output", "json"));
        string path = (result["output"]?["path"] ?? result["outputs"]![0]!["path"])!.GetValue<string>();
        return File.ReadAllBytes(path);
    }

    /// <summary>
    /// The primary document encrypted with <see cref="Password"/>: a PDF by its <c>encrypt</c>
    /// operation, any other format by <c>convert --encrypt-env</c> to its own format.
    /// </summary>
    private static byte[] Encrypt(string product, string primary)
    {
        using var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);
        string source = "seed." + primary;
        string target = "encrypted." + primary;
        File.WriteAllBytes(workspace.File(source), Read($"{product}.{primary}"));
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal) { [PasswordVariable] = Password };
        string[] args = product == "pdf"
            ? ["pdf", "edit", source, "--ops", "ops.json", "--out", target, "--output", "json"]
            : [product, "convert", source, "--to", primary, "--out", target, "--encrypt-env", PasswordVariable, "--output", "json"];
        if (product == "pdf")
        {
            File.WriteAllText(workspace.File("ops.json"),
                $$"""{"ops":[{"op":"encrypt","userPasswordEnv":"{{PasswordVariable}}","ownerPasswordEnv":"{{PasswordVariable}}"}]}""");
        }
        Require(workspace.RunWithEnv(environment, args));
        return File.ReadAllBytes(workspace.File(target));
    }

    private static JsonNode Require(CliResult result) =>
        result.ExitCode == 0
            ? JsonNode.Parse(result.StdOut)!
            : throw new InvalidOperationException($"A scenario fixture could not be built: exit {result.ExitCode}: {result.StdErr}");

    private static byte[] Certificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Aspose CLI scenario fixture", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return certificate.Export(X509ContentType.Pfx, Password);
    }
}
