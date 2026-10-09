using System.CommandLine;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Host.Commands;

/// <summary>
/// Immutable machine-readable view derived from the one executable command
/// tree built for this process.
/// </summary>
internal sealed class CliCapabilitySnapshot
{
    private readonly IReadOnlyDictionary<string, string> _displayNames;
    private readonly IReadOnlyDictionary<string, string> _productRoots;
    private readonly IReadOnlyDictionary<string, string> _commandOwners;
    private readonly IReadOnlyDictionary<string, string> _schemaOwners;

    private CliCapabilitySnapshot(
        CapabilitiesResult result,
        IReadOnlyDictionary<string, string> displayNames,
        IReadOnlyDictionary<string, string> productRoots,
        IReadOnlyDictionary<string, string> commandOwners,
        IReadOnlyDictionary<string, string> schemaOwners)
    {
        Result = result;
        _displayNames = displayNames;
        _productRoots = productRoots;
        _commandOwners = commandOwners;
        _schemaOwners = schemaOwners;
    }

    public CapabilitiesResult Result { get; }

    /// <summary>
    /// Projects the capabilities of every product, or of one, to their commands, formats and
    /// operation names. The product selection and its errors are those of <see cref="Select"/>.
    /// </summary>
    public CapabilitiesSummaryResult Summarize(string? productId)
    {
        CapabilitiesResult selected = Select(productId, commandPath: null);
        return new CapabilitiesSummaryResult
        {
            CliVersion = selected.CliVersion,
            Products = selected.Products
                .Select(product => new ProductCapabilitiesSummary
                {
                    Id = product.Id,
                    Name = _displayNames[product.Id],
                    Description = product.Commands
                        .Single(command => string.Equals(
                            command.Path,
                            _productRoots[product.Id],
                            StringComparison.Ordinal))
                        .Description,
                    Engine = product.Engine?.Id,
                    EngineVersion = product.Engine?.SdkVersion,
                    LoadFormats = product.LoadFormats,
                    ConvertFormats = product.ConvertFormats,
                    RenderFormats = product.RenderFormats,
                    Commands = product.Commands
                        .Where(command => !command.Hidden
                            && command.Path.StartsWith(
                                _productRoots[product.Id] + " ",
                                StringComparison.Ordinal))
                        .Select(command => new CommandCapabilitiesSummary
                        {
                            Command = command.Path[(_productRoots[product.Id].Length + 1)..],
                            Description = command.Description,
                        })
                        .ToArray(),
                    Operations = product.Operations
                        .Select(static operation => new OperationCapabilitiesSummary
                        {
                            Command = operation.Command,
                            Ops = operation.Ops,
                        })
                        .ToArray(),
                })
                .ToArray(),
        };
    }

    public CapabilitiesResult Select(
        string? productId,
        string? commandPath)
    {
        if (string.IsNullOrWhiteSpace(productId))
        {
            return string.IsNullOrWhiteSpace(commandPath)
                ? Result
                : throw SelectionError(
                    "product",
                    productId ?? string.Empty,
                    Result.Products.Select(static product => product.Id));
        }

        ProductCapabilities? product = Result.Products.SingleOrDefault(
            candidate => string.Equals(
                candidate.Id,
                productId.Trim(),
                StringComparison.OrdinalIgnoreCase));
        if (product is null)
        {
            throw SelectionError(
                "product",
                productId,
                Result.Products.Select(static candidate => candidate.Id));
        }

        string? command = string.IsNullOrWhiteSpace(commandPath)
            ? null
            : NormalizeCommand(commandPath);
        string productRoot = _productRoots[product.Id];
        string[] availableCommands = product.Commands
            .Where(candidate => !string.Equals(
                candidate.Path,
                productRoot,
                StringComparison.Ordinal))
            .Select(candidate => candidate.Path[(productRoot.Length + 1)..])
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (command is not null
            && !availableCommands.Contains(command, StringComparer.Ordinal))
        {
            throw SelectionError(
                "command",
                commandPath!,
                availableCommands,
                product.Id);
        }

        ProductCapabilities selected = command is null
            ? product
            : FilterProduct(product, productRoot, command);
        HashSet<string> selectedPaths = selected.Commands
            .Select(static candidate => candidate.Path)
            .ToHashSet(StringComparer.Ordinal);
        return Result with
        {
            Products = [selected],
            EnginePins = Result.EnginePins
                .Where(pin => string.Equals(
                    pin.Product,
                    product.Id,
                    StringComparison.Ordinal))
                .ToArray(),
            // A product's selection keeps the schemas it publishes and those no product owns.
            Schemas = Result.Schemas.Where(schema =>
                    !_schemaOwners.TryGetValue(schema, out string? owner)
                    || string.Equals(owner, product.Id, StringComparison.Ordinal))
                .ToArray(),
            Routing = Result.Routing with
            {
                Routes = Result.Routing.Routes
                    .Where(route => string.Equals(
                        route.Product,
                        product.Id,
                        StringComparison.Ordinal))
                    .ToArray(),
            },
            Commands = Result.Commands
                .Where(candidate =>
                    _commandOwners.TryGetValue(candidate.Path, out string? owner)
                    && string.Equals(owner, product.Id, StringComparison.Ordinal)
                    && selectedPaths.Contains(RelativePath(candidate.Path)))
                .ToArray(),
            Diagnostics = Result.Diagnostics
                .Where(diagnostic => diagnostic.Owner is "common"
                    || string.Equals(
                        diagnostic.Owner,
                        product.Id,
                        StringComparison.Ordinal))
                .ToArray(),
        };
    }

    public static CliCapabilitySnapshot Create(
        Command root,
        ProductCatalog catalog,
        HostSchemaCatalog schemas)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(schemas);

        // A product's commands are those under the command root whose invocation policy names
        // the product where the tree is assembled; a product entry's paths start at that root.
        IReadOnlyDictionary<string, Command> productCommands =
            root.Subcommands
                .Where(static command => command.Policy().ProductId is not null)
                .ToDictionary(
                    static command => command.Policy().ProductId!,
                    StringComparer.Ordinal);
        IReadOnlyList<OwnedCommand> tree = CommandGrammar.Describe(root);
        IReadOnlyDictionary<string, string> productRoots = catalog.Products
            .ToDictionary(
                static product => product.Manifest.Id,
                product => ProductCommand(productCommands, product.Manifest.Id).Name,
                StringComparer.Ordinal);
        ProductCapabilities[] products = catalog.Products
            .Select(product =>
            {
                Command command = ProductCommand(
                    productCommands,
                    product.Manifest.Id);
                ProductCapabilities capabilities = catalog.GetCapabilities(product);
                return capabilities with
                {
                    Verbs = command.Subcommands
                        .Select(static child => child.Name)
                        .ToArray(),
                    Commands = tree
                        .Where(owned => string.Equals(
                            owned.Product,
                            product.Manifest.Id,
                            StringComparison.Ordinal))
                        .Select(static owned => owned.Command with
                        {
                            Path = RelativePath(owned.Command.Path),
                        })
                        .ToArray(),
                };
            })
            .ToArray();
        ValidateOperationCommands(products, productRoots);
        bool licensingApplicable = catalog.Products.Any(
            static product => product.Manifest.Engine.LicenseApplicable);

        return new CliCapabilitySnapshot(new CapabilitiesResult
        {
            CliVersion = VersionInfo.CliVersion,
            SourceRevision = VersionInfo.SourceRevision,
            BuildDirty = VersionInfo.BuildDirty,
            EnginePins = EnginePins(products),
            Products = products,
            Schemas = schemas.Ids,
            Routing = catalog.GetRoutingCapabilities(),
            Commands = tree.Select(static owned => owned.Command).ToArray(),
            ResourceBudgetContractVersion =
                ResourceBudgetDefaults.ContractVersion,
            ResourceBudgets = ResourceBudgetDefaults.Global
                .OrderBy(static budget => budget.Kind, StringComparer.Ordinal)
                .ToArray(),
            Diagnostics = catalog.Diagnostics.All
                .Concat(HostDiagnostics.All)
                .Where(descriptor => licensingApplicable || !descriptor.LicenseSurface)
                .OrderBy(static descriptor => descriptor.Code, StringComparer.Ordinal)
                .Select(static descriptor => new DiagnosticCapabilities
                {
                    Code = descriptor.Code,
                    Owner = descriptor.Owner,
                    Severity = descriptor.Severity.ToString().ToLowerInvariant(),
                    ExitCode = descriptor.ExitCode is null
                        ? null
                        : (int)descriptor.ExitCode.Value,
                    Category = descriptor.Category,
                    DetailsSchemaId = descriptor.DetailsSchemaId,
                })
                .ToArray(),
        },
        catalog.Products.ToDictionary(
            static product => product.Manifest.Id,
            static product => product.Manifest.DisplayName,
            StringComparer.Ordinal),
        productRoots,
        tree
            .Where(static owned => owned.Product is not null)
            .ToDictionary(
                static owned => owned.Command.Path,
                static owned => owned.Product!,
                StringComparer.Ordinal),
        catalog.Products
            .SelectMany(product => catalog.Resources.GetProduct(product.Manifest.Id).SchemaIds
                .Select(schema => (Schema: schema, Owner: product.Manifest.Id)))
            .ToDictionary(
                static owned => owned.Schema,
                static owned => owned.Owner,
                StringComparer.Ordinal));
    }

    /// <summary>The engine pin of each product, as capabilities and <c>--version</c> report them.</summary>
    public static IReadOnlyList<EnginePinCapabilities> EnginePins(
        IEnumerable<ProductCapabilities> products) =>
        products
            .Where(static product => product.Engine is not null)
            .Select(static product => new EnginePinCapabilities
            {
                Product = product.Id,
                Engine = product.Engine!.Id,
                Version = product.Engine.SdkVersion,
            })
            .OrderBy(static pin => pin.Product, StringComparer.Ordinal)
            .ToArray();

    private static Command ProductCommand(
        IReadOnlyDictionary<string, Command> productCommands,
        string productId) =>
        productCommands.TryGetValue(productId, out Command? command)
            ? command
            : throw new InvalidOperationException(
                $"The executable command tree has no root for product '{productId}'.");

    /// <summary>A command path relative to the executable: without the root command's name.</summary>
    private static string RelativePath(string path) =>
        path.IndexOf(' ', StringComparison.Ordinal) is var separator and >= 0
            ? path[(separator + 1)..]
            : string.Empty;

    private static ProductCapabilities FilterProduct(
        ProductCapabilities product,
        string productRoot,
        string command)
    {
        string path = productRoot + " " + command;
        return product with
        {
            Verbs = [command.Split(' ', 2)[0]],
            Operations = product.Operations
                .Where(operation => PathMatches(
                    operation.Command,
                    command)
                    || PathMatches(command, operation.Command))
                .ToArray(),
            Commands = product.Commands
                .Where(candidate => PathMatches(
                    candidate.Path,
                    path))
                .ToArray(),
        };
    }

    private static bool PathMatches(
        string path,
        string prefix) =>
        string.Equals(path, prefix, StringComparison.Ordinal)
        || path.StartsWith(prefix + " ", StringComparison.Ordinal);

    private static string NormalizeCommand(string command) =>
        string.Join(
            ' ',
            command.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries));

    private static CliException SelectionError(
        string selection,
        string requested,
        IEnumerable<string> available,
        string? product = null)
    {
        string scope = product is null
            ? "this build"
            : $"product '{product}'";
        return CliErrors.OptionInvalid(
            selection,
            $"unknown {selection} '{requested}' for {scope}",
            selection == "product"
                ? "Choose a product from details.available."
                : "Choose a product-relative command path from details.available.",
            Mistake.Of(requested, available.Order(StringComparer.Ordinal)));
    }

    private static void ValidateOperationCommands(
        IEnumerable<ProductCapabilities> products,
        IReadOnlyDictionary<string, string> productRoots)
    {
        foreach (ProductCapabilities product in products)
        {
            var commands = product.Commands
                .Select(command => command.Path)
                .ToHashSet(StringComparer.Ordinal);
            foreach (ProductOperationDescriptor operation in
                product.Operations)
            {
                string path = productRoots[product.Id] + " " + operation.Command;
                if (!commands.Contains(path))
                {
                    throw new InvalidOperationException(
                        $"Product '{product.Id}' operations reference missing command '{operation.Command}'.");
                }
            }
        }
    }
}
