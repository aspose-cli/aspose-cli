using System.CommandLine;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
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
public sealed record BoundedEditDefinition<TOp, TBatch>
    where TOp : BoundedOperation
    where TBatch : BoundedOperationEnvelope<TOp>
{
    /// <summary>The product's operation vocabulary.</summary>
    public required OperationCatalog<TOp> Catalog { get; init; }

    /// <summary>The product's source-generated contract serializer.</summary>
    public required ProductJsonDefinition Contracts { get; init; }

    /// <summary>The <c>--set</c> shorthand, or null when the product has none.</summary>
    public SetDirectiveGrammar<TOp, TBatch>? SetDirectives { get; init; }

    /// <summary>Help for <c>--verify</c>, or null when the product has no staged verification.</summary>
    public string? VerifyDescription { get; init; }

    /// <summary>
    /// Resolves the file paths an operation reads, such as a document it appends or an image it
    /// inserts, through <see cref="OperationPaths.ResolveInput"/>, which records each one so the
    /// edit never publishes over a file it reads. When null, the catalog resolves the
    /// <see cref="InputPathAttribute"/> members of a generated vocabulary.
    /// </summary>
    public Func<TOp, OperationPaths, TOp>? NormalizePaths { get; init; }

    /// <summary>
    /// Names the environment variables whose secrets an operation reads, such as its
    /// <c>passwordEnv</c> fields; a null entry is an omitted optional field. When null, the
    /// catalog names the <see cref="SecretEnvAttribute"/> members of a generated vocabulary.
    /// </summary>
    public Func<TOp, IEnumerable<string?>>? SecretVariables { get; init; }
}

/// <summary>
/// Resolves the files that operations read against the invocation directory and records each
/// resolved path.
/// </summary>
public sealed class OperationPaths
{
    private readonly PathResolver _paths;
    private readonly List<string> _inputs = [];

    internal OperationPaths(PathResolver paths) => _paths = paths;

    /// <summary>The files resolved so far, which the edit never publishes over.</summary>
    internal IReadOnlyList<string> Inputs => _inputs;

    /// <summary>Resolves a file an operation reads, which must exist, and records it.</summary>
    /// <exception cref="CliException"><c>FILE_NOT_FOUND</c> when the file does not exist.</exception>
    public string ResolveInput(string path)
    {
        string resolved = _paths.ResolveInput(path);
        _inputs.Add(resolved);
        return resolved;
    }
}

/// <summary>One fully resolved bounded-edit invocation, ready for the product port.</summary>
/// <param name="Batch">The validated, identified and path-normalized operation document.</param>
/// <param name="Target">Where and how the result is published.</param>
/// <param name="Options">Precondition, dry-run and best-effort semantics.</param>
/// <param name="Verify">Whether staged verification was requested.</param>
/// <param name="Secrets">
/// The operations' secrets by environment variable name; a missing or empty variable is absent,
/// and the operation that names it fails through <see cref="OperationSecrets.Resolve"/>.
/// </param>
public sealed record BoundedEditInvocation<TBatch>(
    TBatch Batch,
    MutationTarget Target,
    EditCommandOptions Options,
    bool Verify,
    IReadOnlyDictionary<string, string> Secrets);

/// <summary>
/// The one command skeleton of every bounded, atomic product edit. It owns the operation
/// document (<c>--ops</c>) and its composition with <c>--set</c> directives and the
/// execution semantics (<c>--if-match</c>, <c>--dry-run</c>, <c>--best-effort</c>,
/// <c>--verify</c>), so every product accepts and rejects the same combinations. Its
/// command is a <see cref="StandardCommand"/> that publishes the edited document as a
/// mutation output (<c>--out</c>, <c>--overwrite</c>, <c>--in-place</c>, <c>--backup</c>),
/// and whose input password refuses standard input when the operation document comes from it.
/// </summary>
public sealed class BoundedEditCommand<TOp, TBatch>
    where TOp : BoundedOperation
    where TBatch : BoundedOperationEnvelope<TOp>
{
    private const string OpsOption = StandardOptionNames.Ops;
    private const string StandardInputSource = "-";
    private readonly BoundedEditDefinition<TOp, TBatch> _definition;
    private readonly Option<string?> _ops;
    private readonly Option<string[]>? _set;
    private readonly Option<string?> _ifMatch;
    private readonly Option<bool> _dryRun;
    private readonly Option<bool> _bestEffort;
    private readonly Option<bool>? _verify;
    private readonly IReadOnlyList<Option> _options;

    /// <summary>Creates the option surface for one product edit command.</summary>
    public BoundedEditCommand(BoundedEditDefinition<TOp, TBatch> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(definition.Catalog);
        ArgumentNullException.ThrowIfNull(definition.Contracts);
        _definition = definition;
        string schema = definition.Catalog.Describe("edit").Descriptor.InputSchema;
        _ops = new Option<string?>(OpsOption)
        {
            Required = definition.SetDirectives is null,
            Description = "The ops JSON: a path to the document, '-' to read it from stdin, or the "
                + "document itself when the value starts with { or [ (inline). To name a file "
                + "whose name starts with '[', prefix it with ./ . "
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
        _dryRun = new Option<bool>(StandardOptionNames.DryRun)
        {
            Description = "Resolve and apply operations in memory without writing.",
        };
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
            _dryRun,
            _bestEffort,
            .. _verify is null ? [] : new Option[] { _verify },
        ];
    }

    /// <summary>
    /// Creates the product's edit command: the document argument, the shared edit options,
    /// the product's own parameters, then the mutation output and the common options its
    /// traits select. The handler receives the composed, path-normalized batch with its
    /// resolved secrets.
    /// </summary>
    /// <param name="host">The product's command host.</param>
    /// <param name="name">The command name.</param>
    /// <param name="description">The command help.</param>
    /// <param name="traits">
    /// The edited document, its output password and fonts; the edit publishes to a mutation output.
    /// </param>
    /// <param name="parameters">The product's own arguments and options, each kind in help order.</param>
    /// <param name="handler">Maps the invocation to the product port call.</param>
    /// <param name="checkUsage">
    /// Rejects a combination of the product's own options that needs no input; it runs before
    /// the document, the operation document, standard input or any secret is read.
    /// </param>
    public Command Create<TPort>(
        IProductCommandHost<TPort> host,
        string name,
        string description,
        CommandTraits traits,
        IReadOnlyList<Symbol> parameters,
        Func<ParseResult, BoundedEditInvocation<TBatch>, StandardInvocation<TPort>, ResultEnvelope> handler,
        Action<ParseResult>? checkUsage = null)
        where TPort : class
    {
        ArgumentNullException.ThrowIfNull(traits);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(handler);
        if (traits.Other is not null || traits.Output is not null)
        {
            throw new ArgumentException(
                "An edit reads one input document and publishes it as its mutation output.",
                nameof(traits));
        }

        return StandardCommand.Create(
            host,
            name,
            description,
            traits with { Output = OutputTarget.Mutation },
            [.. _options, .. parameters],
            parse => parse.GetValue(_ops) == StandardInputSource,
            (parse, standard) =>
            {
                checkUsage?.Invoke(parse);
                return handler(
                    parse,
                    Read(parse, standard),
                    standard);
            });
    }

    /// <summary>Whether <c>--verify</c> was requested; false when the product has no verification.</summary>
    public bool IsVerifyRequested(ParseResult parse)
    {
        ArgumentNullException.ThrowIfNull(parse);
        return _verify is not null && parse.GetValue(_verify);
    }

    /// <summary>
    /// Validates the option combination, then reads and composes the operation document and
    /// resolves its secrets. Every check that needs no input runs before <c>--ops -</c>
    /// consumes standard input.
    /// </summary>
    /// <param name="parse">The parsed command line.</param>
    /// <param name="standard">The invocation's common values.</param>
    private BoundedEditInvocation<TBatch> Read(ParseResult parse, StandardInvocation standard)
    {
        string? source = parse.GetValue(_ops);
        string[] directives = _set is null ? [] : parse.GetValue(_set) ?? [];
        if (source is null && directives.Length == 0)
        {
            throw CliErrors.Usage([$"Give {OpsOption} (an ops JSON document), --set, or both."]);
        }

        bool verify = IsVerifyRequested(parse);
        bool dryRun = parse.GetValue(_dryRun);
        if (verify && dryRun)
        {
            throw CliErrors.OptionInvalid(
                StandardOptionNames.Verify,
                $"cannot be combined with {StandardOptionNames.DryRun}",
                "Run the dry run first, then edit with --verify.");
        }

        MutationTarget target = standard.MutationTarget();
        PathResolver paths = standard.Paths;
        TOp[] compiled = _definition.SetDirectives is { } grammar
            ? directives.Select(grammar.Parse).ToArray()
            : [];
        TBatch batch = Compose(source is null ? null : ParseDocument(source, paths, standard.Inputs), compiled);
        var read = new OperationPaths(paths);
        Func<TOp, OperationPaths, TOp> normalize = _definition.NormalizePaths
            ?? ((op, files) => _definition.Catalog.ResolveInputPaths(op, files.ResolveInput));
        batch = (TBatch)((BoundedOperationEnvelope<TOp>)batch with
        {
            Ops = batch.Ops.Select(op => normalize(op, read)).ToArray(),
        });
        if (!target.InPlace)
        {
            // The target was resolved before the operations were read; it must not name
            // a file they read either.
            OutputFileOption.EnsureNotInput(target.OutputPath, StandardOptionNames.Out, inPlaceAvailable: true, read.Inputs);
        }

        return new BoundedEditInvocation<TBatch>(
            batch,
            target,
            new EditCommandOptions
            {
                IfMatch = ReconcileIfMatch(parse.GetValue(_ifMatch), batch.IfMatch),
                DryRun = dryRun,
                BestEffort = parse.GetValue(_bestEffort),
            },
            verify,
            ResolveSecrets(batch, standard.ReadEnvironment));
    }

    private IReadOnlyDictionary<string, string> ResolveSecrets(TBatch batch, Func<string, string?> readEnvironment)
    {
        var secrets = new Dictionary<string, string>(StringComparer.Ordinal);
        Func<TOp, IEnumerable<string?>> variables = _definition.SecretVariables ?? _definition.Catalog.SecretVariables;

        // A missing or empty variable is left out: it fails only the operation that names it,
        // through OperationSecrets.Resolve.
        foreach (string variable in batch.Ops.SelectMany(variables).OfType<string>().Distinct(StringComparer.Ordinal))
        {
            if (readEnvironment(variable) is { Length: > 0 } secret)
            {
                secrets[variable] = secret;
            }
        }

        return secrets;
    }

    private TBatch Compose(TBatch? document, TOp[] compiled)
    {
        if (document is null)
        {
            return _definition.Catalog.Prepare(_definition.SetDirectives!.CreateBatch(compiled));
        }

        return compiled.Length == 0
            ? document
            : _definition.Catalog.Prepare((TBatch)((BoundedOperationEnvelope<TOp>)document with
            {
                Ops = [.. document.Ops, .. compiled],
            }));
    }

    private TBatch ParseDocument(string source, PathResolver paths, InputSource inputs)
    {
        string text = JsonInputSource.Read(source, paths, inputs, OpsOption);
        try
        {
            return _definition.Catalog.Parse<TBatch>(text, _definition.Contracts);
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
            throw new CliException(
                exception.Code,
                exception.Message,
                hint: $"{exception.Hint} The value reached the CLI without any double quotes; "
                    + "Windows PowerShell strips them from inline arguments. Escape them as \\\", "
                    + $"pipe the document through '{OpsOption} -'{shorthand}.",
                details: exception.Details,
                docs: exception.Docs);
        }
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
