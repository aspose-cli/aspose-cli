using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.ViewerService;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Host.LocalServices;
using Xunit;

namespace Aspose.Cli.Host.Tests;

public sealed partial class ProcessFramesTests
{
    [Theory]
    [InlineData("", ResourceBudgetDefaults.DefaultInputBytes)]
    [InlineData(",\"maxInputBytes\":0", 0L)]
    [InlineData(",\"maxInputBytes\":null", null)]
    public void ViewerInputBudget_UsesItsWireDefaultOnlyWhenOmitted(string field, long? expected)
    {
        string json = "{\"file\":\"input.xlsx\"" + field + "}";
        if (expected is null)
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(json, ViewerServiceJsonContext.Default.ViewerOpenRequest));
        }
        else
        {
            Assert.Equal(expected.Value, JsonSerializer.Deserialize(json, ViewerServiceJsonContext.Default.ViewerOpenRequest)!.MaxInputBytes);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(ProcessFrames.MaximumBytes + 1)]
    public async Task Read_RejectsLengthBeforeReadingPayload(int length)
    {
        byte[] header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, length);
        using var input = new MemoryStream(header);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            ProcessFrames.ReadAsync(input, TestFrameContext.Default.String, CancellationToken.None));
    }

    [Fact]
    public async Task Read_RejectsTruncatedPayload()
    {
        using var input = new MemoryStream([10, 0, 0, 0, 123]);
        await Assert.ThrowsAsync<EndOfStreamException>(() =>
            ProcessFrames.ReadAsync(input, TestFrameContext.Default.String, CancellationToken.None));
    }

    [Fact]
    public async Task Write_RejectsOversizedValueWithoutPublishingAFrame()
    {
        using var output = new MemoryStream();
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            ProcessFrames.WriteAsync(output, new string('x', ProcessFrames.MaximumBytes), TestFrameContext.Default.String,
                CancellationToken.None));
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public async Task Frames_PreserveUnicodeNullAndEmptyValues()
    {
        using var pipe = new MemoryStream();
        string?[] expected = [null, "", "arbitrary \" \u03bb \u4e2d\nvalue"];
        foreach (string? value in expected)
        {
            await ProcessFrames.WriteAsync(pipe, new FrameMessage(value), TestFrameContext.Default.FrameMessage, CancellationToken.None);
        }
        pipe.Position = 0;
        foreach (string? value in expected)
        {
            Assert.Equal(value, (await ProcessFrames.ReadAsync(pipe, TestFrameContext.Default.FrameMessage, CancellationToken.None)).Value);
        }
        Assert.Equal(pipe.Length, pipe.Position);
    }

    [Fact]
    public async Task Read_ReservesMemoryBeforeReadingAPayload()
    {
        using var input = new MemoryStream([12, 0, 0, 0]);
        await Assert.ThrowsAsync<OutOfMemoryException>(() =>
            ProcessFrames.ReadAsync(input, TestFrameContext.Default.FrameMessage, CancellationToken.None,
                reserve: _ => throw new OutOfMemoryException("Synthetic budget refusal.")));
        Assert.Equal(4, input.Position);
    }

    [Fact]
    public async Task EndOfStream_IsAllowedOnlyBetweenFrames()
    {
        using var empty = new MemoryStream();
        Assert.Null(await ProcessFrames.ReadOrEndAsync(empty, TestFrameContext.Default.FrameMessage, CancellationToken.None));
        using var truncated = new MemoryStream([10, 0]);
        await Assert.ThrowsAsync<EndOfStreamException>(() =>
            ProcessFrames.ReadOrEndAsync(truncated, TestFrameContext.Default.FrameMessage, CancellationToken.None));
    }

    /// <summary>
    /// The frames the Host's processes exchange keep the bytes the reflection serializer with the
    /// web defaults wrote: camel case, nulls left out, and the default encoder.
    /// </summary>
    [Fact]
    public async Task Frames_AreByteIdenticalToTheWebDefaults()
    {
        var secret = new WireSecret("päss \"w<o>rd\" 中");
        var request = new RenderWorkerRequest(MaxInputBytes: 4096)
        {
            Id = 7,
            Source = @"C:\work\copy\report é <1>.docx",
            SourceOrigin = @"C:\docs\report é <1>.docx",
            Output = @"C:\work\out",
            MaxPartCount = 8,
            TimeoutMs = 120_000,
            ExpiresAtTick = 9_876_543_210,
            Product = "words",
            Password = secret,
            EvaluationRequested = true,
            RequesterLicense = new RequesterLicense
            {
                WorkDirectory = @"C:\work",
                Variables = new Dictionary<string, WireSecret> { ["ASPOSE_LICENSE"] = secret },
            },
            FontDirectories = [@"C:\fonts", "λ"],
            Presentation = true,
        };
        await AssertFrame(request, (stream, value) => ProcessFrames.WriteAsync(stream, value, RenderWorkerJsonContext.Default.RenderWorkerRequest, CancellationToken.None));
        await AssertFrame(new RenderWorkerRequest { Id = 1, Source = "a", Output = "b", MaxPartCount = 1, TimeoutMs = 1 },
            (stream, value) => ProcessFrames.WriteAsync(stream, value, RenderWorkerJsonContext.Default.RenderWorkerRequest, CancellationToken.None));
        await AssertFrame(new InvocationStartMessage(1, @"C:\work", null, 123, EvaluationRequested: true),
            (stream, value) => ProcessFrames.WriteAsync(stream, value, InvocationJsonContext.Default.InvocationStartMessage, CancellationToken.None));
        await AssertFrame(new InvocationStartMessage(1, null, @"C:\l.lic", null),
            (stream, value) => ProcessFrames.WriteAsync(stream, value, InvocationJsonContext.Default.InvocationStartMessage, CancellationToken.None));
        await AssertFrame(new EnvironmentValueRequest("ASPOSE_PASSWORD", 1024),
            (stream, value) => ProcessFrames.WriteAsync(stream, value, InvocationJsonContext.Default.EnvironmentValueRequest, CancellationToken.None));
        await AssertFrame(new EnvironmentValueReply("välue \"<&>\""),
            (stream, value) => ProcessFrames.WriteAsync(stream, value, InvocationJsonContext.Default.EnvironmentValueReply, CancellationToken.None));
        await AssertFrame(new EnvironmentValueReply(null, 70_000),
            (stream, value) => ProcessFrames.WriteAsync(stream, value, InvocationJsonContext.Default.EnvironmentValueReply, CancellationToken.None));
        await AssertFrame(new ServiceStartSecrets
        {
            WorkDirectory = @"C:\work",
            LicensePath = @"C:\l.lic",
            ServiceToken = secret,
            ServiceNonce = "nonce",
        }, (stream, value) => ProcessFrames.WriteAsync(stream, value, LocalServiceJsonContext.Default.ServiceStartSecrets, CancellationToken.None));
        await AssertFrame(
            new LocalServiceControlRequest(1, "request", "viewer", "instance", "nonce", secret, "open",
                Path: @"C:\docs\report é.docx", Payload: "{\"file\":\"a\"}", ExpiresAtTick: 42),
            (stream, value) => LocalServiceControlCodec.WriteAsync(stream, value, CancellationToken.None));
        await AssertFrame(
            new LocalServiceControlResponse(1, "request", "viewer", "instance", "nonce", Ok: false, Message: "No <file>.",
                Result: JsonSerializer.SerializeToElement(new JsonObject { ["code"] = "X", ["details"] = new JsonObject { ["suggestions"] = new JsonArray("a", "é") } })),
            (stream, value) => LocalServiceControlCodec.WriteAsync(stream, value, CancellationToken.None));
        await AssertFrame(
            new LocalServiceControlResponse(1, "request", "viewer", "instance", "nonce", Ok: true,
                Result: JsonSerializer.SerializeToElement(new JsonObject { ["pid"] = 42, ["url"] = "http://127.0.0.1:1/" })),
            (stream, value) => LocalServiceControlCodec.WriteAsync(stream, value, CancellationToken.None));

        // A running viewer service may be older than the CLI asking it: its failure result is the
        // one payload that crosses versions, so its bytes stay fixed.
        JsonElement failure = ViewerFailureResult();
        Assert.Equal(
            "{\"code\":\"SHEET_NOT_FOUND\",\"exit\":" + (int)Aspose.Cli.Sdk.Errors.ExitCode.InputError
                + ",\"message\":\"No sheet Salez.\",\"hint\":\"Use one of details.suggestions.\""
                + ",\"details\":{\"requested\":\"Salez\",\"suggestions\":[\"Sales\",\"Sale\"]},\"docs\":\"cells/sheets\"}",
            failure.GetRawText());
        await AssertFrame(
            new LocalServiceControlResponse(1, "request", "viewer", "instance", "nonce", Ok: false,
                Message: "No sheet Salez.", Result: failure),
            (stream, value) => LocalServiceControlCodec.WriteAsync(stream, value, CancellationToken.None));
    }

    private static JsonElement ViewerFailureResult() => JsonSerializer.SerializeToElement(
        new WireError(
            "SHEET_NOT_FOUND",
            (int)Aspose.Cli.Sdk.Errors.ExitCode.InputError,
            "No sheet Salez.",
            "Use one of details.suggestions.",
            new JsonObject { ["requested"] = "Salez", ["suggestions"] = new JsonArray("Sales", "Sale") },
            "cells/sheets"),
        ViewerServiceJsonContext.Default.WireError);

    /// <summary>A frame reads its property names in any case and its numbers from strings.</summary>
    [Fact]
    public async Task Frames_ReadNamesInAnyCaseAndNumbersFromStrings()
    {
        byte[] json = """{"ID":"3","Source":"a","OUTPUT":"b","maxPartCount":"8","timeoutMs":"100","maxInputBytes":"12"}"""u8.ToArray();
        using var input = new MemoryStream();
        input.Write(BitConverter.GetBytes(json.Length));
        input.Write(json);
        input.Position = 0;

        RenderWorkerRequest request = await ProcessFrames.ReadAsync(
            input, RenderWorkerJsonContext.Default.RenderWorkerRequest, CancellationToken.None);

        Assert.Equal((3, "a", "b", 8, 100, 12L),
            (request.Id, request.Source, request.Output, request.MaxPartCount, request.TimeoutMs, request.MaxInputBytes));
    }

    private static readonly JsonSerializerOptions WebDefaults = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    private static async Task AssertFrame<T>(T value, Func<Stream, T, Task> write)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(value, WebDefaults);
        byte[] expected = [.. BitConverter.GetBytes(payload.Length), .. payload];
        using var actual = new MemoryStream();
        await write(actual, value);
        Assert.Equal(Convert.ToHexString(expected), Convert.ToHexString(actual.ToArray()));
    }

    private sealed record FrameMessage(string? Value);

    [JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(FrameMessage))]
    [JsonSerializable(typeof(string))]
    private sealed partial class TestFrameContext : JsonSerializerContext;
}
