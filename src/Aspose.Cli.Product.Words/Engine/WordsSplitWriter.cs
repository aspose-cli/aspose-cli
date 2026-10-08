using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Results;
using Aspose.Words;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>
/// Stages every split document before publishing any of them, then commits the
/// set with rollback of both new and overwritten targets if a later write fails.
/// </summary>
internal sealed class WordsSplitWriter : IDisposable
{
    private readonly string _outputDirectory;
    private readonly bool _overwrite;
    private readonly OutputSet<Document> _transaction;
    private readonly List<StagedPart> _parts = [];

    public WordsSplitWriter(OutputPipeline<Document> outputs, string outputDirectory, bool overwrite)
    {
        _outputDirectory = Path.GetFullPath(outputDirectory);
        _overwrite = overwrite;
        _transaction = outputs.BeginSet([_outputDirectory], "words-split");
    }

    public void Stage(Document document, int index, string source)
    {
        string name = $"part-{index:000}.docx";
        string target = Path.Combine(_outputDirectory, name);
        WordsSavePipeline.RemoveMacrosUnlessKept(document, "docx");
        _transaction.Stage(
            target,
            _overwrite,
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
