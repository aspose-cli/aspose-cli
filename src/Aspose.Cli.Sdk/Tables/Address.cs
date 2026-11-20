using System.Globalization;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.Tables;

/// <summary>One step of an address: a segment and its key below the previous object.</summary>
/// <param name="Placement">Where the step's kind sits below the previous one.</param>
/// <param name="Alias">The alias the step uses, or null when it uses the placement's own key.</param>
/// <param name="Key">The key as the address writes it.</param>
internal sealed record AddressStep(Placement Placement, AliasPlacement? Alias, Key Key)
{
    /// <summary>The step as the address writes it, such as <c>shape/3</c>.</summary>
    public string Text => Key.Parts.Count == 0
        ? Segment
        : $"{Segment}/{Key}";

    private string Segment => Alias?.Segment ?? Placement.Segment;
}

/// <summary>
/// An address of the object table, checked against the table but not yet looked up in a
/// document: <c>presentation</c> for the document itself, otherwise segments and keys such as
/// <c>slide/256/shape/3/paragraph/0</c>.
/// </summary>
internal sealed partial record TableAddress(string Text, ObjectKind Kind, IReadOnlyList<AddressStep> Steps)
{
    /// <summary>The placement of the addressed object below its parent, or null for the document.</summary>
    public Placement? Placement => Steps.Count == 0 ? null : Steps[^1].Placement;

    /// <summary>Checks an address against the table.</summary>
    /// <exception cref="TableValueException">The address does not follow the table's segments and keys.</exception>
    public static TableAddress Parse(DocumentTable table, string text)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (string.IsNullOrEmpty(text))
        {
            throw new TableValueException("the address is empty");
        }

        if (text == table.Root.Name)
        {
            return new TableAddress(text, table.Root, []);
        }

        string[] tokens = Split(text);
        var steps = new List<AddressStep>();
        ObjectKind current = table.Root;
        int position = 0;
        while (position < tokens.Length)
        {
            string segment = tokens[position++];
            Placement? placement = table.Placements.FirstOrDefault(candidate => candidate.Parent == current && candidate.Segment == segment);
            AliasPlacement? alias = placement is null
                ? table.Aliases.FirstOrDefault(candidate => candidate.Target.Parent == current && candidate.Segment == segment)
                : null;
            if (placement is null && alias is null)
            {
                string[] segments = [.. table.Placements.Where(candidate => candidate.Parent == current).Select(static candidate => candidate.Segment)
                    .Concat(table.Aliases.Where(candidate => candidate.Target.Parent == current).Select(static candidate => candidate.Segment))];
                string after = steps.Count == 0 ? "the start of an address" : $"'{string.Join('/', steps.Select(static step => step.Text))}', a {current.Name},";
                throw new TableValueException(segments.Length == 0
                    ? $"'{text}' goes on after {after} which has no objects below it"
                    : $"'{segment}' in '{text}' is no segment after {after}; segments there: {string.Join(", ", segments)}");
            }

            IReadOnlyList<KeyPart> parts = alias?.Key ?? placement!.Key;
            var key = new List<string>();
            foreach (KeyPart part in parts)
            {
                if (position >= tokens.Length)
                {
                    throw new TableValueException($"'{text}' ends before the key of '{segment}', which is {Describe(parts, alias?.Optional)}");
                }

                string token = tokens[position++];
                if (!Matches(part, token))
                {
                    throw new TableValueException($"'{token}' in '{text}' is not {Describe(part)}, the key of '{segment}'");
                }

                key.Add(token);
            }

            if (alias?.Optional is { } optional && position < tokens.Length && Matches(optional, tokens[position]))
            {
                key.Add(tokens[position++]);
            }

            Placement target = placement ?? alias!.Target;
            steps.Add(new AddressStep(target, alias, Key.Parsed(key)));
            current = target.Kind;
        }

        return new TableAddress(text, current, steps);
    }

    /// <summary>
    /// A key part as an address writes it: a <c>/</c> in it is written <c>\/</c> and a <c>\</c>
    /// is written <c>\\</c>, so a name the engine allows any character in has an address.
    /// </summary>
    public static string Escape(string part)
    {
        ArgumentNullException.ThrowIfNull(part);
        return part.Contains('/', StringComparison.Ordinal) || part.Contains('\\', StringComparison.Ordinal)
            ? part.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("/", "\\/", StringComparison.Ordinal)
            : part;
    }

    /// <summary>
    /// The segments and key parts of an address: split at each <c>/</c> that is not written
    /// <c>\/</c>, with <c>\/</c> and <c>\\</c> read back as <c>/</c> and <c>\</c>; any other
    /// <c>\</c> is kept as it is.
    /// </summary>
    private static string[] Split(string text)
    {
        if (!text.Contains('\\', StringComparison.Ordinal))
        {
            return text.Split('/');
        }

        var tokens = new List<string>();
        var token = new System.Text.StringBuilder();
        for (int position = 0; position < text.Length; position++)
        {
            char current = text[position];
            if (current == '\\' && position + 1 < text.Length && text[position + 1] is '/' or '\\')
            {
                token.Append(text[++position]);
            }
            else if (current == '/')
            {
                tokens.Add(token.ToString());
                token.Clear();
            }
            else
            {
                token.Append(current);
            }
        }

        tokens.Add(token.ToString());
        return [.. tokens];
    }

    /// <summary>Whether <paramref name="token"/> has the form of <paramref name="part"/>; what it names is the engine's to say.</summary>
    internal static bool Matches(KeyPart part, string token) => part switch
    {
        KeyPart.Index => int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out _),
        KeyPart.Ordinal => int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out int ordinal) && ordinal >= 1,
        KeyPart.Id or KeyPart.Number => uint.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out _),
        KeyPart.Guid => System.Guid.TryParseExact(token, "D", out _),
        KeyPart.Text => token.Length > 0,
        KeyPart.CellName => CellNamePattern().IsMatch(token),
        KeyPart.Area => AreaPattern().IsMatch(token),
        KeyPart.RowName => RowNamePattern().IsMatch(token),
        KeyPart.ColumnName => ColumnNamePattern().IsMatch(token),
        _ => NamePattern().IsMatch(token),
    };

    /// <summary>What a key part is, as an error states it.</summary>
    internal static string Describe(KeyPart part) => part switch
    {
        KeyPart.Index => "a position from 0",
        KeyPart.Ordinal => "a position from 1",
        KeyPart.Id => "an id",
        KeyPart.Number => "a whole number",
        KeyPart.Guid => "a GUID",
        KeyPart.Text => "a name",
        KeyPart.CellName => "a cell name such as B5",
        KeyPart.Area => "an area such as B2:D10, B5, 5:5 or C:C",
        KeyPart.RowName => "a row number from 1, such as the 5 of B5",
        KeyPart.ColumnName => "a column name such as the B of B5",
        _ => "a name",
    };

    private static string Describe(IReadOnlyList<KeyPart> parts, KeyPart? optional) =>
        string.Join(" then ", parts.Select(Describe)) + (optional is { } part ? $", optionally then {Describe(part)}" : string.Empty);

    /// <summary>The pattern of a key part that is a name.</summary>
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9._-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();

    [GeneratedRegex("^[A-Za-z]{1,3}[1-9][0-9]{0,6}$", RegexOptions.CultureInvariant)]
    private static partial Regex CellNamePattern();

    [GeneratedRegex("^(?:[A-Za-z]{1,3}[1-9][0-9]{0,6}(?::[A-Za-z]{1,3}[1-9][0-9]{0,6})?|[A-Za-z]{1,3}:[A-Za-z]{1,3}|[1-9][0-9]{0,6}:[1-9][0-9]{0,6})$", RegexOptions.CultureInvariant)]
    private static partial Regex AreaPattern();

    [GeneratedRegex("^[1-9][0-9]{0,6}$", RegexOptions.CultureInvariant)]
    private static partial Regex RowNamePattern();

    [GeneratedRegex("^[A-Za-z]{1,3}$", RegexOptions.CultureInvariant)]
    private static partial Regex ColumnNamePattern();
}

/// <summary>An object an address found in a document.</summary>
/// <param name="Item">The engine object.</param>
/// <param name="Parent">The engine object it sits below, or null for the document.</param>
/// <param name="Placement">Where it sits, or null for the document.</param>
/// <param name="Kind">Its kind.</param>
/// <param name="Address">Its canonical address, which names it by its placement's own key.</param>
/// <param name="ParentAddress">The canonical address of its parent, or null for the document.</param>
internal sealed record Located(object Item, object? Parent, Placement? Placement, ObjectKind Kind, string Address, string? ParentAddress);

/// <summary>Looks addresses up in a document.</summary>
internal static class AddressResolver
{
    /// <summary>Finds the object an address names.</summary>
    /// <exception cref="CliException"><c>ADDRESS_NOT_FOUND</c> or <c>ADDRESS_AMBIGUOUS</c>.</exception>
    public static Located Resolve(DocumentTable table, object document, TableAddress address)
    {
        object current = document;
        object? parent = null;
        Placement? placement = null;
        string canonical = table.Root.Name;
        string? parentAddress = null;
        var path = new List<string>();
        foreach (AddressStep step in address.Steps)
        {
            string here = path.Count == 0 ? table.Root.Name : string.Join('/', path);
            parentAddress = here;
            object? found;
            if (step.Alias is { } alias)
            {
                IReadOnlyList<object> matches = Engine(table, address.Text, () => alias.Find(current, step.Key));
                if (matches.Count == 0)
                {
                    throw TableErrors.NotFound(alias.Segment, step.Key.ToString(), here, [.. alias.List(current).Select(static key => key.ToString())], address.Text);
                }

                if (matches.Count > 1)
                {
                    string prefix = path.Count == 0 ? string.Empty : here + "/";
                    throw TableErrors.Ambiguous(alias.Segment, step.Key.ToString(), here,
                        [.. matches.Select(match => $"{prefix}{Text(step.Placement, step.Placement.KeyOf(current, match))}")], address.Text);
                }

                found = matches[0];
            }
            else
            {
                IReadOnlyList<object> matches = Engine(table, address.Text, () => step.Placement.Find(current, step.Key));
                if (matches.Count == 0)
                {
                    throw TableErrors.NotFound(step.Placement.Segment, step.Key.ToString(), path.Count == 0 ? null : here,
                        [.. step.Placement.List(current).Select(item => step.Placement.KeyOf(current, item).ToString())], address.Text);
                }

                if (matches.Count > 1)
                {
                    string prefix = path.Count == 0 ? string.Empty : here + "/";
                    throw TableErrors.Ambiguous(step.Placement.Segment, step.Key.ToString(), here,
                        [.. matches.Select(match => $"{prefix}{Text(step.Placement, step.Placement.KeyOf(current, match))}")], address.Text);
                }

                found = matches[0];
            }

            path.Add(Text(step.Placement, step.Placement.KeyOf(current, found)));
            parent = current;
            current = found;
            placement = step.Placement;
        }

        if (path.Count > 0)
        {
            canonical = string.Join('/', path);
        }

        return new Located(current, parent, placement, address.Kind, canonical, parentAddress);
    }

    /// <summary>Runs an engine lookup; an exception the engine throws is reported as the engine's, at the address.</summary>
    internal static T Engine<T>(DocumentTable table, string address, Func<T> lookup)
    {
        try
        {
            return lookup();
        }
        catch (Exception exception) when (exception is not CliException and not OperationCanceledException && TableErrors.FromEngine(exception, table.Engine))
        {
            throw TableErrors.Engine(null, address, null, exception);
        }
    }

    /// <summary>The address of an object below a parent whose address is <paramref name="parentAddress"/>.</summary>
    public static string Below(DocumentTable table, string parentAddress, Placement placement, object parent, object item)
    {
        string step = Text(placement, placement.KeyOf(parent, item));
        return parentAddress == table.Root.Name ? step : $"{parentAddress}/{step}";
    }

    private static string Text(Placement placement, Key key) =>
        key.Parts.Count == 0 ? placement.Segment : $"{placement.Segment}/{key}";
}
