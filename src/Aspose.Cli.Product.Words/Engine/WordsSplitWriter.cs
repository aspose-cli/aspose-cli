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
    internal static readonly FormatDescriptor Docx = WordsFormats.SplitParts[0];

    private readonly ResolvedDirectory _output;
    private readonly string _stem;
    private readonly OutputSet<Document> _transaction;
    private readonly List<Func<OutputInfo, WordsPartOutput>> _parts = [];
    private readonly OutputProtection _protection;
    private Dictionary<int, string> _paths = [];

    /// <summary>
    /// Begins the set of parts of <paramref name="input"/>, named after its file name, each
    /// protected as <paramref name="protection"/> decides.
    /// </summary>
    public WordsSplitWriter(OutputPipeline<Document> outputs, ResolvedDirectory output, string input, OutputProtection protection)
    {
        _output = output;
        _protection = protection;
        _stem = Path.GetFileNameWithoutExtension(input);
        _transaction = outputs.BeginSet([output.Path], "words-split");
    }

    /// <summary>Names the parts numbered <paramref name="numbers"/> before any is staged.</summary>
    public void Number(IReadOnlyList<int> numbers) =>
        _paths = numbers.Zip(_output.Parts(_stem, "docx", numbers)).ToDictionary(static pair => pair.First, static pair => pair.Second);

    /// <summary>
    /// Stages the part numbered <paramref name="number"/>, one of those <see cref="Number"/> named;
    /// <paramref name="describe"/> states what of the document it holds beside its file.
    /// </summary>
    public void Stage(Document document, int number, Func<OutputInfo, WordsPartOutput> describe)
    {
        string target = _paths[number];
        WordsSavePipeline.RemoveMacrosUnlessKept(document, "docx");
        _transaction.Stage(
            target,
            _output.Overwrite,
            document,
            staged => document.Save(staged, WordsSavePipeline.Options(Docx.Id, _protection.Password)),
            encrypted: _protection.Encrypted);
        _parts.Add(describe);
    }

    public IReadOnlyList<WordsPartOutput> Commit()
    {
        IReadOnlyList<PublishedFile> files = _transaction.Commit();
        return _parts.Select((describe, index) => describe(FileObjects.Output(files[index], Docx, encrypted: _protection.Encrypted))).ToArray();
    }

    public void Dispose() => _transaction.Dispose();
}
