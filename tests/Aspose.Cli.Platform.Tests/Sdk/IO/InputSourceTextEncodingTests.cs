using System.Text;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.IO.Budgets;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

/// <summary>
/// Text input is decoded in the encoding its byte order mark names, UTF-8 without one, and the
/// mark is not part of the text; bytes that encoding cannot decode are refused, after a mark as
/// without one, also when they arrive a byte at a time, as from a pipe.
/// </summary>
public sealed class InputSourceTextEncodingTests
{
    // A, e with an acute accent, the Chinese for "Han" and an emoji outside the Basic Multilingual Plane.
    private const string Text = "Aé汉😀";

    public static TheoryData<string, byte[]> Decodable => new()
    {
        { "utf-8", Encoding.UTF8.GetBytes(Text) },
        { "utf-8 with a mark", [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(Text)] },
        { "utf-16le with a mark", [0xFF, 0xFE, .. Encoding.Unicode.GetBytes(Text)] },
        { "utf-16be with a mark", [0xFE, 0xFF, .. Encoding.BigEndianUnicode.GetBytes(Text)] },
        { "utf-32le with a mark", [0xFF, 0xFE, 0, 0, .. new UTF32Encoding(bigEndian: false, byteOrderMark: false).GetBytes(Text)] },
        { "utf-32be with a mark", [0, 0, 0xFE, 0xFF, .. new UTF32Encoding(bigEndian: true, byteOrderMark: false).GetBytes(Text)] },
    };

    public static TheoryData<string, byte[]> Undecodable => new()
    {
        { "utf-8", [0x41, 0xC3, 0xDC, 0x42] },
        { "utf-8 with a mark", [0xEF, 0xBB, 0xBF, 0x41, 0xC3, 0xDC, 0x42] },
        { "utf-8 with a mark, cut inside a character", [0xEF, 0xBB, 0xBF, 0x41, 0xE6, 0xB1] },
        { "utf-16le with a mark, a lone surrogate", [0xFF, 0xFE, 0x41, 0x00, 0x00, 0xD8, 0x42, 0x00] },
        { "utf-16be with a mark, an odd byte", [0xFE, 0xFF, 0x00, 0x41, 0x00] },
        { "utf-32le with a mark, beyond U+10FFFF", [0xFF, 0xFE, 0, 0, 0x00, 0x00, 0x11, 0x00] },
    };

    [Theory]
    [MemberData(nameof(Decodable))]
    public void Text_IsDecodedWithoutItsMark(string encoding, byte[] bytes) =>
        Assert.True(Text == Read(new MemoryStream(bytes)), encoding);

    /// <summary>A mark that arrives a byte at a time, as a pipe may deliver it, is still a mark.</summary>
    [Theory]
    [MemberData(nameof(Decodable))]
    public void TextThatArrivesAByteAtATime_IsDecodedWithoutItsMark(string encoding, byte[] bytes) =>
        Assert.True(Text == Read(new Trickle(bytes)), encoding);

    [Theory]
    [MemberData(nameof(Undecodable))]
    public void BytesTheEncodingCannotDecode_AreRefused(string encoding, byte[] bytes)
    {
        foreach (Stream input in new Stream[] { new MemoryStream(bytes), new Trickle(bytes) })
        {
            CliException error = Assert.Throws<CliException>(() => Read(input));
            Assert.True(ErrorCodes.InputEncodingInvalid == error.Code, encoding);
            Assert.Equal("stdin-decode", error.Details!["phase"]!.GetValue<string>());
        }
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 0xEF, 0xBB, 0xBF })]
    [InlineData(new byte[] { 0xFF, 0xFE })]
    public void NoTextAfterAMark_IsEmpty(byte[] bytes) => Assert.Equal(string.Empty, Read(new MemoryStream(bytes)));

    /// <summary>A character whose bytes straddle two reads is decoded across them, after a mark as without one.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ACharacterAcrossTwoReads_IsDecoded(bool marked)
    {
        // The 4096 bytes of the first read end inside the Chinese character.
        string text = new string('a', marked ? 4091 : 4094) + "汉" + new string('b', 5000);
        byte[] bytes = [.. marked ? (byte[])[0xEF, 0xBB, 0xBF] : [], .. Encoding.UTF8.GetBytes(text)];

        Assert.Equal(text, Read(new MemoryStream(bytes)));
    }

    private static string Read(Stream input)
    {
        using OperationDeadline deadline = OperationDeadline.Start(null);
        var budgets = new ResourceBudgetLedger(deadline, new Dictionary<string, long>(StringComparer.Ordinal)
        {
            [ResourceBudgetKinds.StandardInputBytes] = 1 << 20,
            [ResourceBudgetKinds.DecodedTextCharacters] = 1 << 20,
        });
        return budgets.Inputs.ReadStandardInputText(input);
    }

    /// <summary>A stream that returns one byte per read, as a pipe may.</summary>
    private sealed class Trickle(byte[] bytes) : MemoryStream(bytes)
    {
        public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(count, 1));

        public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(buffer.Length, 1)]);
    }
}
