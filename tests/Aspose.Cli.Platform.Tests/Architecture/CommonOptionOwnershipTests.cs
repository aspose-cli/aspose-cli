using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Architecture;

/// <summary>
/// Reads the compiled product assemblies: the common options belong to the command template,
/// which declares them from a command's traits, so no product names their owner types. The
/// Host builds its own commands from the owners it may use and proves the scan sees them.
/// </summary>
public sealed class CommonOptionOwnershipTests
{
    private static readonly string[] TemplateOwned =
    [
        typeof(OutputFileOptions).FullName!,
        typeof(OutputDirectoryOption).FullName!,
        typeof(MutationFileOptions).FullName!,
        typeof(OutputOptions).FullName!,
        typeof(PasswordOptions).FullName!,
        typeof(FontDirectoryOptions).FullName!,
    ];

    [Fact]
    public void ProductsLeaveTheCommonOptionsToTheCommandTemplate()
    {
        string directory = Path.GetDirectoryName(CliRunner.ExecutablePath)!;
        string[] products = Directory.GetFiles(directory, "Aspose.Cli.Product.*.dll");
        Assert.NotEmpty(products);

        string[] violations = products
            .SelectMany(product => ReferencedTypes(product)
                .Intersect(TemplateOwned, StringComparer.Ordinal)
                .Select(type => $"{Path.GetFileName(product)} uses {type}"))
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
        Assert.Contains(
            typeof(PasswordOptions).FullName!,
            ReferencedTypes(Path.Combine(directory, "Aspose.Cli.Host.dll")));
    }

    private static string[] ReferencedTypes(string assembly)
    {
        using FileStream stream = File.OpenRead(assembly);
        using var pe = new PEReader(stream);
        MetadataReader metadata = pe.GetMetadataReader();
        return
        [
            .. metadata.TypeReferences
                .Select(metadata.GetTypeReference)
                .Select(reference => $"{metadata.GetString(reference.Namespace)}.{metadata.GetString(reference.Name)}"),
        ];
    }
}
