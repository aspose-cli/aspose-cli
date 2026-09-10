using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Product.Words.Engine.Editing;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Product.Words.Operations;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using static Aspose.Cli.Product.Words.Engine.WordsEngineSupport;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>Owns Words edit orchestration and mutation diagnostics.</summary>
internal sealed class WordsMutationService
{
    private readonly ILicenseGate _licenseGate;
    private readonly SafeFileWriter _writer;
    private readonly WordsDocumentLoader _loader;
    private readonly InputSource _inputs;

    internal WordsMutationService(
        ILicenseGate licenseGate,
        SafeFileWriter writer,
        WordsDocumentLoader loader,
        InputSource inputs)
    {
        _licenseGate = licenseGate ?? throw new ArgumentNullException(nameof(licenseGate));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _inputs = inputs ?? throw new ArgumentNullException(nameof(inputs));
    }

    /// <summary>Applies a validated operation batch and commits it atomically.</summary>
    internal WordsEditResult ApplyOps(string filePath, WordsOpsBatch batch, WordsEditRequest request) =>
        WordsErrorTranslator.Execute("edit", () => ApplyOpsCore(filePath, batch, request));

    private WordsEditResult ApplyOpsCore(string filePath, WordsOpsBatch batch, WordsEditRequest request)
    {
        batch = WordsOpsParser.Prepare(batch);
        LicenseState state = _licenseGate.EnsureApplied();
        FileWritePrecondition precondition = FileWritePrecondition.Capture(filePath);
        using LoadedDocument loaded = _loader.Open(filePath, request.Password);
        bool inputHadRevisions = loaded.Document.Revisions.Count > 0;
        bool inputWasSigned = loaded.Format.HasDigitalSignature;
        int remoteResourcesBlocked = loaded.RemoteResourcesBlocked;
        bool evaluationInputTruncated = loaded.EvaluationInputTruncated;
        WordsEditResult result = WordsOpsExecutor.Apply(
            loaded,
            filePath,
            precondition,
            batch,
            request,
            _writer,
            _loader,
            _inputs);
        return result with
        {
            License = EnvelopeParts.License(state),
            Warnings = MutationWarnings(
                state,
                inputHadRevisions,
                inputWasSigned,
                remoteResourcesBlocked,
                evaluationInputTruncated),
        };
    }

    private static IReadOnlyList<Warning>? MutationWarnings(
        LicenseState state,
        bool inputHadRevisions,
        bool inputWasSigned,
        int remoteResourcesBlocked,
        bool evaluationInputTruncated)
    {
        var extra = new List<Warning>();
        if (inputHadRevisions)
        {
            extra.Add(new Warning { Code = WordsDiagnostics.TrackedChangesPresent, Message = "The document contains tracked changes.", Hint = "Disclose them and accept or reject only when explicitly requested." });
        }

        if (inputWasSigned)
        {
            extra.Add(new Warning { Code = WarningCodes.SignatureInvalidated, Message = "Editing invalidates the document's digital signature.", Hint = "Re-sign the produced document after review." });
        }

        if (remoteResourcesBlocked > 0)
        {
            extra.Add(RemoteWarning(remoteResourcesBlocked)![0]);
        }

        if (evaluationInputTruncated)
        {
            extra.Add(new Warning { Code = WarningCodes.EvalInputTruncated, Message = "Aspose.Words evaluation mode truncated the input document while loading it.", Hint = "Do not deliver this edit as complete; apply a license and retry." });
        }

        return Combine(EnvelopeParts.OutputWarnings(state), extra);
    }
}
