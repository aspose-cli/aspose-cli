using System.Globalization;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.TestKit;

/// <summary>
/// What the SDK runner reports an operation changed, for a test that records it with a product's
/// verifier directly. Only the SDK constructs an <see cref="AppliedOperation"/>, so the operation
/// runs through <see cref="BoundedOperationRunner"/> over a model that changes the given number of
/// objects and leaves the document alone.
/// </summary>
public static class AppliedOperations
{
    /// <summary>The applied operation the runner reports for <paramref name="operation"/>, which has an id.</summary>
    public static AppliedOperation Changed<TOp>(OperationCatalog<TOp> catalog, TOp operation, long itemsAffected)
        where TOp : BoundedOperation
    {
        var recorder = new Recorder<TOp>();
        BoundedOperationRunner.Run(catalog, [operation], bestEffort: false, deadline: null, new Model<TOp>(itemsAffected), recorder);
        return recorder.Applied ?? throw new InvalidOperationException("The runner recorded no applied operation.");
    }

    private sealed class Model<TOp>(long itemsAffected) : IOperationModel<TOp>
        where TOp : BoundedOperation
    {
        private bool _applied;

        public OperationSnapshot Snapshot(TOp operation)
        {
            var snapshot = new OperationSnapshot();
            for (long item = 1; item <= itemsAffected; item++)
            {
                snapshot.Add($"test/{item.ToString(CultureInfo.InvariantCulture)}", _applied ? "after" : "before");
            }

            return snapshot;
        }

        public void Apply(TOp operation) => _applied = true;

        public IReadOnlyList<string> AttemptedTargets(TOp operation) => [];
    }

    private sealed class Recorder<TOp> : IEditVerifier<TOp>
        where TOp : BoundedOperation
    {
        public AppliedOperation? Applied { get; private set; }

        public void Record(TOp operation, AppliedOperation applied) => Applied = applied;
    }
}
