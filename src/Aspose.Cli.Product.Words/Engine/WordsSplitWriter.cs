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
    /// <summary>The format every part is written in.</summary>
    private static readonly FormatDescriptor Docx = WordsFormats.Definitions.Named(FormatUse.Convert, "docx")!;

    private readonly ResolvedDirectory _output;
    private readonly string _stem;
    private readonly OutputSet<Document> _transaction;
    private readonly List<StagedPart> _parts = [];
    private readonly Secret? _password;
    private Dictionary<int, string> _paths = [];

    /// <summary>
    /// Begins the set of parts of <paramref name="input"/>, named after its file name, each
    /// opening with <paramref name="password"/> when one is given.
    /// </summary>
    public WordsSplitWriter(OutputPipeline<Document> outputs, ResolvedDirectory output, string input, Secret? password)
    {
        _output = output;
        _password = password;
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
            staged => document.Save(staged, WordsSavePipeline.Options(Docx.Id, _password)),
            encrypted: _password is not null);
        _parts.Add(new StagedPart(index, source, target));
    }

    public IReadOnlyList<WordsSplitOutput> Commit()
    {
        IReadOnlyList<PublishedFile> files = _transaction.Commit();
        return _parts.Select((part, index) => new WordsSplitOutput
        {
            Index = part.Index,
            Source = part.Source,
            Output = FileObjects.Output(files[index], Docx, encrypted: _password is not null),
        }).ToArray();
    }

    public void Dispose() => _transaction.Dispose();

    private sealed class StagedPart(int index, string source, string target)
    {
        public int Index { get; } = index;
        public string Source { get; } = source;
        public string Target { get; } = target;
    }
}
