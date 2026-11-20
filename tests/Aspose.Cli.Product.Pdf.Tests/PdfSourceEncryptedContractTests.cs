using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// The <c>input</c> a PDF result describes is the shared input record, whose <c>encrypted</c>
/// states whether the file opens only with its password. Against the real engine: every result
/// reports it, it is the truth, and every PDF result schema accepts the boolean truth there and
/// refuses any other type.
/// </summary>
public sealed class PdfSourceEncryptedContractTests : IDisposable
{
    private const string UserPassword = "PDF_SOURCE_USER_PASSWORD";
    private const string OwnerPassword = "PDF_SOURCE_OWNER_PASSWORD";
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    // Twelve licensed CLI runs and the load of every published schema: about 10 s even at once.
    [Category(TestCategory.Slow)]
    [LicensedFact]
    public void SourceEncrypted_IsTruthfulAndAcceptedByEveryResultSchema()
    {
        string license = TestLicense.Path!;
        var passwords = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [UserPassword] = "open-sesame",
            [OwnerPassword] = "owner-sesame",
        };
        File.WriteAllText(_workspace.File("page.html"), "<html><body><p>Encrypted source.</p></body></html>");
        File.WriteAllText(
            _workspace.File("encrypt.json"),
            $$$"""{"ops":[{"do":"encrypt","args":{"userPasswordEnv":"{{{UserPassword}}}","ownerPasswordEnv":"{{{OwnerPassword}}}","permissions":[],"cryptoAlgorithm":"AESx256"}}]}""");
        _workspace.Run("pdf", "convert", "page.html", "--out", "plain.pdf", "--license", license, "--output-format", "json").Succeeded();
        _workspace.RunWithEnv(passwords, "pdf", "edit", "plain.pdf", "--ops", "encrypt.json", "--out", "locked.pdf", "--license", license, "--output-format", "json").Succeeded();

        (string Name, bool Encrypted, CliResult Result)[] runs =
        [
            .. new[] { (File: "plain.pdf", Encrypted: false), (File: "locked.pdf", Encrypted: true) }
                .SelectMany(static input => Commands(input.File), static (input, command) => (input.Encrypted, Command: command))
                .Select(run => (
                    string.Join(' ', run.Command),
                    run.Encrypted,
                    _workspace.RunWithEnv(
                        passwords,
                        [.. run.Command, "--license", license, "--output-format", "json", .. run.Encrypted ? new[] { "--password-env", UserPassword } : []]))),
        ];

        var problems = new List<string>();
        foreach ((string name, bool encrypted, CliResult result) in runs)
        {
            if (result.ExitCode != 0)
            {
                problems.Add($"{name} failed: {result.StdErr}");
                continue;
            }

            Check(name, JsonNode.Parse(result.StdOut)!.AsObject(), encrypted, problems);
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    private static string[][] Commands(string file) =>
    [
        ["pdf", "read", file],
        ["pdf", "search", file, "--pattern", "Encrypted"],
        ["pdf", "render", file, "--out", $"{file}.png", "--overwrite"],
        ["pdf", "validate", file, "--profile", "pdfa-1b"],
        ["pdf", "extract", file, "--what", "images", "--out-dir", $"{file}-images"],
    ];

    private static void Check(string name, JsonObject result, bool encrypted, List<string> problems)
    {
        string id = PublishedSchemas.IdOf(result["schema"]!.GetValue<string>());
        const string member = "input";
        if (result[member] is not JsonObject input)
        {
            problems.Add($"{name}: {id} does not describe its input as input");
            return;
        }

        if (input["encrypted"] is not JsonNode reported
            || reported.GetValueKind() is not (JsonValueKind.True or JsonValueKind.False) || reported.GetValue<bool>() != encrypted)
        {
            problems.Add($"{name}: {member}.encrypted is {input["encrypted"]?.ToJsonString() ?? "missing"} for a file that is {(encrypted ? "" : "not ")}encrypted");
        }

        if (!IsValid(id, result, member, JsonValue.Create(encrypted)))
        {
            problems.Add($"{name}: {id} refuses {member}.encrypted {(encrypted ? "true" : "false")}, which the shared input record carries");
        }

        if (IsValid(id, result, member, JsonValue.Create("yes")))
        {
            problems.Add($"{name}: {id} accepts a {member}.encrypted that is not a boolean");
        }
    }

    private static bool IsValid(string id, JsonObject result, string member, JsonNode value)
    {
        var copy = result.DeepClone().AsObject();
        copy[member]!["encrypted"] = value;
        using JsonDocument instance = JsonDocument.Parse(copy.ToJsonString());
        return PublishedSchemas.Schema(id).Evaluate(instance.RootElement).IsValid;
    }
}
