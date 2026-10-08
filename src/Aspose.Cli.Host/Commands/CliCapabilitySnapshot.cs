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

    private CliCapabilitySnapshot(
        CapabilitiesResult result,
        IReadOnlyDictionary<string, string> displayNames)
    {
        Result = result;
        _displayNames = displayNames;
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
                            product.Id,
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
                                product.Id + " ",
                                StringComparison.Ordinal))
                        .Select(command => new CommandCapabilitiesSummary
                        {
                            Command = command.Path[(product.Id.Length + 1)..],
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
        string[] availableCommands = product.Commands
            .Where(candidate => !string.Equals(
                candidate.Path,
                product.Id,
                StringComparison.Ordinal))
            .Select(candidate => candidate.Path[(product.Id.Length + 1)..])
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
            : FilterProduct(product, command);
        string productCommandPrefix = "aspose-cli " + product.Id
            + (command is null ? string.Empty : " " + command);
        return Result with
        {
            Products = [selected],
            EnginePins = Result.EnginePins
                .Where(pin => string.Equals(
                    pin.Product,
                    product.Id,
                    StringComparison.Ordinal))
                .ToArray(),
            Schemas = Result.Schemas.Where(schema =>
                    schema.StartsWith("v2/common/", StringComparison.Ordinal)
                    || schema.StartsWith(
                        $"v2/{product.Id}/",
                        StringComparison.Ordinal))
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
                .Where(candidate => PathMatches(
                    candidate.Path,
                    productCommandPrefix))
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

        IReadOnlyDictionary<string, Command> productCommands =
            root.Subcommands
                .Where(command => catalog.TryGet(
                    command.Name,
                    out ProductDefinition? _))
                .ToDictionary(
                    static command => command.Name,
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
                };
            })
            .ToArray();
        IReadOnlyDictionary<string, ProductCapabilities> productIndex =
            products.ToDictionary(
                static product => product.Id,
                StringComparer.Ordinal);
        products = products
            .Select(product => product with
            {
                Commands = CommandGrammar.Describe(
                    ProductCommand(productCommands, product.Id),
                    productIndex),
            })
            .ToArray();
        ValidateOperationCommands(products);
        productIndex = products.ToDictionary(
            static product => product.Id,
            StringComparer.Ordinal);
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
            Commands = CommandGrammar.Describe(root, productIndex),
            ResourceBudgetContractVersion =
                ResourceBudgetDefaults.ContractVersion,
            ResourceBudgets = ResourceBudgetDefaults.Global
                .OrderBy(static budget => budget.Kind, StringComparer.Ordinal)
                .ToArray(),
            Diagnostics = catalog.Diagnostics.All
                .Where(descriptor =>
                    licensingApplicable
                    || !IsLicenseSurfaceDiagnostic(descriptor.Code))
                .Select(static descriptor => new DiagnosticCapabilities
                {
                    Code = descriptor.Code,
                    Owner = descriptor.Owner,
                    Severity = descriptor.Severity.ToString().ToLowerInvariant(),
                    ExitCode = descriptor.ExitCode is null
                        ? null
                        : (int)descriptor.ExitCode.Value,
                    Category = descriptor.Category,
                    MessageTemplateId = descriptor.MessageTemplateId,
                    HintTemplateId = descriptor.HintTemplateId,
                    DetailsSchemaId = descriptor.DetailsSchemaId,
                })
                .ToArray(),
        },
        catalog.Products.ToDictionary(
            static product => product.Manifest.Id,
            static product => product.Manifest.DisplayName,
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

    private static bool IsLicenseSurfaceDiagnostic(string code) =>
        code.StartsWith("LICENSE_", StringComparison.Ordinal)
        || code is "EVALUATION_LIMIT" or WarningCodes.EvalMode or WarningCodes.EvalInputMarked;

    private static Command ProductCommand(
        IReadOnlyDictionary<string, Command> productCommands,
        string productId) =>
        productCommands.TryGetValue(productId, out Command? command)
            ? command
            : throw new InvalidOperationException(
                $"The executable command tree has no root for product '{productId}'.");

    private static ProductCapabilities FilterProduct(
        ProductCapabilities product,
        string command)
    {
        string path = product.Id + " " + command;
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
        IEnumerable<ProductCapabilities> products)
    {
        foreach (ProductCapabilities product in products)
        {
            var commands = product.Commands
                .Select(command => command.Path)
                .ToHashSet(StringComparer.Ordinal);
            foreach (ProductOperationDescriptor operation in
                product.Operations)
            {
                string path = product.Id + " " + operation.Command;
                if (!commands.Contains(path))
                {
                    throw new InvalidOperationException(
                        $"Product '{product.Id}' operations reference missing command '{operation.Command}'.");
                }
            }
        }
    }
}
