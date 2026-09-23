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

    /// <summary>Resolves file paths carried by an operation against the invocation directory.</summary>
    public Func<TOp, PathResolver, TOp>? NormalizePaths { get; init; }
}

/// <summary>One fully resolved bounded-edit invocation, ready for the product port.</summary>
/// <param name="Batch">The validated, identified and path-normalized operation document.</param>
/// <param name="Target">Where and how the result is published.</param>
/// <param name="Options">Precondition, dry-run and best-effort semantics.</param>
/// <param name="Verify">Whether staged verification was requested.</param>
/// <param name="OpsFromStandardInput">Whether the document consumed standard input.</param>
public sealed record BoundedEditInvocation<TBatch>(
    TBatch Batch,
    MutationTarget Target,
    EditCommandOptions Options,
    bool Verify,
    bool OpsFromStandardInput);

/// <summary>
/// The one command skeleton of every bounded, atomic product edit. It owns the operation
/// document (<c>--ops</c>) and its composition with <c>--set</c> directives, the publication
/// target (<c>--out</c>, <c>--in-place</c>, <c>--overwrite</c>, <c>--backup</c>) and the
/// execution semantics (<c>--if-match</c>, <c>--dry-run</c>, <c>--best-effort</c>,
/// <c>--verify</c>), so every product accepts and rejects the same combinations.
/// </summary>
public sealed class BoundedEditCommand<TOp, TBatch>
    where TOp : BoundedOperation
    where TBatch : BoundedOperationEnvelope<TOp>
{
    private const string OpsOption = "--ops";
    private readonly BoundedEditDefinition<TOp, TBatch> _definition;
    private readonly Option<string?> _ops;
    private readonly Option<string[]>? _set;
    private readonly MutationFileOptions _target = new();
    private readonly Option<string?> _ifMatch;
    private readonly Option<bool> _dryRun;
    private readonly Option<bool> _bestEffort;
    private readonly Option<bool>? _verify;

    /// <summary>Creates the option surface for one product edit command.</summary>
    public BoundedEditCommand(BoundedEditDefinition<TOp, TBatch> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(definition.Catalog);
        ArgumentNullException.ThrowIfNull(definition.Contracts);
        _definition = definition;
        string schema = definition.Catalog.Describe("edit").InputSchema;
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
            _set = new Option<string[]>("--set")
            {
                Description = grammar.Description,
            }.WithInput(InputKind.None);
        }

        _ifMatch = new Option<string?>("--if-match")
        {
            Description = "Require the current input SHA-256 fingerprint before editing.",
        }.WithInput(InputKind.None);
        _dryRun = new Option<bool>("--dry-run")
        {
            Description = "Resolve and apply operations in memory without writing.",
        };
        _bestEffort = new Option<bool>("--best-effort")
        {
            Description = "Keep successful operations, report failures, and exit 8.",
        };
        if (definition.VerifyDescription is { } verify)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(verify);
            _verify = new Option<bool>("--verify") { Description = verify };
        }
    }

    /// <summary>Whether <c>--verify</c> was requested; false when the product has no verification.</summary>
    public bool IsVerifyRequested(ParseResult parse)
    {
        ArgumentNullException.ThrowIfNull(parse);
        return _verify is not null && parse.GetValue(_verify);
    }

    /// <summary>Adds every shared edit option to one product command.</summary>
    public void AddTo(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Options.Add(_ops);
        if (_set is not null)
        {
            command.Options.Add(_set);
        }

        _target.AddTo(command);
        command.Options.Add(_ifMatch);
        command.Options.Add(_dryRun);
        command.Options.Add(_bestEffort);
        if (_verify is not null)
        {
            command.Options.Add(_verify);
        }
    }

    /// <summary>
    /// Validates the option combination, then reads and composes the operation document.
    /// Every check that needs no input runs before <c>--ops -</c> consumes standard input.
    /// </summary>
    /// <param name="parse">The parsed command line.</param>
    /// <param name="paths">The invocation path resolver.</param>
    /// <param name="inputs">The invocation's bounded input reader.</param>
    /// <param name="inputPath">The resolved document to edit.</param>
    public BoundedEditInvocation<TBatch> Read(
        ParseResult parse,
        PathResolver paths,
        InputSource inputs,
        string inputPath)
    {
        ArgumentNullException.ThrowIfNull(parse);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);

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
                "--verify",
                "cannot be combined with --dry-run",
                "Run the dry run first, then edit with --verify.");
        }

        MutationTarget target = _target.Resolve(parse, paths, inputPath);
        TOp[] compiled = _definition.SetDirectives is { } grammar
            ? directives.Select(grammar.Parse).ToArray()
            : [];
        TBatch batch = Compose(source is null ? null : ParseDocument(source, paths, inputs), compiled);
        if (_definition.NormalizePaths is { } normalize)
        {
            batch = (TBatch)((BoundedOperationEnvelope<TOp>)batch with
            {
                Ops = batch.Ops.Select(op => normalize(op, paths)).ToArray(),
            });
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
            source == "-");
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
                "--if-match",
                "the command value differs from the operations document ifMatch value",
                "Use one current source fingerprint in either location, or the same value in both.");
        }

        return command ?? document;
    }
}
