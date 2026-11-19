using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>Operation batches for engine calls, checked against the Slides table as the edit command checks them.</summary>
internal static class TestBatch
{
    /// <summary>Checks <paramref name="ops"/>, the operations of one batch, with <paramref name="secrets"/> as the environment.</summary>
    public static TableBatch Parse(JsonArray ops, IReadOnlyDictionary<string, string>? secrets = null) =>
        TableBatch.Parse(new JsonObject { ["ops"] = ops }.ToJsonString(), SlidesTable.Definition, new TestInputs(secrets));

    /// <summary>
    /// What a batch reads outside the presentation: paths resolved against the working directory,
    /// image files read as they are, and secrets from a dictionary instead of the environment.
    /// </summary>
    private sealed class TestInputs(IReadOnlyDictionary<string, string>? secrets) : ITableInputs
    {
        public string ResolvePath(string path) => Path.GetFullPath(path);

        public byte[] ReadImage(string path) => File.ReadAllBytes(path);

        public Secret? ReadSecret(string variable) =>
            secrets is not null && secrets.TryGetValue(variable, out string? value) ? new Secret(value) : null;
    }
}
