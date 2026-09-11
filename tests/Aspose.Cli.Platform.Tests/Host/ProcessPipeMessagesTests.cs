using System.Buffers.Binary;
using Aspose.Cli.Host.LocalServices;
using Xunit;

namespace Aspose.Cli.Host.Tests;

public sealed class ProcessPipeMessagesTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(ProcessPipeMessages.MaximumBytes + 1)]
    public async Task Read_RejectsLengthBeforeReadingPayload(int length)
    {
        byte[] header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, length);
        using var input = new MemoryStream(header);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            ProcessPipeMessages.ReadAsync<string>(input, CancellationToken.None));
    }

    [Fact]
    public async Task Read_RejectsTruncatedPayload()
    {
        using var input = new MemoryStream([10, 0, 0, 0, 123]);
        await Assert.ThrowsAsync<EndOfStreamException>(() =>
            ProcessPipeMessages.ReadAsync<string>(input, CancellationToken.None));
    }

    [Fact]
    public async Task Write_RejectsOversizedValueWithoutPublishingAFrame()
    {
        using var output = new MemoryStream();
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            ProcessPipeMessages.WriteAsync(output, new string('x', ProcessPipeMessages.MaximumBytes), CancellationToken.None));
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public async Task Frames_PreserveUnicodeNullAndEmptyValues()
    {
        using var pipe = new MemoryStream();
        string?[] expected = [null, "", "arbitrary \" \u03bb \u4e2d\nvalue"];
        foreach (string? value in expected)
        {
            await ProcessPipeMessages.WriteAsync(pipe, new Message(value), CancellationToken.None);
        }
        pipe.Position = 0;
        foreach (string? value in expected)
        {
            Assert.Equal(value, (await ProcessPipeMessages.ReadAsync<Message>(pipe, CancellationToken.None)).Value);
        }
        Assert.Equal(pipe.Length, pipe.Position);
    }

    private sealed record Message(string? Value);
}
