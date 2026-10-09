using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Platform.Tests;

/// <summary>
/// Base of the results these tests declare. The contract generator publishes a schema only for a
/// record that derives directly from one of the SDK's envelope bases, such as
/// <see cref="ResultEnvelope"/>, so a test result needs neither documentation nor a JSON context.
/// </summary>
public abstract record TestResultEnvelope : ResultEnvelope
{
    /// <summary>Starts a version 1 result that states the relative schema id <paramref name="schema"/>.</summary>
    protected TestResultEnvelope(string schema)
        : base(schema, 1)
    {
    }
}
