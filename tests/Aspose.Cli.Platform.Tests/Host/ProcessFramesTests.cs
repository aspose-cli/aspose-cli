using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.IO.Budgets;
using Xunit;

namespace Aspose.Cli.Host.Tests;

public sealed partial class ProcessFramesTests
{
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
