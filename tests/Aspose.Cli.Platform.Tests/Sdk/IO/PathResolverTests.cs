using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

public sealed class PathResolverTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory(
        "aspose-resource-root-").FullName;

    [Fact]
    public void LocalResource_StaysRelativeAndInsideTheInvocationRoot()
    {
        string nested = Directory.CreateDirectory(
            Path.Combine(_root, "assets")).FullName;
        string resource = Path.Combine(nested, "logo.png");
        File.WriteAllBytes(resource, [1, 2, 3]);
        var resolver = new PathResolver(_root);

        Assert.Equal(
            resource,
            resolver.ResolveLocalResource("assets/logo.png"));

        foreach (string unsafePath in new[]
                 {
                     resource,
                     "../outside.png",
                     "https://example.test/logo.png",
                     "\\\\server\\share\\logo.png",
                 })
        {
            CliException error = Assert.Throws<CliException>(() =>
                resolver.ResolveLocalResource(unsafePath));
            Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
