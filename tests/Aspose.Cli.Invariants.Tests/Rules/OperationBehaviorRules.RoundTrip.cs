using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit.Scenarios;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>The parts of a C-R39 round trip that an exemption can excuse.</summary>
[Flags]
internal enum RoundTripPart
{
    /// <summary>The state read from the document, written back unchanged, affects nothing.</summary>
    SameValue = 1,

    /// <summary>One state field changed affects its item alone, names it and reads back.</summary>
    OneChange = 2,

    /// <summary>The changed value reads back; a part of <see cref="OneChange"/> a field exemption can excuse alone.</summary>
    ReadBack = 4,
}

/// <summary>Why an operation's round trip, or one field's change, cannot be generated or judged, and which parts that excuses.</summary>
internal sealed record RoundTripExemption(RoundTripPart Parts, string Reason);

/// <summary>
/// One finding of a round trip: a problem of a part, and of a field for a change, or, without a
/// message, the record that the part (or the field's change) ran to its judgment.
/// </summary>
internal sealed record RoundTripProblem(RoundTripPart? Part, string? Field, string? Message);

// C-R39: what a read reports, written back, round-trips. The cases are generated from the operation
// samples and the read paths the ops schemas declare (x-readBack), never from expectations written
// per operation: a sample's edit brings the document to a known state D, and edits of D then judge
// the outcome the SDK runner derives from its snapshots (D7) against what the reads say changed.
internal static partial class ContractRules
{
    /// <summary>
    /// What cannot be generated or judged, by <c>&lt;product&gt; &lt;op&gt;</c> for the parts of a
    /// whole operation or <c>&lt;product&gt; &lt;op&gt; &lt;field&gt;</c> for the change of one
    /// state field, with the reason. An exempted part or field change that holds, or an entry whose
    /// operation or field is gone or has no read path, fails.
    /// </summary>
    internal static IReadOnlyDictionary<string, RoundTripExemption> RoundTripExempt { get; } = new Dictionary<string, RoundTripExemption>(StringComparer.Ordinal)
    {
        ["cells add_comment"] = new(Both, "Adds a note to the cell's comment: applied again, it appends another."),
        ["cells add_conditional_format"] = new(Both, Adds),
        ["cells add_sparkline"] = new(RoundTripPart.OneChange, "A cell holds one sparkline: another group drawn at the same cell is refused."),
        ["cells define_table"] = new(Both, Unique),
        ["cells format_range style.locked"] = new(RoundTripPart.ReadBack, "A read reports locked only when it is false, the default being true, so true reads back as nothing."),
        ["cells set_validation kind"] = new(RoundTripPart.OneChange, "Another kind needs fields of its own, such as a list's items."),
        ["cells group_columns"] = new(RoundTripPart.SameValue, "Grouping the same columns again nests them one outline level deeper."),
        ["cells group_rows"] = new(RoundTripPart.SameValue, "Grouping the same rows again nests them one outline level deeper."),
        ["cells import_sheet"] = new(Both, Unique),
        ["cells insert_chart"] = new(RoundTripPart.SameValue, AddsAgain),
        ["cells insert_image"] = new(Both, Adds),
        ["cells insert_pivot"] = new(Both, Unique),
        ["cells insert_sheet"] = new(Both, Unique),
        ["pdf add_attachment"] = new(RoundTripPart.SameValue, AddsAgain),
        ["pdf add_bookmark"] = new(Both, Adds),
        ["pdf add_hyperlink"] = new(RoundTripPart.SameValue, AddsAgain),
        ["pdf insert_page"] = new(Both, Adds),
        ["pdf set_page_labels ranges.startPage"] = new(RoundTripPart.OneChange, "The fixture has one page, so a range cannot start on another."),
        ["slides insert_chart"] = new(RoundTripPart.SameValue, AddsAgain),
        ["slides insert_image"] = new(RoundTripPart.SameValue, AddsAgain),
        ["slides insert_section"] = new(Both, Unique),
        ["slides insert_shape"] = new(Both, Adds),
        ["slides insert_slide"] = new(Both, Adds),
        ["slides insert_table"] = new(RoundTripPart.SameValue, AddsAgain),
        ["words add_comment"] = new(RoundTripPart.SameValue, AddsAgain),
        ["words define_style basedOn"] = new(RoundTripPart.OneChange, "Names another style, which must exist."),
        ["words define_style font"] = new(RoundTripPart.OneChange, LatinFont),
        ["words format_text font"] = new(RoundTripPart.OneChange, LatinFont),
        ["words insert_break"] = new(Both, Adds),
        ["words insert_field"] = new(Both, Adds),
        ["words insert_hyperlink"] = new(Both, Adds),
        ["words insert_image"] = new(Both, Adds),
        ["words insert_paragraphs"] = new(Both, Adds),
        ["words insert_section"] = new(Both, Adds),
        ["words insert_table"] = new(Both, Adds),
        ["words insert_toc"] = new(Both, Adds),
        ["words set_default_font font"] = new(RoundTripPart.OneChange, LatinFont),
    };

    private const RoundTripPart Both = RoundTripPart.SameValue | RoundTripPart.OneChange;

    private const string Adds = "Adds an object each time: written again, with the values read or one changed, it adds another instead of rewriting the first.";

    private const string Unique = "Adds an object under a name the document holds once: written again, it is refused.";

    private const string AddsAgain = "Adds an object each time: written again with the values read, it adds another; with one value changed, the new object alone differs, as it should.";

    private const string LatinFont = "The sample also writes latinFont, which takes precedence over font for Latin text, so font alone changes nothing.";

    // ----- C-R39: the state a read reports round-trips through the operation that writes it -----

    /// <summary>
    /// Every operation with a state field whose declaration gives a read path, through the first
    /// sample that writes each such field: the sample's edit makes a document D, and the reads of D
    /// locate where each written value reads back. Written back with the values read there, the
    /// operation affects nothing (<see cref="SameValueProblems"/>); with one field changed, for
    /// each field in turn, it affects exactly the items whose reads changed and names them
    /// (<see cref="OneChangeProblems"/>, <see cref="RoundTripAddress"/>, which also reads the product's content), and the new value reads
    /// back (<see cref="ReadsBackAt"/>). A field no sample writes is a problem of its own.
    /// </summary>
    private static IEnumerable<InvariantCase> R39Run(CliCatalog catalog)
    {
        var first = new HashSet<string>(StringComparer.Ordinal);
        foreach (CliProduct product in catalog.Products.Where(static product => ScenarioFixtures.Products.Contains(product.Id)))
        {
            foreach (string op in product.Ops)
            {
                (string Field, string Path)[] fields = [.. ReadPaths(product, op)];
                if (fields.Length == 0)
                {
                    continue;
                }
                string key = $"{product.Id} {op}";
                Dictionary<string, RoundTripExemption> exemptFields = RoundTripExempt
                    .Where(pair => pair.Key.StartsWith(key + " ", StringComparison.Ordinal))
                    .ToDictionary(pair => pair.Key[(key.Length + 1)..], static pair => pair.Value, StringComparer.Ordinal);
                yield return Check("C-R39", $"{key} round trip",
                    () => Excused(RoundTrip(product, op, fields), RoundTripExempt.GetValueOrDefault(key), exemptFields),
                    shard: product.Id, slow: !first.Add(product.Id));
            }
            yield return Check("C-R39", $"{product.Id} round-trip exemptions", () => RoundTripExemptions(product));
        }
    }

    /// <summary>
    /// The problems exemptions leave: those of the parts and field changes they do not excuse, and
    /// one for each excused part or field change that ran to its judgment and holds.
    /// </summary>
    internal static IEnumerable<string> Excused(
        IEnumerable<RoundTripProblem> problems, RoundTripExemption? exemption, IReadOnlyDictionary<string, RoundTripExemption> fields)
    {
        RoundTripProblem[] all = [.. problems];
        bool ExcusedBy(RoundTripProblem problem) =>
            problem.Part is { } part && (Covers(exemption, part)
                || (problem.Field is not null && Covers(fields.GetValueOrDefault(problem.Field), part)));
        foreach (RoundTripProblem problem in all.Where(problem => problem.Message is not null && !ExcusedBy(problem)))
        {
            yield return problem.Message!;
        }
        foreach (RoundTripPart part in Enum.GetValues<RoundTripPart>().Where(part => exemption?.Parts.HasFlag(part) == true))
        {
            if (all.Any(item => item.Part == part && item.Message is null)
                && !all.Any(item => item.Message is not null && item.Part is { } found && Covers(new RoundTripExemption(part, string.Empty), found)))
            {
                yield return $"the exemption of {part} no longer applies: it holds";
            }
        }
        foreach ((string field, RoundTripExemption excuse) in fields)
        {
            bool judged = all.Any(item => item.Part == RoundTripPart.OneChange && item.Field == field && item.Message is null);
            if (judged && !all.Any(item => item.Field == field && item.Message is not null && item.Part is { } found && Covers(excuse, found)))
            {
                yield return $"the exemption of {field} no longer applies: changing it holds";
            }
        }
    }

    /// <summary>Whether an exemption excuses a part; an exemption of a change also excuses what the change reads back.</summary>
    private static bool Covers(RoundTripExemption? excuse, RoundTripPart part) =>
        excuse is not null && (excuse.Parts.HasFlag(part) || (part == RoundTripPart.ReadBack && excuse.Parts.HasFlag(RoundTripPart.OneChange)));

    /// <summary>Every exemption names an operation of the product that still has a read path, and a field exemption one of its state fields.</summary>
    private static IEnumerable<string> RoundTripExemptions(CliProduct product)
    {
        foreach (string key in RoundTripExempt.Keys.Where(key => key.StartsWith(product.Id + " ", StringComparison.Ordinal)))
        {
            string[] parts = key.Split(' ', 3);
            string op = parts[1];
            if (!product.Ops.Contains(op))
            {
                yield return $"the exemption of {op} no longer applies: no such operation";
            }
            else if (!ReadPaths(product, op).Any())
            {
                yield return $"the exemption of {op} no longer applies: it has no state field with a read path";
            }
            else if (parts.Length == 3 && !ReadPaths(product, op).Any(field => field.Field == parts[2] || parts[2].StartsWith(field.Field + ".", StringComparison.Ordinal)))
            {
                yield return $"the exemption of {op} {parts[2]} no longer applies: no such state field with a read path";
            }
        }
    }

    /// <summary>One written value of a sample: where the operation holds it, and the read name that reports it.</summary>
    private sealed record RoundTripValue(string Field, object[] At, JsonNode? Value, string SchemaId, string Read, string OpSchemaId, JsonObject OpSchema);

    /// <summary>The round trips of an operation: each state field through the first sample that writes it.</summary>
    private static IEnumerable<RoundTripProblem> RoundTrip(CliProduct product, string op, (string Field, string Path)[] fields)
    {
        var assigned = new HashSet<string>(StringComparer.Ordinal);
        foreach (OperationSample sample in OperationSamples.All.Where(item => item.Product == product.Id && item.Op == op))
        {
            JsonObject operation = OperationRuns.Operation(sample);
            (string Field, string Path)[] writes = [.. fields.Where(field => !assigned.Contains(field.Field) && Written(operation, field.Field.Split('.')).Any())];
            if (writes.Length == 0)
            {
                continue;
            }
            assigned.UnionWith(writes.Select(static field => field.Field));
            foreach (RoundTripProblem problem in RoundTrip(product, sample, writes))
            {
                yield return problem;
            }
        }
        foreach ((string field, _) in fields.Where(field => !assigned.Contains(field.Field)))
        {
            yield return new(RoundTripPart.OneChange, field, $"{field} is written by no sample, so it does not round-trip");
        }
    }

    private static IEnumerable<RoundTripProblem> RoundTrip(CliProduct product, OperationSample sample, (string Field, string Path)[] fields)
    {
        using var workspace = new RuleWorkspace();
        (string? source, JsonObject operation, string? failure) = OperationRuns.Prepare(workspace, sample);
        if (failure is not null)
        {
            yield return new(null, null, failure);
            yield break;
        }
        if (source is null)
        {
            yield break;
        }
        string extension = OperationRuns.Extension(sample.Product);
        string[] open = OperationRuns.Open(sample);
        string staged = "staged." + extension;
        RuleRun stage = OperationRuns.Edit(workspace, sample.Product, source, staged, [OperationRuns.WithId(operation)], open);
        if (stage.ExitCode != 0)
        {
            if (!stage.Unjudged)
            {
                yield return new(null, null, $"the edit of {sample.Subject} fails: " + stage.Describe());
            }
            yield break;
        }

        // Where each written value reads back in D: the first place that holds it and did not before.
        var reads = new Dictionary<string, (JsonArray Before, JsonArray After)>(StringComparer.Ordinal);
        var located = new List<(RoundTripValue Value, string Location)>();
        foreach (RoundTripValue value in WrittenValues(product, sample, operation, fields))
        {
            if (!reads.TryGetValue(value.SchemaId, out (JsonArray Before, JsonArray After) state))
            {
                state = (OperationRuns.Read(workspace, sample.Product, source, open, value.SchemaId),
                    OperationRuns.Read(workspace, sample.Product, staged, open, value.SchemaId));
                reads[value.SchemaId] = state;
            }
            if (Locate(state.After, value.Read).Where(hit => OperationRuns.Holds(value.Value, hit.Value))
                .FirstOrDefault(hit => IsNew(state.Before, state.After, hit.Location, value.Value)) is { Location: not null } hit)
            {
                located.Add((value, hit.Location));
            }
            else if (!located.Any(item => item.Value.Field == value.Field))
            {
                yield return new(RoundTripPart.OneChange, value.Field,
                    $"{value.Field} {OperationRuns.Show(value.Value)} does not read back as a new {value.Read}, so it does not round-trip");
            }
        }

        // A1: every located value replaced by what the read reports there, in the operation's
        // shape, and an address the sample's edit changed, such as a renamed sheet, by its new value.
        JsonObject same = OperationRuns.WithId(operation);
        foreach ((RoundTripValue value, string location) in located)
        {
            JsonArray after = reads[value.SchemaId].After;
            if (Project(value.Value, At(after, location).Value) is { } read)
            {
                Replace(same, value.At, read.Value);
            }
            if (At(reads[value.SchemaId].Before, location).Value is JsonValue old && old.GetValueKind() == JsonValueKind.String
                && At(after, location).Value is JsonValue renamed && renamed.GetValueKind() == JsonValueKind.String)
            {
                foreach ((string name, JsonNode? address) in operation.Where(pair => pair.Key is not "op" and not "id"
                    && !fields.Any(field => field.Field.Split('.')[0] == pair.Key)).ToArray())
                {
                    if (address is JsonValue text && text.GetValueKind() == JsonValueKind.String && text.GetValue<string>() == old.GetValue<string>())
                    {
                        same[name] = renamed.DeepClone();
                    }
                }
            }
        }
        RuleRun unchanged = OperationRuns.Edit(workspace, sample.Product, staged, "same." + extension, [same], open);
        if (!unchanged.Unjudged)
        {
            yield return new(RoundTripPart.SameValue, null, null);
            foreach (string problem in SameValueProblems(unchanged.ExitCode, unchanged.Document, unchanged.Describe()))
            {
                yield return new(RoundTripPart.SameValue, null, problem);
            }
        }

        // A2: each field in turn, its first located value changed and the rest as read; what
        // changed is judged by the reads of the state fields and the product's content read.
        var judged = reads.ToDictionary(static pair => pair.Key, static pair => pair.Value.After, StringComparer.Ordinal);
        string content = $"v2/{sample.Product}/read-result";
        if (!judged.ContainsKey(content))
        {
            judged[content] = OperationRuns.Read(workspace, sample.Product, staged, open, content);
        }
        int number = 0;
        foreach ((RoundTripValue value, string location) in located.DistinctBy(static item => item.Value.Field))
        {
            if (Changed(value, location, reads[value.SchemaId].Before) is not { } changedValue)
            {
                yield return new(RoundTripPart.OneChange, value.Field, $"no other value can be generated for {value.Field}");
                continue;
            }
            JsonObject changed = (JsonObject)same.DeepClone();
            Replace(changed, value.At, changedValue);
            string output = $"changed-{++number}.{extension}";
            RuleRun edit = OperationRuns.Edit(workspace, sample.Product, staged, output, [changed], open);
            if (edit.Unjudged)
            {
                continue;
            }
            yield return new(RoundTripPart.OneChange, value.Field, null);
            string subject = $"{value.Field} {OperationRuns.Show(value.Value)} -> {OperationRuns.Show(changedValue)}";
            if (edit.ExitCode != 0)
            {
                foreach (string problem in OneChangeProblems(edit.ExitCode, edit.Document, edit.Describe(), subject, []))
                {
                    yield return new(RoundTripPart.OneChange, value.Field, problem);
                }
                continue;
            }
            // The items whose reads changed, by the address a target names them with.
            var items = new SortedSet<string>(StringComparer.Ordinal);
            var unnamed = new List<string>();
            JsonArray? read = null;
            foreach ((string schemaId, JsonArray staging) in judged)
            {
                JsonArray now = OperationRuns.Read(workspace, sample.Product, output, open, schemaId);
                read = schemaId == value.SchemaId ? now : read;
                JsonArray before = ResolveStyles(staging);
                JsonArray after = ResolveStyles(now);
                foreach (string change in Changes(before, after))
                {
                    if (RoundTripAddress(sample.Product, change, after, before) is { } addresses)
                    {
                        items.UnionWith(addresses);
                    }
                    else
                    {
                        unnamed.Add(change);
                    }
                }
            }
            foreach (string problem in OneChangeProblems(edit.ExitCode, edit.Document, edit.Describe(), subject, items))
            {
                yield return new(RoundTripPart.OneChange, value.Field, problem);
            }
            if (unnamed.Count > 0)
            {
                yield return new(RoundTripPart.OneChange, value.Field,
                    $"changing {subject}, no address names the item of {string.Join(", ", unnamed.Take(3))}{(unnamed.Count > 3 ? ", ..." : string.Empty)}");
            }
            if (!ReadsBackAt(reads[value.SchemaId].After, read!, location, value.Read, changedValue))
            {
                (JsonNode? actual, bool exists) = At(read!, location);
                yield return new(RoundTripPart.ReadBack, value.Field,
                    $"changing {subject}, {location} reads {(exists ? OperationRuns.Show(actual) : "nothing")}");
            }
        }
    }

    /// <summary>
    /// Whether a changed value reads back: at its place in D, or under the same read name at a
    /// place that did not hold it in D, since the place may move with the change, as a cell's style
    /// id does. A read leaves out a boolean that is false, so false also reads back as the
    /// property's absence from an object that is there.
    /// </summary>
    internal static bool ReadsBackAt(JsonArray staged, JsonArray read, string location, string name, JsonNode? value)
    {
        (JsonNode? actual, bool exists) = At(read, location);
        if (exists && OperationRuns.Holds(value, actual))
        {
            return true;
        }
        if (Locate(read, name).Any(hit => OperationRuns.Holds(value, hit.Value) && IsNew(staged, read, hit.Location, value)))
        {
            return true;
        }
        return value?.GetValueKind() == JsonValueKind.False && !exists && At(read, ParentOf().Replace(location, string.Empty)).Exists;
    }

    /// <summary>
    /// Every leaf place where two reads differ, as <c>command: path</c>; an item one read lacks is
    /// one place. A read of the active sheet that reads another sheet differs only in its position.
    /// </summary>
    internal static IEnumerable<string> Changes(JsonArray before, JsonArray after)
    {
        Dictionary<string, JsonNode?> old = before.ToDictionary(static item => item!["command"]!.GetValue<string>(), static item => item!["result"], StringComparer.Ordinal);
        foreach (JsonNode? item in after)
        {
            string command = item!["command"]!.GetValue<string>();
            JsonNode? previous = old.GetValueOrDefault(command);
            if (previous?["sheet"]?["position"] is JsonValue was && item["result"]?["sheet"]?["position"] is JsonValue now && !JsonNode.DeepEquals(was, now))
            {
                yield return $"{command}: sheet.position";
                continue;
            }
            foreach (string path in Differences(previous, item["result"], string.Empty))
            {
                yield return $"{command}: {path}";
            }
        }

        static IEnumerable<string> Differences(JsonNode? x, JsonNode? y, string path)
        {
            if (JsonNode.DeepEquals(x, y))
            {
                yield break;
            }
            if (x is JsonObject a && y is JsonObject b)
            {
                foreach (string key in a.Select(static pair => pair.Key).Union(b.Select(static pair => pair.Key)))
                {
                    foreach (string inner in Differences(a[key], b[key], path.Length == 0 ? key : $"{path}.{key}"))
                    {
                        yield return inner;
                    }
                }
            }
            else if (x is JsonArray c && y is JsonArray d)
            {
                for (int index = 0; index < Math.Max(c.Count, d.Count); index++)
                {
                    foreach (string inner in Differences(index < c.Count ? c[index] : null, index < d.Count ? d[index] : null, $"{path}[{index}]"))
                    {
                        yield return inner;
                    }
                }
            }
            else
            {
                yield return path;
            }
        }
    }

    /// <summary>
    /// Judges a write of the state read from the document, unchanged: it succeeds, affects no
    /// item and warns <see cref="NoEffectWarning"/>.
    /// </summary>
    internal static IEnumerable<string> SameValueProblems(int exitCode, JsonNode? document, string failure)
    {
        if (exitCode != 0)
        {
            yield return $"written back unchanged, it fails: {failure}";
            yield break;
        }
        if (document?["applied"]?[0]?["itemsAffected"] is not JsonValue count)
        {
            yield return "written back unchanged, the result has no applied[0].itemsAffected";
            yield break;
        }
        bool noEffect = EffectWarnings(document).Contains(NoEffectWarning, StringComparer.Ordinal);
        if (count.GetValue<int>() != 0)
        {
            // The count is left out: it varies with the license mode, and above 0 is the violation.
            yield return $"written back unchanged, itemsAffected is above 0{(noEffect ? string.Empty : $" and there is no {NoEffectWarning} warning")}";
        }
        else if (!noEffect)
        {
            yield return $"written back unchanged, itemsAffected is 0 without an {NoEffectWarning} warning";
        }
    }

    /// <summary>
    /// Judges a write that changes one state field: it succeeds without
    /// <see cref="NoEffectWarning"/>, some read changes, its targets are exactly the items whose
    /// reads changed, no other and none fewer, and <c>itemsAffected</c> counts them (D7).
    /// </summary>
    internal static IEnumerable<string> OneChangeProblems(int exitCode, JsonNode? document, string failure, string subject, IReadOnlyCollection<string> changedItems)
    {
        if (exitCode != 0)
        {
            yield return $"changing {subject}, it fails: {failure}";
            yield break;
        }
        if (document?["applied"]?[0]?["itemsAffected"] is not JsonValue count)
        {
            yield return $"changing {subject}, the result has no applied[0].itemsAffected";
            yield break;
        }
        if (EffectWarnings(document).Contains(NoEffectWarning, StringComparer.Ordinal))
        {
            yield return $"changing {subject}, it warns {NoEffectWarning}";
        }
        if (changedItems.Count == 0)
        {
            yield return $"changing {subject}, no read changed";
        }
        string[] targets = Targets(document);
        string[] missing = [.. changedItems.Where(item => !targets.Contains(item, StringComparer.Ordinal))];
        string[] extra = [.. targets.Where(target => !changedItems.Contains(target, StringComparer.Ordinal))];
        if (missing.Length > 0)
        {
            yield return $"changing {subject}, targets do not name {string.Join(", ", missing)}, whose read changed";
        }
        if (extra.Length > 0)
        {
            yield return $"changing {subject}, targets name {string.Join(", ", extra)}, whose read did not change";
        }
        int affected = count.GetValue<int>();
        if (affected != changedItems.Count)
        {
            yield return $"changing {subject}, itemsAffected is {affected} where the reads changed {changedItems.Count} items";
        }
    }

    /// <summary>The warnings of a result that speak of the operation's effect; the evaluation warning does not.</summary>
    private static string[] EffectWarnings(JsonNode? document) =>
        [.. ScenarioContract.Codes(document?["warnings"]).Where(static code => code != "EVALUATION_MODE")];

    private static string[] Targets(JsonNode? document) =>
        [.. (document?["applied"]?[0]?["targets"] as JsonArray ?? []).Select(static target => target?.GetValue<string>()).OfType<string>()];

    /// <summary>Every value a sample writes into a state field with a read path, in field order, as <see cref="ReadEntries"/> splits them.</summary>
    private static IEnumerable<RoundTripValue> WrittenValues(CliProduct product, OperationSample sample, JsonObject operation, (string Field, string Path)[] fields)
    {
        Dictionary<string, (string SchemaId, JsonObject Schema)> declared = Declarations(product, sample.Op)
            .ToDictionary(static item => item.Field, static item => (item.SchemaId, item.Schema), StringComparer.Ordinal);
        foreach ((string field, string path) in fields)
        {
            string schemaId = ReadTarget(field, path).SchemaId;
            (string opSchemaId, JsonObject opSchema) = declared[field];
            foreach ((object[] at, JsonNode? written) in WrittenAt(operation, field.Split('.'), []))
            {
                foreach ((string? entry, string read, JsonNode? value) in ReadEntries(field, path, written))
                {
                    if (entry is null)
                    {
                        yield return new RoundTripValue(field, at, value, schemaId, read, opSchemaId, opSchema);
                        continue;
                    }
                    (string entryId, JsonObject entrySchema) = PublishedSchema.Resolve(opSchemaId, opSchema);
                    yield return new RoundTripValue($"{field}.{entry}", [.. at, entry], value, schemaId, read, entryId,
                        entrySchema["additionalProperties"] as JsonObject ?? []);
                }
            }
        }
    }

    /// <summary>
    /// Where a read reports a written value: under the read name of its path, or, for a field of
    /// named entries such as custom properties whose path is a dictionary's additionalProperties,
    /// each entry under its own key in that dictionary, with the entry's key.
    /// </summary>
    private static IEnumerable<(string? Entry, string Read, JsonNode? Value)> ReadEntries(string field, string path, JsonNode? written)
    {
        string[] pointer = path.Split('/');
        if (pointer[^1] == "additionalProperties" && written is JsonObject named)
        {
            foreach ((string key, JsonNode? value) in named)
            {
                yield return (key, $"{pointer[^2]}.{key}", value);
            }
            yield break;
        }
        yield return (null, ReadTarget(field, path).Name, written);
    }

    /// <summary>The values an operation writes at a dotted field path, with the path to each, one per item of a list on the way.</summary>
    private static IEnumerable<(object[] At, JsonNode? Value)> WrittenAt(JsonNode? node, string[] path, object[] at)
    {
        if (path.Length == 0)
        {
            yield return (at, node);
            yield break;
        }
        if (node is JsonArray items)
        {
            for (int index = 0; index < items.Count; index++)
            {
                foreach (var value in WrittenAt(items[index], path, [.. at, index]))
                {
                    yield return value;
                }
            }
        }
        else if (node is JsonObject obj && obj.TryGetPropertyValue(path[0], out JsonNode? next))
        {
            foreach (var value in WrittenAt(next, path[1..], [.. at, path[0]]))
            {
                yield return value;
            }
        }
    }

    private static void Replace(JsonObject operation, object[] at, JsonNode? value)
    {
        JsonNode node = operation;
        foreach (object step in at[..^1])
        {
            node = step is int index ? node[index]! : node[(string)step]!;
        }
        if (at[^1] is int last)
        {
            node.AsArray()[last] = value?.DeepClone();
        }
        else
        {
            node.AsObject()[(string)at[^1]] = value?.DeepClone();
        }
    }

    /// <summary>
    /// A read value in the shape of the written one: the written properties of an object, the
    /// items of an array, a scalar as read; null when the read lacks part of the written shape.
    /// </summary>
    private static (JsonNode? Value, bool Found)? Project(JsonNode? written, JsonNode? read)
    {
        switch (written)
        {
            case JsonObject obj:
                if (read is not JsonObject other)
                {
                    return null;
                }
                var projected = new JsonObject();
                foreach ((string key, JsonNode? value) in obj)
                {
                    if (!other.TryGetPropertyValue(key, out JsonNode? item) || Project(value, item) is not { } inner)
                    {
                        return null;
                    }
                    projected[key] = inner.Value?.DeepClone();
                }
                return (projected, true);
            case JsonArray array:
                if (read is not JsonArray items || items.Count != array.Count)
                {
                    return null;
                }
                var list = new JsonArray();
                for (int index = 0; index < array.Count; index++)
                {
                    if (Project(array[index], items[index]) is not { } inner)
                    {
                        return null;
                    }
                    list.Add(inner.Value?.DeepClone());
                }
                return (list, true);
            default:
                return read is JsonObject or JsonArray ? null : (read?.DeepClone(), true);
        }
    }

    /// <summary>
    /// The other value a change writes: the value its place held before the sample's edit, when
    /// that was another and read whole, else one the op schema allows (<see cref="Vary"/>).
    /// </summary>
    private static JsonNode? Changed(RoundTripValue value, string location, JsonArray before)
    {
        (JsonNode? old, bool existed) = At(before, location);
        // An evaluation read cuts a text short and says so: that is no value to write.
        return existed && old is not null && !OperationRuns.Holds(value.Value, old) && Project(value.Value, old) is { Value: { } previous }
            && !previous.ToJsonString().Contains("evaluation version", StringComparison.Ordinal)
            ? previous
            : Vary(value.Value, value.OpSchemaId, value.OpSchema);
    }

    /// <summary>
    /// Another value the op schema accepts: the next enum value, a flipped boolean, a number one
    /// step within its bounds, another color, a string with its last number one higher or else its
    /// last letter the next of its case, of the same length, which an evaluation run still reads back whole;
    /// the first item or property that can change of an array or object.
    /// </summary>
    private static JsonNode? Vary(JsonNode? value, string schemaId, JsonObject schema, int depth = 0)
    {
        if (depth > 8)
        {
            return null;
        }
        (string id, JsonObject node) = PublishedSchema.Resolve(schemaId, schema);
        // allOf only adds constraints, such as minProperties, to the schema it sits in.
        foreach (string alternatives in new[] { "anyOf", "oneOf" })
        {
            if (node[alternatives] is JsonArray options)
            {
                foreach (JsonObject option in options.OfType<JsonObject>())
                {
                    if (Vary(value, id, option, depth + 1) is { } varied)
                    {
                        return varied;
                    }
                }
                return null;
            }
        }
        if (node["enum"] is JsonArray values)
        {
            JsonNode? next = values.FirstOrDefault(item => item is not null && item.GetValueKind() == value?.GetValueKind() && !JsonNode.DeepEquals(item, value));
            return next?.DeepClone();
        }
        switch (value?.GetValueKind())
        {
            case JsonValueKind.True or JsonValueKind.False:
                return JsonValue.Create(!value.GetValue<bool>());
            case JsonValueKind.Number:
                return Step(value.GetValue<double>(), node);
            case JsonValueKind.String:
                return Retext(value.GetValue<string>(), node) is { } text ? JsonValue.Create(text) : null;
            case JsonValueKind.Array when node["items"] is JsonObject items:
                JsonArray array = value.AsArray();
                for (int index = 0; index < array.Count; index++)
                {
                    if (Vary(array[index], id, items, depth + 1) is { } item)
                    {
                        var copy = (JsonArray)array.DeepClone();
                        copy[index] = item;
                        return copy;
                    }
                }
                return null;
            case JsonValueKind.Object when node["properties"] is JsonObject properties:
                foreach ((string key, JsonNode? property) in value.AsObject())
                {
                    if (properties[key] is JsonObject propertySchema && Vary(property, id, propertySchema, depth + 1) is { } item)
                    {
                        var copy = (JsonObject)value.DeepClone();
                        copy[key] = item;
                        return copy;
                    }
                }
                return null;
            default:
                return null;
        }
    }

    private static JsonNode? Step(double number, JsonObject node)
    {
        double? maximum = node["maximum"]?.GetValue<double>() ?? (node["exclusiveMaximum"]?.GetValue<double>() is { } below ? below - 1 : null);
        double? minimum = node["minimum"]?.GetValue<double>() ?? (node["exclusiveMinimum"]?.GetValue<double>() is { } above ? above + 1 : null);
        foreach (double candidate in new[] { number + 1, number - 1 })
        {
            if ((maximum is null || candidate <= maximum) && (minimum is null || candidate >= minimum))
            {
                // Parsed, as a read value is, so that it compares with read numbers.
                return JsonNode.Parse(candidate.ToString("R", CultureInfo.InvariantCulture));
            }
        }
        return null;
    }

    private static string? Retext(string text, JsonObject node)
    {
        Regex? pattern = node["pattern"]?.GetValue<string>() is { } source ? new Regex(source, RegexOptions.CultureInvariant) : null;
        IEnumerable<string> candidates = HexColor().IsMatch(text)
            ? new[] { "#00B050", "#7030A0" }
            : LastNumber().Match(text) is { Success: true } number
                ? [text[..number.Index] + (long.Parse(number.Value, CultureInfo.InvariantCulture) + 1).ToString(CultureInfo.InvariantCulture) + text[(number.Index + number.Length)..]]
                : [text.Length == 0 ? "x" : text[..^1] + NextLetter(text[^1])];
        return candidates.FirstOrDefault(candidate => !string.Equals(candidate, text, StringComparison.OrdinalIgnoreCase)
            && (pattern is null || pattern.IsMatch(candidate))
            && (node["maxLength"]?.GetValue<int>() is not { } length || candidate.Length <= length));
    }

    /// <summary>The next letter of the same case, so that a column letter stays a column letter; another character becomes x.</summary>
    private static char NextLetter(char last) => last switch
    {
        'Z' => 'A',
        'z' => 'a',
        >= 'A' and < 'Z' or >= 'a' and < 'z' => (char)(last + 1),
        _ => 'x',
    };

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColor();

    [GeneratedRegex(@"\d+(?=\D*$)")]
    private static partial Regex LastNumber();
}
