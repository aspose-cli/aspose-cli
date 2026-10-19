using System.Diagnostics.CodeAnalysis;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// The request of a bounded, atomic product edit: the fields every edit takes from its
/// <see cref="BoundedEditInvocation{TBatch}"/>. A product's request derives from it, adds its own
/// options, and is bound with the constructor that takes the invocation.
/// </summary>
/// <typeparam name="TBatch">The product's operation document.</typeparam>
public abstract record BoundedEditRequest<TBatch>
{
    /// <summary>Creates a request whose fields an object initializer sets.</summary>
    protected BoundedEditRequest()
    {
    }

    /// <summary>Creates the request of one resolved edit invocation.</summary>
    /// <param name="edit">The resolved invocation.</param>
    [SetsRequiredMembers]
    protected BoundedEditRequest(BoundedEditInvocation<TBatch> edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        Input = edit.Input;
        Batch = edit.Batch;
        Output = edit.Output;
        Options = edit.Options;
        Verify = edit.Verify;
        OpSecrets = edit.Secrets;
        Password = edit.Password;
        EncryptPassword = edit.EncryptPassword;
    }

    /// <summary>The document to edit.</summary>
    public required string Input { get; init; }

    /// <summary>The validated operation batch to apply atomically.</summary>
    public required TBatch Batch { get; init; }

    /// <summary>The resolved output: its format, path, overwrite permission and in-place backup.</summary>
    public required ResolvedOutput Output { get; init; }

    /// <summary>Shared stale-input, dry-run and best-effort semantics.</summary>
    public EditCommandOptions Options { get; init; } = new();

    /// <summary>Whether the staged output is read back against the effect of each operation (<c>--verify</c>).</summary>
    public bool Verify { get; init; }

    /// <summary>The operations' secrets by the environment variable their <c>*Env</c> fields name; never part of ops JSON or results.</summary>
    public IReadOnlyDictionary<string, Secret>? OpSecrets { get; init; }

    /// <summary>The password that opens the input document.</summary>
    public Secret? Password { get; init; }

    /// <summary>
    /// The password the output opens with (<c>--encrypt</c>); null keeps the encryption the
    /// product's edit otherwise leaves.
    /// </summary>
    public Secret? EncryptPassword { get; init; }
}
