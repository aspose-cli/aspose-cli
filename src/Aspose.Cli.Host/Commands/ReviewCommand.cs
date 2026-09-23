using System.CommandLine;
using Aspose.Cli.Host.Catalog;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.Review;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Rendering;
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
        var file = new Argument<string>("file")
        {
            Description = "Source file to review; bounded content evidence selects the product.",
        }.WithInput(InputKind.File);
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
        var password = new PasswordOptions(
            "--password",
            "the review source",
            allowStdin: true);
        var fonts = new FontDirectoryOptions();
        var command = new Command(
            "review",
            "Create a portable static evidence directory for visual inspection.");
        command.Arguments.Add(file);
        command.Options.Add(output);
        command.Options.Add(maxItems);
        command.Options.Add(product);
        command.Options.Add(view);
        password.AddTo(command);
        fonts.AddTo(command);
        command.SetAction(parse => executor.Run(parse, globals, context =>
        {
            int maximum = parse.GetValue(maxItems);
            OptionGuards.EnsureInRange(
                "--max-items",
                maximum,
                1,
                ReviewEvidenceWriter.MaximumMaxItems,
                $"Pass a value from 1 to {ReviewEvidenceWriter.MaximumMaxItems}.");
            string input = context.Paths.ResolveInput(parse.GetRequiredValue(file));
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
            string target = parse.GetValue(output) is { } requestedOutput
                ? context.Paths.ResolveOutput(requestedOutput)
                : input + ".review";
            ProductBinding binding = context.Activate(definition);
            LicenseState license = binding.LicenseGate.EnsureApplied();
            FontSearchProfile fontProfile = fonts.Read(parse);
            EnsureFontProfileSupported(definition, fontProfile);
            var request = new ViewRenderRequest
            {
                View = selectedView,
                MaxParts = maximum,
                Purpose = ViewPurpose.Evidence,
                Password = password.Resolve(parse, context.ResourceBudgets.Inputs, context.ReadEnvironment),
                FontProfile = fontProfile.IsAmbient ? null : fontProfile,
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
                context.ResourceBudgets);
        }));
        return command;
    }

    private static void EnsureFontProfileSupported(
        ProductDefinition product,
        FontSearchProfile profile)
    {
        if (!profile.IsAmbient
            && !product.Manifest.Engine.SupportsExplicitFontProfiles)
        {
            throw CliErrors.OptionInvalid(
                "--font-dir",
                $"explicit font profiles are not supported by {product.Manifest.Id}",
                "Omit --font-dir or use a product that advertises supportsExplicitFontProfiles.");
        }
    }
}
