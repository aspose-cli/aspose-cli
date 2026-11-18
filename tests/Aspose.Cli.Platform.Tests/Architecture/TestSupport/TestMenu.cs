using System.CommandLine;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>The one-command menu a test product needs to build.</summary>
internal static class TestMenu
{
    /// <summary>
    /// Describes the product and adds one command, <paramref name="name"/>, with the given
    /// options; it returns a new <typeparamref name="TResult"/>.
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
                    static (_, _) => new TestRequest()),
                static (TSession _, TestRequest _) => new TResult());

    /// <summary>The request of a test command.</summary>
    public sealed record TestRequest;
}
