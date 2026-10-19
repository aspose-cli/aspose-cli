using System.Security.Cryptography;
using System.Text;
using Aspose.Cli.Architecture.Tests.TestSupport;
using Aspose.Cli.Sdk.Serialization;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// Pins the exact bytes of every published schema and of every canonical common result, so a
/// change of who owns a contract cannot change what an agent reads.
/// </summary>
public sealed class SchemaSnapshotTests
{
    private const string UpdateSnapshotsVariable = Aspose.Cli.Sdk.DistributionInfo.EnvironmentVariablePrefix + "TEST_UPDATE_SNAPSHOTS";

    /// <summary>Every schema <c>aspose-cli schema</c> lists prints the bytes it printed when the snapshot was taken.</summary>
    [Fact]
    public void EveryPublishedSchema_MatchesItsSnapshot()
    {
        IEnumerable<string> lines = PublishedSchemas.Ids
            .Order(StringComparer.Ordinal)
            .Select(static id => Sha256(PublishedSchemas.Text(id)) + "  " + id);

        AssertSnapshot("schemas.sha256", lines);
    }

    /// <summary>Every canonical common result serializes to the bytes it serialized to when the snapshot was taken.</summary>
    [Fact]
    public void EveryCommonSample_SerializesToItsSnapshot()
    {
        ContractJsonSerializer serializer = new([], Aspose.Cli.Host.Contracts.HostResultJsonContext.Default);
        IEnumerable<string> lines = CommonSchemaSamples.All
            .Select((sample, index) => Sha256(serializer.Serialize(sample.Value)) + "  " + index.ToString("D2", System.Globalization.CultureInfo.InvariantCulture) + " " + sample.Value.GetType().Name);

        AssertSnapshot("common-samples.sha256", lines);
    }

    private static string Sha256(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static void AssertSnapshot(string name, IEnumerable<string> lines)
    {
        string actual = string.Join('\n', lines) + "\n";
        string path = Path.Combine(
            RepositoryPaths.Root, "tests", "Aspose.Cli.Platform.Tests", "Integration", "Snapshots", name);
        if (Environment.GetEnvironmentVariable(UpdateSnapshotsVariable) == "1")
        {
            File.WriteAllText(path, actual, new UTF8Encoding(false));
            return;
        }

        Assert.True(File.Exists(path), $"Snapshot {name} is missing; set {UpdateSnapshotsVariable}=1 to create it.");
        string expected = File.ReadAllText(path, Encoding.UTF8).ReplaceLineEndings("\n");
        Assert.Equal(expected, actual);
    }
}
