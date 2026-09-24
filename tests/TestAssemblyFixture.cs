// Compiled into every test project by tests/Directory.Build.props.
[assembly: Xunit.AssemblyFixture(typeof(Aspose.Cli.TestKit.TestEnvironment))]

namespace Aspose.Cli.TestKit;

/// <summary>
/// Tests that change an SDK's process-wide font sources. They run alone, after the parallel
/// tests, so no other test lays out a document while the sources change.
/// </summary>
[Xunit.CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessFontSourcesCollection
{
    public const string Name = "Process font sources";
}
