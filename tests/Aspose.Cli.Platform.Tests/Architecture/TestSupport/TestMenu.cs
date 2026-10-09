using System.CommandLine;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>The one-command menu a test product needs to build.</summary>
internal static class TestMenu
{
    /// <summary>
    /// Describes the product and adds one command, <paramref name="name"/>, with the given
    /// options; it returns a new <typeparamref name="TResult"/>, which prints as an empty table.
    /// A catalog renders each result type once, so each product of one catalog names its own.
    /// </summary>
    public static ProductDefinitionBuilder<TSession> WithCommand<TSession, TResult>(
        this ProductDefinitionBuilder<TSession> product,
        string name = "run",
        params Option[] options)
        where TSession : class
        where TResult : ResultEnvelope, new() =>
        product
            .Describe("A test product.")
            .Command(
                () => new CommandDefinition<TestRequest, TResult>(
                    name,
                    "Runs.",
                    new CommandTraits(),
                    options,
                    static (_, _) => new TestRequest(),
                    NoTable),
                static (TSession _, TestRequest _) => new TResult());

    private static void NoTable<TResult>(TResult result, TableSurface table)
    {
    }

    /// <summary>The request of a test command.</summary>
    public sealed record TestRequest;
}
