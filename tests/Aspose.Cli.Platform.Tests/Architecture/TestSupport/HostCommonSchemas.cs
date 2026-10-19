using Aspose.Cli.Host.Commands;
using Aspose.Cli.TestKit;
using Json.Schema;

namespace Aspose.Cli.Architecture.Tests.TestSupport;

/// <summary>The <c>v2/common</c> schemas the host publishes: the SDK's and the host's own.</summary>
internal static class HostCommonSchemas
{
    /// <summary>Every common schema id, in ordinal order.</summary>
    public static IReadOnlyList<string> Ids => HostSchemaCatalog.CommonIds;

    /// <summary>The common schema document of <paramref name="id"/>.</summary>
    public static string Read(string id) =>
        HostSchemaCatalog.TryReadCommon(id, out string? document)
            ? document
            : throw new ArgumentException($"Unknown common schema id '{id}'.", nameof(id));

    /// <summary>Build options with every common schema registered by identity.</summary>
    public static BuildOptions CreateOptions() => SchemaTestRegistry.CreateOptions(Ids, Read);
}
