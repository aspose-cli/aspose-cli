using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Results;
using Aspose.Words;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>
/// Stages every split document before publishing any of them, then commits the
/// set with rollback of both new and overwritten targets if a later write fails.
/// </summary>
internal sealed class WordsSplitWriter : IDisposable
{
    private readonly ResolvedDirectory _output;
    private readonly string _stem;
    private readonly OutputSet<Document> _transaction;
    private readonly List<StagedPart> _parts = [];
    private Dictionary<int, string> _paths = [];

    /// <summary>Begins the set of parts of <paramref name="input"/>, named after its file name.</summary>
    public WordsSplitWriter(OutputPipeline<Document> outputs, ResolvedDirectory output, string input)
    {
        _output = output;
        _stem = Path.GetFileNameWithoutExtension(input);
        _transaction = outputs.BeginSet([output.Path], "words-split");
    }

    /// <summary>Names the parts numbered <paramref name="numbers"/> before any is staged.</summary>
    public void Number(IReadOnlyList<int> numbers) =>
        _paths = numbers.Zip(_output.Parts(_stem, "docx", numbers)).ToDictionary(static pair => pair.First, static pair => pair.Second);

    /// <summary>Stages the part numbered <paramref name="number"/>, one of those <see cref="Number"/> named.</summary>
    public void Stage(Document document, int number, string source)
    {
        string target = _paths[number];
        int index = _parts.Count + 1;
        WordsSavePipeline.RemoveMacrosUnlessKept(document, "docx");
        _transaction.Stage(
            target,
            _output.Overwrite,
            document,
            staged => document.Save(staged, SaveFormat.Docx));
        _parts.Add(new StagedPart(index, source, target));
    }

    public IReadOnlyList<SplitOutput> Commit()
    {
        IReadOnlyList<long> sizes = _transaction.Commit();
        for (int index = 0; index < _parts.Count; index++)
        {
            _parts[index].Size = sizes[index];
        }

        return _parts.Select(static part => new SplitOutput
        {
            Index = part.Index,
            Source = part.Source,
            Output = new OutputInfo
            {
                Path = part.Target,
                Format = "docx",
                SizeBytes = part.Size,
            },
        }).ToArray();
    }

    public void Dispose() => _transaction.Dispose();

    private sealed class StagedPart(int index, string source, string target)
    {
        public int Index { get; } = index;
        public string Source { get; } = source;
        public string Target { get; } = target;
        public long Size { get; set; }
    }
}
