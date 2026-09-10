using System.Reflection;
using Aspose.Cli.Host;
using Aspose.Cli.Host.Commands;
using Aspose.Cli.Sdk.Extensibility;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>Enforces the final SDK, host, and autonomous-product boundaries.</summary>
public sealed class DependencyRulesTests
{
    private static readonly Assembly Sdk = typeof(IProductModule).Assembly;
    private static readonly Assembly Host = typeof(CliHost).Assembly;

    [Fact]
    public void Sdk_HasNoAsposeProductDependency()
    {
        Assert.DoesNotContain(
            ReferenceNames(Sdk),
            static reference => reference.StartsWith(
                "Aspose.",
                StringComparison.Ordinal)
                && reference != "Aspose.Cli.Sdk");
    }

    [Fact]
    public void Host_DependsOnSdkWithoutDependingOnProducts()
    {
        string[] references = ReferenceNames(Host);
        Assert.Contains("Aspose.Cli.Sdk", references);
        Assert.DoesNotContain(
            references,
            static reference => reference.StartsWith(
                "Aspose.Cli.Product.",
                StringComparison.Ordinal));
        Assert.DoesNotContain(
            references,
            static reference => reference.StartsWith(
                "Aspose.",
                StringComparison.Ordinal)
                && !reference.StartsWith(
                    "Aspose.Cli.",
                    StringComparison.Ordinal));
    }

    [Fact]
    public void HostSource_DoesNotReferenceAProductNamespace()
    {
        string host = Path.Combine(
            RepositoryPaths.Root,
            "src",
            "Aspose.Cli.Host");
        string source = string.Join(
            Environment.NewLine,
            HostSourceFiles(host)
                .Select(File.ReadAllText));

        Assert.DoesNotContain(
            "Aspose.Cli.Product.",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void HostNamespaces_AreExplicitAndIsolated()
    {
        string sourceRoot = Path.Combine(RepositoryPaths.Root, "src");
        string host = Path.Combine(
            sourceRoot,
            "Aspose.Cli.Host");
        string[] hostFiles = HostSourceFiles(host).ToArray();

        string[] namespaceViolations = hostFiles
            .Where(static path => !Path.GetFileName(path).StartsWith(
                "GlobalUsings",
                StringComparison.Ordinal))
            .Select(path =>
            {
                string[] namespaces = CSharpSyntaxTree
                    .ParseText(File.ReadAllText(path))
                    .GetRoot()
                    .DescendantNodes()
                    .OfType<BaseNamespaceDeclarationSyntax>()
                    .Select(static declaration => declaration.Name.ToString())
                    .ToArray();
                bool valid = namespaces.Length == 1
                    && (namespaces[0] == "Aspose.Cli.Host"
                        || namespaces[0].StartsWith(
                            "Aspose.Cli.Host.",
                            StringComparison.Ordinal));
                return valid
                    ? null
                    : $"{Path.GetRelativePath(host, path)}: "
                        + string.Join(", ", namespaces);
            })
            .Where(static violation => violation is not null)
            .Select(static violation => violation!)
            .ToArray();

        Assert.True(
            namespaceViolations.Length == 0,
            "Host source uses a namespace outside Aspose.Cli.Host:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, namespaceViolations));

        string hostSource = string.Join(
            Environment.NewLine,
            hostFiles.Select(File.ReadAllText));
        string[] legacyRoots =
        [
            "Aspose.Cli.App",
            "Aspose.Cli.Catalog",
            "Aspose.Cli.Commands",
            "Aspose.Cli.Invocation",
            "Aspose.Cli.Licensing",
            "Aspose.Cli.LocalServices",
            "Aspose.Cli.Output",
            "Aspose.Cli.Preview",
            "Aspose.Cli.Review",
            "Aspose.Cli.Serialization",
            "Aspose.Cli.Skills",
        ];
        Assert.DoesNotContain(
            legacyRoots,
            legacyRoot => hostSource.Contains(
                legacyRoot,
                StringComparison.Ordinal));

        string[] forbiddenConsumerRoots = Directory
            .GetDirectories(
                sourceRoot,
                "Aspose.Cli.Sdk*",
                SearchOption.AllDirectories)
            .Concat(Directory.GetDirectories(
                sourceRoot,
                "Aspose.Cli.Product.*",
                SearchOption.AllDirectories))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] reverseDependencyViolations = forbiddenConsumerRoots
            .SelectMany(HostSourceFiles)
            .Where(path => CSharpSyntaxTree
                .ParseText(File.ReadAllText(path))
                .GetRoot()
                .DescendantNodes()
                .OfType<NameSyntax>()
                .Any(static name =>
                {
                    string value = name.ToString();
                    return value == "Aspose.Cli.Host"
                        || value.StartsWith(
                            "Aspose.Cli.Host.",
                            StringComparison.Ordinal);
                }))
            .Select(path => Path.GetRelativePath(sourceRoot, path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            reverseDependencyViolations.Length == 0,
            "SDK or product source references the Host namespace:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, reverseDependencyViolations));

        string[] launchers = [Path.Combine(sourceRoot, "Aspose.Cli", "Program.cs")];
        Assert.NotEmpty(launchers);
        foreach (string launcher in launchers)
        {
            CompilationUnitSyntax syntax = (CompilationUnitSyntax)CSharpSyntaxTree
                .ParseText(File.ReadAllText(launcher))
                .GetRoot();
            BaseNamespaceDeclarationSyntax declaration = Assert.Single(
                syntax.DescendantNodes()
                    .OfType<BaseNamespaceDeclarationSyntax>());
            Assert.Equal("Aspose.Cli", declaration.Name.ToString());
            Assert.Contains(
                syntax.Usings,
                static directive =>
                    directive.Name?.ToString() == "Aspose.Cli.Host");
        }
    }

    [Fact]
    public void HostInfrastructure_DoesNotImportCommandAdapters()
    {
        string host = Path.Combine(
            RepositoryPaths.Root,
            "src",
            "Aspose.Cli.Host");
        string[] violations = HostSourceFiles(host)
            .Where(path =>
            {
                string relative = Path.GetRelativePath(host, path);
                return !string.Equals(
                        relative,
                        "Program.cs",
                        StringComparison.OrdinalIgnoreCase)
                    && !relative.StartsWith(
                        "Commands" + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase);
            })
            .Where(path => CSharpSyntaxTree
                .ParseText(File.ReadAllText(path))
                    .GetRoot()
                    .DescendantNodes()
                    .OfType<UsingDirectiveSyntax>()
                    .Any(static directive =>
                        directive.Name?.ToString()
                            == "Aspose.Cli.Host.Commands"))
            .Select(path => Path.GetRelativePath(host, path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "Host infrastructure imports the command adapter namespace:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Invocation_DoesNotReferencePreviewImplementations()
    {
        string invocation = Path.Combine(
            RepositoryPaths.Root,
            "src",
            "Aspose.Cli.Host",
            "Invocation");
        string[] violations = Directory.GetFiles(invocation, "*.cs")
            .Where(path => CSharpSyntaxTree
                .ParseText(File.ReadAllText(path))
                .GetRoot()
                .DescendantNodes()
                .OfType<NameSyntax>()
                .Any(static name =>
                {
                    string value = name.ToString();
                    return value == "Aspose.Cli.Host.Preview"
                        || value.StartsWith(
                            "Aspose.Cli.Host.Preview.",
                            StringComparison.Ordinal);
                }))
            .Select(static path => Path.GetFileName(path)!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "Invocation references concrete Preview implementation types:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void LocalServices_DoesNotReferenceItsConsumers()
    {
        string localServices = Path.Combine(
            RepositoryPaths.Root,
            "src",
            "Aspose.Cli.Host",
            "LocalServices");
        string[] forbidden =
        [
            "Aspose.Cli.Host.App",
            "Aspose.Cli.Host.Preview",
            "Aspose.Cli.Host.Invocation",
        ];
        string[] violations = Directory
            .GetFiles(localServices, "*.cs")
            .Where(path => CSharpSyntaxTree
                .ParseText(File.ReadAllText(path))
                .GetRoot()
                .DescendantNodes()
                .OfType<NameSyntax>()
                .Any(name => forbidden.Any(root =>
                {
                    string value = name.ToString();
                    return value == root
                        || value.StartsWith(
                            root + ".",
                            StringComparison.Ordinal);
                })))
            .Select(static path => Path.GetFileName(path)!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "LocalServices references a consumer namespace:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void HostActivation_IsOnDemandAndHasNoTypeErasedPortSurface()
    {
        string invocation = Path.Combine(
            RepositoryPaths.Root,
            "src",
            "Aspose.Cli.Host",
            "Invocation");
        string invocationSource = string.Join(
            Environment.NewLine,
            Directory.GetFiles(invocation, "*.cs")
                .Select(File.ReadAllText));

        Assert.DoesNotContain("ModuleBindings", invocationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProductPlatformBinding", invocationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("BindPlatform", invocationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("HasRuntime", invocationSource, StringComparison.Ordinal);

        string hostSource = string.Join(
            Environment.NewLine,
            Directory.GetFiles(
                Path.Combine(
                    RepositoryPaths.Root,
                    "src",
                    "Aspose.Cli.Host"),
                "*.cs",
                SearchOption.AllDirectories)
                .Select(File.ReadAllText));
        Assert.DoesNotContain(
            "new SafeFileWriter()",
            hostSource,
            StringComparison.Ordinal);

        Type binding = typeof(ProductBinding);
        Assert.True(binding.IsAbstract);
        Assert.DoesNotContain(
            binding.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            static property => property.PropertyType == typeof(object));
    }

    [Fact]
    public void CommandExecutor_HasOneInvocationScopePipeline()
    {
        string source = File.ReadAllText(Path.Combine(
            RepositoryPaths.Root,
            "src",
            "Aspose.Cli.Host",
            "Invocation",
            "CommandExecutor.cs"));

        Assert.Equal(1, Occurrences(source, "OutputWriterFactory.Create("));
        Assert.Equal(1, Occurrences(source, "CompositionRoot.CreateBudgets("));
        Assert.DoesNotContain(
            "ResourceBudgetScope",
            source,
            StringComparison.Ordinal);
        Assert.Equal(1, Occurrences(source, "ProductInputAdmission.Admit("));
    }

    private static int Occurrences(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;

    private static IEnumerable<string> HostSourceFiles(string host) =>
        Directory.GetFiles(host, "*.cs", SearchOption.AllDirectories)
            .Where(static path =>
                !path.Contains(
                    $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase)
                && !path.Contains(
                    $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase));

    private static string[] ReferenceNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(static reference => reference.Name!)
            .ToArray();

}
