using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

internal static class RoslynTestSupport
{
    public static ImmutableArray<MetadataReference> PlatformReferences()
    {
        string trustedAssemblies = Assert.IsType<string>(
            AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"));
        return trustedAssemblies
            .Split(
                Path.PathSeparator,
                StringSplitOptions.RemoveEmptyEntries)
            .Where(static path => path.StartsWith(
                RuntimeEnvironment.GetRuntimeDirectory(),
                StringComparison.OrdinalIgnoreCase))
            .Select(static path => MetadataReference.CreateFromFile(path))
            .ToImmutableArray<MetadataReference>();
    }
}
