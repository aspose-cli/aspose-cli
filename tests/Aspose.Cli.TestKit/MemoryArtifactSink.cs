using System.Text;
using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.TestKit;

/// <summary>Keeps every artifact a product view writes, by relative path, in memory.</summary>
public sealed class MemoryArtifactSink : IViewArtifactSink
{
    private readonly Dictionary<string, byte[]> _artifacts = new(StringComparer.Ordinal);

    /// <summary>Relative paths of the written artifacts, in ordinal order.</summary>
    public IReadOnlyList<string> Paths => _artifacts.Keys.Order(StringComparer.Ordinal).ToArray();

    public void Write(string relativePath, Action<Stream> contentWriter)
    {
        ArgumentNullException.ThrowIfNull(contentWriter);
        using var stream = new MemoryStream();
        contentWriter(stream);
        _artifacts.Add(relativePath, stream.ToArray());
    }

    public void WriteText(string relativePath, string content) =>
        _artifacts.Add(relativePath, Encoding.UTF8.GetBytes(content));

    public byte[] Bytes(string relativePath) => _artifacts[relativePath];

    public string Text(string relativePath) => Encoding.UTF8.GetString(Bytes(relativePath));
}
