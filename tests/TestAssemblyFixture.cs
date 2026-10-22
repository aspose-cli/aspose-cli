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
/// Tests that set a license variable in the process environment. They run alone, after the
/// parallel tests, so no other test resolves a license while the variable changes.
/// </summary>
[Xunit.CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessLicenseEnvironmentCollection
{
    public const string Name = "Process license environment";
}

/// <summary>
/// Tests that admit the process to the SDK's network gate, which takes over the process's request
/// prefixes and default proxy. They run alone, after the parallel tests, so no other test makes a
/// request while the gate blocks it.
/// </summary>
[Xunit.CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessNetworkGateCollection
{
    public const string Name = "Process network gate";
}

/// <summary>
/// Tests that read what a document engine writes into every document it saves, such as the PDF
/// engine's modification date, which it leaves out of a document saved while other documents of
/// the process are saved. They run alone, after the parallel tests.
/// </summary>
[Xunit.CollectionDefinition(Name, DisableParallelization = true)]
public sealed class EngineSaveStampsCollection
{
    public const string Name = "Engine save stamps";
}

/// <summary>
/// The result schema coverage check of a product's tests. It runs alone and last, after every
/// other test of the process has made its CLI runs (<see cref="ResultSchemaCoverageOrderer"/>).
/// </summary>
[Xunit.CollectionDefinition(ResultSchemaCoverageCollection.Name, DisableParallelization = true)]
public sealed class ResultSchemaCoverageCollectionDefinition;
