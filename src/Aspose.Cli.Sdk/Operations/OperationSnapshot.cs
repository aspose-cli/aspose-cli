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
/// only its content, wherever the product can tell objects apart.
/// </remarks>
public sealed partial class OperationSnapshot
{
    private readonly List<(string Address, string State)> _objects = [];
    private readonly HashSet<string> _addresses = new(StringComparer.Ordinal);
    private readonly List<(string Name, IReadOnlyList<(string Address, string State)> Items)> _sequences = [];

    /// <summary>Adds an object addressed by a name, which keeps its address across the operation.</summary>
    /// <param name="address">The object's <c>&lt;unit&gt;/&lt;address&gt;</c> address, unique in the snapshot.</param>
    /// <param name="state">Text that differs exactly when the object differs.</param>
    /// <exception cref="ArgumentException">The address is not <c>&lt;unit&gt;/&lt;address&gt;</c> or is added twice.</exception>
    public OperationSnapshot Add(string address, string state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Check(address);
        if (!_addresses.Add(address))
        {
            throw new ArgumentException($"The snapshot already has an object at '{address}'.", nameof(address));
        }

        _objects.Add((address, state));
        return this;
    }

    /// <summary>
    /// Adds a sequence of objects addressed by their position, in order, each with its own
    /// address, such as the bookmarks of an outline by their index path.
    /// </summary>
    /// <param name="name">The sequence's name, which matches it with the same sequence of the other snapshot.</param>
    /// <param name="items">The objects in order: each <c>&lt;unit&gt;/&lt;address&gt;</c> address and state.</param>
    public OperationSnapshot AddSequence(string name, IEnumerable<(string Address, string State)> items)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(items);
        if (_sequences.Any(sequence => sequence.Name == name))
        {
            throw new ArgumentException($"The snapshot already has the sequence '{name}'.", nameof(name));
        }

        (string Address, string State)[] list = [.. items];
        foreach ((string address, string state) in list)
        {
            Check(address);
            ArgumentNullException.ThrowIfNull(state);
        }

        _sequences.Add((name, list));
        return this;
    }

    /// <summary>
    /// Adds a sequence of objects numbered from 1 in <paramref name="unit"/>, such as the pages
    /// of a document as <c>page/1</c>, <c>page/2</c> and so on.
    /// </summary>
    /// <param name="unit">The unit, which also names the sequence.</param>
    /// <param name="states">Each object's state, in order.</param>
    public OperationSnapshot AddSequence(string unit, IEnumerable<string> states)
    {
        ArgumentNullException.ThrowIfNull(states);
        return AddSequence(unit, states.Select((state, index) =>
            (string.Create(CultureInfo.InvariantCulture, $"{unit}/{index + 1}"), state)));
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
        foreach ((string address, string state) in after._objects)
        {
            if (!earlier.TryGetValue(address, out string? previous) || previous != state)
            {
                changes.Add(new ObjectChange(address, InOutput: true));
            }
        }

        changes.AddRange(before._objects
            .Where(item => !after._addresses.Contains(item.Address))
            .Select(static item => new ObjectChange(item.Address, InOutput: false)));
        foreach (string name in before._sequences.Select(static sequence => sequence.Name)
            .Concat(after._sequences.Select(static sequence => sequence.Name))
            .Distinct(StringComparer.Ordinal))
        {
            changes.AddRange(SequenceChanges(Items(before, name), Items(after, name)));
        }

        return changes;
    }

    /// <summary>
    /// The changed objects of one sequence: the shortest edit script by state, then an inserted
    /// object paired with a deleted one of the same state (a move) or of the same address (an
    /// object changed in place) counts once, at its address in the output.
    /// </summary>
    private static IEnumerable<ObjectChange> SequenceChanges(
        IReadOnlyList<(string Address, string State)> before,
        IReadOnlyList<(string Address, string State)> after)
    {
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

            yield return new ObjectChange(after[index].Address, InOutput: true);
        }

        foreach (int index in unpaired)
        {
            yield return new ObjectChange(before[index].Address, InOutput: false);
        }
    }

    private static IReadOnlyList<(string Address, string State)> Items(OperationSnapshot snapshot, string name) =>
        snapshot._sequences.FirstOrDefault(sequence => sequence.Name == name).Items ?? [];

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
}

/// <summary>One object an operation changed.</summary>
/// <param name="Address">Its address in the output, or in the input when the operation removed it.</param>
/// <param name="InOutput">Whether the object is in the output: false when the operation removed it.</param>
internal readonly record struct ObjectChange(string Address, bool InOutput);

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
