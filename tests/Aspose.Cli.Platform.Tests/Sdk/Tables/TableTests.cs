using System.Drawing;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Tables;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.Tables;

/// <summary>
/// The object table layer on a table of plain objects: parsing a batch against the table,
/// applying it in order, reading each address back, reading and writing the schema. No engine
/// is involved; the products' tables are tested with their engines.
/// </summary>
public sealed class TableTests
{
    [Fact]
    public void Apply_RunsEachShapeInOrder_AndReadsEveryAddressBack()
    {
        Deck deck = Deck.Sample();
        TableBatch batch = Parse("""
            {"ops":[
              {"set":"deck","props":{"title":"T"}},
              {"set":"card/7","props":{"text":"seven","width":2.5}},
              {"add":"card","args":{"id":9,"index":0},"props":{"text":"nine"}},
              {"add":"line","in":"card/7","args":{"index":1},"props":{"text":"mid"}},
              {"delete":"card/8"},
              {"move":"card/7","to":0},
              {"do":"count"}
            ]}
            """);

        IReadOnlyList<TableEdit.Applied> applied = TableEdit.Apply(DeckTable.Table, deck, batch, Context());
        IReadOnlyList<TableOpReport> reports = TableEdit.ReadBack(DeckTable.Table, deck, applied);

        Assert.Equal(["deck", "card/7", "card/9", "card/7/line/1", "card/8", "card/7", null], reports.Select(static report => report.At));
        Assert.Equal("T", reports[0].Now!["title"]!.GetValue<string>());
        Assert.Equal("seven", reports[1].Now!["text"]!.GetValue<string>());
        Assert.Equal(2.5, reports[1].Now!["width"]!.GetValue<double>());
        Assert.Equal("mid", reports[3].Now!["text"]!.GetValue<string>());
        Assert.False(reports[4].Exists);
        Assert.Null(reports[4].Now);
        Assert.Equal([7u, 9u], deck.Cards.Select(static card => card.Id));
        Assert.Equal("count", reports[6].Do);
        Assert.Null(reports[6].Exists);
        Assert.Equal(2, reports[6].Returned!.GetValue<int>());
    }

    [Fact]
    public void ReadBack_AtAPosition_ReadsWhatTheFinalDocumentHoldsThere()
    {
        // A position names the object that is there after the whole batch: the line this add put
        // at position 2 is at position 1 once the later delete removed the line before it.
        Deck deck = Deck.Sample();
        TableBatch batch = Parse("""
            {"ops":[
              {"add":"line","in":"card/7","args":{"index":2},"props":{"text":"added"}},
              {"delete":"card/7/line/0"}
            ]}
            """);

        IReadOnlyList<TableOpReport> reports = TableEdit.ReadBack(DeckTable.Table, deck, TableEdit.Apply(DeckTable.Table, deck, batch, Context()));

        Assert.Equal("card/7/line/2", reports[0].At);
        Assert.False(reports[0].Exists);
        Assert.Equal("card/7/line/0", reports[1].At);
        Assert.True(reports[1].Exists);
        Assert.Equal("b", reports[1].Now!["text"]!.GetValue<string>());
        Assert.Equal(["b", "added"], deck.Cards[0].Lines.Select(static line => line.Text));
    }

    [Fact]
    public void Apply_ReportsAMissingAddressWithItsPositionAndTheKeysThatExist()
    {
        CliException error = Assert.Throws<CliException>(() => TableEdit.Apply(DeckTable.Table, Deck.Sample(), Parse("""{"ops":[{"set":"deck","props":{"title":"x"}},{"set":"card/5","props":{"text":"x"}}]}"""), Context()));

        Assert.Equal(ErrorCodes.AddressNotFound, error.Code);
        Assert.Equal(1, error.Details!["index"]!.GetValue<int>());
        Assert.Equal("card/5", error.Details["at"]!.GetValue<string>());
        Assert.Equal(["7", "8"], error.Details["available"]!.AsArray().Select(static key => key!.GetValue<string>()));
        Assert.StartsWith("ops[1]: ", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_ReportsWhatTheEngineThrewAsItIs()
    {
        CliException error = Assert.Throws<CliException>(() => TableEdit.Apply(DeckTable.Table, Deck.Sample(), Parse("""{"ops":[{"set":"card/8","props":{"width":-1}}]}"""), Context()));

        Assert.Equal(ErrorCodes.EngineError, error.Code);
        Assert.Equal(0, error.Details!["index"]!.GetValue<int>());
        Assert.Equal("card/8", error.Details["at"]!.GetValue<string>());
        Assert.Equal("width", error.Details["row"]!.GetValue<string>());
        Assert.Equal(typeof(ArgumentOutOfRangeException).FullName, error.Details["engineException"]!.GetValue<string>());
        Assert.Contains("the engine threw System.ArgumentOutOfRangeException", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A document an action opens is an input too: what the document policy left out of it while
    /// it was open is among the warnings the action context gives the edit.
    /// </summary>
    [Fact]
    public void ActionContext_GivesTheWarningsOfTheDocumentsItOpened()
    {
        var context = new TableActionContext<Deck>(new SourceDocuments(), new EngineWarningLog());

        context.Open("source.deck", password: null);

        Assert.Equal([WarningCodes.RemoteResourcesBlocked], context.Warnings.Select(static warning => warning.Code));
    }

    /// <summary>
    /// Only an exception the engine threw is reported as the engine's: the first frame of the
    /// engine's assembly or of the CLI's own decides, past the runtime's frames.
    /// </summary>
    [Fact]
    public void FromEngine_IsDecidedByTheFirstFrameOfTheEngineOrOfTheCli()
    {
        System.Reflection.Assembly engine = typeof(JsonDocument).Assembly;
        Exception engineThrew = Assert.ThrowsAny<JsonException>(static () => JsonDocument.Parse("{"));
        Exception cliThrew = Assert.Throws<ArgumentException>(static () => TableSchema.Of(string.Empty, DeckTable.Table));

        Assert.True(TableErrors.FromEngine(engineThrew, engine));
        Assert.False(TableErrors.FromEngine(cliThrew, engine));
    }

    /// <summary>What the CLI's own code throws during an operation is no engine error: it passes as it was thrown.</summary>
    [Fact]
    public void Apply_PassesWhatTheCliItselfThrew()
    {
        ArgumentException thrown = Assert.Throws<ArgumentException>(() => TableEdit.Apply(DeckTable.Table, Deck.Sample(), Parse("""{"ops":[{"do":"schema"}]}"""), Context()));

        Assert.IsNotType<CliException>(thrown);
        Assert.Contains("TableSchema.Of", thrown.StackTrace, StringComparison.Ordinal);
    }

    /// <summary>
    /// An add whose parent is of the right kind but not of the engine type the add needs is
    /// OPS_INVALID, naming the adds that object takes; on an object of that type it adds.
    /// </summary>
    [Fact]
    public void Apply_RefusesAnAddOnAParentOfAnotherEngineType()
    {
        Deck deck = Deck.Sample();
        deck.Cards.Add(new RuledCard { Id = 9 });

        CliException refused = Assert.Throws<CliException>(() => TableEdit.Apply(DeckTable.Table, deck, Parse("""{"ops":[{"add":"rule","in":"card/7"}]}"""), Context()));
        var applied = TableEdit.Apply(DeckTable.Table, deck, Parse("""{"ops":[{"add":"rule","in":"card/9"}]}"""), Context());

        Assert.Equal(ErrorCodes.OpsInvalid, refused.Code);
        Assert.Equal(("in", "card/7"), (refused.Details!["field"]!.GetValue<string>(), refused.Details["at"]!.GetValue<string>()));
        Assert.Equal(["line"], refused.Details["available"]!.AsArray().Select(static add => add!.GetValue<string>()));
        Assert.Equal("card/9/line/0", Assert.Single(applied).At);
    }

    [Theory]
    [InlineData("""{"ops":[{"set":"card/7","props":{"colour":"x"}}]}""", "unknown-field", "props.colour")]
    [InlineData("""{"ops":[{"set":"card/7","props":{"id":3}}]}""", "read-only-field", "props.id")]
    [InlineData("""{"ops":[{"set":"card/7","delete":"card/8"}]}""", "invalid-value", "delete")]
    [InlineData("""{"ops":[{"add":"box","args":{}}]}""", "unknown-op", "add")]
    [InlineData("""{"ops":[{"add":"line","args":{"index":0}}]}""", "missing-field", "in")]
    [InlineData("""{"ops":[{"add":"line","in":"deck","args":{"index":0}}]}""", "invalid-value", "in")]
    [InlineData("""{"ops":[{"add":"card","args":{"id":1}}]}""", "missing-field", "args.index")]
    [InlineData("""{"ops":[{"set":"card/7","props":{"width":"wide"}}]}""", "invalid-value", "props.width")]
    [InlineData("""{"ops":[{"set":"card/x","props":{"text":"x"}}]}""", "invalid-value", "set")]
    [InlineData("""{"ops":[{"set":"lines/0","props":{"text":"x"}}]}""", "invalid-value", "set")]
    [InlineData("""{"ops":[{"move":"card/7/line/0","to":1}]}""", "invalid-value", "move")]
    [InlineData("""{"ops":[{"do":"shuffle"}]}""", "unknown-op", "do")]
    public void Parse_RefusesWhatTheTableDoesNotHave_BeforeAnyDocumentIsOpened(string json, string reason, string field)
    {
        CliException error = Assert.Throws<CliException>(() => Parse(json));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Equal(0, error.Details!["index"]!.GetValue<int>());
        Assert.Equal(reason, error.Details["reason"]!.GetValue<string>());
        Assert.Equal(field, error.Details["field"]!.GetValue<string>());
    }

    /// <summary>The operation document has one shape; a bare array is refused with the shape stated.</summary>
    [Fact]
    public void Parse_RefusesABareArray_StatingTheDocumentsShape()
    {
        CliException error = Assert.Throws<CliException>(() => Parse("""[{"set":"card/7","props":{"text":"x"}}]"""));

        Assert.Equal("wrong-type", error.Details!["reason"]!.GetValue<string>());
        Assert.Equal("""The operation document is invalid: the ops document is an object with one member "ops"; this one is an array""", error.Message);
    }

    [Fact]
    public void Parse_ListsEveryInvalidOperation()
    {
        CliException error = Assert.Throws<CliException>(() => Parse("""{"ops":[{"set":"card/7","props":{"x":1}},{"do":"count"},{"delete":"deck"}]}"""));

        Assert.Equal(2, error.Details!["errors"]!.AsArray().Count);
        Assert.Equal([0, 2], error.Details["errors"]!.AsArray().Select(static item => item!["index"]!.GetValue<int>()));
    }

    [Fact]
    public void Parse_ResolvesFilesAndSecretsThroughTheInputs()
    {
        var inputs = new Inputs { Secrets = { ["PW"] = "s3cret" } };
        TableBatch batch = TableBatch.Parse("""{"ops":[{"do":"open","args":{"file":"a.bin","password":"PW"}}]}""", DeckTable.Table, inputs);

        Assert.Equal([Path.GetFullPath("a.bin")], batch.Files);
        CliException missing = Assert.Throws<CliException>(() => TableBatch.Parse("""{"ops":[{"do":"open","args":{"file":"a.bin","password":"NONE"}}]}""", DeckTable.Table, inputs));
        Assert.Equal("args.password", missing.Details!["field"]!.GetValue<string>());
        Assert.DoesNotContain("s3cret", missing.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Walk_ListsTheDocumentInOrder_ObjectThenWhatSitsBelowIt()
    {
        string[] addresses = [.. TableRead.Walk(DeckTable.Table, Deck.Sample(), DeckTable.Table.Root, "deck").Select(static found => found.Address)];

        Assert.Equal(["deck", "card/7", "card/7/line/0", "card/7/line/1", "card/8"], addresses);
    }

    [Fact]
    public void Inventory_ListsACountUnderReadExactlyWhenTheTablePlacesItsKind()
    {
        TableInventory inventory = TableRead.Inventory(DeckTable.Table, Deck.Sample());

        Assert.Equal(new Dictionary<string, long> { ["cards"] = 2, ["lines"] = 2 }, inventory.Read);
        Assert.Equal(new Dictionary<string, long> { ["stickers"] = 0 }, inventory.NotRead);
        Assert.Equal(new Dictionary<string, IReadOnlyList<long>> { ["cards"] = [2, 3] }, TableEdit.Changed(
            new Dictionary<string, long> { ["cards"] = 2, ["lines"] = 2 },
            new Dictionary<string, long> { ["cards"] = 3, ["lines"] = 2 }));
    }

    [Fact]
    public void Schema_IsWrittenFromTheTable_WithAViewForEachKindAddAndAction()
    {
        TableSchema schema = TableSchema.Of("deck", DeckTable.Table);
        JsonNode document = JsonNode.Parse(schema.Document)!;

        Assert.Equal("v2/deck/ops", schema.Id);
        Assert.Equal(["directory", "deck", "card", "line", "add.card", "add.line", "add.rule", "do.schema", "do.count", "do.open"], schema.Names);
        Assert.True(document["$defs"]!["card"]!["properties"]!["id"]!["readOnly"]!.GetValue<bool>());
        JsonNode directory = JsonNode.Parse(schema.Views["directory"])!;
        Assert.Equal(["card/<id>"], directory["kinds"]!["card"]!["addresses"]!.AsArray().Select(static form => form!.GetValue<string>()));
        Assert.Contains("id", directory["kinds"]!["card"]!["readOnlyRows"]!.AsArray().Select(static row => row!.GetValue<string>()));
        Assert.DoesNotContain("id", directory["kinds"]!["card"]!["rows"]!.AsArray().Select(static row => row!.GetValue<string>()));
        Assert.Equal("card", directory["adds"]!["line"]!["in"]![0]!.GetValue<string>());
        Assert.Equal(["card/<id>"], document["$defs"]!["card"]!["x-addresses"]!.AsArray().Select(static form => form!.GetValue<string>()));
        Assert.All(schema.Views.Values, static view => Assert.NotNull(JsonNode.Parse(view)));
        Assert.Equal(schema.Document, TableSchema.Of("deck", DeckTable.Table).Document);
    }

    [Fact]
    public void ValueKinds_WriteOneFixedForm_AndRefuseAnyOther()
    {
        Assert.Equal("#0A0B0C", ValueKinds.Color.Encode(Color.FromArgb(10, 11, 12))!.GetValue<string>());
        Assert.Equal(Color.FromArgb(255, 171, 205, 239), ValueKinds.Color.Decode(Json("\"#abcdef\"")));
        Assert.Null(ValueKinds.Color.Encode(Color.Empty));
        Assert.Null(ValueKinds.Length.Encode(float.NaN));
        Assert.Equal("Friday", ValueKinds.Enum<DayOfWeek>().Encode(DayOfWeek.Friday)!.GetValue<string>());
        Assert.Throws<TableValueException>(() => ValueKinds.Enum<DayOfWeek>().Decode(Json("\"friday\"")));
        Assert.Throws<TableValueException>(() => ValueKinds.Enum<DayOfWeek>().Decode(Json("\"5\"")));
        Assert.Throws<TableValueException>(() => ValueKinds.Text.Decode(Json("null")));
        Assert.Null(ValueKinds.Optional(ValueKinds.UnsignedInteger).Encode(null));
        Assert.Equal("a\r\nb ", ValueKinds.Text.Encode("a\r\nb ")!.GetValue<string>());
    }

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

    /// <summary>The context of actions that open no other document.</summary>
    private static TableActionContext<Deck> Context() => new(null!, new EngineWarningLog());

    private static TableBatch Parse(string json) => TableBatch.Parse(json, DeckTable.Table, new Inputs());

    private sealed class Inputs : ITableInputs
    {
        public Dictionary<string, string> Secrets { get; } = [];

        public string ResolvePath(string path) => Path.GetFullPath(path);

        public byte[] ReadImage(string path) => [];


        public byte[] ReadFile(string path) => [];

        public Secret? ReadSecret(string variable) => Secrets.TryGetValue(variable, out string? value) ? new Secret(value) : null;
    }

    /// <summary>Opens every document as a sample deck whose policy left a remote resource out.</summary>
    private sealed class SourceDocuments : ITableDocuments<Deck>
    {
        public DocumentTable<Deck> Table => DeckTable.Table;

        public IReadOnlyList<Aspose.Cli.Sdk.Formats.FormatDescriptor> Formats => [];

        public IOpenedDocument<Deck> Open(string path, Secret? password, EngineWarningLog warnings) => new OpenedDeck();

        public IOpenedDocument<Deck> Reopen(string path, Secret? password, EngineWarningLog warnings) => new OpenedDeck();

        public void Save(IOpenedDocument<Deck> document, string path, string format, Aspose.Cli.Sdk.IO.Output.OutputProtection protection, EngineWarningLog warnings) =>
            throw new NotSupportedException();
    }

    private sealed class OpenedDeck : IOpenedDocument<Deck>
    {
        public Deck Document { get; } = Deck.Sample();

        public string FormatId => "deck";

        public IReadOnlyList<Warning> Warnings { get; } = [new Warning(WarningCodes.RemoteResourcesBlocked, "A remote resource was left out.")];

        public Aspose.Cli.Sdk.IO.Input.InputProtection Protection => new(false);

        public void Dispose()
        {
        }
    }

    private sealed class Deck
    {
        public string Title { get; set; } = string.Empty;

        public List<Card> Cards { get; } = [];

        public static Deck Sample() => new()
        {
            Cards =
            {
                new Card { Id = 7, Lines = { new Line { Text = "a" }, new Line { Text = "b" } } },
                new Card { Id = 8 },
            },
        };
    }

    private class Card
    {
        private double _width = 10;

        public uint Id { get; init; }

        public string Text { get; set; } = string.Empty;

        public double Width
        {
            get => _width;
            set => _width = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), "A width is not negative.");
        }

        public List<Line> Lines { get; } = [];
    }

    private sealed class RuledCard : Card;

    private sealed class Line
    {
        public string Text { get; set; } = string.Empty;
    }

    private static class DeckTable
    {
        private static readonly ObjectKind<Deck> DeckKind = new("deck", "The deck.")
        {
            Row.Text<Deck>("title", "Deck.Title", static d => d.Title, static (d, v) => d.Title = v),
        };

        private static readonly ObjectKind<Card> CardKind = new("card", "A card.")
        {
            Row.UnsignedInteger<Card>("id", "Card.Id", static c => c.Id),
            Row.Text<Card>("text", "Card.Text", static c => c.Text, static (c, v) => c.Text = v),
            Row.Number<Card>("width", "Card.Width", static c => c.Width, static (c, v) => c.Width = v),
        };

        private static readonly ObjectKind<Line> LineKind = new("line", "A line of a card.")
        {
            Row.Text<Line>("text", "Line.Text", static l => l.Text, static (l, v) => l.Text = v),
        };

        public static DocumentTable<Deck> Table { get; } = Build();

        private static DocumentTable<Deck> Build()
        {
            var table = new DocumentTable<Deck>(DeckKind);
            table.Place(DeckKind, CardKind, new Locator<Deck, Card>
            {
                Segment = "card",
                Key = [KeyPart.Id],
                Find = static (deck, key) => deck.Cards.FirstOrDefault(card => card.Id == key.Id()),
                List = static deck => deck.Cards,
                KeyOf = static (_, card) => Key.Of(card.Id),
                Delete = static (deck, card) => deck.Cards.Remove(card),
                Move = static (deck, card, to) =>
                {
                    deck.Cards.Remove(card);
                    deck.Cards.Insert(to, card);
                },
            });
            table.Adds(DeckKind, CardKind, new Adder<Deck, Card>
            {
                Name = "card",
                Description = "Inserts a card.",
                Arguments = [Argument.Of("id", ValueKinds.UnsignedInteger, "The id."), Argument.Of("index", ValueKinds.Integer, "The position.")],
                Create = static (deck, a) =>
                {
                    var card = new Card { Id = a.Get<uint>("id") };
                    deck.Cards.Insert(a.Get<int>("index"), card);
                    return card;
                },
            });
            table.Place(CardKind, LineKind, new Locator<Card, Line>
            {
                Segment = "line",
                Key = [KeyPart.Index],
                Find = static (card, key) => key.Index() < card.Lines.Count ? card.Lines[key.Index()] : null,
                List = static card => card.Lines,
                KeyOf = static (card, line) => Key.Of(card.Lines.IndexOf(line)),
                Delete = static (card, line) => card.Lines.Remove(line),
            });
            table.Adds(CardKind, LineKind, new Adder<Card, Line>
            {
                Name = "line",
                Description = "Inserts a line.",
                Arguments = [Argument.Of("index", ValueKinds.Integer, "The position.")],
                Create = static (card, a) =>
                {
                    var line = new Line();
                    card.Lines.Insert(a.Get<int>("index"), line);
                    return line;
                },
            });
            table.Adds(CardKind, LineKind, new Adder<RuledCard, Line>
            {
                Name = "rule",
                Description = "Adds a rule to a ruled card.",
                Create = static (card, _) =>
                {
                    var line = new Line { Text = "---" };
                    card.Lines.Add(line);
                    return line;
                },
            });
            table.Action(new TableAction<Deck>
            {
                Name = "schema",
                Description = "Writes a schema, through the CLI's own code, for a product without an id.",
                Run = static (_, _, _) => new ActionResult(TableSchema.Of(string.Empty, Table).Id),
            });
            table.Action(new TableAction<Deck>
            {
                Name = "count",
                Description = "Counts the cards.",
                Run = static (deck, _, _) => new ActionResult(JsonValue.Create(deck.Cards.Count)),
            });
            table.Action(new TableAction<Deck>
            {
                Name = "open",
                Description = "Opens another file.",
                Arguments = [Argument.Of("file", ValueKinds.File, "The file."), Argument.Of("password", ValueKinds.SecretVariable, "Its password.")],
                Run = static (_, _, _) => new ActionResult(null),
            });
            table.Inventory(
                [new InventoryKind("cards", "The cards.", CardKind), new InventoryKind("lines", "The lines.", LineKind), new InventoryKind("stickers", "The stickers.", Kind: null)],
                static deck => new Dictionary<string, long> { ["cards"] = deck.Cards.Count, ["lines"] = deck.Cards.Sum(static card => (long)card.Lines.Count), ["stickers"] = 0 });
            return table;
        }
    }
}
