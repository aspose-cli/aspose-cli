using System.Globalization;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Pdf;
using static Aspose.Cli.Product.Pdf.Engine.PdfTextMatch;

namespace Aspose.Cli.Product.Pdf.Engine.Editing;

/// <summary>
/// The document a <c>pdf edit</c> batch changes, as the SDK runner applies operations to it
/// and snapshots them (<see cref="PdfSnapshots"/>). It also keeps what the save needs after the
/// batch: the encryption the operations leave, what each redaction took beside its selection or
/// moved, and the redactions whose text the output must no longer hold.
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
    private readonly List<(int Page, int Lines, int Characters)> _besidePages = [];
    private readonly List<string> _beside = [];
    private readonly SortedSet<int> _numberedPages = [];
    private readonly List<(string Id, IReadOnlyList<int> Pages)> _numberedTwice = [];
    private readonly List<PdfRedactionAudit.Redaction> _redactions = [];

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
        _handlers = new PdfMutationHandlers(loader, inputs, document, secrets, _pages, _movedPages, _besidePages, _numberedPages);
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

    /// <summary>
    /// What each redaction's cover took beside its selection, by page, as
    /// <c>'id' (op) on page N: C character(s) on L neighbouring line(s)</c>; never the text.
    /// </summary>
    internal IReadOnlyList<string> TextBeside => _beside;

    /// <summary>
    /// Each <c>add_page_numbers</c> that numbered pages whose header or footer already showed a
    /// page number, with those pages as the operation saw them.
    /// </summary>
    internal IReadOnlyList<(string Id, IReadOnlyList<int> Pages)> NumberedTwice => _numberedTwice;

    /// <summary>The ids of the <c>redact_text</c> operations the batch applied, in batch order.</summary>
    internal IReadOnlyList<string> Redactions => [.. _redactions.Select(static redaction => redaction.Id)];

    /// <summary>
    /// Where the reopened output still holds the text of a <c>redact_text</c> the batch applied
    /// (<see cref="PdfRedactionAudit"/>); empty when it holds none.
    /// </summary>
    /// <param name="output">The reopened output.</param>
    /// <param name="properties">The document's properties before the save (<see cref="PdfRedactionAudit.Properties"/>).</param>
    /// <param name="license">The license state.</param>
    /// <param name="deadline">The deadline of the invocation.</param>
    internal IReadOnlyList<VerificationIssue> RedactedTextLeft(
        Document output, IReadOnlyDictionary<string, string> properties, LicenseState license, OperationDeadline? deadline) =>
        _redactions.Count == 0 ? [] : PdfRedactionAudit.Check(output, properties, _redactions, _pages.Order(_document), license, deadline);

    public OperationSnapshot Snapshot(PdfOp operation) => PdfMutationHandlers.Engine(() => operation.Accept(_snapshots).Take());

    public void Apply(PdfOp operation)
    {
        // A later encrypt, or --encrypt, makes the save encrypt a copy, which keeps the text.
        if (operation is SetPropertiesOp metadata && Encryption is null && _document.IsEncrypted && !_encryptOption
            && !_batch.SkipWhile(op => !ReferenceEquals(op, operation)).Skip(1).Any(static later => later is EncryptDocumentOp))
        {
            RefuseTextTheEncryptionGarbles(metadata);
        }

        _movedPages.Clear();
        _besidePages.Clear();
        _numberedPages.Clear();
        _snapshots.Invalidate();
        // The pages a redaction covers, by identity, which later operations may renumber.
        PdfPageIdentity[]? redacted = operation is RedactTextOp { Pages: { } selection }
            ? [.. PdfMutationSupport.Resolve(_document, selection).Select(number => _pages.Of(_document.Pages[number]))]
            : null;
        _handlers.Run(operation);
        string name = PdfOp.Catalog.NameOf(operation);
        if (_movedPages.Count > 0)
        {
            _textMoved.Add($"'{operation.Id}' ({name}) on page{(_movedPages.Count == 1 ? "" : "s")} {string.Join(", ", _movedPages)}");
        }

        foreach (IGrouping<int, (int Page, int Lines, int Characters)> page in _besidePages.GroupBy(static entry => entry.Page))
        {
            int lines = page.Sum(static entry => entry.Lines), characters = page.Sum(static entry => entry.Characters);
            _beside.Add(string.Create(CultureInfo.InvariantCulture,
                $"'{operation.Id}' ({name}) on page {page.Key}: {characters} character{(characters == 1 ? "" : "s")} on {lines} neighbouring line{(lines == 1 ? "" : "s")}"));
        }

        if (_numberedPages.Count > 0)
        {
            _numberedTwice.Add((operation.Id!, [.. _numberedPages]));
        }

        if (operation is RedactTextOp redaction)
        {
            _redactions.Add(new PdfRedactionAudit.Redaction(operation.Id!, TextPattern(redaction.Find, redaction.Regex, redaction.CaseSensitive), redacted));
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

    public IReadOnlyList<string> AttemptedTargets(PdfOp operation) => operation.Accept(_snapshots).Targets;

    /// <summary>The identity of each page, in page order, which numbers what the batch changed on it.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<object>> Positions() =>
        new Dictionary<string, IReadOnlyList<object>>(StringComparer.Ordinal) { [PdfSnapshots.PageUnit] = _pages.Order(_document) };

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
        foreach ((string key, string value) in PdfPropertyNames.Written(metadata))
        {
            if (value.Any(static character => character > '\u00FF'))
            {
                string name = PdfPropertyNames.Standard(key)?.Name ?? key;
                throw new OperationInvalidException(
                    $"the {name} '{value}' has characters beyond Latin-1, which the PDF engine writes garbled into an encrypted file, and the input stays encrypted",
                    "Add encrypt_document after set_properties in the same batch, or pass --encrypt-env: the edit then encrypts a copy that keeps the text. "
                    + "Or add decrypt_document before set_properties to save the output without encryption, or use Latin-1 text.");
            }
        }
    }
}
