using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;

namespace Aspose.Cli.Invariants.Tests;

// Result and operation schemas. Each case judges one result schema, or one definition of an ops
// schema, so that a contract change to one command or operation clears its own cases.
internal static partial class ContractRules
{
    private static readonly Regex KebabValue = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant);
    private static readonly Regex CamelCaseName = new("^[a-z][a-zA-Z0-9]*$", RegexOptions.CultureInvariant);
    private static readonly Regex NameWords = new("[A-Z]?[a-z0-9]+|[A-Z]+(?![a-z])", RegexOptions.CultureInvariant);

    /// <summary>
    /// The op verbs of C-R19, in the order the rule lists them: an op name is one of them, an
    /// underscore and a noun.
    /// </summary>
    private static readonly string[] OpVerbs =
    [
        "insert", "add", "set", "import", "copy", "move", "rename", "sort", "resize", "format", "define", "refresh",
        "delete", "clear", "replace", "redact", "protect", "unprotect", "encrypt", "decrypt", "merge", "unmerge",
        "group", "ungroup", "freeze", "unfreeze", "accept", "reject", "flatten", "optimize", "rotate", "crop",
        "repeat", "mail_merge",
    ];

    /// <summary>Domain verbs of the table that name a whole op on their own, without a further noun.</summary>
    private static readonly HashSet<string> WholeOpVerbs = new(StringComparer.Ordinal) { "mail_merge" };

    private static readonly Regex OpName = new(
        "^(" + string.Join("|", OpVerbs) + ")_[a-z0-9_]+$", RegexOptions.CultureInvariant);

    /// <summary>
    /// The integer fields C-R18 reads as a document position, ordinal or level, which count from 1:
    /// pages, slides, blocks, sections, rows and table columns, a sheet's or chart's position or
    /// index, outline and list levels, the 1-based anchors and range ends that address one of them,
    /// the nth match, revision numbers, and the pages or slides a result lists. <c>start</c>
    /// is left out on purpose: it is a character offset (slides search) or the first page number
    /// (page numbering), which the rule excludes.
    /// </summary>
    private static readonly HashSet<string> OneBasedNames = new(StringComparer.Ordinal)
    {
        "page", "pages", "slide", "slides", "block", "blocks", "section", "sections", "row", "rows", "col", "column",
        "position", "index", "indexes", "level", "listLevel", "headingLevel", "maxLevel", "startPage", "startSlide",
        "at", "after", "from", "to", "first", "last", "nth", "occurrence", "revision", "revisions",
        "pagesTouched", "slidesTouched", "scannedPagesSuspected",
    };

    /// <summary>The abbreviations C-R20 bans as a word of a field name, and the abbreviated names it bans whole.</summary>
    private static readonly HashSet<string> AbbreviatedWords = new(StringComparer.Ordinal) { "col", "cols", "bg" };
    private static readonly HashSet<string> AbbreviatedNames = new(StringComparer.Ordinal) { "hAlign", "vAlign" };

    /// <summary>
    /// C-R20's fixed names: a font size is <c>fontSize</c>, a page size <c>pageSize</c> (a slide
    /// size <c>slideSize</c>, as the slides option names it), a fill <c>fillColor</c>. A <c>size</c>
    /// beside font fields is a font size; the page-size fields and the fill names are listed here.
    /// </summary>
    private static readonly HashSet<string> FontFieldNames = new(StringComparer.Ordinal) { "font", "latinFont", "eastAsianFont", "bold", "italic" };

    private static readonly Dictionary<(string Schema, string Definition, string Field), string> PageSizeFields = new()
    {
        [("v2/cells/ops", "set_page_setup", "paperSize")] = "pageSize",
        [("v2/words/ops", "wordsPageSetupInput", "size")] = "pageSize",
    };

    private static readonly HashSet<string> FillNames = new(StringComparer.Ordinal) { "bg", "bgColor", "background", "backgroundColor", "fill", "fillColour" };

    /// <summary>One-letter names that are not abbreviations: coordinates.</summary>
    private static readonly HashSet<string> CoordinateNames = new(StringComparer.Ordinal) { "x", "y" };

    /// <summary>
    /// Kept on purpose: the compact cell of a cells read, and the
    /// cell sides that edit and compare report in the same shape, name the value, type and formula
    /// <c>v</c>, <c>t</c> and <c>f</c>, because a read returns thousands of them and the short names
    /// keep the payload small. C-R20 does not count them as abbreviations, and C-R32 accepts the
    /// cell matrix of the sheet projection as the units of a cells read.
    /// </summary>
    private static readonly HashSet<string> CompactCellNames = new(StringComparer.Ordinal) { "v", "t", "f" };
    private const string CompactCellSchemaPrefix = "v2/cells/";
    private const string CellsReadSchema = "v2/cells/workbook-read";

    /// <summary>Field names whose products may give them different types (C-R20): each follows its product's address or style model.</summary>
    private static readonly HashSet<string> ProductModelNames = new(StringComparer.Ordinal) { "at", "target", "style", "column" };

    /// <summary>
    /// C-R33 exceptions: enums where <c>none</c> is a real choice an agent makes, not the absence of
    /// a value, by schema id and path.
    /// </summary>
    private static readonly Dictionary<(string Schema, string Path), string> RealNoneChoices = new()
    {
        [("v2/slides/ops", "set_transition.kind")] = "a transition of kind none removes the slide's transition; the rule names it",
        [("v2/slides/read-result", "slidesTransitionData.kind")] = "reads back the transition kind none that set_transition writes",
        [("v2/pdf/ops", "pdfPageLabelRange.numberStyle")] = "a page-label range of style none labels its pages with the prefix alone, a numbering style of its own",
        [("v2/pdf/inspect-result", "pdfPageLabelInfo.numberStyle")] = "reads back the page-label style none that set_page_labels writes",
    };

    /// <summary>C-R33: boolean names that say "contains" or a state without has&lt;Noun&gt; or an adjective.</summary>
    private static readonly Regex BannedBooleanName = new("^is[A-Z]|.(Present|Exists)$", RegexOptions.CultureInvariant);

    /// <summary>
    /// C-R33: one fact, one boolean name across products. Each entry maps a name a schema must not
    /// use to the name the other products give the same fact.
    /// </summary>
    private static readonly Dictionary<string, string> BooleanSynonyms = new(StringComparer.Ordinal)
    {
        ["hasVba"] = "hasMacros",
    };

    /// <summary>C-R33: names of timestamps, which are ISO-8601 strings with <c>format: date-time</c>.</summary>
    private static readonly Regex DateName = new("^(date|time|timestamp)$|[a-z](Date|Time|Utc|At|Timestamp)$", RegexOptions.CultureInvariant);

    private static readonly string[] FileCore = ["path", "format", "sizeBytes", "fingerprint"];

    private const string OperationOutcome = "v2/common/operation-outcome";
    private const string ErrorEnvelope = "v2/common/error";

    /// <summary>The license mode block a result carries only when it ran unlicensed (C-R33).</summary>
    private const string LicenseMode = "v2/common/license";

    private static IEnumerable<InvariantCase> R17Schemas(CliCatalog catalog)
    {
        // Format ids and check codes keep their own spelling (C-R17's exceptions).
        HashSet<string> exempt = new(
            catalog.Products.SelectMany(static product => product.LoadFormats.Concat(product.ConvertFormats).Concat(product.RenderFormats))
                .Concat(catalog.Diagnostics.Keys),
            StringComparer.Ordinal);
        foreach (SchemaSubject subject in PublishedSchema.Subjects(catalog))
        {
            yield return Check("C-R17", subject.Name, () =>
                from node in subject.Nodes()
                let values = PublishedSchema.Enum(node.Node)
                where values is not null
                let bad = values.Where(value => !KebabValue.IsMatch(value) && !exempt.Contains(value)).ToArray()
                where bad.Length > 0
                select $"{Located(node.Path)}: enum values {string.Join(", ", bad)} are not lowercase kebab-case");
        }
    }

    private static IEnumerable<InvariantCase> R18(CliCatalog catalog)
    {
        foreach (SchemaSubject subject in PublishedSchema.Subjects(catalog))
        {
            yield return Check("C-R18", subject.Name, () =>
                subject.Properties().Where(static property => OneBasedNames.Contains(property.Name)).SelectMany(OneBased));
        }

        static IEnumerable<string> OneBased(SchemaProperty property)
        {
            string[] types = PublishedSchema.Types(property.Schema);
            if (types.Contains("integer"))
            {
                return Minimum(property.Path, property.Schema);
            }
            return types.Contains("array") && property.Schema["items"] is JsonObject items && PublishedSchema.Types(items).Contains("integer")
                ? Minimum(property.Path + "[]", items)
                : [];
        }

        static IEnumerable<string> Minimum(string path, JsonObject node)
        {
            string? minimum = node["minimum"]?.ToJsonString();
            if (minimum == "1" || node["exclusiveMinimum"]?.ToJsonString() == "0")
            {
                yield break;
            }
            yield return $"{path}: minimum {minimum ?? "absent"}, not 1";
        }
    }

    private static IEnumerable<InvariantCase> R19Names(CliCatalog catalog)
    {
        foreach (CliProduct product in PublishedSchema.OpsProducts(catalog))
        {
            foreach (string op in product.Ops)
            {
                yield return Check("C-R19", PublishedSchema.OpSubject(product, op).Name, () =>
                    OpName.IsMatch(op) || WholeOpVerbs.Contains(op) ? [] : [$"op {op} is not <verb>_<noun> with a verb of the rule's table"]);
            }
        }
    }

    private static IEnumerable<InvariantCase> R20(CliCatalog catalog)
    {
        foreach (SchemaSubject subject in PublishedSchema.Subjects(catalog))
        {
            foreach (NameAspect aspect in Enum.GetValues<NameAspect>())
            {
                if (PublishedSchema.IsOps(subject.SchemaId) || aspect is NameAspect.Spelling or NameAspect.FixedNames or NameAspect.Variants)
                {
                    yield return Check("C-R20", $"{subject.Name} {aspect.ToString().ToLowerInvariant()}", () =>
                        FieldNames(subject).Where(item => item.Aspect == aspect).Select(static item => item.Message));
                }
            }
        }
        foreach ((string schema, string definition) in MeasuredFields.Keys.Select(static key => (key.Schema, key.Definition)).Distinct())
        {
            yield return Check("C-R20", $"{schema}#{definition} units", () => UnitNames(schema, definition));
        }
        CliProduct[] products = [.. PublishedSchema.OpsProducts(catalog)];
        var shapes = products.ToDictionary(static product => product.Id, FieldShapes, StringComparer.Ordinal);
        foreach (CliProduct product in products)
        {
            foreach (string name in shapes[product.Id].Keys.Order(StringComparer.Ordinal))
            {
                yield return Check("C-R20", $"{product.OpsSchemaId} field {name}", () => ProductFieldTypes(product.OpsSchemaId!, name, shapes[product.Id][name]));
            }
        }
        foreach (string name in shapes.Values.SelectMany(static fields => fields.Keys).Distinct().Order(StringComparer.Ordinal))
        {
            if (!ProductModelNames.Contains(name) && shapes.Values.Count(fields => fields.ContainsKey(name)) > 1)
            {
                yield return Check("C-R20", $"ops field {name} across products", () => CrossProductFieldTypes(shapes, name));
            }
        }
    }

    /// <summary>
    /// C-R20 on the names one subject declares: camelCase and no abbreviation everywhere, no
    /// variant named <c>type</c> in ops or results; in ops also no <c>from</c>/<c>to</c> range,
    /// secrets named <c>*PasswordEnv</c> and a source file's password <c>sourcePasswordEnv</c>.
    /// </summary>
    private static IEnumerable<(NameAspect Aspect, string Message)> FieldNames(SchemaSubject subject)
    {
        bool ops = PublishedSchema.IsOps(subject.SchemaId);
        bool compactCells = subject.SchemaId.StartsWith(CompactCellSchemaPrefix, StringComparison.Ordinal);
        bool compactCellSeen = false;
        foreach (SchemaProperty property in subject.Properties())
        {
            string name = property.Name;
            if (!CamelCaseName.IsMatch(name))
            {
                yield return (NameAspect.Spelling, $"{property.Path}: not camelCase");
            }
            if (compactCells && CompactCellNames.Contains(name))
            {
                compactCellSeen = true;
            }
            else if (AbbreviatedNames.Contains(name)
                || NameWords.Matches(name).Any(word => AbbreviatedWords.Contains(word.Value.ToLowerInvariant()))
                || (name.Length == 1 && !CoordinateNames.Contains(name)))
            {
                yield return (NameAspect.Spelling, $"{property.Path}: abbreviated name");
            }
            if (name == "size" && FontFieldNames.Any(font => property.Owner["properties"]?[font] is not null))
            {
                yield return (NameAspect.FixedNames, $"{property.Path}: a font size is named fontSize");
            }
            if (FillNames.Contains(name) && IsColor(subject.SchemaId, property.Schema))
            {
                yield return (NameAspect.FixedNames, $"{property.Path}: a fill is named fillColor");
            }
            if (subject.Definition is { } owner && property.OwnerPath.Length == 0
                && PageSizeFields.TryGetValue((subject.SchemaId, owner, name), out string? pageSize))
            {
                yield return (NameAspect.FixedNames, $"{property.Path}: a page size is named {pageSize}");
            }
            if (name == "type" && IsVariant(subject.SchemaId, property.Schema))
            {
                yield return (NameAspect.Variants, $"{property.Path}: a variant named type, not kind");
            }
            if (!ops)
            {
                continue;
            }
            if (name == "to" && property.Owner["properties"]?["from"] is not null)
            {
                yield return (NameAspect.Ranges, $"{property.Path}: to ends a from-to range; a range names its ends first and last");
            }
            if (name.Contains("assword", StringComparison.Ordinal) && !name.EndsWith("PasswordEnv", StringComparison.Ordinal) && name != "passwordEnv")
            {
                yield return (NameAspect.Secrets, $"{property.Path}: a secret is named *PasswordEnv");
            }
            if (name == "passwordEnv" && property.Owner["properties"]?["path"] is not null)
            {
                yield return (NameAspect.Secrets, $"{property.Path}: the source file's password is named sourcePasswordEnv");
            }
        }
        if (subject.Definition is { } definition)
        {
            JsonNode? declared = PublishedSchema.Definition(subject.SchemaId, definition)["properties"];
            foreach ((string schema, string owner, string field) in PageSizeFields.Keys)
            {
                if (schema == subject.SchemaId && owner == definition && declared?[field] is null)
                {
                    yield return (NameAspect.FixedNames, $"{field}: listed as a page size but no longer declared; update the list");
                }
            }
        }
        if (subject.SchemaId == CellsReadSchema && !compactCellSeen)
        {
            yield return (NameAspect.Spelling, "the compact-cell exemption (v, t, f) no longer matches; remove it");
        }
    }

    /// <summary>The parts of C-R20's name rules, one case each, since each is a fix of its own.</summary>
    private enum NameAspect
    {
        /// <summary>camelCase and no abbreviation.</summary>
        Spelling,

        /// <summary>fontSize, pageSize and fillColor.</summary>
        FixedNames,

        /// <summary>A variant is named kind, not type.</summary>
        Variants,

        /// <summary>No from-to range in ops.</summary>
        Ranges,

        /// <summary>Secrets named *PasswordEnv, a source file's sourcePasswordEnv.</summary>
        Secrets,
    }

    /// <summary>Whether a field holds a color: a string, or a reference to a color definition.</summary>
    private static bool IsColor(string schemaId, JsonObject schema) =>
        schema["$ref"]?.GetValue<string>() is { } reference
            ? reference.EndsWith("/color", StringComparison.Ordinal)
            : PublishedSchema.Types(schema).SequenceEqual(["string"]);

    /// <summary>Whether a field holds one of a fixed set of variants: an enum or constants.</summary>
    private static bool IsVariant(string schemaId, JsonObject schema) =>
        PublishedSchema.Shape(schemaId, schema, values: false).Split(" | ").All(static shape => shape == "enum");

    /// <summary>
    /// The types each field name of a product's ops takes, with the definitions that give it each;
    /// a constraint that repeats a field inside <c>allOf</c>, <c>if</c> and the like declares nothing.
    /// </summary>
    private static Dictionary<string, SortedDictionary<string, List<string>>> FieldShapes(CliProduct product)
    {
        string id = product.OpsSchemaId!;
        var shapes = new Dictionary<string, SortedDictionary<string, List<string>>>(StringComparer.Ordinal);
        foreach (string definition in PublishedSchema.Definitions(id))
        {
            foreach (SchemaProperty property in new SchemaSubject(id, definition).Properties())
            {
                if (property.Name is "id" or "op" || ConstraintPath.IsMatch(property.OwnerPath))
                {
                    continue;
                }
                string shape = PublishedSchema.Shape(id, property.Schema, values: false);
                if (!shapes.TryGetValue(property.Name, out SortedDictionary<string, List<string>>? byShape))
                {
                    shapes[property.Name] = byShape = new(StringComparer.Ordinal);
                }
                if (!byShape.TryGetValue(shape, out List<string>? places))
                {
                    byShape[shape] = places = [];
                }
                places.Add(property.OwnerPath.Length == 0 ? definition : $"{definition}.{property.OwnerPath}");
            }
        }
        return shapes;
    }

    private static readonly Regex ConstraintPath = new(@"(anyOf|oneOf|allOf)\[|\.(if|then|else|not)(\.|\[|$)", RegexOptions.CultureInvariant);

    private static IEnumerable<string> ProductFieldTypes(string schema, string name, SortedDictionary<string, List<string>> shapes)
    {
        string[] places = [.. shapes.Values.SelectMany(static owners => owners).Order(StringComparer.Ordinal)];
        if (ShapedByTheirObject.TryGetValue((schema, name), out (string[] Definitions, string Reason) exception))
        {
            if (shapes.Count > 1 && places.SequenceEqual(exception.Definitions.Order(StringComparer.Ordinal), StringComparer.Ordinal))
            {
                return [];
            }

            return [$"the {name} exception for {string.Join(", ", exception.Definitions)} no longer matches ({string.Join(", ", places)}); update or remove it"];
        }

        return shapes.Count < 2
            ? []
            : [.. shapes.Select(static shape => $"{shape.Key} in {string.Join(", ", shape.Value.Take(3))}{(shape.Value.Count > 3 ? $" and {shape.Value.Count - 3} more" : string.Empty)}")];
    }

    /// <summary>
    /// C-R20 exceptions to one type per field name within a product's ops: fields whose shape
    /// follows the object they name, by ops schema and field name, with exactly the definitions
    /// that declare them and why.
    /// </summary>
    private static readonly Dictionary<(string Schema, string Field), (string[] Definitions, string Reason)> ShapedByTheirObject = new()
    {
        [("v2/slides/ops", "values")] = (
            ["insert_table", "slidesChartSeriesInput"],
            "values are the values of the object they name, shaped as that object is: a table's a two-dimensional grid, a chart series' a one-dimensional array"),
    };

    /// <summary>The products whose fields of one name agree among themselves but not with another product's; a product that disagrees with itself has its own case.</summary>
    private static IEnumerable<string> CrossProductFieldTypes(Dictionary<string, Dictionary<string, SortedDictionary<string, List<string>>>> shapes, string name)
    {
        (string Product, string Shape)[] uniform =
        [
            .. shapes.Where(pair => pair.Value.TryGetValue(name, out var fields) && fields.Count == 1)
                .Select(pair => (pair.Key, pair.Value[name].Keys.Single())),
        ];
        return uniform.Select(static item => item.Shape).Distinct().Count() > 1
            ? [.. uniform.Select(static item => $"{item.Product} {item.Shape}")]
            : [];
    }

    private static IEnumerable<InvariantCase> R29(CliCatalog catalog)
    {
        foreach (CliProduct product in PublishedSchema.OpsProducts(catalog))
        {
            CliCommand? edit = catalog.Commands.FirstOrDefault(command => command.Words == $"{product.Id} edit");
            yield return Check("C-R29", $"{product.Id} edit", () => Verification(product.Id, edit));
        }

        static IEnumerable<string> Verification(string product, CliCommand? edit)
        {
            if (edit is null || !edit.Has("--verify"))
            {
                yield return $"{product} edit has no --verify";
            }
            string id = $"v2/{product}/edit-result";
            if (PublishedSchema.Document(id)["properties"]?["verification"] is not JsonObject declared)
            {
                yield return $"{id} declares no verification";
                yield break;
            }
            (_, JsonObject verification) = PublishedSchema.Resolve(id, declared);
            string[] required = PublishedSchema.Required(verification);
            string[] missing = [.. new[] { "ok", "issues", "checkedOps" }.Where(name => !required.Contains(name))];
            if (missing.Length > 0)
            {
                yield return $"{id} verification does not require {string.Join(", ", missing)}";
            }
            if (verification["additionalProperties"]?.ToJsonString() != "false")
            {
                yield return $"{id} verification does not declare its product fields";
            }
        }
    }

    private static IEnumerable<InvariantCase> R30Schema(CliCatalog catalog)
    {
        yield return Check("C-R30", $"{OperationOutcome} index", static () =>
            PublishedSchema.Document(OperationOutcome)["properties"]?["index"] is null
                ? []
                : ["applied[] has index; an outcome names its op by id"]);
        yield return Check("C-R30", $"{OperationOutcome} error", OutcomeError);
        foreach (CliProduct product in PublishedSchema.OpsProducts(catalog))
        {
            string id = $"v2/{product.Id}/edit-result";
            yield return Check("C-R30", $"{id} applied", () =>
                PublishedSchema.Document(id)["properties"]?["applied"]?["items"]?["$ref"]?.GetValue<string>() is { } reference
                    && PublishedSchemas.IdOf(reference) == OperationOutcome
                    ? []
                    : [$"applied[] is not {OperationOutcome}"]);
        }

        static IEnumerable<string> OutcomeError()
        {
            if (PublishedSchema.Document(OperationOutcome)["properties"]?["error"] is not JsonObject error)
            {
                yield return "applied[] declares no error";
                yield break;
            }
            JsonObject payload = PublishedSchema.Resolve(ErrorEnvelope, PublishedSchema.Document(ErrorEnvelope)["properties"]!["error"]!.AsObject()).Node;
            JsonObject resolved = PublishedSchema.Resolve(OperationOutcome, error).Node;
            if (!ReferenceEquals(resolved, payload))
            {
                yield return $"applied[].error defines its own error object instead of referencing {ErrorEnvelope}";
            }
            string[] envelopeRequired = PublishedSchema.Required(payload);
            string[] stricter = [.. PublishedSchema.Required(resolved).Where(name => !envelopeRequired.Contains(name))];
            if (stricter.Length > 0)
            {
                yield return $"applied[].error requires {string.Join(", ", stricter)}, which the error envelope leaves optional";
            }
        }
    }

    private static IEnumerable<InvariantCase> R31(CliCatalog catalog)
    {
        foreach (string id in PublishedSchema.ResultIds(catalog))
        {
            var subject = new SchemaSubject(id, null);
            yield return Check("C-R31", $"{id} names", () => FileObjects(subject, FileAspect.Names));
            yield return Check("C-R31", $"{id} fields", () => FileObjects(subject, FileAspect.Fields));
            yield return Check("C-R31", $"{id} outputs", () => FileObjects(subject, FileAspect.Outputs));
        }
        foreach (InvariantCase sample in FilePathSamples(catalog))
        {
            yield return sample;
        }
    }

    /// <summary>What one C-R31 case judges of a result's file objects, each a fix of its own.</summary>
    private enum FileAspect
    {
        /// <summary>A read file named <c>source</c>.</summary>
        Names,

        /// <summary>The fields of every input, compared side and output.</summary>
        Fields,

        /// <summary>The shape of a multi-file <c>outputs</c> list.</summary>
        Outputs,
    }

    /// <summary>The file objects of one result, as far as <paramref name="aspect"/> judges them.</summary>
    private static IEnumerable<string> FileObjects(SchemaSubject subject, FileAspect aspect)
    {
        bool compare = subject.SchemaId.EndsWith("compare-result", StringComparison.Ordinal);
        foreach (SchemaProperty property in subject.Properties())
        {
            IEnumerable<string> problems = property.Name switch
            {
                "outputs" => aspect == FileAspect.Outputs ? Outputs(subject.SchemaId, property) : [],
                _ when aspect == FileAspect.Outputs => [],
                "source" when IsFileObject(subject.SchemaId, property.Schema) => aspect == FileAspect.Names
                    ? [$"{property.Path}: the read file is named source, not input"]
                    : FileObject(subject.SchemaId, property, producedFile: false),
                _ when aspect == FileAspect.Names => [],
                "input" => FileObject(subject.SchemaId, property, producedFile: false),
                "left" or "right" when compare && IsFileObject(subject.SchemaId, property.Schema) => FileObject(subject.SchemaId, property, producedFile: false),
                "output" => FileObject(subject.SchemaId, property, producedFile: true),
                _ => [],
            };
            foreach (string problem in problems)
            {
                yield return problem;
            }
        }

        static bool IsFileObject(string id, JsonObject schema) =>
            PublishedSchema.Resolve(id, schema).Node["properties"]?["path"] is not null;
    }

    /// <summary>
    /// The differences of one file object from <c>{path, format, sizeBytes, fingerprint, encrypted}</c>,
    /// all required but a produced file's <c>encrypted</c>; a <c>required</c> beside a reference
    /// adds to the referenced schema's.
    /// </summary>
    private static IEnumerable<string> FileObject(string id, SchemaProperty property, bool producedFile)
    {
        JsonObject node = PublishedSchema.Resolve(id, property.Schema).Node;
        if (node["properties"] is not JsonObject properties)
        {
            yield return $"{property.Path}: a {string.Join("/", PublishedSchema.Types(node))}, not the file object";
            yield break;
        }
        string[] expected = [.. FileCore, "encrypted"];
        string[] declared = [.. properties.Select(static pair => pair.Key)];
        HashSet<string> required = [.. PublishedSchema.Required(node), .. PublishedSchema.Required(property.Schema)];
        string[] missing = [.. expected.Where(name => !declared.Contains(name) && !(producedFile && name == "encrypted"))];
        string[] extra = [.. declared.Where(name => !expected.Contains(name))];
        string[] optional = [.. expected.Where(name => declared.Contains(name) && !required.Contains(name) && !(producedFile && name == "encrypted"))];
        if (missing.Length > 0)
        {
            yield return $"{property.Path}: lacks {string.Join(", ", missing)}";
        }
        if (extra.Length > 0)
        {
            yield return $"{property.Path}: has {string.Join(", ", extra)}";
        }
        if (optional.Length > 0)
        {
            yield return $"{property.Path}: {string.Join(", ", optional)} optional";
        }
    }

    /// <summary>The differences of a multi-file list from a flat <c>{&lt;unit&gt;?, path, format, sizeBytes, fingerprint}</c>, all but the unit required.</summary>
    private static IEnumerable<string> Outputs(string id, SchemaProperty property)
    {
        (string listId, JsonObject list) = PublishedSchema.Resolve(id, property.Schema);
        if (list["items"] is not JsonObject declaredItems)
        {
            yield return $"{property.Path}: not a list of file objects";
            yield break;
        }
        (string itemId, JsonObject item) = PublishedSchema.Resolve(listId, declaredItems);
        if (item["properties"] is not JsonObject properties)
        {
            yield return $"{property.Path}[]: a {string.Join("/", PublishedSchema.Types(item))}, not a file object";
            yield break;
        }
        string[] declared = [.. properties.Select(static pair => pair.Key)];
        string[] required = PublishedSchema.Required(item);
        string[] missing = [.. FileCore.Where(name => !declared.Contains(name))];
        string[] optional = [.. FileCore.Where(name => declared.Contains(name) && !required.Contains(name))];
        string[] nested =
        [
            .. properties.Where(pair => !FileCore.Contains(pair.Key) && pair.Value is JsonObject schema
                    && PublishedSchema.Shape(itemId, schema, values: false) is var shape
                    && (shape.StartsWith("object", StringComparison.Ordinal) || shape.StartsWith("array", StringComparison.Ordinal)))
                .Select(static pair => pair.Key),
        ];
        string[] extra = [.. declared.Where(name => !FileCore.Contains(name) && !nested.Contains(name))];
        if (missing.Length > 0)
        {
            yield return $"{property.Path}[]: lacks {string.Join(", ", missing)}";
        }
        if (optional.Length > 0)
        {
            yield return $"{property.Path}[]: {string.Join(", ", optional)} optional";
        }
        if (nested.Length > 0)
        {
            yield return $"{property.Path}[]: not flat ({string.Join(", ", nested)})";
        }
        if (extra.Length > 1)
        {
            yield return $"{property.Path}[]: {string.Join(", ", extra)} beside the file fields, not one unit field";
        }
    }

    private static IEnumerable<InvariantCase> R32(CliCatalog catalog)
    {
        foreach (string id in PublishedSchema.ResultIds(catalog))
        {
            if (id.EndsWith("-read", StringComparison.Ordinal) || id.EndsWith("/read-result", StringComparison.Ordinal))
            {
                yield return Check("C-R32", $"{id} input", () => ReadShape(id, units: false));
                yield return Check("C-R32", $"{id} units", () => ReadShape(id, units: true));
            }
            else if (id.EndsWith("search-result", StringComparison.Ordinal))
            {
                yield return Check("C-R32", $"{id} input", () => SearchShape(id, hits: false));
                yield return Check("C-R32", $"{id} hits", () => SearchShape(id, hits: true));
            }
        }

        static IEnumerable<string> ReadShape(string id, bool units)
        {
            JsonObject root = PublishedSchema.Document(id);
            string[] required = PublishedSchema.Required(root);
            if (!units)
            {
                foreach (string name in new[] { "input", "window" }.Where(name => !required.Contains(name)))
                {
                    yield return $"does not require {name}";
                }
                yield break;
            }
            bool listed = required.Any(name => name.EndsWith("Count", StringComparison.Ordinal)
                && required.Contains(name[..^"Count".Length] + "s"));
            if (id == CellsReadSchema)
            {
                // The kept compact cells: a row-major matrix of v/t/f cells in the sheet projection.
                JsonObject? cells = root["properties"]?["sheet"] is JsonObject sheet
                    ? PublishedSchema.Resolve(id, sheet).Node["properties"]?["cells"] as JsonObject
                    : null;
                if (listed || cells?["items"]?["items"] is null)
                {
                    yield return "the compact-cell exemption (a v/t/f matrix in sheet.cells) no longer matches; remove it";
                }
                yield break;
            }
            if (!listed)
            {
                yield return "does not require a <unit>Count and its <units> list";
            }
        }

        static IEnumerable<string> SearchShape(string id, bool hits)
        {
            JsonObject root = PublishedSchema.Document(id);
            string[] required = PublishedSchema.Required(root);
            if (!hits)
            {
                foreach (string name in new[] { "input", "pattern", "hits", "window" }.Where(name => !required.Contains(name)))
                {
                    yield return $"does not require {name}";
                }
                yield break;
            }
            if (root["properties"]?["hits"]?["items"] is not JsonObject declared)
            {
                yield break;
            }
            JsonObject hit = PublishedSchema.Resolve(id, declared).Node;
            if (!PublishedSchema.Required(hit).Contains("text"))
            {
                string[] instead = [.. PublishedSchema.Required(hit).Where(static name => name is "snippet" or "value" or "match")];
                yield return $"hits[] does not require text{(instead.Length > 0 ? $" (it has {string.Join(", ", instead)})" : string.Empty)}";
            }
        }
    }

    private static IEnumerable<InvariantCase> R33(CliCatalog catalog)
    {
        foreach (SchemaSubject subject in PublishedSchema.Subjects(catalog))
        {
            if (!PublishedSchema.IsOps(subject.SchemaId))
            {
                yield return Check("C-R33", $"{subject.Name} envelope", () => Envelope(subject.SchemaId));
            }
            yield return Check("C-R33", $"{subject.Name} none", () => NoneValues(subject));
            yield return Check("C-R33", $"{subject.Name} names", () => ValueNames(subject));
        }
    }

    /// <summary>C-R33: a result has no top-level <c>kind</c> and does not require the license mode block.</summary>
    private static IEnumerable<string> Envelope(string id)
    {
        JsonObject root = PublishedSchema.Document(id);
        if (root["properties"]?["kind"] is not null)
        {
            yield return "top-level kind";
        }
        if (PublishedSchema.Required(root).Contains("license")
            && root["properties"]?["license"]?["$ref"]?.GetValue<string>() is { } license
            && PublishedSchemas.IdOf(license) == LicenseMode)
        {
            yield return "requires license, which a licensed run omits";
        }
    }

    /// <summary>C-R33: no enum offers <c>none</c> for "no value" but the listed real choices, each of which must still match.</summary>
    private static IEnumerable<string> NoneValues(SchemaSubject subject)
    {
        string[] exceptions =
        [
            .. RealNoneChoices.Keys.Where(key => key.Schema == subject.SchemaId
                    && (subject.Definition is { } definition ? key.Path.StartsWith(definition + ".", StringComparison.Ordinal) : !subject.OpsRoot))
                .Select(static key => key.Path),
        ];
        var matched = new HashSet<string>(StringComparer.Ordinal);
        foreach (SchemaNode node in subject.Nodes())
        {
            if (PublishedSchema.Enum(node.Node)?.Contains("none") != true)
            {
                continue;
            }
            string path = subject.Definition is { } definition ? PublishedSchema.Join(definition, node.Path) : node.Path;
            if (exceptions.Contains(path))
            {
                matched.Add(path);
                continue;
            }
            yield return $"{Located(node.Path)}: \"none\" as a value; omit the field instead";
        }
        foreach (string path in exceptions.Where(path => !matched.Contains(path)))
        {
            yield return $"the none exception for {path} no longer matches; remove it";
        }
    }

    /// <summary>
    /// C-R33: no boolean is named <c>is*</c>, <c>*Present</c> or <c>*Exists</c>, or by a synonym of
    /// another product's name for the same fact; a timestamp is a <c>date-time</c> string.
    /// </summary>
    private static IEnumerable<string> ValueNames(SchemaSubject subject)
    {
        foreach (SchemaProperty property in subject.Properties())
        {
            string[] types = PublishedSchema.Types(property.Schema);
            if (types.Contains("boolean") && BannedBooleanName.IsMatch(property.Name))
            {
                yield return $"{property.Path}: a boolean says \"contains\" as has<Noun> and a state as an adjective";
            }
            if (types.Contains("boolean") && BooleanSynonyms.TryGetValue(property.Name, out string? canonical))
            {
                yield return $"{property.Path}: the other products name this fact {canonical}";
            }
            if (types.Contains("string") && DateName.IsMatch(property.Name) && property.Schema["format"]?.GetValue<string>() != "date-time")
            {
                yield return $"{property.Path}: a timestamp without format date-time";
            }
        }
    }

    private static string Located(string path) => path.Length == 0 ? "the schema" : path;
}
