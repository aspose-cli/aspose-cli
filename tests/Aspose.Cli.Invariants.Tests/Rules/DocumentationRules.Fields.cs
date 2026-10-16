using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>
/// C-R38 for the names that a caller once wrote, or commonly writes, and that this build refuses.
/// Each comes from where the build declares it: an operation or field name from the
/// <c>[MistakenFor]</c> declarations in the product sources, read from their C# text, so a name
/// added there joins the table; a removed command, option or option value from the command tree
/// of capabilities, which lists every name that is still accepted, so anything it does not list
/// is removed. A text names an operation's fields when it writes the operation as JSON
/// (<c>{"op":"set_slide_size","slideSize":...}</c>): each key, at every depth, must be a field of
/// that operation at that place, judged by the operation's definition in its product's ops schema,
/// since a mistaken name of one operation is often a field of another (<c>size</c>).
/// </summary>
internal static partial class ContractRules
{
    /// <summary>What a mistaken name stands for.</summary>
    internal enum MistakenKind
    {
        /// <summary>An operation, by its name.</summary>
        Operation,

        /// <summary>A field, by its JSON name.</summary>
        Field,

        /// <summary>A command, by its path such as <c>cells read</c>.</summary>
        Command,
    }

    /// <summary>
    /// One name a product declares mistaken: the product, the name, what it stands for and the
    /// file that declares it.
    /// </summary>
    internal sealed record MistakenName(string? Product, string Name, string Meant, MistakenKind Kind, string Where)
    {
        public bool IsOperation => Kind == MistakenKind.Operation;
    }

    /// <summary>The <c>[MistakenFor]</c> declarations of the product sources, and the problems reading them.</summary>
    internal sealed record MistakenNames(IReadOnlyList<MistakenName> Names, IReadOnlyList<string> Problems)
    {
        private static readonly Lazy<MistakenNames> Sources = new(static () => Read(Path.Combine(RepositoryPaths.Root, "src")));

        /// <summary>The declarations of this repository's sources.</summary>
        public static MistakenNames Current => Sources.Value;

        /// <summary>The operation a mistaken operation name stands for in a product, or null.</summary>
        public string? Operation(string? product, string name) =>
            Names.FirstOrDefault(mistaken => mistaken.IsOperation && Same(mistaken.Product, product)
                && string.Equals(mistaken.Name, name, StringComparison.OrdinalIgnoreCase))?.Meant;

        /// <summary>The fields a mistaken field name stands for in a product.</summary>
        public IEnumerable<string> Fields(string? product, string name) =>
            Names.Where(mistaken => mistaken.Kind == MistakenKind.Field && Same(mistaken.Product, product)
                    && string.Equals(mistaken.Name, name, StringComparison.OrdinalIgnoreCase))
                .Select(static mistaken => mistaken.Meant)
                .Distinct(StringComparer.Ordinal);

        /// <summary>The commands below <paramref name="parent"/>, such as <c>cells</c>, that a mistaken command name stands for.</summary>
        public IEnumerable<string> Commands(string parent, string name) =>
            Names.Where(mistaken => mistaken.Kind == MistakenKind.Command && mistaken.Name == name
                    && mistaken.Meant.StartsWith(parent + " ", StringComparison.Ordinal))
                .Select(static mistaken => mistaken.Meant)
                .Distinct(StringComparer.Ordinal);

        private static bool Same(string? declared, string? product) => product is null || declared is null || declared == product;

        /// <summary>Reads every C# file under <paramref name="sources"/>, its bin and obj folders left out.</summary>
        public static MistakenNames Read(string sources)
        {
            var names = new List<MistakenName>();
            var problems = new List<string>();
            foreach ((string file, string relative) in SourceFiles(sources, "*.cs"))
            {
                names.AddRange(ReadFile(ProductOf(relative.Split('/')[0]), "src/" + relative, File.ReadAllText(file), problems));
            }
            return new MistakenNames(names, problems);
        }

        /// <summary>
        /// Reads the declarations of one file, by their syntax rather than by the name alone, so a
        /// method named <c>MistakenFor</c> declares nothing. A <c>[MistakenFor(...)]</c> attribute
        /// applies to the declaration it is written before, on its line or on the lines above: a
        /// record or class declaration is an operation that its <c>[Operation("...")]</c> names,
        /// any other declaration a property whose JSON name is its <c>[JsonPropertyName]</c> or its
        /// camel-cased name. Every such attribute of the text must be read, so a declaration this
        /// reading cannot place is a problem rather than a name silently left out. A
        /// <c>MistakenFor = [...]</c> initializer belongs to the command definition the nearest
        /// <c>new(</c> before it creates, named by its first string, under the product; a
        /// <c>mistakenFor: [...]</c> argument, to the edit command <c>EditDefinition.Create</c> defines.
        /// </summary>
        public static IEnumerable<MistakenName> ReadFile(string? product, string where, string source, List<string> problems)
        {
            var names = new List<MistakenName>();
            var attributes = new List<string>();
            int read = 0;
            string pending = string.Empty;
            foreach (string raw in source.ReplaceLineEndings("\n").Split('\n'))
            {
                string line = pending.Length > 0 ? pending + " " + raw.Trim() : raw.Trim();
                pending = string.Empty;
                if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }
                string rest = line;
                while (rest.StartsWith('['))
                {
                    int end = AttributeEnd(rest);
                    if (end < 0)
                    {
                        break;
                    }
                    attributes.Add(rest[1..end]);
                    rest = rest[(end + 1)..].TrimStart();
                }
                if (rest.StartsWith('['))
                {
                    // An attribute list that continues on the next line.
                    pending = rest;
                    continue;
                }
                if (rest.Length == 0)
                {
                    continue;
                }
                string[] mistaken = [.. attributes.Where(static attribute => MistakenAttribute().IsMatch(attribute))
                    .SelectMany(static attribute => MistakenAttribute().Matches(attribute))
                    .SelectMany(static match => Literal().Matches(match.Groups["names"].Value).Select(static literal => literal.Groups["text"].Value))];
                read += attributes.Sum(static attribute => MistakenAttribute().Matches(attribute).Count);
                if (mistaken.Length > 0)
                {
                    if (TypeDeclaration().IsMatch(rest))
                    {
                        string? operation = attributes.Select(static attribute => OperationAttribute().Match(attribute))
                            .FirstOrDefault(static match => match.Success)?.Groups["name"].Value;
                        if (operation is null)
                        {
                            problems.Add($"{where}: [MistakenFor({string.Join(", ", mistaken)})] is on a type with no [Operation(\"...\")]");
                        }
                        else
                        {
                            names.AddRange(mistaken.Select(name => new MistakenName(product, name, operation, MistakenKind.Operation, where)));
                        }
                    }
                    else if (PropertyName().Match(rest) is { Success: true } property)
                    {
                        string field = attributes.Select(static attribute => JsonNameAttribute().Match(attribute))
                            .FirstOrDefault(static match => match.Success)?.Groups["name"].Value
                            ?? JsonNamingPolicy.CamelCase.ConvertName(property.Groups["name"].Value);
                        names.AddRange(mistaken.Select(name => new MistakenName(product, name, field, MistakenKind.Field, where)));
                    }
                    else
                    {
                        problems.Add($"{where}: [MistakenFor({string.Join(", ", mistaken)})] is before '{rest}', which reads as no type or property");
                    }
                }
                attributes.Clear();
            }
            int written = MistakenAttributeInSource().Matches(source).Count;
            if (written != read)
            {
                problems.Add($"{where}: writes [MistakenFor(...)] {written} time(s) but {read} were read before a declaration");
            }
            foreach (Match initializer in MistakenInitializer().Matches(source))
            {
                string[] declaredNames = [.. Literal().Matches(initializer.Groups["names"].Value).Select(static literal => literal.Groups["text"].Value)];
                Match? created = initializer.Groups["edit"].Success
                    ? EditCreation().Matches(source[..initializer.Index]).LastOrDefault()
                    : DefinitionCreation().Matches(source[..initializer.Index]).LastOrDefault();
                if (product is null || created is null)
                {
                    problems.Add($"{where}: {initializer.Value} initializes no command definition of a product");
                    continue;
                }
                string command = $"{product} {(initializer.Groups["edit"].Success ? "edit" : created.Groups["name"].Value)}";
                names.AddRange(declaredNames.Select(name => new MistakenName(product, name, command, MistakenKind.Command, where)));
            }
            return names;
        }

        /// <summary>The index of the bracket that closes the attribute list at the start of <paramref name="text"/>, or -1.</summary>
        private static int AttributeEnd(string text)
        {
            int depth = 0;
            bool quoted = false;
            for (int index = 0; index < text.Length; index++)
            {
                char current = text[index];
                if (quoted)
                {
                    if (current == '\\')
                    {
                        index++;
                    }
                    else if (current == '"')
                    {
                        quoted = false;
                    }
                }
                else if (current == '"')
                {
                    quoted = true;
                }
                else if (current == '[')
                {
                    depth++;
                }
                else if (current == ']' && --depth == 0)
                {
                    return index;
                }
            }
            return -1;
        }
    }

    private const string ProductProject = "Aspose.Cli.Product.";

    /// <summary>The fields of each product's operations, read from its ops schema.</summary>
    private sealed class OperationFields(CliCatalog catalog)
    {
        /// <summary>The products whose operations include <paramref name="op"/>, or only <paramref name="product"/> when it is given.</summary>
        public CliProduct[] Owners(string? product, string op) =>
            [.. catalog.Products.Where(candidate => (product is null || candidate.Id == product) && candidate.Ops.Contains(op, StringComparer.Ordinal))];

        /// <summary>
        /// The field names at <paramref name="path"/> inside the operation, the keys of the objects
        /// that hold it (arrays are transparent); null when the schema leaves that place open, such
        /// as a map, or names no object there.
        /// </summary>
        public HashSet<string>? At(CliProduct product, string op, IReadOnlyList<string> path)
        {
            string id = product.OpsSchemaId!;
            List<(string Id, JsonObject Node)> nodes = [(id, PublishedSchema.Definition(id, op))];
            foreach (string key in path)
            {
                nodes = [.. nodes.SelectMany(static node => Branches(node.Id, node.Node))
                    .Where(node => node.Node["properties"]?[key] is JsonObject)
                    .SelectMany(node => Values(node.Id, (JsonObject)node.Node["properties"]![key]!))];
            }
            HashSet<string> fields = [.. nodes.SelectMany(static node => Branches(node.Id, node.Node))
                .SelectMany(static node => node.Node["properties"] is JsonObject properties ? properties.Select(static pair => pair.Key) : [])];
            return fields.Count == 0 ? null : fields;
        }

        /// <summary>A schema and the branches it combines, references followed.</summary>
        private static IEnumerable<(string Id, JsonObject Node)> Branches(string id, JsonObject node)
        {
            (string resolvedId, JsonObject resolved) = PublishedSchema.Resolve(id, node);
            yield return (resolvedId, resolved);
            foreach (string keyword in new[] { "allOf", "anyOf", "oneOf" })
            {
                if (resolved[keyword] is JsonArray branches)
                {
                    foreach (JsonObject branch in branches.OfType<JsonObject>())
                    {
                        foreach ((string Id, JsonObject Node) nested in Branches(resolvedId, branch))
                        {
                            yield return nested;
                        }
                    }
                }
            }
        }

        /// <summary>The schemas of the objects a value holds: itself, or the items of an array, at any depth.</summary>
        private static IEnumerable<(string Id, JsonObject Node)> Values(string id, JsonObject node)
        {
            foreach ((string branchId, JsonObject branch) in Branches(id, node))
            {
                yield return (branchId, branch);
                if (branch["items"] is JsonObject items)
                {
                    foreach ((string Id, JsonObject Node) nested in Values(branchId, items))
                    {
                        yield return nested;
                    }
                }
            }
        }
    }

    /// <summary>
    /// The operations a text writes as JSON and the problems of their names: an <c>op</c> that is
    /// no operation of the product, and a key that is no field of the operation at its place.
    /// </summary>
    private static IEnumerable<string> CheckOperationJson(string text, string? product, OperationFields fields, MistakenNames mistaken, HashSet<string> kinds)
    {
        foreach (WrittenObject operation in WrittenJson.Operations(text))
        {
            string op = operation.Op!;
            if (op.Contains(Hole, StringComparison.Ordinal))
            {
                continue;
            }
            kinds.Add("operation json");
            CliProduct[] owners = fields.Owners(product, op);
            if (owners.Length == 0)
            {
                // A snake_case name that no product has, or that this product declares mistaken, is
                // reported as an operation name already; the JSON adds the names of one word and
                // another product's operations.
                string? meant = mistaken.Operation(product, op);
                if (!LowerSnake().IsMatch(op) || (fields.Owners(null, op).Length > 0 && meant is null))
                {
                    string owner = product is null ? "any product" : product;
                    yield return meant is null
                        ? $"'{op}' is no operation of {owner}"
                        : $"'{op}' is no operation of {owner}; it is a mistaken name of '{meant}'";
                }
                continue;
            }
            foreach (string problem in CheckMembers(operation, [], owners, op, fields, mistaken, kinds))
            {
                yield return problem;
            }
        }
    }

    private static IEnumerable<string> CheckMembers(
        WrittenObject written, List<string> path, CliProduct[] owners, string op, OperationFields fields, MistakenNames mistaken, HashSet<string> kinds)
    {
        HashSet<string>?[] known = [.. owners.Select(owner => fields.At(owner, op, path))];
        foreach (WrittenMember member in written.Members)
        {
            if ((path.Count == 0 && member.Key == "op") || member.Key.Contains(Hole, StringComparison.Ordinal))
            {
                continue;
            }
            if (known.Any(static names => names is null))
            {
                // The schema leaves this place open, so its keys are values.
                yield break;
            }
            kinds.Add("field");
            string place = string.Join('.', path.Append(member.Key));
            if (!known.Any(names => names!.Contains(member.Key)))
            {
                string[] meant = [.. owners.SelectMany(owner => mistaken.Fields(owner.Id, member.Key))
                    .Where(name => known.Any(names => names!.Contains(name)))
                    .Distinct(StringComparer.Ordinal)];
                yield return meant.Length > 0
                    ? $"'{op}' has no field '{place}'; it is a mistaken name of '{string.Join("' or '", meant)}'"
                    : $"'{op}' has no field '{place}'";
                continue;
            }
            foreach (WrittenObject child in member.Objects)
            {
                foreach (string problem in CheckMembers(child, [.. path, member.Key], owners, op, fields, mistaken, kinds))
                {
                    yield return problem;
                }
            }
        }
    }

    /// <summary>One JSON object a text writes: its members, and its <c>op</c> when it names one.</summary>
    private sealed record WrittenObject(List<WrittenMember> Members)
    {
        public string? Op => Members.FirstOrDefault(static member => member.Key == "op")?.Value;
    }

    /// <summary>One member of a written object: its key, its string value, and the objects its value holds at any depth of arrays.</summary>
    private sealed record WrittenMember(string Key, string? Value, List<WrittenObject> Objects);

    /// <summary>
    /// A tolerant reading of the JSON a text writes inside prose, a hint or an example: an object
    /// is read where a <c>{</c> starts one with string keys; a value it cannot read, such as a
    /// hole (<c>…</c>) or a placeholder, is skipped to the next <c>,</c> or closing bracket, and a
    /// <c>{</c> that starts no object (<c>{name}</c>) is not JSON. A member written as an ellipsis
    /// (<c>...</c>), or one without a string key after another member, is skipped alone.
    /// </summary>
    private sealed class WrittenJson(string text)
    {
        private int _index;

        /// <summary>The objects with an <c>op</c> string, at any depth, that the text writes.</summary>
        public static IEnumerable<WrittenObject> Operations(string text)
        {
            var reader = new WrittenJson(text);
            for (int start = 0; start < text.Length; start++)
            {
                if (text[start] != '{')
                {
                    continue;
                }
                reader._index = start;
                if (reader.Object() is { } found)
                {
                    foreach (WrittenObject operation in WithOps(found))
                    {
                        yield return operation;
                    }
                    start = reader._index - 1;
                }
            }
        }

        private static IEnumerable<WrittenObject> WithOps(WrittenObject written)
        {
            if (written.Op is not null)
            {
                yield return written;
                yield break;
            }
            foreach (WrittenObject nested in written.Members.SelectMany(static member => member.Objects).SelectMany(WithOps))
            {
                yield return nested;
            }
        }

        private WrittenObject? Object()
        {
            _index++;
            var members = new List<WrittenMember>();
            while (true)
            {
                Space();
                if (_index >= text.Length)
                {
                    return null;
                }
                if (text[_index] == '}')
                {
                    _index++;
                    return members.Count == 0 ? null : new WrittenObject(members);
                }
                if (text[_index] != '"')
                {
                    // An elided member ("..." or "…"), and after a member was read anything else that
                    // is no string key, is skipped alone; the members around it are still read. A
                    // brace whose first member is neither starts no object ({name}).
                    bool elided = string.CompareOrdinal(text, _index, "...", 0, 3) == 0 || text[_index] == '…';
                    if (members.Count == 0 && !elided)
                    {
                        return null;
                    }
                    int before = _index;
                    Skip();
                    if (_index < text.Length && text[_index] == ',')
                    {
                        _index++;
                    }
                    if (_index == before)
                    {
                        return null;
                    }
                    continue;
                }
                if (String() is not { } key)
                {
                    return null;
                }
                Space();
                if (_index >= text.Length || text[_index] != ':')
                {
                    return null;
                }
                _index++;
                var objects = new List<WrittenObject>();
                string? value = Value(objects);
                members.Add(new WrittenMember(key, value, objects));
                Space();
                if (_index < text.Length && text[_index] == ',')
                {
                    _index++;
                }
            }
        }

        /// <summary>Reads one value, adding the objects it holds; returns it when it is a string.</summary>
        private string? Value(List<WrittenObject> objects)
        {
            Space();
            if (_index >= text.Length)
            {
                return null;
            }
            switch (text[_index])
            {
                case '"':
                    return String();
                case '{':
                    int start = _index;
                    if (Object() is { } nested)
                    {
                        objects.Add(nested);
                    }
                    else
                    {
                        _index = start;
                        Skip();
                    }
                    return null;
                case '[':
                    _index++;
                    while (true)
                    {
                        Space();
                        if (_index >= text.Length || text[_index] == '}')
                        {
                            return null;
                        }
                        if (text[_index] == ']')
                        {
                            _index++;
                            return null;
                        }
                        if (text[_index] == ',')
                        {
                            _index++;
                            continue;
                        }
                        int before = _index;
                        _ = Value(objects);
                        if (_index == before)
                        {
                            _index++;
                        }
                    }
                default:
                    Skip();
                    return null;
            }
        }

        /// <summary>Skips an unreadable value to the next separator or closing bracket of this level.</summary>
        private void Skip()
        {
            int depth = 0;
            while (_index < text.Length)
            {
                char current = text[_index];
                if (current is '{' or '[')
                {
                    depth++;
                }
                else if (current is '}' or ']' || (current == ',' && depth == 0))
                {
                    if (depth == 0)
                    {
                        return;
                    }
                    depth--;
                }
                _index++;
            }
        }

        private string? String()
        {
            int start = ++_index;
            while (_index < text.Length && text[_index] != '"')
            {
                if (text[_index] == '\n')
                {
                    return null;
                }
                _index += text[_index] == '\\' ? 2 : 1;
            }
            if (_index >= text.Length)
            {
                return null;
            }
            return text[start.._index++];
        }

        private void Space()
        {
            while (_index < text.Length && char.IsWhiteSpace(text[_index]))
            {
                _index++;
            }
        }
    }

    /// <summary>A <c>MistakenFor(...)</c> attribute inside an attribute list, first or after another, and its name literals.</summary>
    [GeneratedRegex(@"(?<=^|,)\s*MistakenFor\((?<names>[^)]*)\)")]
    private static partial Regex MistakenAttribute();

    /// <summary>A <c>MistakenFor(</c> attribute in a source: in an attribute list, after its <c>[</c> or the attributes before it.</summary>
    [GeneratedRegex(@"\[\s*(?:[\w.]+(?:\([^()]*\))?\s*,\s*)*MistakenFor\(")]
    private static partial Regex MistakenAttributeInSource();

    /// <summary>A <c>MistakenFor = [...]</c> property initializer or a <c>mistakenFor: [...]</c> argument of an edit definition, and its name literals.</summary>
    [GeneratedRegex(@"(?<![\w.])(?:MistakenFor\s*=|(?<edit>mistakenFor\s*:))\s*\[(?<names>[^\]]*)\]")]
    private static partial Regex MistakenInitializer();

    /// <summary>The creation of an edit command definition, <c>EditDefinition.Create&lt;...&gt;(</c>.</summary>
    [GeneratedRegex(@"\bEditDefinition\.Create<")]
    private static partial Regex EditCreation();

    /// <summary>The creation of a command definition, <c>new(</c> or <c>new CommandDefinition&lt;...&gt;(</c>, and the command name it starts with.</summary>
    [GeneratedRegex(@"\bnew(?:\s+\w+(?:<[^>]*>)?)?\(\s*""(?<name>[a-z][a-z0-9-]*)""")]
    private static partial Regex DefinitionCreation();

    [GeneratedRegex(@"(?<![\w.])Operation\(\s*""(?<name>[^""]+)""\s*\)")]
    private static partial Regex OperationAttribute();

    [GeneratedRegex(@"(?<![\w.])JsonPropertyName\(\s*""(?<name>[^""]+)""\s*\)")]
    private static partial Regex JsonNameAttribute();

    [GeneratedRegex(@"""(?<text>(?:[^""\\]|\\.)*)""")]
    private static partial Regex Literal();

    [GeneratedRegex(@"\b(?:record|class|struct|interface)\s+[A-Z]\w*")]
    private static partial Regex TypeDeclaration();

    /// <summary>The name of a property declaration: the identifier before its accessors, initializer or arrow.</summary>
    [GeneratedRegex(@"(?<name>[A-Za-z_]\w*)\s*(?:\{|=>|=(?!=)|;)")]
    private static partial Regex PropertyName();
}
