using System.Text.Json.Nodes;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
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
        Assert.Equal(2, error.Details!["index"]!.GetValue<int>());
        Assert.Equal("set", error.Details["op"]!.GetValue<string>());
        Assert.Equal("value must be at least 0", error.Details["reason"]!.GetValue<string>());
        Assert.Contains("aspose-cli schema v2/common/ops", error.Hint, StringComparison.Ordinal);
        Assert.Equal(["op-0001", "op-0002"], Catalog.Prepare(new TestBatch { Ops = [Set(1), Note()] })
            .Ops.Select(static op => op.Id));
    }

    [Theory]
    [InlineData("""{"ops":[{"op":"set","vlaue":1}]}""", "set",
        "unknown field 'vlaue'; set accepts: op, id, value", "op,id,value", "value")]
    [InlineData("""{"ops":[{"op":"set","amount":1}]}""", "set",
        "unknown field 'amount'; set accepts: op, id, value", "op,id,value", "value")]
    [InlineData("""{"ops":[{"op":"note","text":"a","colour":"red"}]}""", "note",
        "unknown field 'colour'; note accepts: op, id, text, pinned", "op,id,text,pinned", null)]
    [InlineData("""{"ops":[{"op":"place","pages":"1","all":true,"box":{"widht":2}}]}""", "place",
        "unknown field 'box.widht'; box accepts: width", "width", "width")]
    [InlineData("""{"ops":[{"op":"place","pages":"1","all":true,"style":{"FontWeight":true}}]}""", "place",
        "unknown field 'style.FontWeight'; style accepts: font, size, bold", "font,size,bold", "bold")]
    [InlineData("""{"ops":[{"op":"place","pages":"1","all":true,"style":{"typeface":"Arial"}}]}""", "place",
        "unknown field 'style.typeface'; style accepts: font, size, bold", "font,size,bold", "font")]
    public void Parse_NamesTheAcceptedFieldsOfAnUnknownField(
        string document, string op, string reason, string allowed, string? suggestion)
    {
        CliException error = Assert.Throws<CliException>(() => Catalog.Parse<TestBatch>(document, TestContracts.Json));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Equal(0, error.Details!["index"]!.GetValue<int>());
        Assert.Equal(op, error.Details["op"]!.GetValue<string>());
        Assert.Equal(reason, error.Details["reason"]!.GetValue<string>());
        Assert.Equal(allowed.Split(','), error.Details["allowedFields"]!.AsArray().Select(static item => item!.GetValue<string>()));
        Assert.Equal(suggestion, error.Details["suggestions"]?[0]?.GetValue<string>());
    }

    [Fact]
    public void Prepare_ReportsEveryInvalidOperationAtOnce()
    {
        CliException error = Assert.Throws<CliException>(() => Catalog.Prepare(
            new TestBatch { Ops = [Set(-1), Note(), Set(-2)] }));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Equal(0, error.Details!["index"]!.GetValue<int>());
        Assert.Equal([0, 2], error.Details["errors"]!.AsArray().Select(static item => item!["index"]!.GetValue<int>()));
        Assert.All(error.Details["errors"]!.AsArray(), item => Assert.Equal("value must be at least 0", item!["reason"]!.GetValue<string>()));
        Assert.Contains("1 more operation is invalid", error.Message, StringComparison.Ordinal);
        Assert.Null(Assert.Throws<CliException>(() => Catalog.Prepare(new TestBatch { Ops = [Set(-1)] })).Details!["errors"]);
    }

    [Fact]
    public void Parse_ReportsEveryInvalidOperationAtOnce()
    {
        CliException error = Assert.Throws<CliException>(() => Catalog.Parse<TestBatch>(
            """{"ops":[{"op":"set","value":-1},{"op":"note","text":"a"},{"op":"set","value":"x"},{"op":"sett"}]}""",
            TestContracts.Json));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Equal(0, error.Details!["index"]!.GetValue<int>());
        JsonArray errors = error.Details["errors"]!.AsArray();
        Assert.Equal([0, 2, 3], errors.Select(static item => item!["index"]!.GetValue<int>()));
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
        Assert.Equal([0, 1], error.Details!["errors"]!.AsArray().Select(static item => item!["index"]!.GetValue<int>()));
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
        Assert.Equal(0, error.Details!["index"]!.GetValue<int>());
        Assert.Null(error.Details["op"]);
        Assert.StartsWith($"unknown op '{name}'; valid ops: label, link, note,", error.Details["reason"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(Catalog.Names, error.Details["available"]!.AsArray().Select(static item => item!.GetValue<string>()));
        Assert.Equal(suggestion, error.Details["suggestions"]?[0]?.GetValue<string>());
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
        "style must be an object with the fields: font, size, bold", "font,size,bold")]
    [InlineData("""{"ops":[{"op":"place","pages":"1","all":true,"box":[2]}]}""",
        "box must be an object with the fields: width", "width")]
    public void Parse_NamesTheFieldsOfAnObjectGivenAnotherKindOfValue(string document, string reason, string allowed)
    {
        CliException error = Assert.Throws<CliException>(() => Catalog.Parse<TestBatch>(document, TestContracts.Json));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Equal(0, error.Details!["index"]!.GetValue<int>());
        Assert.Equal("place", error.Details["op"]!.GetValue<string>());
        Assert.Equal(reason, error.Details["reason"]!.GetValue<string>());
        Assert.Equal(allowed.Split(','), error.Details["allowedFields"]!.AsArray().Select(static item => item!.GetValue<string>()));
        Assert.Null(error.Details["suggestions"]);
    }

    [Theory]
    [InlineData("""{"ops":[{"op":"set","value":1}],"extra":1}""",
        "unknown field 'extra'; the document accepts: schema, schemaVersion, ifMatch, ops", null)]
    [InlineData("""{"op":[{"op":"set","value":1}]}""",
        "unknown field 'op'; the document accepts: schema, schemaVersion, ifMatch, ops", "ops")]
    [InlineData("""{"steps":[{"op":"set","value":1}]}""",
        "unknown field 'steps'; the document accepts: schema, schemaVersion, ifMatch, ops", "ops")]
    public void Parse_NamesTheAcceptedFieldsOfAnUnknownDocumentField(string document, string reason, string? suggestion)
    {
        CliException error = Assert.Throws<CliException>(() => Catalog.Parse<TestBatch>(document, TestContracts.Json));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Null(error.Details!["index"]);
        Assert.Equal(reason, error.Details["reason"]!.GetValue<string>());
        Assert.Equal(["schema", "schemaVersion", "ifMatch", "ops"],
            error.Details["allowedFields"]!.AsArray().Select(static item => item!.GetValue<string>()));
        Assert.Equal(suggestion, error.Details["suggestions"]?[0]?.GetValue<string>());
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

    [Fact]
    public void Prepare_EnforcesTheDeclaredOperationLimit()
    {
        CliException error = Assert.Throws<CliException>(() => Catalog.Prepare(
            new TestBatch { Ops = [.. Enumerable.Range(0, 9).Select(static _ => Note())] }));

        Assert.Contains("1-8 operations", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_BestEffortRecordsRejectionsAndContinues()
    {
        TestBatch batch = Catalog.Prepare(new TestBatch { Ops = [Set(1), Note(), Set(2)] });

        IReadOnlyList<BoundedOperationOutcome> outcomes = Run(batch, bestEffort: true, (op, _) => op is NoteOp
            ? throw new OperationInvalidException("the note has no anchor")
            : new AppliedOperation(1, ["test/value"]));

        Assert.Equal([OpStatuses.Ok, OpStatuses.Failed, OpStatuses.Ok], outcomes.Select(static item => item.Status));
        Assert.Equal(ErrorCodes.OpsInvalid.Name, outcomes[1].Error!.Code);
        Assert.Equal(["test/attempted"], outcomes[1].Targets);
    }

    [Fact]
    public void Run_KeepsADomainFailureCodeAndDetailsAndAddsItsPosition()
    {
        TestBatch batch = Catalog.Prepare(new TestBatch { Ops = [Note()] });
        Func<TestOp, int, AppliedOperation> missingPage = (_, _) =>
            throw CliErrors.NotFoundAt(ErrorCodes.PageNotFound, "page", "4", 3);

        CliException error = Assert.Throws<CliException>(() => Run(batch, bestEffort: false, missingPage));
        OpError recorded = Run(batch, bestEffort: true, missingPage)[0].Error!;

        Assert.Equal(ErrorCodes.PageNotFound, error.Code);
        Assert.Equal(0, error.Details!["index"]!.GetValue<int>());
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

        CliException error = Assert.Throws<CliException>(() => Run(batch, bestEffort, (op, _) =>
        {
            applied++;
            return op is NoteOp
                ? throw new EngineOpException("engine failed", new InvalidOperationException())
                : new AppliedOperation(1, []);
        }));

        Assert.Equal(ErrorCodes.FeatureUnsupported, error.Code);
        Assert.Equal(1, error.Details!["index"]!.GetValue<int>());
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

        Exception thrown = Assert.ThrowsAny<Exception>(() => Run(batch, bestEffort: true, (_, _) => throw cause));

        Assert.Same(cause, thrown);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Run_ReportsAMissingFileTheOperationDeclaresAsThatFile(bool bestEffort)
    {
        string missing = Path.Combine(Path.GetTempPath(), "missing-picture.png");
        TestBatch batch = Catalog.Prepare(new TestBatch { Ops = [Set(1), new LinkOp { Path = missing }] });
        AppliedOperation Apply(TestOp op, int index) => op is LinkOp
            ? throw new FileNotFoundException("Could not find file.", missing)
            : new AppliedOperation(1, []);

        if (bestEffort)
        {
            OpError recorded = Run(batch, bestEffort: true, Apply)[1].Error!;
            Assert.Equal(ErrorCodes.FileNotFound.Name, recorded.Code);
            Assert.Equal(missing, recorded.Details!["path"]!.GetValue<string>());
            return;
        }

        CliException error = Assert.Throws<CliException>(() => Run(batch, bestEffort: false, Apply));
        Assert.Equal(ErrorCodes.FileNotFound, error.Code);
        Assert.Equal(1, error.Details!["index"]!.GetValue<int>());
        Assert.Equal(missing, error.Details["path"]!.GetValue<string>());
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

        Exception thrown = Assert.ThrowsAny<Exception>(() => Run(batch, bestEffort: true, (op, _) =>
        {
            applied++;
            return op is LinkOp ? throw missing : new AppliedOperation(1, []);
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

        CliException error = Assert.Throws<CliException>(() => Run(batch, bestEffort: false, (_, _) =>
            throw new EngineOpException("in use", cause)));

        Assert.Equal(ErrorCodes.FeatureUnsupported, error.Code);
        Assert.DoesNotContain("may not support", error.Hint, StringComparison.Ordinal);
        Assert.Contains("close", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_KeepsTheFeatureHintWhenTheEngineReadsTruncatedData()
    {
        TestBatch batch = Catalog.Prepare(new TestBatch { Ops = [Note()] });

        CliException error = Assert.Throws<CliException>(() => Run(batch, bestEffort: false, (_, _) =>
            throw new EngineOpException("truncated", new EndOfStreamException("Unable to read beyond the end."))));

        Assert.Equal(EngineErrors.EngineFailed("any", new InvalidOperationException()).Hint, error.Hint);
    }

    private static string[] Targets(int count) => [.. Enumerable.Range(1, count).Select(static index => $"test/item/{index}")];

    [Fact]
    public void Run_ListsUpToTheMaximumTargetsAsTheyAre()
    {
        TestBatch batch = Catalog.Prepare(new TestBatch { Ops = [Note()] });

        IReadOnlyList<BoundedOperationOutcome> outcomes = BoundedOperationRunner.Run(
            Catalog, batch.Ops, bestEffort: false, deadline: null,
            static (_, _) => new AppliedOperation(BoundedOperationRunner.MaximumTargets, Targets(BoundedOperationRunner.MaximumTargets)),
            static (_, _) => ["test/attempted"],
            static (_, _) => throw new InvalidOperationException("An outcome of 100 targets needs no degenerate form."));

        Assert.Equal(100, BoundedOperationRunner.MaximumTargets);
        Assert.Equal(Targets(100), Assert.Single(outcomes).Targets);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Run_ListsTheDegenerateFormOfMoreThanTheMaximumTargets(bool failed)
    {
        TestBatch batch = Catalog.Prepare(new TestBatch { Ops = [Note()] });
        IReadOnlyList<string>? received = null;

        IReadOnlyList<BoundedOperationOutcome> outcomes = BoundedOperationRunner.Run(
            Catalog, batch.Ops, bestEffort: true, deadline: null,
            (_, _) => failed ? throw new OperationInvalidException("the note has no anchor") : new AppliedOperation(101, Targets(101)),
            static (_, _) => Targets(101),
            (op, targets) =>
            {
                Assert.IsType<NoteOp>(op);
                received = targets;
                return ["test/all"];
            });

        Assert.Equal(failed ? OpStatuses.Failed : OpStatuses.Ok, Assert.Single(outcomes).Status);
        Assert.Equal(["test/all"], outcomes[0].Targets);
        Assert.Equal(Targets(101), received);
    }

    public static TheoryData<string[]> InvalidDegenerateForms() => new() { Array.Empty<string>(), Targets(101) };

    [Theory]
    [MemberData(nameof(InvalidDegenerateForms))]
    public void Run_RefusesADegenerateFormThatIsEmptyOrTooLong(string[] degenerate)
    {
        TestBatch batch = Catalog.Prepare(new TestBatch { Ops = [Note()] });

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => BoundedOperationRunner.Run(
            Catalog, batch.Ops, bestEffort: false, deadline: null,
            static (_, _) => new AppliedOperation(150, Targets(150)),
            static (_, _) => ["test/attempted"],
            (_, _) => degenerate));

        Assert.Contains("'note'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_RefusesMoreThanTheMaximumTargetsWithoutADegenerateForm()
    {
        TestBatch batch = Catalog.Prepare(new TestBatch { Ops = [Note()] });

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Run(batch, bestEffort: false,
            static (_, _) => new AppliedOperation(101, Targets(101))));

        Assert.Contains("'note'", error.Message, StringComparison.Ordinal);
    }

    private static IReadOnlyList<BoundedOperationOutcome> Run(
        TestBatch batch, bool bestEffort, Func<TestOp, int, AppliedOperation> apply) =>
        BoundedOperationRunner.Run(Catalog, batch.Ops, bestEffort, deadline: null, apply, static (_, _) => ["test/attempted"]);
}
