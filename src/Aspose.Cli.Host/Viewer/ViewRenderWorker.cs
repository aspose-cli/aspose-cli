using System.Text;
using System.Text.Json;
using Aspose.Cli.Host.Catalog;
using Aspose.Cli.Host.Commands;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.Licensing;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.ViewerService;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Serialization;
using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Host.Viewer;

/// <summary>
/// Serves the viewer service's render requests in a child process that keeps
/// the product engines warm. Every request runs in its own composition root,
/// with its own deadline and resource ledger, so one render never inherits
/// the budgets of the last. The worker exits when its input ends, and asks to
/// be recycled as soon as the license it applied for a product no longer
/// matches the configured one, because an engine cannot swap a license.
/// </summary>
internal static class ViewRenderWorker
{
    public static int Run(ProductCatalog catalog, GlobalValues globals)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(globals);

        using Stream input = Console.OpenStandardInput();
        using Stream output = Console.OpenStandardOutput();
        LocalServiceResourceLimits limits = LocalServiceResourceLimits.Resolve();
        var licenses = new Dictionary<string, string>(StringComparer.Ordinal);
        while (true)
        {
            RenderWorkerRequest? request = ProcessPipeMessages
                .ReadOrEndAsync<RenderWorkerRequest>(input, CancellationToken.None)
                .GetAwaiter().GetResult();
            if (request is null)
            {
                return 0;
            }

            RenderWorkerResponse response = Serve(catalog, globals, limits, licenses, request);
            ProcessPipeMessages.WriteAsync(output, response, CancellationToken.None)
                .GetAwaiter().GetResult();
            if (response.Recycle)
            {
                return 0;
            }
        }
    }

    private static RenderWorkerResponse Serve(
        ProductCatalog catalog,
        GlobalValues globals,
        LocalServiceResourceLimits limits,
        Dictionary<string, string> licenses,
        RenderWorkerRequest request)
    {
        try
        {
            TimeSpan budget = TimeSpan.FromMilliseconds(request.TimeoutMs);
            using var deadline = request.ExpiresAtTick is { } expires
                ? OperationDeadline.FromAbsoluteTick(budget, expires) : OperationDeadline.Start(budget);
            deadline.ThrowIfExpired("render-admission");
            CommandContext context = CompositionRoot.Create(catalog, globals with
            {
                LicensePath = request.License ?? globals.LicensePath,
                MaxInputBytes = request.MaxInputBytes,
            }, deadline);
            if (request.SourceOrigin is { } origin)
            {
                context.ResourceBudgets.Inputs.SetSnapshotOrigin(request.Source, origin);
            }
            ProductDefinition definition = catalog.ResolveExistingFile(
                request.Source,
                request.Product,
                operation: "preview",
                cancellationToken: context.Deadline.Token);
            ProductBinding binding = context.Activate(definition);
            string product = definition.Manifest.Id;
            using IDisposable fontScope = FontProfiles.Use(
                catalog,
                definition,
                binding,
                FontSearchProfile.Explicit(request.FontDirectories ?? []));
            string license = LicenseFingerprint.Of(binding.LicenseGate.Resolution);
            if (licenses.TryGetValue(product, out string? applied) && applied != license)
            {
                return new RenderWorkerResponse { Id = request.Id, Ok = false, Recycle = true };
            }

            ProductViewDefinition views = definition.View;
            var render = new ViewRenderRequest
            {
                View = request.View ?? views.LiveView,
                MaxParts = request.MaxParts,
                Purpose = ViewPurpose.Display,
                Password = request.Password,
            };
            LicenseState state = binding.LicenseGate.EnsureApplied();
            licenses[product] = license;
            ViewManifest manifest = ViewRendering.Render(
                artifacts => views.Render(binding, request.Source, render, artifacts),
                request.Output,
                request.MaxParts,
                limits);
            File.WriteAllText(
                Path.Combine(request.Output, RenderWorkerProtocol.ManifestFileName),
                JsonSerializer.Serialize(manifest, SdkJsonContext.Default.ViewManifest),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            PrivateUserStorage.ProtectTree(request.Output);
            return new RenderWorkerResponse
            {
                Id = request.Id,
                Ok = true,
                Product = product,
                View = manifest.View,
                License = state.ToContractName(),
                TotalParts = manifest.TotalParts,
                PresenterScript = request.Presentation ? views.Presentation.Script : null,
                PresenterStylesheet = request.Presentation ? views.Presentation.Stylesheet : null,
            };
        }
        catch (Exception exception)
        {
            return new RenderWorkerResponse
            {
                Id = request.Id,
                Ok = false,
                Code = exception is CliException cli ? cli.Code.Name : ErrorCodes.Internal.Name,
                Exit = (int)(exception is CliException failure ? failure.Code.ExitCode : ExitCode.Internal),
                Message = Sanitize(exception.Message, request.Source),
            };
        }
    }

    /// <summary>The service owns the rendered copy; its path is never the user's.</summary>
    private static string Sanitize(string message, string source) =>
        message.Replace(source, Path.GetFileName(source), StringComparison.OrdinalIgnoreCase);
}
