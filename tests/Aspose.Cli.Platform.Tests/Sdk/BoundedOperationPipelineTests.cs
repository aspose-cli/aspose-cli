using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.Operations;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

public sealed class BoundedOperationPipelineTests
{
    private static readonly OperationCatalog<TestOp> Catalog = TestOp.Catalog;

    private static SetOp Set(int value) => new() { Value = value };

    private static NoteOp Note() => new() { Text = "note" };

    [Fact]
    public void Prepare_AssignsIdsAndReportsTheFailingOperationPosition()
    {
        var batch = new TestBatch { Ops = [Set(1), Note(), Set(-1)] };

        CliException error = Assert.Throws<CliException>(() => Catalog.Prepare(batch));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Equal("op-0003", error.Details!["id"]!.GetValue<string>());
        Assert.Equal("set", error.Details["op"]!.GetValue<string>());
        Assert.Equal("value must be at least 0", error.Details["reason"]!.GetValue<string>());
        Assert.Contains("aspose-cli schema v2/common/ops", error.Hint, StringComparison.Ordinal);
        Assert.Equal(["op-0001", "op-0002"], Catalog.Prepare(new TestBatch { Ops = [Set(1), Note()] })
            .Ops.Select(static op => op.Id));
    }

    [Theory]
    [InlineData("""{"ops":[{"op":"set","vlaue":1}]}""", "set",
        "unknown field 'vlaue'", "op,id,value", "value")]
    [InlineData("""{"ops":[{"op":"set","amount":1}]}""", "set",
        "unknown field 'amount'", "op,id,value", "value")]
    [InlineData("""{"ops":[{"op":"note","text":"a","colour":"red"}]}""", "note",
        "unknown field 'colour'", "op,id,text,pinned", null)]
    [InlineData("""{"ops":[{"op":"place","pages":"1","all":true,"box":{"widht":2}}]}""", "place",
        "unknown field 'box.widht'", "width", "width")]
    [InlineData("""{"ops":[{"op":"place","pages":"1","all":true,"style":{"FontWeight":true}}]}""", "place",
        "unknown field 'style.FontWeight'", "font,size,bold", "bold")]
    [InlineData("""{"ops":[{"op":"place","pages":"1","all":true,"style":{"typeface":"Arial"}}]}""", "place",
        "unknown field 'style.typeface'", "font,size,bold", "font")]
    public void Parse_NamesTheAcceptedFieldsOfAnUnknownFieldOnce(
        string document, string op, string reason, string allowed, string? suggestion)
    {
        CliException error = Assert.Throws<CliException>(() => Catalog.Parse<TestBatch>(document, TestContracts.Json));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Equal("op-0001", error.Details!["id"]!.GetValue<string>());
        Assert.Equal(op, error.Details["op"]!.GetValue<string>());
        Assert.Equal(reason, error.Details["reason"]!.GetValue<string>());
        Assert.Equal(allowed.Split(','), error.Details["available"]!.AsArray().Select(static item => item!.GetValue<string>()));
        Assert.Null(error.Details["allowedFields"]);
        Assert.Equal(suggestion, error.Details["suggestions"]?[0]?.GetValue<string>());
        Assert.Equal($"Operation op-0001 ({op}) is invalid: {reason}. Must be one of: {allowed.Replace(",", ", ", StringComparison.Ordinal)}", error.Message);
    }

    [Fact]
    public void Prepare_ReportsEveryInvalidOperationAtOnce()
    {
        CliException error = Assert.Throws<CliException>(() => Catalog.Prepare(
            new TestBatch { Ops = [Set(-1), Note(), Set(-2)] }));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Equal("op-0001", error.Details!["id"]!.GetValue<string>());
        Assert.Equal(["op-0001", "op-0003"], error.Details["errors"]!.AsArray().Select(static item => item!["id"]!.GetValue<string>()));
        Assert.All(error.Details["errors"]!.AsArray(), item => Assert.Equal("value must be at least 0", item!["reason"]!.GetValue<string>()));
        Assert.Equal("2 operations are invalid, each listed in details.errors; the first: Operation op-0001 (set) is invalid: value must be at least 0",
            error.Message);
        Assert.Null(Assert.Throws<CliException>(() => Catalog.Prepare(new TestBatch { Ops = [Set(-1)] })).Details!["errors"]);
    }

    [Fact]
    public void Parse_ReportsEveryInvalidOperationAtOnce()
    {
        CliException error = Assert.Throws<CliException>(() => Catalog.Parse<TestBatch>(
            """{"ops":[{"op":"set","value":-1},{"op":"note","text":"a"},{"op":"set","value":"x"},{"op":"sett"}]}""",
            TestContracts.Json));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Equal("op-0001", error.Details!["id"]!.GetValue<string>());
        JsonArray errors = error.Details["errors"]!.AsArray();
        Assert.Equal(["op-0001", "op-0003", "op-0004"], errors.Select(static item => item!["id"]!.GetValue<string>()));
        Assert.Equal("value must be at least 0", errors[0]!["reason"]!.GetValue<string>());
        Assert.Equal("set", errors[1]!["op"]!.GetValue<string>());
        Assert.Equal("value must be a whole number", errors[1]!["reason"]!.GetValue<string>());
        Assert.Equal(["set"], errors[2]!["suggestions"]!.AsArray().Select(static item => item!.GetValue<string>()));
    }

    [Fact]
    public void Parse_ReportsALaterOperationTheSerializerCannotRead()
    {
        CliException error = Assert.Throws<CliException>(() => Catalog.Parse<TestBatch>(
            """{"ops":[{"op":"set","value":"x"},{"op":"probe","mode":"unsupported"}]}""", TestContracts.Json));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Equal(["op-0001", "op-0002"], error.Details!["errors"]!.AsArray().Select(static item => item!["id"]!.GetValue<string>()));
        Assert.Equal("probe", error.Details["errors"]![1]!["op"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("sett", "set")]
    [InlineData("Note", "note")]
    [InlineData("rotate", null)]
    public void Parse_ListsTheOperationsAndTheClosestForAnUnknownOp(string name, string? suggestion)
    {
        CliException error = Assert.Throws<CliException>(() => Catalog.Parse<TestBatch>(
            $$"""{"ops":[{"op":"{{name}}","value":1}]}""", TestContracts.Json));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Equal("op-0001", error.Details!["id"]!.GetValue<string>());
        // The op as the entry wrote it, so the caller finds the entry by either.
        Assert.Equal(name, error.Details["op"]!.GetValue<string>());
        Assert.Equal($"unknown op '{name}'", error.Details["reason"]!.GetValue<string>());
        string[] available = [.. error.Details["available"]!.AsArray().Select(static item => item!.GetValue<string>())];
        Assert.Equal(Catalog.Names.Order(StringComparer.Ordinal), available.Order(StringComparer.Ordinal));
        Assert.Equal($"Operation op-0001 is invalid: unknown op '{name}'. Must be one of: {string.Join(", ", available)}", error.Message);
        Assert.Equal(suggestion, error.Details["suggestions"]?[0]?.GetValue<string>());
        if (suggestion is not null)
        {
            Assert.Equal(suggestion, available[0]);
        }
    }

    [Fact]
    public void Parse_ListsTheOperationsThatShareAWordWithAnUnknownOpFirst()
    {
        CliException error = Assert.Throws<CliException>(() => Catalog.Parse<TestBatch>(
            """{"ops":[{"op":"zoom_text","value":1}]}""", TestContracts.Json));

        string[] available = [.. error.Details!["available"]!.AsArray().Select(static item => item!.GetValue<string>())];
        Assert.Equal("stamp_text", available[0]);
        Assert.Equal(Catalog.Names.Where(static name => name != "stamp_text"), available.Skip(1));
    }

    [Theory]
    [InlineData("""{"op":"stamp","text":"DRAFT"}""", "stamp_text")]
    [InlineData("""{"op":"stamp","image":"seal.png","id":"a"}""", "stamp_image")]
    [InlineData("""{"op":"stamp"}""", "stamp_image")]
    [InlineData("""{"op":"stamp","colour":"red"}""", "stamp_image")]
    [InlineData("""{"op":"Seal","text":"DRAFT"}""", "stamp_image")]
    public void Parse_SuggestsTheDeclaredOrClosestOperationThatAcceptsTheGivenFields(string operation, string suggestion)
    {
        CliException error = Assert.Throws<CliException>(() => Catalog.Parse<TestBatch>(
            $$"""{"ops":[{{operation}}]}""", TestContracts.Json));

        Assert.Equal(suggestion, error.Details!["suggestions"]?[0]?.GetValue<string>());
        Assert.StartsWith($"Did you mean '{suggestion}'", error.Hint, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"ops":[{"op":"place","pages":"1","all":true,"style":"bold"}]}""",
        "style must be an object; details.available lists its fields", "font,size,bold")]
    [InlineData("""{"ops":[{"op":"place","pages":"1","all":true,"box":[2]}]}""",
        "box must be an object; details.available lists its fields", "width")]
    public void Parse_NamesTheFieldsOfAnObjectGivenAnotherKindOfValue(string document, string reason, string allowed)
    {
        CliException error = Assert.Throws<CliException>(() => Catalog.Parse<TestBatch>(document, TestContracts.Json));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Equal("op-0001", error.Details!["id"]!.GetValue<string>());
        Assert.Equal("place", error.Details["op"]!.GetValue<string>());
        Assert.Equal(reason, error.Details["reason"]!.GetValue<string>());
        Assert.Equal(allowed.Split(','), error.Details["available"]!.AsArray().Select(static item => item!.GetValue<string>()));
        Assert.Null(error.Details["suggestions"]);
    }

    [Theory]
    [InlineData("""{"ops":[{"op":"set","value":1}],"extra":1}""", "unknown field 'extra'", null)]
    [InlineData("""{"op":[{"op":"set","value":1}]}""", "unknown field 'op'", "ops")]
    [InlineData("""{"steps":[{"op":"set","value":1}]}""", "unknown field 'steps'", "ops")]
    public void Parse_NamesTheAcceptedFieldsOfAnUnknownDocumentField(string document, string reason, string? suggestion)
    {
        CliException error = Assert.Throws<CliException>(() => Catalog.Parse<TestBatch>(document, TestContracts.Json));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Null(error.Details!["id"]);
        Assert.Equal(reason, error.Details["reason"]!.GetValue<string>());
        Assert.Equal(["schema", "schemaVersion", "ifMatch", "ops"],
            error.Details["available"]!.AsArray().Select(static item => item!.GetValue<string>()));
        Assert.Equal(suggestion, error.Details["suggestions"]?[0]?.GetValue<string>());
        Assert.EndsWith($"{reason}. Must be one of: schema, schemaVersion, ifMatch, ops", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_NumbersTheLineOfASyntaxErrorFromOne()
    {
        CliException error = Assert.Throws<CliException>(() => Catalog.Parse<TestBatch>(
            "{\"ops\":[\n  {\"op\":\"note\",\"text\":\"\\d\"}\n]}", TestContracts.Json));

        string reason = error.Details!["reason"]!.GetValue<string>();
        Assert.StartsWith("the document is not valid JSON: 'd' is an invalid escapable character", reason, StringComparison.Ordinal);
        Assert.EndsWith("(line 2, byte 25)", reason, StringComparison.Ordinal);
        Assert.DoesNotContain("LineNumber", reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// A document that is not JSON names no operation, so its hint points at the syntax error's
    /// position, and a backslash a JSON string cannot hold, as in a regular expression, is shown
    /// written as two.
    /// </summary>
    [Theory]
    [InlineData("{\"ops\":[{\"op\":\"note\",\"text\":\"(\\+86 *)?1[3-9]\\\\d{9}\"}]}", true)]
    [InlineData("{\"ops\":[{\"op\":\"note\" \"text\":\"a\"}]}", false)]
    public void Parse_HintsAtTheJsonSyntaxRatherThanAnOperation(string document, bool escape)
    {
        CliException error = Assert.Throws<CliException>(() => Catalog.Parse<TestBatch>(document, TestContracts.Json));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.DoesNotContain("named operation", error.Hint, StringComparison.Ordinal);
        Assert.Contains("JSON syntax", error.Hint, StringComparison.Ordinal);
        Assert.Contains("line", error.Hint, StringComparison.Ordinal);
        Assert.Equal(escape, error.Hint!.Contains("\"\\\\+\"", StringComparison.Ordinal));
    }

    /// <summary>
    /// A trailing comma is explained in the document's terms, at its position: the comma before
    /// the closing bracket or brace is deleted; nothing speaks of the JSON reader's options.
    /// </summary>
    [Theory]
    [InlineData("{\"ops\":[{\"op\":\"note\",\"text\":\"a\"},]}")]
    [InlineData("{\"ops\":[{\"op\":\"note\",\"text\":\"a\",}]}")]
    public void Parse_ExplainsATrailingCommaWithoutTheReadersOptions(string document)
    {
        CliException error = Assert.Throws<CliException>(() => Catalog.Parse<TestBatch>(document, TestContracts.Json));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.DoesNotContain("reader options", error.Message + error.Hint, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("in this mode", error.Message + error.Hint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("delete the comma", error.Message, StringComparison.Ordinal);
        Assert.Contains("delete the comma", error.Hint, StringComparison.Ordinal);
        Assert.Matches(@"\(line 1, byte \d+\)$", error.Details!["reason"]!.GetValue<string>());
    }

    [Fact]
    public void Parse_StatesOnlyWhereADocumentThatMustNotBeQuotedBreaks()
    {
        CliException error = Assert.Throws<CliException>(() => Catalog.Parse<TestBatch>(
            "Secret-7c2a\n{\"ops\":[]}", TestContracts.Json, quotesContent: false));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Equal("the document is not valid JSON (line 1, byte 1)", error.Details!["reason"]!.GetValue<string>());
        Assert.DoesNotContain("'S'", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("5")]
    [InlineData("\"ops\"")]
    public void Parse_NamesBothShapesOfADocumentThatIsNeither(string document)
    {
        CliException error = Assert.Throws<CliException>(() => Catalog.Parse<TestBatch>(document, TestContracts.Json));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Equal("the document must be an object with ops or the bare array of operations", error.Details!["reason"]!.GetValue<string>());
    }

    [Fact]
    public void Parse_ReadsTheBareArrayAsTheDocumentWithOnlyItsOperations()
    {
        TestBatch batch = Catalog.Parse<TestBatch>("""[{"op":"set","value":1},{"op":"note","text":"a"}]""", TestContracts.Json);

        Assert.Equal(["set", "note"], batch.Ops.Select(Catalog.NameOf));
        Assert.Equal(["op-0001", "op-0002"], batch.Ops.Select(static op => op.Id));
        CliException error = Assert.Throws<CliException>(() => Catalog.Parse<TestBatch>(
            """[{"op":"set","value":1},{"op":"set","value":"x"}]""", TestContracts.Json));
        Assert.Equal("op-0002", error.Details!["id"]!.GetValue<string>());
    }

    /// <summary>
    /// An entry the serializer cannot read is named by the id its outcome would carry: the id it
    /// states, else the one assigned around the ids other entries state.
    /// </summary>
    [Fact]
    public void Parse_NamesAnUnreadableEntryByTheIdItsOutcomeWouldCarry()
    {
        const string Readable = """[{"id":"op-0002","op":"note","text":"a"},{"op":"set","value":1},{"id":"total","op":"set","value":2}]""";
        const string Unreadable = """[{"id":"op-0002","op":"note","text":"a"},{"op":"set","value":"x"},{"id":"total","op":"set","value":"y"}]""";

        TestBatch batch = Catalog.Parse<TestBatch>(Readable, TestContracts.Json);
        CliException error = Assert.Throws<CliException>(() => Catalog.Parse<TestBatch>(Unreadable, TestContracts.Json));

        Assert.Equal(["op-0002", "op-0003", "total"], batch.Ops.Select(static op => op.Id));
        Assert.Equal(["op-0003", "total"], error.Details!["errors"]!.AsArray().Select(static item => item!["id"]!.GetValue<string>()));
        Assert.Contains("Operation op-0003 (set) is invalid", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Prepare_EnforcesTheDeclaredOperationLimit()
    {
        CliException error = Assert.Throws<CliException>(() => Catalog.Prepare(
            new TestBatch { Ops = [.. Enumerable.Range(0, BoundedOperation.MaximumPerBatch + 1).Select(static _ => Note())] }));

        Assert.Equal(10_000, BoundedOperation.MaximumPerBatch);
        Assert.Equal(BoundedOperation.MaximumPerBatch, Catalog.MaximumOperationCount);
        Assert.Contains("1-10000 operations", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_BestEffortRecordsRejectionsAndContinues()
    {
        TestBatch batch = Catalog.Prepare(new TestBatch { Ops = [Set(1), Note(), Set(2)] });

        IReadOnlyList<BoundedOperationOutcome> outcomes = Run(batch, bestEffort: true, static op =>
        {
            if (op is NoteOp)
            {
                throw new OperationInvalidException("the note has no anchor");
            }
        });

        Assert.Equal([OpStatuses.Ok, OpStatuses.Failed, OpStatuses.Ok], outcomes.Select(static item => item.Status));
        Assert.Equal(ErrorCodes.OpsInvalid.Name, outcomes[1].Error!.Code);
        Assert.Equal(["test/attempted"], outcomes[1].Targets);
    }

    [Fact]
    public void Run_KeepsADomainFailureCodeAndDetailsAndAddsItsPosition()
    {
        TestBatch batch = Catalog.Prepare(new TestBatch { Ops = [Note()] });
        Action<TestOp> missingPage = static _ =>
            throw CliErrors.NotFoundAt(ErrorCodes.PageNotFound, "page", "4", 3);

        CliException error = Assert.Throws<CliException>(() => Run(batch, bestEffort: false, missingPage));
        ErrorPayload recorded = Run(batch, bestEffort: true, missingPage)[0].Error!;

        Assert.Equal(ErrorCodes.PageNotFound, error.Code);
        Assert.Equal("op-0001", error.Details!["id"]!.GetValue<string>());
        Assert.Equal("note", error.Details["op"]!.GetValue<string>());
        Assert.Equal(ErrorCodes.PageNotFound.Name, recorded.Code);
        Assert.Equal(error.Details.ToJsonString(), recorded.Details!.ToJsonString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Run_StopsTheBatchWhenTheEngineFailsMidChange(bool bestEffort)
    {
        TestBatch batch = Catalog.Prepare(new TestBatch { Ops = [Set(1), Note()] });
        int applied = 0;

        CliException error = Assert.Throws<CliException>(() => Run(batch, bestEffort, op =>
        {
            applied++;
            if (op is NoteOp)
            {
                throw new EngineOpException("engine failed", new InvalidOperationException());
            }
        }));

        Assert.Equal(ErrorCodes.FeatureUnsupported, error.Code);
        Assert.Equal("op-0002", error.Details!["id"]!.GetValue<string>());
        Assert.Equal(2, applied);
        Assert.Contains("the batch stopped and nothing was written", error.Message, StringComparison.Ordinal);
        Assert.Equal(EngineErrors.EngineFailed("any", new InvalidOperationException()).Hint, error.Hint);
    }

    public static TheoryData<Exception> CliIoFailures() => new()
    {
        new EndOfStreamException("Unable to read beyond the end of the stream."),
        new IOException("The process cannot access the file 'a.csv'.", unchecked((int)0x80070020)),
        new UnauthorizedAccessException("Access to the path 'a.csv' is denied."),
    };

    /// <summary>
    /// An I/O exception the CLI itself raised inside an operation is not an engine failure: it
    /// propagates unchanged, as it did before, to the Host's boundary. The engine's own I/O
    /// failures, which name the operation, are covered by the products' tests.
    /// </summary>
    [Theory]
    [MemberData(nameof(CliIoFailures))]
    public void Run_LetsAnIoFailureOfTheCliPropagate(Exception cause)
    {
        TestBatch batch = Catalog.Prepare(new TestBatch { Ops = [Note()] });

        Exception thrown = Assert.ThrowsAny<Exception>(() => Run(batch, bestEffort: true, _ => throw cause));

        Assert.Same(cause, thrown);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Run_ReportsAMissingFileTheOperationDeclaresAsThatFile(bool bestEffort)
    {
        string missing = Path.Combine(Path.GetTempPath(), "missing-picture.png");
        TestBatch batch = Catalog.Prepare(new TestBatch { Ops = [Set(1), new LinkOp { Path = missing }] });
        void Apply(TestOp op)
        {
            if (op is LinkOp)
            {
                throw new FileNotFoundException("Could not find file.", missing);
            }
        }

        if (bestEffort)
        {
            ErrorPayload recorded = Run(batch, bestEffort: true, Apply)[1].Error!;
            Assert.Equal(ErrorCodes.FileNotFound.Name, recorded.Code);
            Assert.Equal(missing, recorded.Details!["path"]!.GetValue<string>());
            return;
        }

        CliException error = Assert.Throws<CliException>(() => Run(batch, bestEffort: false, Apply));
        Assert.Equal(ErrorCodes.FileNotFound, error.Code);
        Assert.Equal("op-0002", error.Details!["id"]!.GetValue<string>());
        Assert.Equal(missing, error.Details["path"]!.GetValue<string>());
    }

    /// <summary>A file failure as .NET words it, naming <paramref name="path"/>.</summary>
    private static Exception FileFailure(string kind, string path) => kind switch
    {
        // HRESULT_FROM_WIN32(ERROR_SHARING_VIOLATION): another program holds the file.
        "locked" => new IOException($"The process cannot access the file '{path}' because it is being used by another process.", unchecked((int)0x80070020)),
        "denied" => new UnauthorizedAccessException($"Access to the path '{path}' is denied."),
        _ => new FileNotFoundException($"Could not find file '{path}'."),
    };

    /// <summary>
    /// A file the operation declares that cannot be opened keeps the code of that failure and
    /// names the declared file, also when the exception names it only in its message, raised
    /// directly or by the engine.
    /// </summary>
    [Theory]
    [InlineData("locked", "FILE_LOCKED", false)]
    [InlineData("locked", "FILE_LOCKED", true)]
    [InlineData("denied", "FILE_ACCESS_DENIED", false)]
    [InlineData("denied", "FILE_ACCESS_DENIED", true)]
    [InlineData("missing", "FILE_NOT_FOUND", true)]
    public void Run_ReportsADeclaredFileThatCannotBeOpenedByItsCode(string kind, string code, bool engine)
    {
        string declared = Path.Combine(Path.GetTempPath(), "held-picture.png");
        TestBatch batch = Catalog.Prepare(new TestBatch { Ops = [Set(1), new LinkOp { Path = declared }] });
        void Apply(TestOp op)
        {
            if (op is LinkOp)
            {
                Exception cause = FileFailure(kind, declared);
                throw engine ? new EngineOpException("the engine could not open the file", cause) : cause;
            }
        }

        CliException error = Assert.Throws<CliException>(() => Run(batch, bestEffort: false, Apply));
        ErrorPayload recorded = Run(batch, bestEffort: true, Apply)[1].Error!;

        Assert.Equal(code, error.Code.Name);
        Assert.Equal("op-0002", error.Details!["id"]!.GetValue<string>());
        Assert.Equal(declared, error.Details["path"]!.GetValue<string>());
        Assert.Equal(code, recorded.Code);
    }

    /// <summary>
    /// A failure of another file than the one the operation declares, such as a temporary file
    /// the engine writes or a file whose name only ends like the declared one's, is not the
    /// declared file's: the engine's stops the batch as an engine failure, in every mode.
    /// </summary>
    [Theory]
    [InlineData("locked")]
    [InlineData("denied")]
    public void Run_NeverBlamesTheDeclaredFileForAnotherFilesFailure(string kind)
    {
        string declared = Path.Combine(Path.GetTempPath(), "a.png");
        TestBatch batch = Catalog.Prepare(new TestBatch { Ops = [new LinkOp { Path = declared }, Set(1)] });
        foreach (string other in new[] { Path.Combine(Path.GetTempPath(), "engine-scratch.tmp"), Path.Combine(Path.GetTempPath(), "ba.png") })
        {
            CliException error = Assert.Throws<CliException>(() => Run(batch, bestEffort: true, op =>
            {
                if (op is LinkOp)
                {
                    throw new EngineOpException("the engine failed", FileFailure(kind, other));
                }
            }));

            Assert.Equal(ErrorCodes.FeatureUnsupported, error.Code);
            Assert.Null(error.Details!["path"]);
        }
    }

    /// <summary>
    /// A missing file the operation does not declare, such as an assembly the runtime could not
    /// load, is no rejection a best-effort batch could continue past.
    /// </summary>
    [Fact]
    public void Run_NeverContinuesPastAMissingFileTheOperationDoesNotDeclare()
    {
        string declared = Path.Combine(Path.GetTempPath(), "picture.png");
        TestBatch batch = Catalog.Prepare(new TestBatch { Ops = [new LinkOp { Path = declared }, Set(1)] });
        var missing = new FileNotFoundException("Could not load file or assembly 'Imaging'.", "Imaging, Version=1.0.0.0");
        int applied = 0;

        Exception thrown = Assert.ThrowsAny<Exception>(() => Run(batch, bestEffort: true, op =>
        {
            applied++;
            if (op is LinkOp)
            {
                throw missing;
            }
        }));

        Assert.Same(missing, thrown);
        Assert.Equal(1, applied);
    }

    [Fact]
    public void ExceptionOrigin_TellsAThirdPartyRaiserFromTheCli()
    {
        Exception raised;
        try { throw new IOException("raised here"); }
        catch (IOException exception) { raised = exception; }

        Assert.True(ExceptionOrigin.IsThirdParty(raised, new HashSet<System.Reflection.Assembly> { typeof(CliException).Assembly }));
        Assert.False(ExceptionOrigin.IsThirdParty(raised, new HashSet<System.Reflection.Assembly> { typeof(BoundedOperationPipelineTests).Assembly }));
    }

    public static TheoryData<Exception> FileAccessFailures() => new()
    {
        // HRESULT_FROM_WIN32(ERROR_SHARING_VIOLATION): another program holds the file.
        new IOException("The process cannot access the file 'a.csv'.", unchecked((int)0x80070020)),
        new UnauthorizedAccessException("Access to the path 'a.csv' is denied."),
        new FileNotFoundException("Could not find file 'a.csv'."),
        new DirectoryNotFoundException("Could not find a part of the path 'x\\a.csv'."),
    };

    [Theory]
    [MemberData(nameof(FileAccessFailures))]
    public void Run_BlamesFileAccessNotTheDocumentWhenTheEngineCannotOpenAFile(Exception cause)
    {
        TestBatch batch = Catalog.Prepare(new TestBatch { Ops = [Note()] });

        CliException error = Assert.Throws<CliException>(() => Run(batch, bestEffort: false, _ =>
            throw new EngineOpException("in use", cause)));

        Assert.Equal(ErrorCodes.FeatureUnsupported, error.Code);
        Assert.DoesNotContain("may not support", error.Hint, StringComparison.Ordinal);
        Assert.Contains("close", error.Hint, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Run_NamesTheOperationWhoseDeadlineExpiredInsideIt(bool bestEffort)
    {
        TestBatch batch = Catalog.Prepare(new TestBatch { Ops = [Set(1), Note()] });
        using OperationDeadline deadline = OperationDeadline.Start(TimeSpan.FromSeconds(1));

        CliException error = Assert.Throws<CliException>(() => Run(batch, bestEffort, deadline, op =>
        {
            if (op is NoteOp)
            {
                deadline.Token.WaitHandle.WaitOne();
                deadline.ThrowIfExpired("import");
            }
        }));

        Assert.Equal(ErrorCodes.OperationTimeout, error.Code);
        Assert.Equal("op-0002", error.Details!["id"]!.GetValue<string>());
        Assert.Equal("note", error.Details["op"]!.GetValue<string>());
        Assert.Equal("import", error.Details["phase"]!.GetValue<string>());
        Assert.StartsWith("Operation op-0002 (note) failed: ", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_NamesTheOperationWhoseRegularExpressionRanOutOfTime()
    {
        TestBatch batch = Catalog.Prepare(new TestBatch { Ops = [Note()] });

        CliException error = Assert.Throws<CliException>(() => Run(batch, bestEffort: true, static _ =>
            throw new RegexMatchTimeoutException("aaaa!", "(a+)+$", TimeSpan.FromSeconds(1))));

        Assert.Equal(ErrorCodes.OperationTimeout, error.Code);
        Assert.Equal("op-0001", error.Details!["id"]!.GetValue<string>());
        Assert.Equal("note", error.Details["op"]!.GetValue<string>());
        Assert.Equal("regex", error.Details["phase"]!.GetValue<string>());
    }

    /// <summary>A deadline that expires between operations is no operation's failure, so it names none.</summary>
    [Fact]
    public void Run_NamesNoOperationWhenTheDeadlineExpiresBeforeOne()
    {
        TestBatch batch = Catalog.Prepare(new TestBatch { Ops = [Set(1), Note()] });
        using OperationDeadline deadline = OperationDeadline.Start(TimeSpan.FromMilliseconds(200));

        CliException error = Assert.Throws<CliException>(() => Run(batch, bestEffort: false, deadline, op =>
        {
            if (op is SetOp)
            {
                deadline.Token.WaitHandle.WaitOne();
            }
        }));

        Assert.Equal(ErrorCodes.OperationTimeout, error.Code);
        Assert.Null(error.Details!["id"]);
        Assert.Equal("operations", error.Details["phase"]!.GetValue<string>());
    }

    [Fact]
    public void Run_KeepsTheFeatureHintWhenTheEngineReadsTruncatedData()
    {
        TestBatch batch = Catalog.Prepare(new TestBatch { Ops = [Note()] });

        CliException error = Assert.Throws<CliException>(() => Run(batch, bestEffort: false, static _ =>
            throw new EngineOpException("truncated", new EndOfStreamException("Unable to read beyond the end."))));

        Assert.Equal(EngineErrors.EngineFailed("any", new InvalidOperationException()).Hint, error.Hint);
    }

    private static IReadOnlyList<BoundedOperationOutcome> Run(TestBatch batch, bool bestEffort, Action<TestOp> apply) =>
        Run(batch, bestEffort, deadline: null, apply);

    private static IReadOnlyList<BoundedOperationOutcome> Run(TestBatch batch, bool bestEffort, OperationDeadline? deadline, Action<TestOp> apply) =>
        BoundedOperationRunner.Run(Catalog, batch.Ops, bestEffort, deadline, new Model(apply)).Outcomes;

    /// <summary>A document of one value that each operation that applies changes.</summary>
    private sealed class Model(Action<TestOp> apply) : IOperationModel<TestOp>
    {
        private int _version;

        public OperationSnapshot Snapshot(TestOp operation) =>
            new OperationSnapshot().Add("test/value", _version.ToString(System.Globalization.CultureInfo.InvariantCulture));

        public void Apply(TestOp operation)
        {
            apply(operation);
            _version++;
        }

        public IReadOnlyList<string> AttemptedTargets(TestOp operation) => ["test/attempted"];
    }
}
