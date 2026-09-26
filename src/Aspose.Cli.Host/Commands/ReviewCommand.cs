using System.CommandLine;
using Aspose.Cli.Host.Catalog;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.Review;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Host.Commands;

/// <summary>Product-neutral static evidence generation for visual review.</summary>
internal static class ReviewCommand
{
    private const string AutoView = "auto";

    public static Command Create(
        CommandExecutor executor,
        ProductCatalog catalog,
        Aspose.Cli.Sdk.Serialization.ContractJsonSerializer serializer,
        GlobalOptions globals)
    {
        var output = new Option<string?>("--out", "-o")
        {
            Description = "New evidence directory; defaults to <filename>.review beside the source.",
        }.WithInput(InputKind.None);
        var maxItems = new Option<int>("--max-items")
        {
            Description = "Maximum visual units rendered and listed in review.json.",
            DefaultValueFactory = _ => ReviewEvidenceWriter.DefaultMaxItems,
        };
        var product = new Option<string?>("--product")
        {
            Description = "Explicit product override; normally inferred from bounded content evidence.",
        }.WithInput(InputKind.None);
        product.AcceptOnlyFromAmong(
            catalog.Products
                .Select(static item => item.Manifest.Id)
                .ToArray());
        var view = new Option<string>("--view")
        {
            Description = "Review view; auto uses the product default.",
            DefaultValueFactory = _ => AutoView,
        }.WithInput(InputKind.None);
        view.AcceptOnlyFromAmong(
            catalog.Products
                .SelectMany(static item => item.View.ReviewViews)
                .Append(AutoView)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray());
        var codes = new Option<string[]>("--code")
        {
            Description = "Report only findings of this check (a code from capabilities review.checks); repeat "
                + "for several. Errors of other checks then do not fail the review.",
        }.WithInput(InputKind.None);
        codes.AcceptOnlyFromAmong(
            catalog.Products
                .SelectMany(static item => item.View.Checks)
                .Select(static check => check.Code)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray());
        var standard = new StandardOptions(new CommandTraits
        {
            Input = new InputDocument(
                "Source file to review; bounded content evidence selects the product.", "the review source"),
            UsesFonts = true,
        });
        Command command = standard.CreateCommand(
            "review",
            "Create a portable static evidence directory for visual inspection.",
            [output, maxItems, product, view, codes]);
        command.SetAction(parse => executor.Run(parse, globals, context =>
        {
            StandardInvocation invocation = standard.Bind(
                parse, context.Paths, context.ResourceBudgets.Inputs, context.ReadEnvironment);
            int maximum = parse.GetValue(maxItems);
            OptionGuards.EnsureInRange(
                "--max-items",
                maximum,
                1,
                ReviewEvidenceWriter.MaximumMaxItems,
                $"Pass a value from 1 to {ReviewEvidenceWriter.MaximumMaxItems}.");
            string input = invocation.Input;
            ProductDefinition definition = catalog
                .ResolveExistingFile(
                    input,
                    parse.GetValue(product),
                    operation: "review",
                    cancellationToken: context.Deadline.Token);
            string requestedView = parse.GetRequiredValue(view);
            ProductViewDefinition views = definition.View;
            string selectedView = requestedView == AutoView
                ? views.ReviewView
                : requestedView;
            if (!views.ReviewViews.Contains(
                    selectedView,
                    StringComparer.Ordinal))
            {
                throw CliErrors.OptionInvalid(
                    "--view",
                    $"review view '{selectedView}' is not supported by {definition.Manifest.Id}",
                    $"Use {string.Join(", ", views.ReviewViews)}.");
            }
            IReadOnlyList<string>? selectedCodes = SelectedCodes(parse.GetValue(codes), definition);
            string target = parse.GetValue(output) is { } requestedOutput
                ? context.Paths.ResolveOutput(requestedOutput)
                : input + ".review";
            ProductBinding binding = context.Activate(definition);
            using IDisposable fontScope = FontProfiles.Use(catalog, definition, binding, invocation.FontDirectories);
            LicenseState license = binding.LicenseGate.EnsureApplied();
            var request = new ViewRenderRequest
            {
                View = selectedView,
                MaxParts = maximum,
                Purpose = ViewPurpose.Evidence,
                Password = invocation.InputPassword,
            };
            return ReviewEvidenceWriter.Write(
                input,
                definition.Manifest.Id,
                target,
                maximum,
                views.VisualInspectionRequired,
                views.Presentation,
                artifacts => views.Render(binding, input, request, artifacts),
                rendered => views.Assess(binding, input, request, rendered),
                license,
                serializer,
                context.ResourceBudgets,
                selectedCodes);
        }));
        return command;
    }

    /// <summary>The requested check codes in code order, each one a check of the reviewed product.</summary>
    private static IReadOnlyList<string>? SelectedCodes(string[]? requested, ProductDefinition definition)
    {
        if (requested is not { Length: > 0 })
        {
            return null;
        }

        string[] available = [.. definition.View.Checks.Select(static check => check.Code)];
        string? foreign = requested.FirstOrDefault(code => !available.Contains(code, StringComparer.Ordinal));
        if (foreign is not null)
        {
            throw CliErrors.OptionInvalidAvailable(
                "--code",
                $"'{foreign}' is not a review check of {definition.Manifest.Id}",
                "Use the codes this product's review reports.",
                available);
        }

        return [.. requested.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }
}
