using Aspose.Cli.Sdk.IO;
using Aspose.Pdf;

namespace Aspose.Cli.Product.Pdf.Engine.Editing;

/// <summary>
/// The document a <c>pdf edit</c> batch changes, as the SDK runner applies operations to it
/// and snapshots them (<see cref="PdfSnapshots"/>). It also keeps what the save needs after the
/// batch: the encryption the operations leave and the pages on which a redaction moved text.
/// </summary>
internal sealed class PdfEditModel : IOperationModel<PdfOp>
{
    private readonly Document _document;
    private readonly IReadOnlyList<PdfOp> _batch;
    private readonly bool _encryptOption;
    private readonly IReadOnlyDictionary<string, Secret>? _secrets;
    private readonly PdfPageIdentities _pages = new();
    private readonly PdfMutationHandlers _handlers;
    private readonly PdfSnapshots _snapshots;
    private readonly SortedSet<int> _movedPages = [];
    private readonly List<string> _textMoved = [];

    // The encryption the operations set, by the variables of its passwords and its permissions;
    // null for the input's own until an operation encrypts.
    private string? _encryption;

    /// <param name="loader">Opens the PDFs that operations insert.</param>
    /// <param name="inputs">Reads and charges the files that operations read.</param>
    /// <param name="document">The document being edited.</param>
    /// <param name="batch">The batch, whose later operations decide how document information is saved.</param>
    /// <param name="password">The input's password, which the output keeps unless an operation changes the encryption.</param>
    /// <param name="encryptOption">Whether <c>--encrypt</c> replaces the encryption the operations leave.</param>
    /// <param name="secrets">The operations' secrets by environment variable name.</param>
    internal PdfEditModel(
        PdfDocumentLoader loader,
        InputResourceScope inputs,
        Document document,
        IReadOnlyList<PdfOp> batch,
        Secret? password,
        bool encryptOption,
        IReadOnlyDictionary<string, Secret>? secrets)
    {
        _document = document;
        _batch = batch;
        _encryptOption = encryptOption;
        _secrets = secrets;
        OutputPassword = password;
        _handlers = new PdfMutationHandlers(loader, inputs, document, secrets, _pages, _movedPages);
        _snapshots = new PdfSnapshots(document, _pages, () => _encryption);
    }

    /// <summary>The open password of the output the operations leave.</summary>
    internal Secret? OutputPassword { get; private set; }

    /// <summary>The last encrypt_document operation, unless a later decrypt_document undid it.</summary>
    internal EncryptDocumentOp? Encryption { get; private set; }

    /// <summary>
    /// PDF-REDACT-TEXT-SHIFT: each redaction that moved the text after what it removed, as
    /// <c>'id' (op) on pages …</c>.
    /// </summary>
    internal IReadOnlyList<string> TextMoved => _textMoved;

    public OperationSnapshot Snapshot(PdfOp operation) => PdfMutationHandlers.Engine(() => operation.Accept(_snapshots));

    public void Apply(PdfOp operation)
    {
        // A later encrypt, or --encrypt, makes the save encrypt a copy, which keeps the text.
        if (operation is SetPropertiesOp metadata && Encryption is null && _document.IsEncrypted && !_encryptOption
            && !_batch.SkipWhile(op => !ReferenceEquals(op, operation)).Skip(1).Any(static later => later is EncryptDocumentOp))
        {
            RefuseTextTheEncryptionGarbles(metadata);
        }

        _movedPages.Clear();
        _snapshots.Invalidate();
        _handlers.Run(operation);
        if (_movedPages.Count > 0)
        {
            _textMoved.Add($"'{operation.Id}' ({PdfOp.Catalog.NameOf(operation)}) on page{(_movedPages.Count == 1 ? "" : "s")} {string.Join(", ", _movedPages)}");
        }

        if (operation is EncryptDocumentOp encrypt)
        {
            OutputPassword = OperationSecrets.Resolve(_secrets, encrypt.UserPasswordEnv);
            Encryption = encrypt;
            // The variables an invocation reads once stand for their passwords.
            _encryption = $"{encrypt.UserPasswordEnv}|{encrypt.OwnerPasswordEnv}|{encrypt.Permissions}";
        }
        else if (operation is DecryptDocumentOp)
        {
            OutputPassword = null;
            Encryption = null;
            _encryption = null;
        }
    }

    public IReadOnlyList<string> AttemptedTargets(PdfOp operation) => operation.Accept(PdfAttemptedTargets.Instance);

    public NoEffect? Explain(PdfOp operation) => operation switch
    {
        // Like a verification issue, it never repeats the pattern.
        RedactTextOp => new NoEffect(NoEffectReasons.NoMatch,
            $"Operation {operation.Id} (redact_text) matched no text on the pages searched, so nothing was redacted there.")
        {
            Hint = "Search the pages with 'pdf search' and the same pattern. When the text shows on the page but is not found, "
                + "its extracted text differs, for example by spaces between characters: match it with a regular expression "
                + "that allows them (\\s*), or cover it with redact_area.",
        },
        FlattenFormsOp => new NoEffect(NoEffectReasons.NoMatch,
            $"Operation {operation.Id} (flatten_forms) found no form field to flatten."),
        DecryptDocumentOp => new NoEffect(NoEffectReasons.NoChange,
            $"Operation {operation.Id} (decrypt_document) changed nothing: the document is not encrypted."),
        OptimizeDocumentOp => new NoEffect(NoEffectReasons.NoChange,
            $"Operation {operation.Id} (optimize_document) changed no image or font. Removing unused objects and compressing streams "
            + "may still make the file smaller; they store the same content and change no object."),
        _ => null,
    };

    /// <summary>
    /// PDF-ENCRYPTED-INFO-TEXT: the engine writes document information beyond Latin-1 into an
    /// encrypted file garbled, and the encryption the input keeps when no later operation
    /// encrypts cannot be applied to a copy, because its passwords are not all known.
    /// </summary>
    private static void RefuseTextTheEncryptionGarbles(SetPropertiesOp metadata)
    {
        string[] values =
        [
            metadata.Title ?? string.Empty,
            metadata.Author ?? string.Empty,
            metadata.Subject ?? string.Empty,
            metadata.Keywords ?? string.Empty,
            .. metadata.Custom?.Values ?? [],
        ];
        if (values.Any(static value => value.Any(static character => character > '\u00FF')))
        {
            throw new OperationInvalidException(
                "the input stays encrypted, and the PDF engine garbles document information with characters beyond Latin-1 that it writes into an encrypted file",
                "Add an encrypt_document operation after set_properties in the same batch: the batch then encrypts a copy that keeps the text.");
        }
    }
}
