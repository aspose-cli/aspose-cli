using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Tables;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.Tables;

/// <summary>
/// What the object table layer has for a grid document: keys written as the engine names them
/// (any text, A1 cells, areas, rows and columns), a lookup that can match several objects, kinds a
/// read lists only when --kind names them, and action arguments that are addresses. A table of
/// plain objects, no engine.
/// </summary>
public sealed class TableKeysAndListingTests
{
    [Theory]
    [InlineData("sheet/Sales 2026", true)]
    [InlineData("sheet/销售 Sales/cell/B5", true)]
    [InlineData("sheet/it's!/cell/b5", true)]
    [InlineData("sheet/S/cell/XFD1048576", true)]
    [InlineData("sheet/S/cell/B0", false)]
    [InlineData("sheet/S/cell/5B", false)]
    [InlineData("sheet/S/cell/$B$5", false)]
    [InlineData("sheet/S/area/B2:D10", true)]
    [InlineData("sheet/S/area/B2", true)]
    [InlineData("sheet/S/area/5:5", true)]
    [InlineData("sheet/S/area/C:E", true)]
    [InlineData("sheet/S/area/B2:", false)]
    [InlineData("sheet/S/area/B2:5", false)]
    [InlineData("sheet/S/row/5", true)]
    [InlineData("sheet/S/row/0", false)]
    [InlineData("sheet/S/row/A", false)]
    [InlineData("sheet/S/column/AB", true)]
    [InlineData("sheet/S/column/ABCD", false)]
    [InlineData("sheet/S/column/1", false)]
    public void Parse_ChecksTheFormOfEachKeyPart(string address, bool valid)
    {
        if (valid)
        {
            Assert.Equal(address, TableAddress.Parse(Grid.Table, address).Text);
        }
        else
        {
            Assert.Throws<TableValueException>(() => TableAddress.Parse(Grid.Table, address));
        }
    }

    /// <summary>A key the engine's lookup matches on more than one object is ambiguous, never the first match.</summary>
    [Fact]
    public void Resolve_ReportsAKeyThatMatchesSeveralObjectsAsAmbiguous()
    {
        var book = new Book { Sheets = { new Sheet("Data"), new Sheet("data") } };

        CliException error = Assert.Throws<CliException>(() => AddressResolver.Resolve(Grid.Table, book, TableAddress.Parse(Grid.Table, "sheet/DATA")));

        Assert.Equal(ErrorCodes.AddressAmbiguous, error.Code);
        Assert.Equal(["sheet/Data", "sheet/data"], error.Details!["available"]!.AsArray().Select(static match => match!.GetValue<string>()));
        Assert.Equal("sheet/data", AddressResolver.Resolve(Grid.Table, new Book { Sheets = { new Sheet("data") } }, TableAddress.Parse(Grid.Table, "sheet/DATA")).Address);
    }

    /// <summary>
    /// Without --kind, a read passes over the kinds listed only when named and says which; with
    /// --kind, it lists the kinds named, walking down to them.
    /// </summary>
    [Fact]
    public void Read_ListsAKindListedWhenNamed_OnlyWhenKindNamesIt()
    {
        Book book = Book.Sample();
        var root = new Located(book, null, null, Grid.Table.Root, "book", null);

        (var all, IReadOnlyList<string> notListed) = TableRead.Select(Grid.Table, root, []);
        (var cells, IReadOnlyList<string> named) = TableRead.Select(Grid.Table, root, ["cell"]);

        Assert.Equal(["book", "sheet/Data", "sheet/Data/area/A1:B2"], all.Select(static found => found.Address));
        Assert.Equal(["cell"], notListed);
        Assert.Equal(["sheet/Data/cell/A1", "sheet/Data/cell/B2"], cells.Select(static found => found.Address));
        Assert.Empty(named);
    }

    /// <summary>
    /// A read that names a kind walks only the placements that lead to it, and finds them in a
    /// table whose kinds hold each other, as a block holds blocks: each kind is visited once, so a
    /// kind no placement below leads to is found in one pass, not one per path.
    /// </summary>
    [Fact]
    public async Task Read_OfAKindInATableWhoseKindsHoldEachOther_EndsAndListsIt()
    {
        var root = new Located(new Nest(), null, null, Nesting.Table.Root, "nest", null);

        Task<string[]> marks = Task.Run(() => TableRead.Select(Nesting.Table, root, ["mark"]).Selected.Select(static found => found.Address).ToArray(), TestContext.Current.CancellationToken);
        Task finished = await Task.WhenAny(marks, Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));

        Assert.True(finished == marks, "The read of a kind in a table whose kinds hold each other did not end.");
        Assert.Equal(["mark/0"], await marks);
    }

    [Fact]
    public void Parse_RefusesAnAddressArgumentOfAnotherKind()
    {
        CliException error = Assert.Throws<CliException>(() => Parse("""{"ops":[{"do":"clear","args":{"sheet":"sheet/Data/cell/A1"}}]}"""));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Equal("args.sheet", error.Details!["field"]!.GetValue<string>());
        Assert.Contains("names a cell; it takes the address of a sheet", error.Message, StringComparison.Ordinal);
    }

    /// <summary>The object an address argument names is found before the call; a missing one is the address's error, at its operation.</summary>
    [Fact]
    public void Apply_FindsTheObjectOfAnAddressArgumentBeforeTheCall()
    {
        Book book = Book.Sample();

        IReadOnlyList<TableEdit.Applied> applied = TableEdit.Apply(Grid.Table, book, Parse("""{"ops":[{"do":"clear","args":{"sheet":"sheet/DATA"}}]}"""), Context());
        CliException missing = Assert.Throws<CliException>(() => TableEdit.Apply(Grid.Table, book, Parse("""{"ops":[{"do":"clear","args":{"sheet":"sheet/Notes"}}]}"""), Context()));

        Assert.Empty(book.Sheets[0].Cells);
        Assert.Equal("sheet/Data", applied[0].At);
        Assert.Equal(ErrorCodes.AddressNotFound, missing.Code);
        Assert.Equal(0, missing.Details!["index"]!.GetValue<int>());
        Assert.Equal("sheet/Notes", missing.Details["at"]!.GetValue<string>());
    }

    /// <summary>A value of the row's kind that does not fit the object is refused at the row, with the fact the row stated.</summary>
    [Fact]
    public void Apply_ReportsAValueThatDoesNotFitTheObjectAtItsRow()
    {
        CliException error = Assert.Throws<CliException>(() => TableEdit.Apply(Grid.Table, Book.Sample(),
            Parse("""{"ops":[{"set":"sheet/Data/area/A1:B2","props":{"values":["a","b","c"]}}]}"""), Context()));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Equal("props.values", error.Details!["field"]!.GetValue<string>());
        Assert.Contains("3 values and the area 2 cells", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A set that changes the object's key reports, and reads back, the object at its new address.</summary>
    [Fact]
    public void Set_ThatChangesTheObjectsKey_ReadsItBackAtItsNewAddress()
    {
        Book book = Book.Sample();

        IReadOnlyList<TableEdit.Applied> applied = TableEdit.Apply(Grid.Table, book, Parse("""{"ops":[{"set":"sheet/Data","props":{"name":"Renamed"}}]}"""), Context());
        IReadOnlyList<TableOpReport> reports = TableEdit.ReadBack(Grid.Table, book, applied);

        Assert.Equal("sheet/Renamed", reports[0].At);
        Assert.True(reports[0].Exists);
        Assert.Equal("Renamed", reports[0].Now!["name"]!.GetValue<string>());
    }

    /// <summary>An operation that leaves its object at an address another object has too fails as ambiguous, at its position.</summary>
    [Theory]
    [InlineData("""{"ops":[{"add":"sheet","in":"book","args":{"name":"Data"}}]}""")]
    [InlineData("""{"ops":[{"add":"sheet","in":"book","args":{"name":"Notes"}},{"set":"sheet/Notes","props":{"name":"DATA"}}]}""")]
    public void Apply_ThatLeavesAnObjectAtAnAddressAnotherHas_IsAmbiguous(string json)
    {
        TableBatch batch = Parse(json);

        CliException error = Assert.Throws<CliException>(() => TableEdit.Apply(Grid.Table, Book.Sample(), batch, Context()));

        Assert.Equal(ErrorCodes.AddressAmbiguous, error.Code);
        Assert.Equal(batch.Count - 1, error.Details!["index"]!.GetValue<int>());
    }

    [Fact]
    public void Parse_RefusesArgsForAnActionThatTakesNone_SayingSo()
    {
        CliException error = Assert.Throws<CliException>(() => Parse("""{"ops":[{"do":"count","args":{"x":1}}]}"""));

        Assert.Equal("args", error.Details!["field"]!.GetValue<string>());
        Assert.EndsWith("'count' takes no args", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Schema_MarksTheKindsListedWhenNamed_AndTheAddressesAnArgumentTakes()
    {
        TableSchema schema = TableSchema.Of("grid", Grid.Table);
        JsonNode document = JsonNode.Parse(schema.Document)!;
        JsonNode directory = JsonNode.Parse(schema.Views["directory"])!;

        Assert.True(document["$defs"]!["cell"]!["x-listedWhenNamed"]!.GetValue<bool>());
        Assert.Null(document["$defs"]!["sheet"]!["x-listedWhenNamed"]);
        Assert.True(directory["kinds"]!["cell"]!["listedWhenNamed"]!.GetValue<bool>());
        Assert.Null(directory["kinds"]!["sheet"]!["listedWhenNamed"]);
        Assert.Equal(["sheet/<name>/cell/<cell>"], document["$defs"]!["cell"]!["x-addresses"]!.AsArray().Select(static form => form!.GetValue<string>()));
        Assert.Equal(["sheet/<name>"], document["$defs"]!["do.clear"]!["properties"]!["args"]!["properties"]!["sheet"]!["x-addresses"]!.AsArray().Select(static form => form!.GetValue<string>()));
    }

    /// <summary>
    /// An add made under several kinds of parent is one definition and one branch of the add
    /// shape, so an operation that makes it matches that branch alone, and its description says
    /// each parent in a sentence of its own.
    /// </summary>
    [Fact]
    public void Schema_ListsAnAddMadeUnderSeveralParentsOnce()
    {
        JsonNode document = JsonNode.Parse(TableSchema.Of("nest", Nesting.Table).Document)!;

        string[] branches = [.. document["$defs"]!["add"]!["oneOf"]!.AsArray().Select(static branch => branch!["$ref"]!.GetValue<string>())];

        Assert.Equal(["#/$defs/add.a"], branches);
        Assert.Equal("Adds a a to a nest: Makes an a. Adds a a to a a: Makes an a. Adds a a to a b: Makes an a.",
            document["$defs"]!["add.a"]!["description"]!.GetValue<string>());
    }

    private static TableActionContext<Book> Context() => new(null!, new EngineWarningLog());

    private static TableBatch Parse(string json) => TableBatch.Parse(json, Grid.Table, new NoInputs());

    private sealed class NoInputs : ITableInputs
    {
        public string ResolvePath(string path) => Path.GetFullPath(path);

        public byte[] ReadImage(string path) => [];


        public byte[] ReadFile(string path) => [];

        public Secret? ReadSecret(string variable) => null;
    }

    private sealed class Nest;

    /// <summary>A table of two kinds that each hold both, as paragraphs and tables hold each other, and a kind held only by the root.</summary>
    private static class Nesting
    {
        private static readonly ObjectKind<Nest> NestKind = new("nest", "The root.");

        private static readonly ObjectKind<Nest> MarkKind = new("mark", "A mark the root holds.");

        public static DocumentTable<Nest> Table { get; } = Build();

        private static DocumentTable<Nest> Build()
        {
            var table = new DocumentTable<Nest>(NestKind);
            ObjectKind<Nest>[] holders = [new("a", "A kind that holds a and b."), new("b", "A kind that holds a and b.")];
            foreach (ObjectKind<Nest> holder in holders)
            {
                table.Place(NestKind, holder, Locator(holder.Name));
                foreach (ObjectKind<Nest> parent in holders)
                {
                    table.Place(parent, holder, Locator(holder.Name));
                }
            }
            foreach (ObjectKind<Nest> parent in new[] { NestKind }.Concat(holders))
            {
                table.Adds(parent, holders[0], new Adder<Nest, Nest>
                {
                    Name = "a",
                    Description = "Makes an a",
                    Arguments = [],
                    Create = static (nest, _) => nest,
                });
            }
            table.Place(NestKind, MarkKind, new Locator<Nest, Nest>
            {
                Segment = "mark",
                Key = [KeyPart.Index],
                Find = static (nest, key) => key.Index() == 0 ? nest : null,
                List = static nest => [nest],
                KeyOf = static (_, _) => Key.Of(0),
            });
            return table;
        }

        private static Locator<Nest, Nest> Locator(string segment) => new()
        {
            Segment = segment,
            Key = [KeyPart.Index],
            Find = static (_, _) => null,
            List = static _ => [],
            KeyOf = static (_, _) => Key.Of(0),
        };
    }

    private sealed class Book
    {
        public List<Sheet> Sheets { get; } = [];

        public static Book Sample() => new() { Sheets = { new Sheet("Data") { Cells = { ["A1"] = "a", ["B2"] = "b" } } } };
    }

    private sealed class Sheet(string name)
    {
        public string Name { get; set; } = name;

        public Dictionary<string, string> Cells { get; } = [];
    }

    private sealed class GridCell(Sheet sheet, string name)
    {
        public string Name { get; } = name;

        public string Text => sheet.Cells.GetValueOrDefault(Name, string.Empty);
    }

    private sealed class GridArea(Sheet sheet, string name)
    {
        public string Name { get; } = name;

        public IReadOnlyList<string> Values
        {
            get => [.. sheet.Cells.Values];
            set
            {
                if (value.Count != sheet.Cells.Count)
                {
                    throw TableErrors.DoesNotFit($"the value is {value.Count} values and the area {sheet.Cells.Count} cells");
                }
            }
        }
    }

    private static class Grid
    {
        private static readonly ObjectKind<Book> BookKind = new("book", "The book.");

        private static readonly ObjectKind<Sheet> SheetKind = new("sheet", "A sheet.")
        {
            Row.Text<Sheet>("name", "Sheet.Name", static s => s.Name, static (s, v) => s.Name = v),
        };

        private static readonly ObjectKind<GridCell> CellKind = new("cell", "A cell.")
        {
            Row.Text<GridCell>("text", "GridCell.Text", static c => c.Text),
        };

        private static readonly ObjectKind<GridArea> AreaKind = new("area", "An area.")
        {
            Row.Of<GridArea, IReadOnlyList<string?>>("values", ValueKinds.ListOf(ValueKinds.Text), "GridArea.Values", static a => a.Values, static (a, v) => a.Values = [.. v.Select(static text => text ?? string.Empty)]),
        };

        public static DocumentTable<Book> Table { get; } = Build();

        private static DocumentTable<Book> Build()
        {
            var table = new DocumentTable<Book>(BookKind);
            table.Place(BookKind, SheetKind, new Locator<Book, Sheet>
            {
                Segment = "sheet",
                Key = [KeyPart.Text],
                FindAll = static (book, key) => [.. book.Sheets.Where(sheet => string.Equals(sheet.Name, key.Name(), StringComparison.OrdinalIgnoreCase))],
                List = static book => book.Sheets,
                KeyOf = static (_, sheet) => Key.Of(sheet.Name),
            });
            table.Adds(BookKind, SheetKind, new Adder<Book, Sheet>
            {
                Name = "sheet",
                Description = "Adds a sheet.",
                Arguments = [Argument.Of("name", ValueKinds.Text, "Its name.")],
                Create = static (book, a) =>
                {
                    var sheet = new Sheet(a.Get<string>("name"));
                    book.Sheets.Add(sheet);
                    return sheet;
                },
            });
            table.Place(SheetKind, CellKind, new Locator<Sheet, GridCell>
            {
                Segment = "cell",
                Key = [KeyPart.CellName],
                Find = static (sheet, key) => new GridCell(sheet, key.Name().ToUpperInvariant()),
                List = static sheet => sheet.Cells.Keys.Select(name => new GridCell(sheet, name)),
                KeyOf = static (_, cell) => Key.Of(cell.Name),
                ListedWhenNamed = true,
            });
            table.Place(SheetKind, AreaKind, new Locator<Sheet, GridArea>
            {
                Segment = "area",
                Key = [KeyPart.Area],
                Find = static (sheet, key) => new GridArea(sheet, key.Name()),
                List = static sheet => [new GridArea(sheet, "A1:B2")],
                KeyOf = static (_, area) => Key.Of(area.Name),
            });
            table.Place(SheetKind, new ObjectKind<Sheet>("row", "A row, for the key form only."), new Locator<Sheet, Sheet>
            {
                Segment = "row",
                Key = [KeyPart.RowName],
                Find = static (sheet, _) => sheet,
                List = static _ => [],
                KeyOf = static (_, _) => Key.Of("1"),
            });
            table.Place(SheetKind, new ObjectKind<Sheet>("column", "A column, for the key form only."), new Locator<Sheet, Sheet>
            {
                Segment = "column",
                Key = [KeyPart.ColumnName],
                Find = static (sheet, _) => sheet,
                List = static _ => [],
                KeyOf = static (_, _) => Key.Of("A"),
            });
            table.Action(new TableAction<Book>
            {
                Name = "count",
                Description = "Counts the sheets.",
                Run = static (book, _, _) => new ActionResult(JsonValue.Create(book.Sheets.Count)),
            });
            table.Action(new TableAction<Book>
            {
                Name = "clear",
                Description = "Clears a sheet's cells.",
                Arguments = [Argument.Address("sheet", SheetKind, "The sheet to clear.")],
                Run = static (_, a, _) =>
                {
                    a.Get<Sheet>("sheet").Cells.Clear();
                    return new ActionResult(null, a.AddressOf("sheet"));
                },
            });
            return table;
        }
    }
}
