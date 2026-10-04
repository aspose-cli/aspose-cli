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
        Assert.Contains("aspose-cli schema v2/test/ops", error.Hint, StringComparison.Ordinal);
        Assert.Equal(["op-0001", "op-0002"], Catalog.Prepare(new TestBatch { Ops = [Set(1), Note()] })
            .Ops.Select(static op => op.Id));
    }

    [Theory]
    [InlineData("""{"ops":[{"op":"set","vlaue":1}]}""", "set",
        "unknown field 'vlaue'; set accepts: op, id, value (did you mean 'value'?)", "op,id,value", "value")]
    [InlineData("""{"ops":[{"op":"set","amount":1}]}""", "set",
        "unknown field 'amount'; set accepts: op, id, value (did you mean 'value'?)", "op,id,value", "value")]
    [InlineData("""{"ops":[{"op":"note","text":"a","colour":"red"}]}""", "note",
        "unknown field 'colour'; note accepts: op, id, text, pinned", "op,id,text,pinned", null)]
    [InlineData("""{"ops":[{"op":"place","pages":"1","all":true,"box":{"widht":2}}]}""", "place",
        "unknown field 'box.widht'; box accepts: width (did you mean 'width'?)", "width", "width")]
    [InlineData("""{"ops":[{"op":"place","pages":"1","all":true,"style":{"FontWeight":true}}]}""", "place",
        "unknown field 'style.FontWeight'; style accepts: font, size, bold (did you mean 'bold'?)", "font,size,bold", "bold")]
    [InlineData("""{"ops":[{"op":"place","pages":"1","all":true,"style":{"typeface":"Arial"}}]}""", "place",
        "unknown field 'style.typeface'; style accepts: font, size, bold (did you mean 'font'?)", "font,size,bold", "font")]
    public void Parse_NamesTheAcceptedFieldsOfAnUnknownField(
        string document, string op, string reason, string allowed, string? suggestion)
    {
        CliException error = Assert.Throws<CliException>(() => Catalog.Parse<TestBatch>(document, TestContracts.Json));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Equal(0, error.Details!["index"]!.GetValue<int>());
        Assert.Equal(op, error.Details["op"]!.GetValue<string>());
        Assert.Equal(reason, error.Details["reason"]!.GetValue<string>());
        Assert.Equal(allowed.Split(','), error.Details["allowedFields"]!.AsArray().Select(static item => item!.GetValue<string>()));
        Assert.Equal(suggestion, error.Details["suggestion"]?.GetValue<string>());
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
        Assert.Equal("set", errors[2]!["suggestion"]!.GetValue<string>());
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
        Assert.Equal(suggestion, error.Details["suggestion"]?.GetValue<string>());
    }

    [Theory]
    [InlineData("""{"op":"stamp","text":"DRAFT"}""", "stamp_text")]
    [InlineData("""{"op":"stamp","image":"seal.png","id":"a"}""", "stamp_image")]
    [InlineData("""{"op":"stamp"}""", "stamp_image")]
    [InlineData("""{"op":"stamp","colour":"red"}""", "stamp_image")]
    public void Parse_SuggestsTheClosestOperationThatAcceptsTheGivenFields(string operation, string suggestion)
    {
        CliException error = Assert.Throws<CliException>(() => Catalog.Parse<TestBatch>(
            $$"""{"ops":[{{operation}}]}""", TestContracts.Json));

        Assert.Equal(suggestion, error.Details!["suggestion"]?.GetValue<string>());
        Assert.EndsWith($"(did you mean '{suggestion}'?)", error.Details["reason"]!.GetValue<string>(), StringComparison.Ordinal);
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
        Assert.Null(error.Details["suggestion"]);
    }

    [Theory]
    [InlineData("""{"ops":[{"op":"set","value":1}],"extra":1}""",
        "unknown field 'extra'; the document accepts: schema, schemaVersion, ifMatch, ops", null)]
    [InlineData("""{"op":[{"op":"set","value":1}]}""",
        "unknown field 'op'; the document accepts: schema, schemaVersion, ifMatch, ops (did you mean 'ops'?)", "ops")]
    [InlineData("""{"steps":[{"op":"set","value":1}]}""",
        "unknown field 'steps'; the document accepts: schema, schemaVersion, ifMatch, ops (did you mean 'ops'?)", "ops")]
    public void Parse_NamesTheAcceptedFieldsOfAnUnknownDocumentField(string document, string reason, string? suggestion)
    {
        CliException error = Assert.Throws<CliException>(() => Catalog.Parse<TestBatch>(document, TestContracts.Json));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Null(error.Details!["index"]);
        Assert.Equal(reason, error.Details["reason"]!.GetValue<string>());
        Assert.Equal(["schema", "schemaVersion", "ifMatch", "ops"],
            error.Details["allowedFields"]!.AsArray().Select(static item => item!.GetValue<string>()));
        Assert.Equal(suggestion, error.Details["suggestion"]?.GetValue<string>());
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
        Assert.Equal(CliErrors.EngineFailed("any", new InvalidOperationException()).Hint, error.Hint);
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

        Assert.Equal(CliErrors.EngineFailed("any", new InvalidOperationException()).Hint, error.Hint);
    }

    private static IReadOnlyList<BoundedOperationOutcome> Run(
        TestBatch batch, bool bestEffort, Func<TestOp, int, AppliedOperation> apply) =>
        BoundedOperationRunner.Run(Catalog, batch.Ops, bestEffort, deadline: null, apply, static (_, _) => ["test/attempted"]);
}
