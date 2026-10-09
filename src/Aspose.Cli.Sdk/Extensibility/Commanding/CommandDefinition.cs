using System.CommandLine;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility.Output;
using Aspose.Cli.Sdk.Operations;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// One product command: its name, help, traits and own parameters, how the parsed command line
/// becomes the request (<c>bind</c>), and how the result prints as a table (<c>table</c>). A
/// product's menu pairs it with the static handler that turns the request into the result.
/// </summary>
/// <remarks>
/// <para>
/// The SDK runs every menu command in one fixed order, so the first error a caller sees does not
/// depend on the product: the host admits the declared input files and activates the product;
/// the input documents are resolved (a missing one is <c>FILE_NOT_FOUND</c>); then
/// <c>--font-dir</c> is validated before any secret or standard input is read; then <c>bind</c> runs, the
/// product's usage checks first and then the inputs and secrets it reads; then the font scope is
/// entered; then the product guard runs around the handler; then <see cref="Finish"/>; then the
/// write pipeline discloses evaluation output. A password error about one of two input documents
/// is restated with that document's own option. The font scope ends after the result is complete.
/// </para>
/// <para>How a product command is written:</para>
/// <list type="bullet">
/// <item>The menu line in the product module pairs the definition factory with the handler:
/// <c>.Command(RenderCommand.Create, DocumentRender.Run)</c>; <c>.Group("query", "…", query =&gt; query.Command(…))</c>
/// nests a group. The menu lists only method groups; its order is the order of help and
/// capabilities. <c>.Describe(description, help)</c> gives the product command its help, and
/// <c>.Guard((session, run) =&gt; …)</c> wraps every handler and view adapter call.</item>
/// <item>The session is an immutable record of the dependencies the handlers share, built by the
/// product's activator from the binding: for a licensed engine its <c>OutputPipeline&lt;TDocument&gt;</c>
/// (license state and publishing), the <c>ResourceBudgetLedger</c> and the product's document
/// loader. It has no methods and no fonts; the SDK enters the font scope.</item>
/// <item>A handler is any <c>static TResult Run(TSession session, TRequest request)</c> in the
/// product's Engine; commands that share a body may point at one theme class. Handlers never see
/// the command line, and commands never see the engine.</item>
/// <item>The command file (<c>Commands/XxxCommand.cs</c>) holds the options, <c>bind</c> and the
/// table renderer. Commands that share a result type pass the same renderer method.</item>
/// <item>Every request declares <c>required string Input</c> (or the paths it reads), so a binding
/// that forgets it does not compile.</item>
/// <item>Tests call the handlers directly with a session and a request; there are no test shims
/// with the shape of an engine port.</item>
/// </list>
/// </remarks>
/// <typeparam name="TRequest">The request <c>bind</c> produces and the handler receives.</typeparam>
/// <typeparam name="TResult">The result the handler returns.</typeparam>
public sealed class CommandDefinition<TRequest, TResult>
    where TResult : ResultEnvelope
{
    private readonly CommandTraits _traits;
    private readonly IReadOnlyList<Symbol> _parameters;
    private readonly Func<ParseResult, bool>? _standardInputTaken;
    private readonly Func<ParseResult, StandardInvocation, TRequest> _bind;

    /// <summary>Defines a standard command.</summary>
    /// <param name="name">The command name.</param>
    /// <param name="description">The command help.</param>
    /// <param name="traits">What the command reads and writes, which decides its common parameters.</param>
    /// <param name="parameters">The product's own arguments and options, each kind in help order.</param>
    /// <param name="bind">
    /// Maps the parsed command line to the request: the product's usage checks first, then the
    /// inputs and secrets it reads through <see cref="StandardInvocation"/>.
    /// </param>
    /// <param name="table">
    /// Prints a <typeparamref name="TResult"/> as a table; null only when <typeparamref name="TResult"/>
    /// is a base of the results the handler returns, whose renderers <see cref="Renderers"/> lists.
    /// </param>
    public CommandDefinition(
        string name,
        string description,
        CommandTraits traits,
        IReadOnlyList<Symbol> parameters,
        Func<ParseResult, StandardInvocation, TRequest> bind,
        Action<TResult, TableSurface>? table)
        : this(name, description, traits, parameters, standardInputTaken: null, bind, table)
    {
    }

    internal CommandDefinition(
        string name,
        string description,
        CommandTraits traits,
        IReadOnlyList<Symbol> parameters,
        Func<ParseResult, bool>? standardInputTaken,
        Func<ParseResult, StandardInvocation, TRequest> bind,
        Action<TResult, TableSurface>? table)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Name = name;
        Description = description;
        _traits = traits ?? throw new ArgumentNullException(nameof(traits));
        _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        _standardInputTaken = standardInputTaken;
        _bind = bind ?? throw new ArgumentNullException(nameof(bind));
        Table = table;
    }

    /// <summary>The command name.</summary>
    public string Name { get; }

    /// <summary>The command help.</summary>
    public string Description { get; }

    /// <summary>The table renderer of <typeparamref name="TResult"/>, or null.</summary>
    public Action<TResult, TableSurface>? Table { get; }

    /// <summary>
    /// Completes the result with what only the command line knows, such as the continuation of a
    /// truncated read that repeats the caller's own options; it runs after the handler, inside the
    /// font scope, and receives the same parse, request and invocation as <c>bind</c>.
    /// </summary>
    public Func<ParseResult, TRequest, TResult, StandardInvocation, TResult>? Finish { get; init; }

    /// <summary>Help examples, written after the executable name.</summary>
    public IReadOnlyList<string> Examples { get; init; } = [];

    /// <summary>Help links.</summary>
    public IReadOnlyList<CommandHelpLink> Links { get; init; } = [];

    /// <summary>
    /// Renderers of further result types the handler returns, for a command whose
    /// <typeparamref name="TResult"/> is their common base.
    /// </summary>
    public IReadOnlyList<ProductOutputDefinition> Renderers { get; init; } = [];

    /// <summary>Every renderer the definition declares.</summary>
    internal IEnumerable<ProductOutputDefinition> Outputs()
    {
        ProductOutputDefinition[] outputs = Table is null
            ? [.. Renderers]
            : [ProductOutputDefinition.Create(Table), .. Renderers];
        return outputs.Length > 0
            ? outputs
            : throw new InvalidOperationException(
                $"Command '{Name}' declares no table renderer; give it a table or its result types' renderers.");
    }

    /// <summary>Creates the System.CommandLine command that runs <paramref name="handler"/>.</summary>
    internal Command Create<TSession>(MenuContext<TSession> context, Func<TSession, TRequest, TResult> handler)
        where TSession : class
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(handler);
        var options = new StandardOptions(_traits);
        Command command = options.CreateCommand(Name, Description, _parameters);
        command.SetAction(parse => context.Run(parse, scope => Execute(options, parse, scope, context, handler)));
        return Examples.Count == 0 && Links.Count == 0 ? command : command.WithExamples(Examples, Links);
    }

    private ResultEnvelope Execute<TSession>(
        StandardOptions options,
        ParseResult parse,
        ProductCommandScope scope,
        MenuContext<TSession> context,
        Func<TSession, TRequest, TResult> handler)
        where TSession : class
    {
        var invocation = new StandardInvocation(
            options,
            parse,
            scope.Paths,
            scope.Inputs,
            scope.ReadEnvironment,
            _standardInputTaken?.Invoke(parse) != true)
        {
            DetectFormat = context.DetectFormat,
        };
        try
        {
            invocation.ResolveDocuments();
            FontSearchProfile? fonts = invocation.UsesFonts ? invocation.FontDirectories : null;
            TRequest request = _bind(parse, invocation);
            using IDisposable? fontScope = fonts is null ? null : scope.Binding.UseFonts(fonts);
            TSession session = ((ProductBinding<TSession>)scope.Binding).Session;
            TResult result = context.Guarded(handler, session, request);
            if (Finish is not null)
            {
                result = Finish(parse, request, result, invocation);
            }

            // The write pipeline discloses evaluation output for every product command alike.
            return scope.Binding.Publishing?.Disclose(result) ?? result;
        }
        catch (CliException error) when (invocation.ForPairedInput(error) is var restated && restated != error)
        {
            throw restated;
        }
    }
}

/// <summary>Defines a product's bounded, atomic edit command as a <see cref="CommandDefinition{TRequest, TResult}"/>.</summary>
public static class EditDefinition
{
    /// <summary>
    /// Defines an edit command on the shared bounded-edit skeleton (<see cref="BoundedEditCommand{TOp, TBatch}"/>):
    /// <c>--ops</c> and the product's <c>--set</c> shorthand, <c>--if-match</c>, <c>--dry-run</c>,
    /// <c>--best-effort</c>, <c>--verify</c> and the mutation output, with <c>--ops -</c> taking
    /// standard input away from the input password. <c>bind</c> receives the composed,
    /// path-normalized batch with its resolved secrets.
    /// </summary>
    /// <param name="edit">The product's edit skeleton, which <paramref name="checkUsage"/> may consult.</param>
    /// <param name="description">The command help.</param>
    /// <param name="traits">The edited document, its output password and fonts; the edit publishes to a mutation output.</param>
    /// <param name="parameters">The product's own arguments and options, each kind in help order.</param>
    /// <param name="bind">Maps the batch and the command line to the request.</param>
    /// <param name="table">Prints the result as a table.</param>
    /// <param name="checkUsage">
    /// Rejects a combination of the product's own options that needs no input; it runs once the
    /// input paths are resolved, before the document's content, the operation document, standard
    /// input or any secret is read.
    /// </param>
    /// <param name="name">The command name.</param>
    /// <param name="examples">The help examples.</param>
    /// <param name="links">The help's further-reading links.</param>
    public static CommandDefinition<TRequest, TResult> Create<TOp, TBatch, TRequest, TResult>(
        BoundedEditCommand<TOp, TBatch> edit,
        string description,
        CommandTraits traits,
        IReadOnlyList<Symbol> parameters,
        Func<ParseResult, BoundedEditInvocation<TBatch>, StandardInvocation, TRequest> bind,
        Action<TResult, TableSurface> table,
        Action<ParseResult>? checkUsage = null,
        string name = "edit",
        IReadOnlyList<string>? examples = null,
        IReadOnlyList<CommandHelpLink>? links = null)
        where TOp : BoundedOperation, IOperationVocabulary<TOp>
        where TBatch : BoundedOperationEnvelope<TOp>
        where TResult : ResultEnvelope
    {
        ArgumentNullException.ThrowIfNull(edit);
        ArgumentNullException.ThrowIfNull(bind);
        ArgumentNullException.ThrowIfNull(table);
        return new CommandDefinition<TRequest, TResult>(
            name,
            description,
            edit.EditTraits(traits),
            edit.EditParameters(parameters),
            edit.TakesStandardInput,
            (parse, standard) =>
            {
                checkUsage?.Invoke(parse);
                return bind(parse, edit.Read(parse, standard), standard);
            },
            table)
        {
            Examples = examples ?? [],
            Links = links ?? [],
        };
    }
}
