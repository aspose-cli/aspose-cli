using System.Buffers.Binary;
using System.Text;

namespace Aspose.Cli.TestKit;

/// <summary>
/// Creates a TrueType font whose family no system has installed, so a test can
/// prove that a font directory, and nothing else, makes the family available.
/// The glyphs come from a system TrueType font; only the naming table is new.
/// </summary>
public static class FontFixtures
{
    /// <summary>Family name of the font written by <see cref="WriteUniqueFont"/>.</summary>
    public const string UniqueFamily = "Aspose CLI Fixture Sans";

    /// <summary>Writes the uniquely named font into <paramref name="directory"/> and returns its path.</summary>
    public static string WriteUniqueFont(string directory)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "aspose-cli-fixture-sans.ttf");
        File.WriteAllBytes(path, Rename(File.ReadAllBytes(SystemTrueTypeFont()), UniqueFamily));
        return path;
    }

    private static string SystemTrueTypeFont()
    {
        string[] roots =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
            "/usr/share/fonts",
            "/System/Library/Fonts",
            "/Library/Fonts",
        ];
        string[] preferred = ["arial.ttf", "DejaVuSans.ttf", "LiberationSans-Regular.ttf", "Arial.ttf"];
        foreach (string root in roots.Where(static root => root.Length > 0 && Directory.Exists(root)))
        {
            string[] candidates = Directory.EnumerateFiles(root, "*.ttf", SearchOption.AllDirectories).ToArray();
            string? match = preferred
                .Select(name => candidates.FirstOrDefault(file =>
                    string.Equals(Path.GetFileName(file), name, StringComparison.OrdinalIgnoreCase)))
                .FirstOrDefault(static file => file is not null)
                ?? candidates.FirstOrDefault(IsTrueType);
            if (match is not null)
            {
                return match;
            }
        }
        throw new InvalidOperationException("No system TrueType font is available to derive the fixture font from.");
    }

    private static bool IsTrueType(string path)
    {
        Span<byte> header = stackalloc byte[4];
        using FileStream stream = File.OpenRead(path);
        return stream.Read(header) == 4 && BinaryPrimitives.ReadUInt32BigEndian(header) == 0x00010000;
    }

    /// <summary>Rebuilds the font with a naming table that holds only the new family.</summary>
    private static byte[] Rename(byte[] font, string family)
    {
        int count = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4));
        using var output = new MemoryStream();
        output.Write(font, 0, 12);
        output.Write(new byte[16 * count]);
        int headOffset = -1;
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> record = font.AsSpan(12 + (16 * index), 16);
            string tag = Encoding.ASCII.GetString(record[..4]);
            int offset = (int)BinaryPrimitives.ReadUInt32BigEndian(record[8..]);
            int length = (int)BinaryPrimitives.ReadUInt32BigEndian(record[12..]);
            byte[] table = tag == "name" ? NameTable(family) : font.AsSpan(offset, length).ToArray();
            int position = (int)output.Length;
            if (tag == "head")
            {
                BinaryPrimitives.WriteUInt32BigEndian(table.AsSpan(8), 0);
                headOffset = position;
            }
            output.Write(table);
            while (output.Length % 4 != 0)
            {
                output.WriteByte(0);
            }

            byte[] entry = new byte[16];
            Encoding.ASCII.GetBytes(tag, entry);
            BinaryPrimitives.WriteUInt32BigEndian(entry.AsSpan(4), Checksum(table));
            BinaryPrimitives.WriteUInt32BigEndian(entry.AsSpan(8), (uint)position);
            BinaryPrimitives.WriteUInt32BigEndian(entry.AsSpan(12), (uint)table.Length);
            output.Position = 12 + (16 * index);
            output.Write(entry);
            output.Position = output.Length;
        }

        byte[] result = output.ToArray();
        if (headOffset >= 0)
        {
            BinaryPrimitives.WriteUInt32BigEndian(
                result.AsSpan(headOffset + 8),
                unchecked(0xB1B0AFBAu - Checksum(result)));
        }
        return result;
    }

    private static byte[] NameTable(string family)
    {
        string postScript = family.Replace(" ", string.Empty, StringComparison.Ordinal) + "-Regular";
        (int Id, string Value)[] names = [(1, family), (2, "Regular"), (4, family), (6, postScript)];
        (int Platform, int Encoding, int Language, Encoding Text)[] platforms =
        [
            (1, 0, 0, Encoding.ASCII),
            (3, 1, 0x0409, Encoding.BigEndianUnicode),
        ];
        using var strings = new MemoryStream();
        using var table = new MemoryStream();
        byte[] header = new byte[6];
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(2), (ushort)(names.Length * platforms.Length));
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(4), (ushort)(6 + (12 * names.Length * platforms.Length)));
        table.Write(header);
        foreach ((int platform, int encoding, int language, Encoding text) in platforms)
        {
            foreach ((int id, string value) in names)
            {
                byte[] bytes = text.GetBytes(value);
                byte[] record = new byte[12];
                BinaryPrimitives.WriteUInt16BigEndian(record, (ushort)platform);
                BinaryPrimitives.WriteUInt16BigEndian(record.AsSpan(2), (ushort)encoding);
                BinaryPrimitives.WriteUInt16BigEndian(record.AsSpan(4), (ushort)language);
                BinaryPrimitives.WriteUInt16BigEndian(record.AsSpan(6), (ushort)id);
                BinaryPrimitives.WriteUInt16BigEndian(record.AsSpan(8), (ushort)bytes.Length);
                BinaryPrimitives.WriteUInt16BigEndian(record.AsSpan(10), (ushort)strings.Length);
                table.Write(record);
                strings.Write(bytes);
            }
        }
        strings.Position = 0;
        strings.CopyTo(table);
        return table.ToArray();
    }

    private static uint Checksum(byte[] data)
    {
        uint sum = 0;
        for (int index = 0; index < data.Length; index += 4)
        {
            uint value = 0;
            for (int offset = 0; offset < 4; offset++)
            {
                value = (value << 8) | (index + offset < data.Length ? data[index + offset] : 0u);
            }
            sum = unchecked(sum + value);
        }
        return sum;
    }
}
