using System.Text.Json;
using Aspose.Cli.Sdk.Resources;
using Json.Schema;

namespace Aspose.Cli.TestKit;

/// <summary>
/// A disposable working directory and isolated user configuration for real CLI tests.
/// </summary>
public sealed class TempWorkspace : IDisposable
{
    private static readonly object SchemaLock = new();
    private static readonly Dictionary<string, JsonSchema> Schemas =
        new(StringComparer.Ordinal);
    private static readonly BuildOptions SchemaBuildOptions =
        SchemaTestRegistry.CreateOptions();
    private readonly TempDirectory _directory = new();
    private readonly TempDirectory _configDirectory = new();

    /// <summary>Absolute path of the working directory.</summary>
    public string Path => _directory.Path;

    /// <summary>Absolute path of the isolated CLI configuration directory.</summary>
    public string ConfigDirectory => _configDirectory.File("aspose-cli");

    /// <summary>Resolves a path inside the working directory.</summary>
    public string File(string relativePath) => _directory.File(relativePath);

    public CliResult Run(params string[] args) =>
        Execute(standardInput: null, variables: null, args);

    public CliResult RunWithInput(string standardInput, params string[] args) =>
        Execute(standardInput, variables: null, args);

    /// <summary>
    /// Runs a command line the CLI printed for the caller to run verbatim, such as a result's
    /// <c>window.next</c>: it is split the way a shell splits the quoting the continuation writes
    /// (double quotes, with <c>\"</c>, <c>\$</c> and <c>\`</c> escaped inside them), and its
    /// leading executable name is dropped.
    /// </summary>
    public CliResult RunCommandLine(string commandLine)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandLine);
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        bool quoted = false;
        bool started = false;
        for (int index = 0; index < commandLine.Length; index++)
        {
            char character = commandLine[index];
            if (quoted && character == '\\' && index + 1 < commandLine.Length
                && commandLine[index + 1] is '"' or '$' or '`')
            {
                current.Append(commandLine[++index]);
            }
            else if (character == '"')
            {
                quoted = !quoted;
                started = true;
            }
            else if (character == ' ' && !quoted)
            {
                if (started)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                    started = false;
                }
            }
            else
            {
                current.Append(character);
                started = true;
            }
        }

        if (started)
        {
            tokens.Add(current.ToString());
        }

        if (tokens.Count == 0 || tokens[0] != Aspose.Cli.Sdk.DistributionInfo.CommandName)
        {
            throw new ArgumentException($"Not a command line of this CLI: {commandLine}", nameof(commandLine));
        }

        return Run([.. tokens.Skip(1)]);
    }

    /// <summary>Runs in evaluation mode with extra environment variables set on the child.</summary>
    public CliResult RunWithEnv(IReadOnlyDictionary<string, string?> variables, params string[] args)
        => Execute(standardInput: null, variables, args);

    /// <summary>
    /// A start of the tested CLI in this workspace, under the same isolated evaluation
    /// environment as <see cref="Run"/>, for tests that supervise the process themselves.
    /// </summary>
    public System.Diagnostics.ProcessStartInfo StartInfo(IReadOnlyDictionary<string, string?>? variables = null)
    {
        var start = new System.Diagnostics.ProcessStartInfo(CliRunner.ExecutablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path,
        };
        CliEnvironment.Evaluation(_configDirectory.Path, variables).Apply(start.Environment);
        return start;
    }

    public void Dispose()
    {
        _directory.Dispose();
        _configDirectory.Dispose();
    }

    private CliResult Execute(
        string? standardInput,
        IReadOnlyDictionary<string, string?>? variables,
        IReadOnlyList<string> args)
    {
        var environment = CliEnvironment.Evaluation(
            _configDirectory.Path,
            variables);
        CliResult result = new CliProcess(
            CliRunner.ExecutablePath,
            environment).Run(Path, standardInput, args: [.. args]);
        return ValidateJsonContract(result, args);
    }

    private CliResult ValidateJsonContract(
        CliResult result,
        IReadOnlyList<string> args)
    {
        if (!RequestsJson(args))
        {
            return result;
        }

        // Partial results are successful envelopes written to stdout with
        // exit code 8. All failures keep stdout empty and write one error
        // envelope to stderr.
        string document = string.IsNullOrWhiteSpace(result.StdOut)
            ? result.StdErr
            : result.StdOut;
        if (string.IsNullOrWhiteSpace(document))
        {
            throw new InvalidOperationException(
                $"JSON CLI invocation [{string.Join(' ', args)}] returned no JSON document.");
        }

        using JsonDocument instance = ParseSingleJsonDocument(document, args);
        if (IsSchemaCommand(args))
        {
            return result;
        }

        if (instance.RootElement.ValueKind != JsonValueKind.Object
            || !instance.RootElement.TryGetProperty(
                "schema",
                out JsonElement schemaProperty))
        {
            throw new InvalidOperationException(
                $"JSON CLI invocation [{string.Join(' ', args)}] returned an envelope without a schema.");
        }
        string schemaUri = schemaProperty.GetString()
            ?? throw new InvalidOperationException(
                "A JSON CLI envelope has a null schema.");
        JsonSchema schema = GetSchema(schemaUri);
        EvaluationResults evaluation = schema.Evaluate(instance.RootElement);
        if (!evaluation.IsValid)
        {
            throw new InvalidOperationException(
                $"Real CLI output does not conform to '{schemaUri}' for "
                    + $"arguments [{string.Join(' ', args)}]: "
                    + JsonSerializer.Serialize(evaluation)
                    + " Instance: "
                    + instance.RootElement.GetRawText());
        }
        ResultSchemaCoverage.Record(PublishedSchemas.IdOf(schemaUri));
        return result;
    }

    private static JsonDocument ParseSingleJsonDocument(
        string document,
        IReadOnlyList<string> args)
    {
        try
        {
            return JsonDocument.Parse(document);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"JSON CLI invocation [{string.Join(' ', args)}] did not return exactly one JSON document.",
                exception);
        }
    }

    private JsonSchema GetSchema(string schemaUri)
    {
        lock (SchemaLock)
        {
            if (Schemas.TryGetValue(schemaUri, out JsonSchema? cached))
            {
                return cached;
            }
            const string prefix = "https://schemas.aspose.com/aspose-cli/";
            const string suffix = ".schema.json";
            if (!schemaUri.StartsWith(prefix, StringComparison.Ordinal)
                || !schemaUri.EndsWith(suffix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"CLI returned a non-canonical schema URI '{schemaUri}'.");
            }
            string id = schemaUri[prefix.Length..^suffix.Length];
            if (SdkSchemaCatalog.TryRead(id, out _)
                && SchemaBuildOptions.SchemaRegistry.Get(
                    new Uri(schemaUri)) is JsonSchema commonSchema)
            {
                Schemas.Add(schemaUri, commonSchema);
                return commonSchema;
            }
            CliResult schemaResult = new CliProcess(
                CliRunner.ExecutablePath,
                CliEnvironment.Evaluation(_configDirectory.Path)).Run(
                    Path,
                    standardInput: null,
                    args: ["schema", id]);
            if (schemaResult.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"CLI schema '{id}' could not be loaded: "
                        + schemaResult.StdErr);
            }
            JsonSchema schema = JsonSchema.FromText(
                schemaResult.StdOut,
                SchemaBuildOptions);
            Schemas.Add(schemaUri, schema);
            return schema;
        }
    }

    private static bool IsSchemaCommand(IReadOnlyList<string> args) =>
        args.Count > 0
        && string.Equals(args[0], "schema", StringComparison.OrdinalIgnoreCase);

    private static bool RequestsJson(IReadOnlyList<string> args)
    {
        for (int index = 0; index < args.Count; index++)
        {
            if (string.Equals(
                    args[index],
                    "--output=json",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(
                    args[index],
                    "--output",
                    StringComparison.OrdinalIgnoreCase)
                && index + 1 < args.Count
                && string.Equals(
                    args[index + 1],
                    "json",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
