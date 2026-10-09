using System.CommandLine;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>What the host gives one invocation of a product command.</summary>
/// <param name="Binding">The product, activated for this invocation.</param>
/// <param name="Paths">Path resolver scoped to the invocation working directory.</param>
/// <param name="Inputs">Single bounded reader for user-controlled file and stdin input.</param>
/// <param name="ReadEnvironment">Resolves a named environment secret through this invocation's input source.</param>
public sealed record ProductCommandScope(
    ProductBinding Binding,
    PathResolver Paths,
    InputSource Inputs,
    Func<string, string?> ReadEnvironment);

/// <summary>
/// The host's command pipeline, which owns output rendering, errors, deadlines and admission: it
/// runs the body of one product command in a scope for that product and returns the exit code.
/// </summary>
/// <param name="parse">The parsed command line.</param>
/// <param name="run">The command body.</param>
public delegate int ProductCommandRunner(ParseResult parse, Func<ProductCommandScope, ResultEnvelope> run);
