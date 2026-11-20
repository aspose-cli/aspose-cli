using System.CommandLine;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.IO.Paths;
using Aspose.Cli.Sdk.Operations;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

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

    /// <summary>
    /// Help for <c>--verify</c>: what the product's verification reads back, to which the command
    /// adds what every verification shares; or null when the product has no staged verification.
    /// </summary>
    public string? VerifyDescription { get; init; }
}

/// <summary>One fully resolved bounded-edit invocation, ready for the product request.</summary>
/// <param name="Input">The edited document.</param>
/// <param name="Batch">The validated, identified and path-normalized operation document.</param>
/// <param name="Output">Where, how and in which format the result is published.</param>
/// <param name="Options">Dry-run semantics.</param>
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
/// document (<c>--ops</c>) and the
/// execution semantics (<c>--dry-run</c>, <c>--verify</c>), so every product accepts and rejects the same combinations. Its
/// command, defined through <see cref="EditDefinition"/>, publishes the edited document as a
/// mutation output (<c>--out</c>, <c>--overwrite</c>, <c>--in-place</c>).
/// An edit without <c>--ops</c> is a usage error.
/// </summary>
public sealed class BoundedEditCommand<TOp, TBatch>
    where TOp : BoundedOperation, IOperationVocabulary<TOp>
    where TBatch : BoundedOperationEnvelope<TOp>
{
    private const string OpsOption = StandardOptionNames.Ops;

    /// <summary>What every verification adds to the product's description of what it reads back.</summary>
    private const string VerifyCoverage =
        "A warning with affectsCompleteness about content the edit lost is also an issue; uncheckedOps lists each operation not read back.";

    private readonly BoundedEditDefinition<TOp, TBatch> _definition;
    private readonly Option<string?> _ops;
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
            Description = "The ops JSON, an object with ops or the bare array of operations: a path "
                + "to the document, '-' to read it from stdin, or the document itself when the value "
                + "starts with { or [ (inline). To name a file whose name starts with '[', prefix it "
                + $"with ./ . At most {BoundedOperation.MaximumPerBatch} operations. "
                + $"Vocabulary: {DistributionInfo.CommandName} schema {schema}.",
        }.WithInput(InputKind.JsonSource);
        if (definition.VerifyDescription is { } verify)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(verify);
            _verify = new Option<bool>(StandardOptionNames.Verify) { Description = $"{verify} {VerifyCoverage}" };
        }

        _options =
        [
            _ops,
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
        if (source is null)
        {
            throw CliErrors.Usage([$"Give {OpsOption} (an ops JSON document)."]);
        }

        bool verify = IsVerifyRequested(parse);
        bool dryRun = standard.DryRun;
        if (verify && dryRun)
        {
            throw CliErrors.OptionInvalid(
                StandardOptionNames.Verify,
                $"cannot be combined with {StandardOptionNames.DryRun}");
        }

        ResolvedOutput output = standard.Output;
        PathResolver paths = standard.Paths;
        TBatch batch = ParseDocument(source, paths, standard.Inputs);
        // Every [InputPath] member resolves against the invocation directory; the edit never
        // publishes over a file an operation reads. A file that does not exist fails the
        // operation that reads it, which the runner numbers.
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
            OutputFileOption.EnsureNotInput(output.Path, StandardOptionNames.Out, read);
        }

        var options = new EditCommandOptions
        {
            DryRun = dryRun,
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

    private TBatch ParseDocument(string source, PathResolver paths, InputSource inputs)
    {
        string text = JsonInputSource.Read(source, paths, inputs, OpsOption);
        // A document read from stdin is never quoted back: stdin may hold something else.
        return TOp.Catalog.Parse<TBatch>(text, _definition.Contracts,
            quotesContent: JsonInputSource.Classify(source, OpsOption) != JsonSourceKind.StandardInput);
    }
}
