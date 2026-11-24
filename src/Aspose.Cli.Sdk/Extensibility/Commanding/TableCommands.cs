using System.CommandLine;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Tables;
using Aspose.Cli.Sdk.Text;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// The commands of a product on the object table: <c>edit</c>, <c>read</c> and <c>search</c>, each
/// with the same options in every such product. The edit publishes its input as a mutation output
/// and its <c>--ops</c> document follows the table's schema.
/// </summary>
public static class TableCommands
{
    private const string AtOption = "--at";
    private const string KindOption = "--kind";
    private const string MaxObjectsOption = "--max-objects";

    /// <summary>Defines the <c>edit</c> command.</summary>
    /// <param name="table">Loads the product's table; it is called only when the command runs.</param>
    /// <param name="description">The command help.</param>
    /// <param name="traits">The edited document, its output password and fonts.</param>
    /// <param name="writes">The formats the edit writes; it writes the format it reads.</param>
    /// <param name="examples">The help examples.</param>
    /// <param name="links">The help links.</param>
    public static CommandDefinition<TableEditRequest, TResult> Edit<TResult>(
        Func<DocumentTable> table,
        string description,
        CommandTraits traits,
        IReadOnlyList<FormatDescriptor> writes,
        IReadOnlyList<string>? examples = null,
        IReadOnlyList<CommandHelpLink>? links = null)
        where TResult : TableEditResult
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(traits);
        if (traits.Input is null || traits.Other is not null || traits.Output is not null)
        {
            throw new ArgumentException("An edit reads one input document and publishes it as its mutation output.", nameof(traits));
        }

        string schema = Schema<TResult>();
        var ops = new Option<string>(StandardOptionNames.Ops)
        {
            Required = true,
            Description = "The operation document, {\"ops\": [...]}: a path to it, '-' to read it from stdin, or the document itself when the value "
                + $"starts with {{ (inline). At most {TableBatch.MaximumOperations} operations; each is a set, add, delete, move or do. "
                + $"The kinds, rows, adds and actions: {schema}/{TableSchema.DirectoryView}; one of them in full: {schema}/<name>.",
        }.WithInput(InputKind.JsonSource);
        return new CommandDefinition<TableEditRequest, TResult>(
            "edit",
            description,
            traits with { Output = OutputTarget.Mutation(writes) },
            [ops],
            (parse, standard) =>
            {
                string source = parse.GetRequiredValue(ops);
                EditOutput output = standard.EditOutput;
                string text = JsonInputSource.Read(source, standard.Paths, standard.Inputs, StandardOptionNames.Ops);
                var inputs = new BatchInputs(standard);
                TableBatch batch = TableBatch.Parse(
                    text,
                    table(),
                    inputs,
                    quotesContent: JsonInputSource.Classify(source, StandardOptionNames.Ops) != JsonSourceKind.StandardInput);
                IReadOnlyList<string> read = batch.Files;
                if (!output.InPlace)
                {
                    // The output must not name a file an operation reads.
                    OutputFileOption.EnsureNotInput(output.Path, StandardOptionNames.Out, read);
                }

                Secret? encrypt = traits.Encrypt is null ? null : standard.EncryptPassword();
                return new TableEditRequest
                {
                    Input = standard.Input,
                    Batch = batch,
                    Output = output,
                    Password = standard.InputPassword,
                    EncryptPassword = encrypt,
                    EncryptOption = standard.EncryptOption,
                };
            })
        {
            Examples = examples ?? [],
            Links =
            [
                new CommandHelpLink($"{schema}/{TableSchema.DirectoryView}", "the kinds and their rows, the adds and the actions an operation names"),
                new CommandHelpLink($"{schema}/<name>", "one kind, add or action in full"),
                .. links ?? [],
            ],
            Table = table,
        };
    }

    /// <summary>What a batch reads before the document is opened, through the invocation's own paths, input budgets and environment.</summary>
    private sealed class BatchInputs(StandardInvocation standard) : ITableInputs
    {
        public string ResolvePath(string path) => standard.Paths.ResolveOutput(path);

        public byte[] ReadImage(string path) => ImageInput.ReadSelfContained(standard.Inputs, path);

        public byte[] ReadFile(string path) => standard.Inputs.ReadAllBytes(path);

        public Secret? ReadSecret(string variable) =>
            standard.ReadEnvironment(variable) is { Length: > 0 } value ? new Secret(value) : null;
    }

    /// <summary>Defines the <c>read</c> command.</summary>
    /// <param name="table">Loads the product's table; it is called only when the command runs.</param>
    /// <param name="description">The command help.</param>
    /// <param name="traits">The read document.</param>
    /// <param name="examples">The help examples.</param>
    /// <param name="links">The help links.</param>
    /// <param name="listedWhenNamed">
    /// The kinds of the product's table that read lists only when --kind names them, which the
    /// --kind help names; none for a table without such kinds.
    /// </param>
    public static CommandDefinition<TableReadRequest, TResult> Read<TResult>(
        Func<DocumentTable> table,
        string description,
        CommandTraits traits,
        IReadOnlyList<string>? examples = null,
        IReadOnlyList<CommandHelpLink>? links = null,
        IReadOnlyList<string>? listedWhenNamed = null)
        where TResult : TableReadResult
    {
        ArgumentNullException.ThrowIfNull(table);
        var at = new Option<string?>(AtOption)
        {
            Description = "The address of the object to read with everything below it, as read gives it in at. Default: the whole document.",
        }.WithInput(InputKind.None);
        var from = new Option<string?>(TableRead.FromOption)
        {
            Description = "The address of the object the window starts at; a truncated read's window.next gives it.",
        }.WithInput(InputKind.None);
        string onlyNamed = listedWhenNamed is { Count: > 0 }
            ? $" Objects of the kinds {string.Join(", ", listedWhenNamed)} are listed only when --kind names them; window.notListedUnlessNamed names those a read left out."
            : string.Empty;
        var kinds = new Option<string[]>(KindOption)
        {
            Description = $"List only objects of this kind, one the ops schema lists; repeat for several. Default: every kind.{onlyNamed}",
        }.WithInput(InputKind.None);
        var maxObjects = new Option<int>(MaxObjectsOption)
        {
            Description = "The most objects one window lists.",
            DefaultValueFactory = _ => TableRead.DefaultMaxObjects,
        }.InRange(1, TableRead.MaximumObjects);
        return new CommandDefinition<TableReadRequest, TResult>(
            "read",
            description,
            traits,
            [at, from, kinds, maxObjects],
            (parse, standard) =>
            {
                int max = parse.GetInRange(maxObjects);
                DocumentTable loaded = table();
                string[] listed = parse.GetValue(kinds) ?? [];
                string[] names = [.. loaded.Kinds.Select(static kind => kind.Name)];
                if (listed.FirstOrDefault(kind => !names.Contains(kind, StringComparer.Ordinal)) is { } unknown)
                {
                    throw CliErrors.OptionInvalid(KindOption, $"'{unknown}' is no kind of the object table", Mistake.Of(unknown, names));
                }

                string? subtree = parse.GetValue(at);
                string? start = parse.GetValue(from);
                if (subtree is not null)
                {
                    TableRead.CheckAddress(loaded, subtree, AtOption);
                }

                if (start is not null)
                {
                    TableRead.CheckAddress(loaded, start, TableRead.FromOption);
                }

                return new TableReadRequest
                {
                    Input = standard.Input,
                    Password = standard.InputPassword,
                    At = subtree,
                    From = start,
                    Kinds = listed,
                    MaxObjects = max,
                };
            })
        {
            Examples = examples ?? [],
            Links = [new CommandHelpLink($"{Schema<TResult>()}/{TableSchema.DirectoryView}", "the kinds an address and --kind name, with their rows"), .. links ?? []],
        };
    }

    /// <summary>Defines the <c>search</c> command, over the text rows the table names.</summary>
    /// <param name="description">The command help.</param>
    /// <param name="traits">The searched document.</param>
    /// <param name="examples">The help examples.</param>
    /// <param name="links">The help links.</param>
    public static CommandDefinition<TableSearchRequest, TResult> Search<TResult>(
        string description,
        CommandTraits traits,
        IReadOnlyList<string>? examples = null,
        IReadOnlyList<CommandHelpLink>? links = null)
        where TResult : TableSearchResult
    {
        var search = new SearchOptions();
        return new CommandDefinition<TableSearchRequest, TResult>(
            "search",
            description,
            traits,
            search.Options,
            (parse, standard) =>
            {
                SearchQuery query = search.Read(parse);
                return new TableSearchRequest { Input = standard.Input, Password = standard.InputPassword, Query = query };
            })
        {
            Examples = examples ?? [],
            Links = links ?? [],
        };
    }

    /// <summary>The command that prints the ops schema of the product whose result <typeparamref name="TResult"/> is.</summary>
    private static string Schema<TResult>() =>
        $"{DistributionInfo.CommandName} schema {ResultEnvelope.CatalogId(ResultEnvelope.OwnerOf(typeof(TResult).Assembly), DocumentTable.SchemaId)}";
}
