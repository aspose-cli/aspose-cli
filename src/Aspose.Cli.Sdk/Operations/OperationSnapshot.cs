using System.Globalization;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Operations;

/// <summary>
/// The state of the objects of a document that one operation can change, which a product takes
/// before and after the operation (<see cref="IOperationModel{TOp}.Snapshot"/>). Each object has
/// an address in the <c>&lt;unit&gt;/&lt;address&gt;</c> vocabulary, such as <c>page/3</c> or
/// <c>property/title</c>, and a state: any text that differs exactly when the object differs,
/// such as a digest of its content. The SDK compares the two snapshots; an object counts as
/// changed when its state differs, or when it is in only one of them.
/// </summary>
/// <remarks>
/// An object addressed by a name keeps its address across the operation and is added with
/// <see cref="Add"/>. Objects addressed by their position, such as pages, are added as a
/// sequence: an object that the operation inserted, deleted or moved shifts the positions of
/// the others, so a sequence is compared by state in order, and an object that kept its state
/// but moved counts once, at its new address. Their state must then identify the object, not
/// only its content, wherever the product can tell objects apart. An operation that moves
/// objects names them (<c>moved</c>), since the states alone cannot tell which objects moved:
/// when two neighbors swap places, either one could have moved. A product whose engine keeps
/// each object as one instance adds the sequence with <see cref="AddObjects"/> instead: the
/// objects are then compared by that instance, so one that only moved never counts, and the
/// batch reports each changed one at its position in the output
/// (<see cref="IOperationModel{TOp}.Positions"/>). An object whose address starts with the
/// number of an object the engine keeps as one instance, such as the page of
/// <c>page/3/image/1</c> or the slide of <c>slide/3/shape/7</c>, names that instance as its
/// owner wherever it is added: the operation's snapshots still compare it as they find it, and
/// the batch numbers the address by where its output holds the owner, or its input when the
/// batch removed it.
/// </remarks>
public sealed partial class OperationSnapshot
{
    private readonly List<Item> _objects = [];
    private readonly HashSet<string> _addresses = new(StringComparer.Ordinal);
    private readonly List<Sequence> _sequences = [];

    /// <summary>Adds an object addressed by a name, which keeps its address across the operation.</summary>
    /// <param name="address">The object's <c>&lt;unit&gt;/&lt;address&gt;</c> address, unique in the snapshot.</param>
    /// <param name="state">Text that differs exactly when the object differs.</param>
    /// <param name="owner">
    /// The instance whose position numbers the address, such as the page of <c>page/3</c> or the
    /// slide of <c>slide/3/notes</c>, which <see cref="IOperationModel{TOp}.Positions"/> lists
    /// under the address's unit; null when a name addresses the object.
    /// </param>
    /// <exception cref="ArgumentException">The address is not <c>&lt;unit&gt;/&lt;address&gt;</c> or is added twice.</exception>
    public OperationSnapshot Add(string address, string state, object? owner = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        Check(address);
        if (!_addresses.Add(address))
        {
            throw new ArgumentException($"The snapshot already has an object at '{address}'.", nameof(address));
        }

        _objects.Add(new Item(address, state, owner));
        return this;
    }

    /// <summary>
    /// Adds a sequence of objects addressed by their position, in order, each with its own
    /// address, such as the bookmarks of an outline by their index path.
    /// </summary>
    /// <param name="name">The sequence's name, which matches it with the same sequence of the other snapshot.</param>
    /// <param name="items">The objects in order: each <c>&lt;unit&gt;/&lt;address&gt;</c> address and state.</param>
    /// <param name="moved">
    /// The states of the objects the operation moves, as the snapshot taken before it finds
    /// them; the SDK reads them only from that snapshot, since a position the operation names may
    /// hold another object after it. Each of them whose position changed counts once, at its
    /// address in the output, and the others count only when their order among themselves
    /// changed. Null when the operation names no object it moves.
    /// </param>
    public OperationSnapshot AddSequence(string name, IEnumerable<(string Address, string State)> items, IEnumerable<string>? moved = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        return AddSequence(name, items.Select(static item => new Item(item.Address, item.State, null)), moved);
    }

    private OperationSnapshot AddSequence(string name, IEnumerable<Item> items, IEnumerable<string>? moved)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (_sequences.Any(sequence => sequence.Name == name))
        {
            throw new ArgumentException($"The snapshot already has the sequence '{name}'.", nameof(name));
        }

        Item[] list = [.. items];
        foreach (Item item in list)
        {
            Check(item.Address);
            ArgumentNullException.ThrowIfNull(item.State);
        }

        _sequences.Add(new Sequence(name, list, null, moved?.ToHashSet(StringComparer.Ordinal)));
        return this;
    }

    /// <summary>
    /// Adds a sequence of objects numbered from 1 in <paramref name="unit"/>, such as the blocks
    /// of a document as <c>block/1</c>, <c>block/2</c> and so on, each with the instance the
    /// engine keeps it as, which identifies it across operations by reference.
    /// </summary>
    /// <param name="unit">The unit, which also names the sequence.</param>
    /// <param name="objects">Each object's instance and state, in order.</param>
    /// <param name="reused">
    /// Whether the operation may rebuild objects on instances that held others, as a field
    /// update rebuilds its result, so an instance no longer identifies one object: the objects are
    /// then compared by state in order, as a sequence of <see cref="AddSequence(string, IEnumerable{string}, IEnumerable{string}?, IEnumerable{object}?)"/>
    /// is, and each changed one the output holds is still reported where the output holds its instance.
    /// </param>
    public OperationSnapshot AddObjects(string unit, IEnumerable<(object Identity, string State)> objects, bool reused = false)
    {
        ArgumentNullException.ThrowIfNull(objects);
        (object Identity, string State)[] list = [.. objects];
        AddSequence(unit, list.Select(static item => item.State), owners: list.Select(static item => item.Identity));
        _sequences[^1] = _sequences[^1] with { Identities = [.. list.Select(static item => item.Identity)], Reused = reused };
        return this;
    }

    /// <summary>
    /// Adds a sequence of objects numbered from 1 in <paramref name="unit"/>, such as the pages
    /// of a document as <c>page/1</c>, <c>page/2</c> and so on.
    /// </summary>
    /// <param name="unit">The unit, which also names the sequence.</param>
    /// <param name="states">Each object's state, in order.</param>
    /// <param name="moved">The states of the objects the operation moves, as the other overload describes them.</param>
    /// <param name="owners">
    /// The instance the engine keeps each object as, in the same order, which
    /// <see cref="IOperationModel{TOp}.Positions"/> lists under <paramref name="unit"/>: the
    /// objects are still compared by state, and the batch numbers each changed one where its
    /// output holds the instance. Null when the engine keeps no such instance.
    /// </param>
    /// <exception cref="ArgumentException">There are not as many owners as states.</exception>
    public OperationSnapshot AddSequence(string unit, IEnumerable<string> states, IEnumerable<string>? moved = null, IEnumerable<object>? owners = null)
    {
        ArgumentNullException.ThrowIfNull(states);
        string[] list = [.. states];
        object?[] instances = owners is null ? new object?[list.Length] : [.. owners];
        if (instances.Length != list.Length)
        {
            throw new ArgumentException($"The sequence '{unit}' has {list.Length} objects but {instances.Length} owners.", nameof(owners));
        }

        return AddSequence(unit, list.Select((state, index) =>
            new Item(string.Create(CultureInfo.InvariantCulture, $"{unit}/{index + 1}"), state, instances[index])), moved);
    }

    /// <summary>Whether an address is <c>&lt;unit&gt;/&lt;address&gt;</c>: a lowercase kebab-case unit, a slash and the address.</summary>
    public static bool IsAddress(string address) => address is not null && Address().IsMatch(address);

    /// <summary>
    /// Compares the snapshots taken before and after one operation: the objects that changed,
    /// each once, at its address in the output, or in the input for an object the operation removed.
    /// </summary>
    internal static IReadOnlyList<ObjectChange> Compare(OperationSnapshot before, OperationSnapshot after)
    {
        var changes = new List<ObjectChange>();
        Dictionary<string, string> earlier = before._objects.ToDictionary(static item => item.Address, static item => item.State, StringComparer.Ordinal);
        foreach (Item item in after._objects)
        {
            if (!earlier.TryGetValue(item.Address, out string? previous) || previous != item.State)
            {
                changes.Add(new ObjectChange(item.Address, InOutput: true, Owner: item.Owner));
            }
        }

        changes.AddRange(before._objects
            .Where(item => !after._addresses.Contains(item.Address))
            .Select(static item => new ObjectChange(item.Address, InOutput: false, Owner: item.Owner)));
        foreach (string name in before._sequences.Select(static sequence => sequence.Name)
            .Concat(after._sequences.Select(static sequence => sequence.Name))
            .Distinct(StringComparer.Ordinal))
        {
            Sequence first = Find(before, name);
            Sequence second = Find(after, name);
            changes.AddRange(first.Identities is not null && second.Identities is not null && !first.Reused && !second.Reused
                ? ObjectChanges(first, second)
                : first.Moved is { } moved
                    ? MovedChanges(first.Items, second.Items, moved)
                    : SequenceChanges(first.Items, second.Items, first.Identities, second.Identities));
        }

        return changes;
    }

    /// <summary>
    /// The changed objects of one sequence the engine keeps by instance: an object in both
    /// snapshots counts when its state differs, wherever it moved; one in only one snapshot was
    /// inserted or removed, except where the engine replaced the instance: an inserted object
    /// paired with a removed one of the same state does not count, as nothing it shows changed,
    /// and one paired with a removed one at the same position counts once, at its address in the
    /// output.
    /// </summary>
    private static IEnumerable<ObjectChange> ObjectChanges(Sequence before, Sequence after)
    {
        var earlier = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
        for (int index = 0; index < before.Items.Count; index++)
        {
            earlier.TryAdd(before.Identities![index], index);
        }

        var kept = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var inserted = new List<int>();
        for (int index = 0; index < after.Items.Count; index++)
        {
            object identity = after.Identities![index];
            if (earlier.TryGetValue(identity, out int previous))
            {
                kept.Add(identity);
                if (before.Items[previous].State != after.Items[index].State)
                {
                    yield return new ObjectChange(after.Items[index].Address, InOutput: true, identity, identity);
                }
            }
            else
            {
                inserted.Add(index);
            }
        }

        var removed = Enumerable.Range(0, before.Items.Count).Where(index => !kept.Contains(before.Identities![index])).ToList();
        foreach (int index in inserted)
        {
            int same = removed.FindIndex(deletion => before.Items[deletion].State == after.Items[index].State);
            if (same >= 0)
            {
                removed.RemoveAt(same);
                continue;
            }

            int pair = removed.FindIndex(deletion => before.Items[deletion].Address == after.Items[index].Address);
            if (pair >= 0)
            {
                removed.RemoveAt(pair);
            }

            yield return new ObjectChange(after.Items[index].Address, InOutput: true, after.Identities![index], after.Identities[index]);
        }

        foreach (int index in removed)
        {
            yield return new ObjectChange(before.Items[index].Address, InOutput: false, before.Identities![index], before.Identities[index]);
        }
    }

    /// <summary>
    /// The changed objects of one sequence: the shortest edit script by state, then an inserted
    /// object paired with a deleted one of the same state (a move) or of the same address (an
    /// object changed in place) counts once, at its address in the output. With the instances of
    /// both snapshots, an object counts with its instance: an inserted one with the instance the
    /// output holds, and a removed one with the instance it had, unless the output holds that
    /// instance as another object. Each counts with its owner, a removed one unless the output
    /// holds that owner as another object.
    /// </summary>
    private static IEnumerable<ObjectChange> SequenceChanges(
        IReadOnlyList<Item> before,
        IReadOnlyList<Item> after,
        IReadOnlyList<object>? beforeIdentities = null,
        IReadOnlyList<object>? afterIdentities = null)
    {
        bool identified = beforeIdentities is not null && afterIdentities is not null;
        var kept = new HashSet<object>(afterIdentities ?? [], ReferenceEqualityComparer.Instance);
        var owned = new HashSet<object>(after.Select(static item => item.Owner).OfType<object>(), ReferenceEqualityComparer.Instance);
        (List<int> deleted, List<int> inserted) = SequenceDiff.Edits(
            before.Select(static item => item.State).ToArray(),
            after.Select(static item => item.State).ToArray());
        var unpaired = new List<int>(deleted);
        foreach (int index in inserted)
        {
            int pair = unpaired.FindIndex(deletion => before[deletion].State == after[index].State);
            if (pair < 0)
            {
                pair = unpaired.FindIndex(deletion => before[deletion].Address == after[index].Address);
            }

            if (pair >= 0)
            {
                unpaired.RemoveAt(pair);
            }

            yield return new ObjectChange(after[index].Address, InOutput: true, identified ? afterIdentities![index] : null, after[index].Owner);
        }

        foreach (int index in unpaired)
        {
            yield return new ObjectChange(before[index].Address, InOutput: false,
                identified && !kept.Contains(beforeIdentities![index]) ? beforeIdentities[index] : null,
                before[index].Owner is { } owner && !owned.Contains(owner) ? owner : null);
        }
    }

    /// <summary>
    /// The changed objects of one sequence whose moved objects the operation named: the other
    /// objects are compared among themselves (<see cref="SequenceChanges"/>), and each moved one
    /// counts once when its position changed, at its address in the output, or in the input when
    /// the operation removed it.
    /// </summary>
    private static IEnumerable<ObjectChange> MovedChanges(
        IReadOnlyList<Item> before,
        IReadOnlyList<Item> after,
        IReadOnlySet<string> moved)
    {
        foreach (ObjectChange change in SequenceChanges(
            [.. before.Where(item => !moved.Contains(item.State))],
            [.. after.Where(item => !moved.Contains(item.State))]))
        {
            yield return change;
        }

        var unpaired = Enumerable.Range(0, before.Count).Where(index => moved.Contains(before[index].State)).ToList();
        for (int index = 0; index < after.Count; index++)
        {
            if (!moved.Contains(after[index].State))
            {
                continue;
            }

            int pair = unpaired.FindIndex(previous => before[previous].State == after[index].State);
            if (pair >= 0)
            {
                int previous = unpaired[pair];
                unpaired.RemoveAt(pair);
                if (previous == index)
                {
                    continue;
                }
            }

            yield return new ObjectChange(after[index].Address, InOutput: true, Owner: after[index].Owner);
        }

        foreach (int index in unpaired)
        {
            yield return new ObjectChange(before[index].Address, InOutput: false, Owner: before[index].Owner);
        }
    }

    private static Sequence Find(OperationSnapshot snapshot, string name) =>
        snapshot._sequences.FirstOrDefault(sequence => sequence.Name == name) ?? new Sequence(name, [], null, null);

    /// <summary>
    /// Shortens more than <see cref="BoundedOperationOutcome.MaximumTargets"/> addresses to one per
    /// unit that covers every one of them: <c>&lt;unit&gt;/&lt;ranges&gt;</c>, such as
    /// <c>page/1-150</c>, when every address of the unit starts with a number, and
    /// <c>&lt;unit&gt;/all</c> otherwise.
    /// </summary>
    internal static IReadOnlyList<string> Cover(IReadOnlyList<string> addresses) =>
        [.. addresses
            .GroupBy(static address => address[..address.IndexOf('/', StringComparison.Ordinal)], StringComparer.Ordinal)
            .Select(static unit =>
            {
                int[] numbers = [.. unit.Select(static address => FirstNumber(address)).OfType<int>()];
                return numbers.Length == unit.Count()
                    ? $"{unit.Key}/{PageRange.Describe(numbers)}"
                    : $"{unit.Key}/all";
            })];

    /// <summary>The 1-based number an address starts with after its unit, such as 3 in <c>page/3</c> or <c>bookmark/3/1</c>.</summary>
    internal static int? FirstNumber(string address)
    {
        string rest = address[(address.IndexOf('/', StringComparison.Ordinal) + 1)..];
        int end = rest.IndexOf('/', StringComparison.Ordinal);
        return int.TryParse(end < 0 ? rest : rest[..end], NumberStyles.None, CultureInfo.InvariantCulture, out int number) && number > 0
            ? number
            : null;
    }

    private static void Check(string address)
    {
        if (!IsAddress(address))
        {
            throw new ArgumentException(
                $"'{address}' is not a <unit>/<address> address: a lowercase kebab-case unit, a slash and the address.", nameof(address));
        }
    }

    [GeneratedRegex(@"^[a-z]+(?:-[a-z]+)*/\S")]
    private static partial Regex Address();

    /// <summary>One object of a snapshot: its address, its state and the instance whose position numbers the address.</summary>
    private readonly record struct Item(string Address, string State, object? Owner);

    /// <summary>One sequence of a snapshot: its objects, the instances of a sequence added with <see cref="AddObjects"/>, and the states of the objects the operation moves.</summary>
    private sealed record Sequence(
        string Name,
        IReadOnlyList<Item> Items,
        IReadOnlyList<object>? Identities,
        IReadOnlySet<string>? Moved)
    {
        /// <summary>Whether the engine may rebuild objects on instances that held others (<see cref="AddObjects"/>).</summary>
        public bool Reused { get; init; }
    }
}

/// <summary>One object an operation changed.</summary>
/// <param name="Address">Its address in the output, or in the input when the operation removed it.</param>
/// <param name="InOutput">Whether the object is in the output: false when the operation removed it.</param>
/// <param name="Identity">The instance the engine keeps the object as, for a sequence added with <see cref="OperationSnapshot.AddObjects"/>.</param>
/// <param name="Owner">The instance whose position numbers the address: the object's own, or the page or slide it is on.</param>
internal readonly record struct ObjectChange(string Address, bool InOutput, object? Identity = null, object? Owner = null);

/// <summary>The shortest edit script between two sequences (Myers' O(ND) algorithm).</summary>
internal static class SequenceDiff
{
    /// <summary>The indexes of the items deleted from <paramref name="before"/> and inserted into <paramref name="after"/>, each ascending.</summary>
    public static (List<int> Deleted, List<int> Inserted) Edits(string[] before, string[] after)
    {
        int prefix = 0;
        while (prefix < before.Length && prefix < after.Length && before[prefix] == after[prefix])
        {
            prefix++;
        }

        int suffix = 0;
        while (suffix < before.Length - prefix && suffix < after.Length - prefix
            && before[before.Length - 1 - suffix] == after[after.Length - 1 - suffix])
        {
            suffix++;
        }

        string[] a = before[prefix..(before.Length - suffix)];
        string[] b = after[prefix..(after.Length - suffix)];
        var deleted = new List<int>();
        var inserted = new List<int>();
        int n = a.Length;
        int m = b.Length;
        if (n == 0 || m == 0)
        {
            deleted.AddRange(Enumerable.Range(prefix, n));
            inserted.AddRange(Enumerable.Range(prefix, m));
            return (deleted, inserted);
        }

        int max = n + m;
        int offset = max + 1;
        int[] v = new int[(2 * max) + 3];
        var trace = new List<int[]>();
        int found = -1;
        for (int d = 0; d <= max && found < 0; d++)
        {
            trace.Add((int[])v.Clone());
            for (int k = -d; k <= d; k += 2)
            {
                int x = k == -d || (k != d && v[offset + k - 1] < v[offset + k + 1]) ? v[offset + k + 1] : v[offset + k - 1] + 1;
                int y = x - k;
                while (x < n && y < m && a[x] == b[y])
                {
                    x++;
                    y++;
                }

                v[offset + k] = x;
                if (x >= n && y >= m)
                {
                    found = d;
                    break;
                }
            }
        }

        int cx = n;
        int cy = m;
        for (int d = found; d > 0; d--)
        {
            int[] previous = trace[d];
            int k = cx - cy;
            int previousK = k == -d || (k != d && previous[offset + k - 1] < previous[offset + k + 1]) ? k + 1 : k - 1;
            int previousX = previous[offset + previousK];
            int previousY = previousX - previousK;
            while (cx > previousX && cy > previousY)
            {
                cx--;
                cy--;
            }

            if (cx == previousX)
            {
                inserted.Add(prefix + previousY);
            }
            else
            {
                deleted.Add(prefix + previousX);
            }

            cx = previousX;
            cy = previousY;
        }

        deleted.Reverse();
        inserted.Reverse();
        return (deleted, inserted);
    }
}
