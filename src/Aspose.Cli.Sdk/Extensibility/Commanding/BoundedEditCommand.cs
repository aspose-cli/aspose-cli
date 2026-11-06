using System.CommandLine;
using System.Text.Json;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.IO.Paths;
using Aspose.Cli.Sdk.Operations;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// A product's one-line shorthand for common operations (<c>--set</c>). Each directive
/// compiles to exactly one operation, appended after the <c>--ops</c> document.
/// </summary>
/// <param name="Description">Help text naming the directive grammar with an example.</param>
/// <param name="Parse">Compiles one directive or rejects it with <c>OPTION_INVALID</c>.</param>
/// <param name="CreateBatch">Creates the product document for directive-only batches.</param>
public sealed record SetDirectiveGrammar<TOp, TBatch>(
    string Description,
    Func<string, TOp> Parse,
    Func<IReadOnlyList<TOp>, TBatch> CreateBatch)
    where TOp : BoundedOperation
    where TBatch : BoundedOperationEnvelope<TOp>;

/// <summary>What a product supplies to the shared bounded-edit command skeleton.</summary>
/// <remarks>The operation vocabulary is the one <typeparamref name="TOp"/> declares.</remarks>
public sealed record BoundedEditDefinition<TOp, TBatch>
    where TOp : BoundedOperation, IOperationVocabulary<TOp>
    where TBatch : BoundedOperationEnvelope<TOp>
{
    /// <summary>The product's source-generated contract serializer.</summary>
    public required ProductJsonDefinition Contracts { get; init; }

    /// <summary>
    /// The formats the edit writes; the output's extension, or the input's when no
    /// <c>--out</c> is named, chooses among them.
    /// </summary>
    public required IReadOnlyList<FormatDescriptor> Writes { get; init; }

    /// <summary>The <c>--set</c> shorthand, or null when the product has none.</summary>
    public SetDirectiveGrammar<TOp, TBatch>? SetDirectives { get; init; }

    /// <summary>Help for <c>--verify</c>, or null when the product has no staged verification.</summary>
    public string? VerifyDescription { get; init; }

    /// <summary>
    /// The operation of the vocabulary that encrypts (<see cref="EncryptsOutputAttribute"/>) as the
    /// edit's output password option does, built from the environment variable that holds the
    /// password, so that the file it writes grants what that option's does; null when the edit
    /// cannot only encrypt. An edit given that option and no operation shows it as the way to only
    /// encrypt.
    /// </summary>
    public Func<string, TOp>? EncryptOnly { get; init; }

    /// <summary>
    /// How the edit encrypts with only an owner password, so that the file it writes still opens
    /// without a password; null when the edit cannot. The hint of a copy that lost an owner password
    /// alone shows it.
    /// </summary>
    public OwnerPasswordEncryption<TOp>? EncryptOwnerOnly { get; init; }
}

/// <summary>
/// How an edit encrypts with only an owner password: the operation of its vocabulary that encrypts
/// (<see cref="EncryptsOutputAttribute"/>), built from the environment variable that holds the owner
/// password, which grants readers no permission; the field of that operation that grants them; and
/// the product's command, with its options, that reads what a document grants, so a caller can grant
/// what the original did.
/// </summary>
/// <param name="Operation">Builds the operation from the variable that holds the owner password.</param>
/// <param name="PermissionsField">The operation's field that grants readers permissions, such as <c>permissions</c>.</param>
/// <param name="PermissionsCommand">The command of the product, below it, that reads what a document grants, such as <c>inspect</c>.</param>
/// <param name="PermissionsOptions">The options and values that command reads the permissions with.</param>
/// <typeparam name="TOp">The edit's operation vocabulary.</typeparam>
public sealed record OwnerPasswordEncryption<TOp>(
    Func<string, TOp> Operation,
    string PermissionsField,
    string PermissionsCommand,
    IReadOnlyList<(string Name, string Value)> PermissionsOptions)
    where TOp : BoundedOperation, IOperationVocabulary<TOp>;

/// <summary>One fully resolved bounded-edit invocation, ready for the product request.</summary>
/// <param name="Input">The edited document.</param>
/// <param name="Batch">The validated, identified and path-normalized operation document.</param>
/// <param name="Output">Where, how and in which format the result is published.</param>
/// <param name="Options">Precondition, dry-run and best-effort semantics.</param>
/// <param name="Verify">Whether staged verification was requested.</param>
/// <param name="Secrets">
/// The operations' secrets by environment variable name; a missing or empty variable is absent,
/// and the operation that names it fails through <see cref="OperationSecrets.Resolve"/>.
/// </param>
/// <param name="Password">The password that opens the edited document, or null when none was given.</param>
/// <param name="EncryptPassword">
/// The password the output opens with (<c>--encrypt</c>), or null when none was given or the
/// edit declares no output password.
/// </param>
public sealed record BoundedEditInvocation<TBatch>(
    string Input,
    TBatch Batch,
    ResolvedOutput Output,
    EditCommandOptions Options,
    bool Verify,
    IReadOnlyDictionary<string, Secret> Secrets,
    Secret? Password,
    Secret? EncryptPassword);

/// <summary>
/// The one command skeleton of every bounded, atomic product edit. It owns the operation
/// document (<c>--ops</c>) and its composition with <c>--set</c> directives and the
/// execution semantics (<c>--if-match</c>, <c>--dry-run</c>, <c>--best-effort</c>,
/// <c>--verify</c>), so every product accepts and rejects the same combinations. Its
/// command, defined through <see cref="EditDefinition"/>, publishes the edited document as a
/// mutation output (<c>--out</c>, <c>--overwrite</c>, <c>--in-place</c>, <c>--backup</c>).
/// <c>--ops</c> is optional only together with <c>--set</c>: an edit needs one of them.
/// </summary>
public sealed class BoundedEditCommand<TOp, TBatch>
    where TOp : BoundedOperation, IOperationVocabulary<TOp>
    where TBatch : BoundedOperationEnvelope<TOp>
{
    private const string OpsOption = StandardOptionNames.Ops;
    private readonly BoundedEditDefinition<TOp, TBatch> _definition;
    private readonly Option<string?> _ops;
    private readonly Option<string[]>? _set;
    private readonly Option<string?> _ifMatch;
    private readonly Option<bool> _bestEffort;
    private readonly Option<bool>? _verify;
    private readonly IReadOnlyList<Option> _options;

    /// <summary>Creates the option surface for one product edit command.</summary>
    public BoundedEditCommand(BoundedEditDefinition<TOp, TBatch> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(definition.Contracts);
        ArgumentNullException.ThrowIfNull(definition.Writes);
        if ((definition.EncryptOnly is not null || definition.EncryptOwnerOnly is not null) && !TOp.Catalog.Encrypts)
        {
            throw new ArgumentException("An edit that declares how to only encrypt needs a vocabulary with an operation that encrypts.", nameof(definition));
        }

        _definition = definition;
        string schema = TOp.Catalog.SchemaCommandId;
        _ops = new Option<string?>(OpsOption)
        {
            Description = "The ops JSON, an object with ops or the bare array of operations: a path "
                + "to the document, '-' to read it from stdin, or the document itself when the value "
                + "starts with { or [ (inline). To name a file whose name starts with '[', prefix it "
                + $"with ./ . At most {BoundedOperation.MaximumPerBatch} operations. "
                + $"Vocabulary: {DistributionInfo.CommandName} schema {schema}.",
        }.WithInput(InputKind.JsonSource);
        if (definition.SetDirectives is { } grammar)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(grammar.Description);
            _set = new Option<string[]>(StandardOptionNames.Set)
            {
                Description = grammar.Description,
            }.WithInput(InputKind.None);
        }

        _ifMatch = new Option<string?>(StandardOptionNames.IfMatch)
        {
            Description = "Require the current input SHA-256 fingerprint before editing.",
        }.WithInput(InputKind.None);
        _bestEffort = new Option<bool>(StandardOptionNames.BestEffort)
        {
            Description = "Keep successful operations, report failures, and exit 8.",
        };
        if (definition.VerifyDescription is { } verify)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(verify);
            _verify = new Option<bool>(StandardOptionNames.Verify) { Description = verify };
        }

        _options =
        [
            _ops,
            .. _set is null ? [] : new Option[] { _set },
            _ifMatch,
            _bestEffort,
            .. _verify is null ? [] : new Option[] { _verify },
        ];
    }

    /// <summary>The traits of the edit command: the product's, publishing to the mutation output.</summary>
    internal CommandTraits EditTraits(CommandTraits traits)
    {
        ArgumentNullException.ThrowIfNull(traits);
        if (traits.Other is not null || traits.Output is not null)
        {
            throw new ArgumentException(
                "An edit reads one input document and publishes it as its mutation output.",
                nameof(traits));
        }

        return traits with { Output = OutputTarget.Mutation(_definition.Writes) };
    }

    /// <summary>The shared edit options, then the product's own parameters.</summary>
    internal IReadOnlyList<Symbol> EditParameters(IReadOnlyList<Symbol> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return [.. _options, .. parameters];
    }

    /// <summary>Whether <c>--verify</c> was requested; false when the product has no verification.</summary>
    public bool IsVerifyRequested(ParseResult parse)
    {
        ArgumentNullException.ThrowIfNull(parse);
        return _verify is not null && parse.GetValue(_verify);
    }

    /// <summary>
    /// Validates the option combination, then reads and composes the operation document and
    /// resolves its secrets, then the output password and the input password. Every check that
    /// needs no input runs before <c>--ops -</c> consumes standard input.
    /// </summary>
    /// <param name="parse">The parsed command line.</param>
    /// <param name="standard">The invocation's common values.</param>
    /// <param name="encrypts">Whether the edit declares an output password.</param>
    internal BoundedEditInvocation<TBatch> Read(ParseResult parse, StandardInvocation standard, bool encrypts)
    {
        string? source = parse.GetValue(_ops);
        string[] directives = _set is null ? [] : parse.GetValue(_set) ?? [];
        if (source is null && directives.Length == 0)
        {
            throw WithEncryptOnly(
                CliErrors.Usage([_set is null
                    ? $"Give {OpsOption} (an ops JSON document)."
                    : $"Give {OpsOption} (an ops JSON document), {StandardOptionNames.Set}, or both."]),
                standard,
                encrypts);
        }

        bool verify = IsVerifyRequested(parse);
        bool dryRun = standard.DryRun;
        if (verify && dryRun)
        {
            throw CliErrors.OptionInvalid(
                StandardOptionNames.Verify,
                $"cannot be combined with {StandardOptionNames.DryRun}",
                "Run the dry run first, then edit with --verify.");
        }

        ResolvedOutput output = standard.Output;
        PathResolver paths = standard.Paths;
        TOp[] compiled = _definition.SetDirectives is { } grammar
            ? directives.Select(grammar.Parse).ToArray()
            : [];
        // A document without operations, and no directive to add one, is refused as no
        // operations at all; it says how to only encrypt as well.
        TBatch batch = Compose(
            source is null
                ? null
                : ParseDocument(source, paths, standard.Inputs, empty => compiled.Length == 0 ? WithEncryptOnly(empty, standard, encrypts) : empty),
            compiled);
        // Every [InputPath] member resolves against the invocation directory; the edit never
        // publishes over a file an operation reads. A file that does not exist fails the
        // operation that reads it, which the runner numbers and --best-effort skips.
        var read = new List<string>();
        string ResolveInput(string path)
        {
            string resolved = paths.ResolveOutput(path);
            read.Add(resolved);
            return resolved;
        }

        batch = (TBatch)((BoundedOperationEnvelope<TOp>)batch with
        {
            Ops = batch.Ops.Select(op => TOp.Catalog.ResolveInputPaths(op, ResolveInput)).ToArray(),
        });
        if (!output.InPlace)
        {
            // The output was resolved before the operations were read; it must not name
            // a file they read either.
            OutputFileOption.EnsureNotInput(output.Path, StandardOptionNames.Out, inPlaceAvailable: true, read);
        }

        var options = new EditCommandOptions
        {
            IfMatch = ReconcileIfMatch(parse.GetValue(_ifMatch), batch.IfMatch),
            DryRun = dryRun,
            BestEffort = parse.GetValue(_bestEffort),
        };
        IReadOnlyDictionary<string, Secret> secrets = ResolveSecrets(batch, standard.ReadEnvironment);
        Secret? encryptPassword = encrypts ? standard.EncryptPassword() : null;
        return new BoundedEditInvocation<TBatch>(
            standard.Input, batch, output, options, verify, secrets, standard.InputPassword, encryptPassword);
    }

    private static IReadOnlyDictionary<string, Secret> ResolveSecrets(TBatch batch, Func<string, string?> readEnvironment)
    {
        var secrets = new Dictionary<string, Secret>(StringComparer.Ordinal);
        // Each [SecretEnv] variable is read once. A missing or empty variable is left out: it
        // fails only the operation that names it, through OperationSecrets.Resolve.
        foreach (string variable in batch.Ops.SelectMany(TOp.Catalog.SecretVariables).OfType<string>().Distinct(StringComparer.Ordinal))
        {
            if (readEnvironment(variable) is { Length: > 0 } secret)
            {
                secrets[variable] = new Secret(secret);
            }
        }

        return secrets;
    }

    private TBatch Compose(TBatch? document, TOp[] compiled)
    {
        if (document is null)
        {
            return TOp.Catalog.Prepare(_definition.SetDirectives!.CreateBatch(compiled));
        }

        return compiled.Length == 0
            ? document
            : TOp.Catalog.Prepare((TBatch)((BoundedOperationEnvelope<TOp>)document with
            {
                Ops = [.. document.Ops, .. compiled],
            }));
    }

    private TBatch ParseDocument(string source, PathResolver paths, InputSource inputs, Func<CliException, CliException> noOperations)
    {
        string text = JsonInputSource.Read(source, paths, inputs, OpsOption);
        try
        {
            // A document read from stdin is never quoted back: stdin may hold something else.
            return TOp.Catalog.Parse<TBatch>(text, _definition.Contracts,
                quotesContent: JsonInputSource.Classify(source, OpsOption) != JsonSourceKind.StandardInput);
        }
        catch (CliException exception) when (exception.Code == ErrorCodes.OpsInvalid && HasNoOperations(text))
        {
            throw noOperations(exception);
        }
        catch (CliException exception) when (
            exception.Code == ErrorCodes.OpsInvalid
            && JsonInputSource.Classify(source, OpsOption) == JsonSourceKind.Inline
            && !text.Contains('"', StringComparison.Ordinal))
        {
            // Valid ops JSON always contains double quotes, so an inline value without one
            // means the shell removed them, as Windows PowerShell does to embedded quotes
            // in native arguments. Same code and message; the hint names the ways out.
            string shorthand = _set is null ? string.Empty : ", or use --set";
            throw CliException.Create(
                exception.Code,
                exception.Message,
                hint: $"{exception.Hint} The value reached the CLI without any double quotes; "
                    + "Windows PowerShell strips them from inline arguments. Escape them as \\\", "
                    + $"pipe the document through '{OpsOption} -'{shorthand}.",
                details: exception.Details,
                docs: exception.Docs);
        }
    }

    // Whether the document parses and holds no operation: the bare empty array, or an object whose
    // ops array is empty.
    private static bool HasNoOperations(string text)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
            JsonElement root = document.RootElement;
            JsonElement ops = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("ops", out JsonElement found) ? found : root;
            return ops.ValueKind == JsonValueKind.Array && ops.GetArrayLength() == 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Adds to the hint of an edit given no operation the way to only encrypt its output, when the
    /// caller named the output password's environment variable and the product declared the
    /// operation that encrypts as that option does (<see cref="BoundedEditDefinition{TOp, TBatch}.EncryptOnly"/>):
    /// the same command line with that operation, built from the caller's variable, in place of the
    /// password option. The code, message and details stay as they are.
    /// </summary>
    private CliException WithEncryptOnly(CliException error, StandardInvocation standard, bool encrypts) =>
        EncryptOnly(standard, encrypts) is { } only
            ? CliException.Create(error.Code, error.Message, hint: $"{error.Hint} {only}", details: error.Details, docs: error.Docs)
            : error;

    /// <summary>
    /// The operation documents with which this edit only encrypts what it publishes, from the
    /// definition's <see cref="BoundedEditDefinition{TOp, TBatch}.EncryptOnly"/> and
    /// <see cref="BoundedEditDefinition{TOp, TBatch}.EncryptOwnerOnly"/>.
    /// </summary>
    internal EncryptionOperations Encryption => new(
        _definition.EncryptOnly is { } encrypt ? variable => EncryptingDocument(encrypt(variable)) : null,
        _definition.EncryptOwnerOnly is { } ownerOnly ? variable => EncryptingDocument(ownerOnly.Operation(variable)) : null,
        _definition.EncryptOwnerOnly is { } reader
            ? new PermissionsReader(reader.PermissionsField, reader.PermissionsCommand, reader.PermissionsOptions)
            : null);

    // The one operation that encrypts as a compact operation array, as --ops takes it.
    private string EncryptingDocument(TOp operation)
    {
        if (TOp.Catalog.NameOf(operation) != TOp.Catalog.Encryptor?.Name)
        {
            throw new InvalidOperationException($"The operation that only encrypts is not the vocabulary's operation that encrypts, but {TOp.Catalog.NameOf(operation)}.");
        }

        var compact = new JsonSerializerOptions(_definition.Contracts.LocalOptions) { WriteIndented = false };
        return $"[{JsonSerializer.Serialize(operation, typeof(TOp), compact)}]";
    }

    // The sentence that shows how to only encrypt: this command line again (StandardInvocation.Again)
    // with that operation in place of the operation document and the output password options; null
    // when the edit cannot, or the command line holds a secret given literally, which a hint never
    // repeats, or a backtick, in which the sentence quotes the command.
    private string? EncryptOnly(StandardInvocation standard, bool encrypts)
    {
        if (!encrypts || _definition.EncryptOnly is not { } encryptOnly || standard.EncryptEnvironment() is not { } environment
            || standard.Again([_ops, .. standard.OutputPasswordOptions]) is not { } again)
        {
            return null;
        }

        ContinuationCommand command = again.Command.Document(OpsOption, EncryptingDocument(encryptOnly(environment.Variable)));
        string quoted = command.Quoted();
        return quoted.StartsWith('`') && !command.ToString().Contains('`', StringComparison.Ordinal)
            ? $"To only encrypt, run {quoted}{again.Repeating}."
            : null;
    }

    private static string? ReconcileIfMatch(string? command, string? document)
    {
        if (command is not null
            && document is not null
            && !string.Equals(command.Trim(), document.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw CliErrors.OptionInvalid(
                StandardOptionNames.IfMatch,
                "the command value differs from the operations document ifMatch value",
                "Use one current source fingerprint in either location, or the same value in both.");
        }

        return command ?? document;
    }
}
