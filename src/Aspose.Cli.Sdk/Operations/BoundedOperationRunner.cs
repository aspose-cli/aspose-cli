using System.Reflection;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Sdk.Operations;

/// <summary>What one successfully applied operation changed.</summary>
/// <remarks>
/// Only the SDK constructs it, from the snapshots an <see cref="IOperationModel{TOp}"/> takes
/// before and after the operation: a product supplies the snapshots and counts nothing.
/// </remarks>
public sealed class AppliedOperation
{
    internal AppliedOperation(long itemsAffected, IReadOnlyList<string> targets)
    {
        ItemsAffected = itemsAffected;
        Targets = targets;
    }

    /// <summary>Number of product-owned items changed.</summary>
    public long ItemsAffected { get; }

    /// <summary>Stable product-owned addresses of the changed items.</summary>
    public IReadOnlyList<string> Targets { get; }
}

/// <summary>
/// Applies a validated batch in order through the product's <see cref="IOperationModel{TOp}"/>
/// and reports one outcome per operation. An outcome lists at most
/// <see cref="BoundedOperationOutcome.MaximumTargets"/> targets: one that changed more parts lists
/// one range per unit that covers them all instead, never a prefix of the parts.
/// </summary>
/// <remarks>
/// Handlers resolve and check their targets before they change the document, so an
/// <see cref="OperationInvalidException"/> or a domain <see cref="CliException"/> means the
/// operation changed nothing: an atomic batch stops, and a best-effort batch records the
/// failure and continues, as does a missing file the operation declares. An
/// <see cref="EngineOpException"/>, or another I/O exception the engine raised, can occur
/// mid-change, so it stops the batch in both modes and nothing is published.
/// </remarks>
public static class BoundedOperationRunner
{
    private const string BestEffortHint = "Fix or remove this operation, then retry the batch.";

    /// <summary>
    /// Runs every operation of a validated batch through the product's object model: the SDK
    /// snapshots the objects each operation can change before and after it, and an operation's
    /// <c>itemsAffected</c> is the number of objects that changed and its <c>targets</c> their
    /// addresses in order, where they are in the output, or in the input for removed ones: an
    /// object the engine keeps by instance (<see cref="IOperationModel{TOp}.Positions"/>), or one
    /// on such an object, such as a shape on a slide, is placed where the output of the whole
    /// batch holds that instance, so a later operation that moved it does not leave a stale
    /// address, and one a later operation removed has its address in the input of the batch. More than
    /// <see cref="BoundedOperationOutcome.MaximumTargets"/> addresses shorten to one per unit
    /// that covers them all, such as <c>page/1-150</c>. An operation that applied but changed
    /// nothing warns <c>OP_NO_EFFECT</c>.
    /// When the snapshot after an operation cannot be read, the operation has applied but no
    /// outcome can say what it changed: the batch stops as after an engine failure.
    /// </summary>
    /// <param name="catalog">The vocabulary the batch was validated against.</param>
    /// <param name="operations">Validated operations with assigned ids.</param>
    /// <param name="bestEffort">Whether a rejected operation is recorded instead of stopping the batch.</param>
    /// <param name="deadline">Invocation deadline checked before every operation.</param>
    /// <param name="model">Snapshots and applies the operations.</param>
    /// <param name="verifier">With <c>--verify</c>, records the effect of each operation that applied.</param>
    /// <param name="pageUnit">
    /// The unit whose numbered addresses, such as <c>page/3</c>, are the pages of the output;
    /// the batch reports the ones it changed in <see cref="AppliedBatch.Pages"/>, numbered as
    /// its targets are.
    /// </param>
    public static AppliedBatch Run<TOp>(
        OperationCatalog<TOp> catalog,
        IReadOnlyList<TOp> operations,
        bool bestEffort,
        OperationDeadline? deadline,
        IOperationModel<TOp> model,
        IEditVerifier<TOp>? verifier = null,
        string? pageUnit = null)
        where TOp : BoundedOperation
    {
        ArgumentNullException.ThrowIfNull(model);
        var warnings = new List<Warning>();
        var pages = new SortedSet<int>();
        var recorded = new List<string>();
        var unrecorded = new HashSet<string>(StringComparer.Ordinal);
        Dictionary<string, Dictionary<object, int>> input = Index(model.Positions());
        NamedScopes? firstScopes = model.Scopes();
        var scopesBefore = new Dictionary<int, NamedScopes>();
        var scopes = new Dictionary<int, NamedScopes>();
        var changed = new Dictionary<int, IReadOnlyList<ObjectChange>>();
        IReadOnlyList<BoundedOperationOutcome> outcomes = Execute(
            catalog,
            operations,
            bestEffort,
            deadline,
            model.GetType().Assembly,
            (operation, index) =>
            {
                OperationSnapshot before = model.Snapshot(operation);
                if (firstScopes is not null && model.Scopes() is { } prior)
                {
                    scopesBefore[index] = prior;
                }

                model.Apply(operation);
                OperationSnapshot after;
                try
                {
                    after = model.Snapshot(operation);
                }
                catch (Exception failure) when (failure is EngineOpException or CliException { IsInvocationFailure: false })
                {
                    // The operation changed the document, but what it changed cannot be read, so
                    // no outcome can say it truthfully, and reporting it failed would deny a change
                    // the output holds: the batch stops and nothing is written, as after an engine
                    // failure, in both modes. Any other exception is a defect and propagates.
                    throw new EngineOpException(
                        $"it applied, but reading what it changed failed: {failure.Message}",
                        failure is EngineOpException { InnerException: { } inner } ? inner : failure);
                }

                IReadOnlyList<ObjectChange> changes = OperationSnapshot.Compare(before, after);
                changed[index] = changes;
                if (firstScopes is not null && model.Scopes() is { } current)
                {
                    scopes[index] = current;
                }

                return new AppliedOperation(changes.Count, [.. changes.Select(static change => change.Address).Distinct(StringComparer.Ordinal)]);
            },
            operation => Covered(model.AttemptedTargets(operation)),
            (operation, id, name, applied) =>
            {
                if (applied.ItemsAffected == 0)
                {
                    NoEffect effect = model.Explain(operation) ?? new NoEffect(
                        NoEffectReasons.NoChange,
                        $"Operation {id} ({name}) changed nothing: what it addresses already had the state it sets.");
                    warnings.Add(catalog.NoEffect(operation, effect.Reason, effect.Message) with { Hint = effect.Hint });
                }

                if (verifier is not null)
                {
                    recorded.Add(id);
                    try
                    {
                        verifier.Record(operation, applied);
                    }
                    catch (Exception exception) when (exception is not (OperationCanceledException or OutOfMemoryException))
                    {
                        // Recording reads the document; what it cannot read stays unchecked.
                        unrecorded.Add(id);
                    }
                }
            });
        Dictionary<string, Dictionary<object, int>> output = input.Count == 0 ? input : Index(model.Positions());
        NamedScopes? lastScopes = firstScopes is null ? null : model.Scopes();
        var placed = new Dictionary<int, IReadOnlyList<ChangedObject>>();
        foreach ((int index, IReadOnlyList<ObjectChange> changes) in changed)
        {
            // An object in the output is named after the operation; a removed one before it.
            placed[index] = [.. changes.Select(change => Place(change, input, output))
                .Select(item => (item.InOutput ? scopes : scopesBefore).TryGetValue(index, out NamedScopes? then)
                    ? Renamed(item, then, lastScopes!, firstScopes!)
                    : item)];
        }

        if (pageUnit is not null)
        {
            // Where the output of the whole batch holds what each operation changed.
            pages.UnionWith(placed.Values.SelectMany(static objects => objects)
                .Where(item => item.InOutput && item.Address.StartsWith(pageUnit + "/", StringComparison.Ordinal))
                .Select(static item => OperationSnapshot.FirstNumber(item.Address))
                .OfType<int>());
        }

        outcomes = [.. outcomes.Select((outcome, index) => outcome.Status == OpStatuses.Ok && placed.TryGetValue(index, out IReadOnlyList<ChangedObject>? objects)
            ? outcome with { Targets = Covered(InOrder(objects.Select(static item => item.Address))) }
            : outcome)];
        return new AppliedBatch(outcomes, warnings, [.. pages], Batch(placed), recorded, unrecorded);
    }

    /// <summary>
    /// <paramref name="item"/> with the scope name that begins its address, as it was when its
    /// operation applied (<paramref name="then"/>: after it for an object it left, before it for
    /// one it removed), replaced by the name the same scope has in the output
    /// (<paramref name="last"/>) when the object is in the output and the scope still is,
    /// and otherwise by the name it had in the input (<paramref name="first"/>), so an object the
    /// batch removed is named as the input names it, whichever operation renamed its scope. A
    /// scope in neither keeps its name.
    /// </summary>
    private static ChangedObject Renamed(ChangedObject item, NamedScopes then, NamedScopes last, NamedScopes first)
    {
        int slash = item.Address.IndexOf('/', StringComparison.Ordinal);
        if (slash < 0 || !then.Units.Contains(item.Address[..slash]))
        {
            return item;
        }

        Dictionary<object, string> output = Names(last);
        Dictionary<object, string> input = Names(first);

        string rest = item.Address[(slash + 1)..];
        foreach ((object identity, string old) in then.Names)
        {
            string? current = item.InOutput && output.TryGetValue(identity, out string? kept) ? kept
                : input.TryGetValue(identity, out string? original) ? original
                : null;
            if (current is null || current == old)
            {
                continue;
            }

            string? renamed = Addressing.TargetName.Escape(old) is var plain && (rest == plain || rest.StartsWith(plain + "/", StringComparison.Ordinal))
                ? Addressing.TargetName.Escape(current) + rest[plain.Length..]
                : Addressing.TargetName.Escape(old, '!') is var sheet && rest.StartsWith(sheet + "!", StringComparison.Ordinal)
                    ? Addressing.TargetName.Escape(current, '!') + rest[sheet.Length..]
                    : null;
            if (renamed is not null)
            {
                return item with { Address = $"{item.Address[..slash]}/{renamed}" };
            }
        }

        return item;

        static Dictionary<object, string> Names(NamedScopes scopes)
        {
            var names = new Dictionary<object, string>(ReferenceEqualityComparer.Instance);
            foreach ((object identity, string name) in scopes.Names)
            {
                names.TryAdd(identity, name);
            }

            return names;
        }
    }

    /// <summary>Each unit's objects by instance, with their 0-based positions.</summary>
    private static Dictionary<string, Dictionary<object, int>> Index(IReadOnlyDictionary<string, IReadOnlyList<object>> positions) =>
        positions.ToDictionary(
            static unit => unit.Key,
            static unit =>
            {
                var index = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
                for (int position = 0; position < unit.Value.Count; position++)
                {
                    index.TryAdd(unit.Value[position], position);
                }

                return index;
            },
            StringComparer.Ordinal);

    /// <summary>
    /// A changed object at its address in the output, or in the input when the batch removed it:
    /// the number that begins its address is its owner's position, so <c>slide/3/shape/7</c> on a
    /// slide the output holds fifth is <c>slide/5/shape/7</c>. One whose owner the batch both
    /// inserted and removed keeps the address its operation reported.
    /// </summary>
    private static ChangedObject Place(ObjectChange change, Dictionary<string, Dictionary<object, int>> input, Dictionary<string, Dictionary<object, int>> output)
    {
        int slash = change.Address.IndexOf('/', StringComparison.Ordinal);
        string unit = change.Address[..slash];
        if (change.Owner is not { } owner || !output.TryGetValue(unit, out Dictionary<object, int>? now))
        {
            return new ChangedObject(change.Address, change.Identity, change.InOutput);
        }

        int end = change.Address.IndexOf('/', slash + 1);
        string rest = end < 0 ? string.Empty : change.Address[end..];
        // An object its operation removed, such as a shape, is still gone when its owner is not.
        return now.TryGetValue(owner, out int position)
            ? new ChangedObject(Numbered(position), change.Identity, change.InOutput)
            : input.TryGetValue(unit, out Dictionary<object, int>? before) && before.TryGetValue(owner, out position)
                ? new ChangedObject(Numbered(position), change.Identity, InOutput: false)
                : new ChangedObject(change.Address, change.Identity, InOutput: false);

        string Numbered(int index) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{unit}/{index + 1}{rest}");
    }

    /// <summary>Every object the batch changed once: by instance where it has one, else by address, which keeps whether its last change left it in the output.</summary>
    private static IReadOnlyList<ChangedObject> Batch(Dictionary<int, IReadOnlyList<ChangedObject>> placed)
    {
        var byIdentity = new Dictionary<object, ChangedObject>(ReferenceEqualityComparer.Instance);
        var byAddress = new Dictionary<string, ChangedObject>(StringComparer.Ordinal);
        foreach (ChangedObject item in placed.OrderBy(static pair => pair.Key).SelectMany(static pair => pair.Value))
        {
            if (item.Identity is { } identity)
            {
                byIdentity[identity] = item;
            }
            else
            {
                byAddress[item.Address] = item;
            }
        }

        ChangedObject[] all = [.. byIdentity.Values, .. byAddress.Values];
        string[] order = [.. InOrder(all.Select(static item => item.Address))];
        return [.. all.OrderBy(item => Array.IndexOf(order, item.Address))];
    }

    /// <summary>Addresses once each, unit by unit in the order the units first appear, and by number within a unit.</summary>
    private static IReadOnlyList<string> InOrder(IEnumerable<string> addresses)
    {
        string[] distinct = [.. addresses.Distinct(StringComparer.Ordinal)];
        List<string> units = [.. distinct.Select(Unit).Distinct(StringComparer.Ordinal)];
        return [.. distinct.OrderBy(address => units.IndexOf(Unit(address))).ThenBy(static address => OperationSnapshot.FirstNumber(address) ?? int.MaxValue)];

        static string Unit(string address) => address[..address.IndexOf('/', StringComparison.Ordinal)];
    }

    private static IReadOnlyList<string> Covered(IReadOnlyList<string> targets) =>
        targets.Count <= BoundedOperationOutcome.MaximumTargets ? targets : OperationSnapshot.Cover(targets);

    private static IReadOnlyList<BoundedOperationOutcome> Execute<TOp>(
        OperationCatalog<TOp> catalog,
        IReadOnlyList<TOp> operations,
        bool bestEffort,
        OperationDeadline? deadline,
        Assembly product,
        Func<TOp, int, AppliedOperation> apply,
        Func<TOp, IReadOnlyList<string>> attemptedTargets,
        Action<TOp, string, string, AppliedOperation> applied)
        where TOp : BoundedOperation
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(operations);

        // The CLI code an operation runs: this SDK and the product that applies it.
        var own = new HashSet<Assembly> { typeof(BoundedOperationRunner).Assembly, product };
        var outcomes = new List<BoundedOperationOutcome>(operations.Count);
        for (int index = 0; index < operations.Count; index++)
        {
            TOp operation = operations[index];
            string name = catalog.NameOf(operation);
            string id = operation.Id
                ?? throw new ArgumentException("Every operation of a validated batch has an id.", nameof(operations));
            deadline?.ThrowIfExpired("operations");
            CliException? rejection = null;
            AppliedOperation? done = null;
            try
            {
                done = apply(operation, index);
            }
            catch (OperationInvalidException invalid)
            {
                rejection = OperationErrors.InvalidAt(id, name, invalid.Message, invalid.Hint ?? catalog.DefaultHint, value: invalid.Mistake, path: invalid.Path);
            }
            catch (CliException failure) when (!failure.IsInvocationFailure)
            {
                rejection = OperationErrors.FailedAt(id, name, failure);
            }
            catch (CliException timeout) when (timeout.Code == ErrorCodes.OperationTimeout)
            {
                // Time running out stops the whole batch in every mode, but inside an operation it
                // names that operation, so the caller knows which one to narrow. The check before an
                // operation starts stays unnamed: that operation never ran.
                throw OperationErrors.FailedAt(id, name, timeout);
            }
            catch (RegexMatchTimeoutException timeout)
            {
                throw OperationErrors.FailedAt(id, name, CliErrors.RegexTimeout(timeout));
            }
            catch (EngineOpException failure)
            {
                rejection = DeclaredFileFailure(failure.InnerException, catalog.InputPaths(operation)) is { } file
                    ? OperationErrors.FailedAt(id, name, file)
                    : throw OperationErrors.EngineFailedAt(id, name, failure);
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
            {
                // A file the operation declares that is missing, locked or denied is that file's
                // error, with its code, and the operation changed nothing; any other I/O failure
                // the engine raised can occur mid-change. One the CLI raised propagates.
                if (DeclaredFileFailure(failure, catalog.InputPaths(operation)) is { } file)
                {
                    rejection = OperationErrors.FailedAt(id, name, file);
                }
                else if (ExceptionOrigin.IsThirdParty(failure, own))
                {
                    throw OperationErrors.EngineFailedAt(id, name, new EngineOpException(failure.Message, failure));
                }
                else
                {
                    throw;
                }
            }

            if (rejection is null)
            {
                // Nothing was rejected, so the operation applied and the model said what it changed.
                AppliedOperation result = done!;
                outcomes.Add(new BoundedOperationOutcome
                {
                    Id = id,
                    Op = name,
                    Status = OpStatuses.Ok,
                    ItemsAffected = result.ItemsAffected,
                    Targets = Covered(result.Targets),
                });
                applied(operation, id, name, result);
                continue;
            }

            if (!bestEffort)
            {
                throw rejection;
            }
            outcomes.Add(new BoundedOperationOutcome
            {
                Id = id,
                Op = name,
                Status = OpStatuses.Failed,
                ItemsAffected = 0,
                Targets = attemptedTargets(operation),
                Error = rejection.ToEnvelope().Error with { Hint = rejection.Hint ?? BestEffortHint },
            });
        }

        return outcomes;
    }

    /// <summary>
    /// The error of a file the operation declares that could not be opened: missing, denied or
    /// held by another program, by the code the CLI gives that failure of any input. Only a
    /// failure that names the declared file counts: by its file name, or by its whole path quoted
    /// in the message, as .NET writes it ('C:\in\a.png'). A failure of any other file, such as
    /// a temporary one the engine writes, is not the declared file's and keeps its own handling,
    /// since it can occur mid-change. Null for any other failure.
    /// </summary>
    private static CliException? DeclaredFileFailure(Exception? failure, IReadOnlyList<string> inputs)
    {
        if (failure is null || inputs.Count == 0)
        {
            return null;
        }

        string? path = failure is FileNotFoundException { FileName: { Length: > 0 } named }
            ? (Declares(inputs, named) ? named : null)
            : inputs.FirstOrDefault(input => PathResolver.TryResolve(Environment.CurrentDirectory, input) is { } full
                && failure.Message.Contains($"'{full}'", FilePaths.Comparison));
        return path is null ? null : failure switch
        {
            FileNotFoundException or DirectoryNotFoundException => CliErrors.FileNotFound(path),
            UnauthorizedAccessException => CliErrors.FileAccessDenied(path),
            IOException io when FileAccessProbe.IsSharingViolation(io) => CliErrors.FileLocked(path),
            _ => null,
        };
    }

    private static bool Declares(IReadOnlyList<string> inputs, string file)
    {
        string? missing = PathResolver.TryResolve(Environment.CurrentDirectory, file);
        return missing is not null && inputs.Any(input =>
            FilePaths.Same(PathResolver.TryResolve(Environment.CurrentDirectory, input), missing));
    }
}
