// Compiled into every test project by tests/Directory.Build.props.
[assembly: Xunit.AssemblyFixture(typeof(Aspose.Cli.TestKit.TestEnvironment))]
[assembly: Xunit.TestCollectionOrderer(typeof(Aspose.Cli.TestKit.ResultSchemaCoverageOrderer))]

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

/// <summary>
/// Tests that redirect the process's standard output or current-user registry. They run alone,
/// after the parallel tests, so no other test writes through the redirection.
/// </summary>
[Xunit.CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessRedirectionCollection
{
    public const string Name = "Process redirection";
}

/// <summary>
/// The result schema coverage check of a product's tests. It runs alone and last, after every
/// other test of the process has made its CLI runs (<see cref="ResultSchemaCoverageOrderer"/>).
/// </summary>
[Xunit.CollectionDefinition(ResultSchemaCoverageCollection.Name, DisableParallelization = true)]
public sealed class ResultSchemaCoverageCollectionDefinition;
