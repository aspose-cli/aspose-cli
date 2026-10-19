using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>Pins which references the layering tests' namespace graph sees.</summary>
public sealed class NamespaceGraphTests
{
    [Fact]
    public void TypeLevelReferences_BelongToTheDeclaredType()
    {
        string directory = Path.Combine(Path.GetTempPath(), "namespace-graph-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string source = Path.Combine(directory, "Sample.cs");
            File.WriteAllText(
                source,
                """
                namespace Sample.High
                {
                    public class Base { }
                    public interface IFace { }
                    public sealed class MarkAttribute : System.Attribute { }
                    public delegate void Handler();
                }

                namespace Sample.Low
                {
                    [Sample.High.Mark]
                    public class Derived : Sample.High.Base, Sample.High.IFace { }

                    public class Constrained<T> where T : Sample.High.Base { }

                    public delegate Sample.High.Base Factory();
                }
                """);

            NamespaceGraph graph = NamespaceGraph.Build("Sample", [source], RoslynTestSupport.PlatformReferences());

            Assert.Empty(graph.UnboundErrors);
            string[] references = graph.Edges.GetValueOrDefault(("Sample.Low", "Sample.High"), [])
                .Select(static reference => $"{reference.Owner.Name} -> {reference.Target.Name}")
                .Order(StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(
                ["Constrained -> Base", "Derived -> Base", "Derived -> IFace", "Derived -> MarkAttribute", "Factory -> Base"],
                references);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
