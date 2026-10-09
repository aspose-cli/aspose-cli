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
}

/// <summary>One fully resolved bounded-edit invocation, ready for the product request.</summary>
/// <param name="Batch">The validated, identified and path-normalized operation document.</param>
/// <param name="Output">Where, how and in which format the result is published.</param>
/// <param name="Options">Precondition, dry-run and best-effort semantics.</param>
/// <param name="Verify">Whether staged verification was requested.</param>
/// <param name="Secrets">
/// The operations' secrets by environment variable name; a missing or empty variable is absent,
/// and the operation that names it fails through <see cref="OperationSecrets.Resolve"/>.
/// </param>
public sealed record BoundedEditInvocation<TBatch>(
    TBatch Batch,
    ResolvedOutput Output,
    EditCommandOptions Options,
    bool Verify,
    IReadOnlyDictionary<string, Secret> Secrets);

/// <summary>
/// The one command skeleton of every bounded, atomic product edit. It owns the operation
/// document (<c>--ops</c>) and its composition with <c>--set</c> directives and the
/// execution semantics (<c>--if-match</c>, <c>--dry-run</c>, <c>--best-effort</c>,
/// <c>--verify</c>), so every product accepts and rejects the same combinations. Its
/// command, defined through <see cref="EditDefinition"/>, publishes the edited document as a
/// mutation output (<c>--out</c>, <c>--overwrite</c>, <c>--in-place</c>, <c>--backup</c>),
/// and its input password refuses standard input when the operation document comes from it.
/// </summary>
public sealed class BoundedEditCommand<TOp, TBatch>
    where TOp : BoundedOperation, IOperationVocabulary<TOp>
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
        ArgumentNullException.ThrowIfNull(definition.Contracts);
        ArgumentNullException.ThrowIfNull(definition.Writes);
        _definition = definition;
        string schema = TOp.Catalog.SchemaCommandId;
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

    /// <summary>Whether the operation document comes from standard input (<c>--ops -</c>).</summary>
    internal bool TakesStandardInput(ParseResult parse) => parse.GetValue(_ops) == StandardInputSource;

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
    internal BoundedEditInvocation<TBatch> Read(ParseResult parse, StandardInvocation standard)
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

        ResolvedOutput output = standard.Output;
        PathResolver paths = standard.Paths;
        TOp[] compiled = _definition.SetDirectives is { } grammar
            ? directives.Select(grammar.Parse).ToArray()
            : [];
        TBatch batch = Compose(source is null ? null : ParseDocument(source, paths, standard.Inputs), compiled);
        // Every [InputPath] member resolves against the invocation directory and must exist;
        // the edit never publishes over a file an operation reads.
        var read = new List<string>();
        string ResolveInput(string path)
        {
            string resolved = paths.ResolveInput(path);
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

        return new BoundedEditInvocation<TBatch>(
            batch,
            output,
            new EditCommandOptions
            {
                IfMatch = ReconcileIfMatch(parse.GetValue(_ifMatch), batch.IfMatch),
                DryRun = dryRun,
                BestEffort = parse.GetValue(_bestEffort),
            },
            verify,
            ResolveSecrets(batch, standard.ReadEnvironment));
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

    private TBatch ParseDocument(string source, PathResolver paths, InputSource inputs)
    {
        string text = JsonInputSource.Read(source, paths, inputs, OpsOption);
        try
        {
            return TOp.Catalog.Parse<TBatch>(text, _definition.Contracts);
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
