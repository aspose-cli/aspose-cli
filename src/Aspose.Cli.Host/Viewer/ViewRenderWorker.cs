using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Host.Catalog;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.Licensing;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.ViewerService;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Errors;
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
/// matches the configured one, because an engine cannot swap a license, or
/// when an evaluation engine refuses more files after earlier requests: an
/// evaluation engine counts the files one process opens, so a new process can
/// serve the request.
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
        bool served = false;
        while (true)
        {
            RenderWorkerRequest? request = ProcessFrames
                .ReadOrEndAsync(input, RenderWorkerJsonContext.Default.RenderWorkerRequest, CancellationToken.None)
                .GetAwaiter().GetResult();
            if (request is null)
            {
                return 0;
            }

            RenderWorkerResponse response = Serve(catalog, globals, limits, licenses, request, served);
            served = true;
            ProcessFrames.WriteAsync(output, response, RenderWorkerJsonContext.Default.RenderWorkerResponse, CancellationToken.None)
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
        RenderWorkerRequest request,
        bool served)
    {
        try
        {
            TimeSpan budget = TimeSpan.FromMilliseconds(request.TimeoutMs);
            using var deadline = request.ExpiresAtTick is { } expires
                ? OperationDeadline.FromAbsoluteTick(budget, expires) : OperationDeadline.Start(budget);
            deadline.ThrowIfExpired("render-admission");
            // Each document carries the license context of the command that opened it; the
            // environment and working directory the service started with never make one.
            RequesterLicense? requester = request.RequesterLicense;
            Func<string, string?> environment = name => requester?.Read(name);
            CommandContext context = CompositionRoot.Create(catalog, globals with
            {
                LicensePath = request.EvaluationRequested ? null : request.License,
                EvaluationRequested = request.EvaluationRequested,
                MaxInputBytes = request.MaxInputBytes,
                WorkDir = requester?.WorkDirectory ?? globals.WorkDir,
            }, deadline, licenseEnvironment: environment);
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
            using IDisposable fontScope = binding.UseFonts(
                FontSearchProfile.Explicit(request.FontDirectories ?? []));
            string license = LicenseFingerprint.Of(binding.LicenseGate.Resolution, environment);
            if (licenses.TryGetValue(product, out string? applied) && applied != license)
            {
                return new RenderWorkerResponse { Id = request.Id, Ok = false, Recycle = true };
            }

            ProductViewDefinition views = definition.View;
            var render = new ViewRenderRequest
            {
                View = request.View ?? views.LiveView,
                MaxPartCount = request.MaxPartCount,
                Purpose = ViewPurpose.Display,
                Password = request.Password?.ToSecret(),
            };
            LicenseState state = binding.LicenseGate.EnsureApplied();
            licenses[product] = license;
            ViewManifest manifest = ViewRendering.Render(
                artifacts => views.Render(binding, request.Source, render, artifacts),
                request.Output,
                request.MaxPartCount,
                limits);
            File.WriteAllText(
                Path.Combine(request.Output, RenderWorkerProtocol.ManifestFileName),
                JsonSerializer.Serialize(manifest, SdkJsonContext.Default.ViewManifest),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return new RenderWorkerResponse
            {
                Id = request.Id,
                Ok = true,
                Product = product,
                View = manifest.View,
                License = state.ToContractName(),
                TotalPartCount = manifest.TotalPartCount,
                PresenterScript = request.Presentation ? views.Presentation.Script : null,
                PresenterStylesheet = request.Presentation ? views.Presentation.Stylesheet : null,
            };
        }
        // An evaluation limit can be the worker's own doing: Aspose.Cells evaluation mode opens at
        // most 100 files in one process, which a resident worker reaches across requests while a
        // single command never does. A worker that already served a request is recycled and the
        // request retried in a fresh one, which reports a limit the document itself reaches.
        catch (CliException exception) when (served && exception.Code == ErrorCodes.EvaluationLimit)
        {
            return new RenderWorkerResponse { Id = request.Id, Ok = false, Recycle = true };
        }
        catch (Exception exception)
        {
            WireError error = exception is CliException cli
                ? WireError.From(cli)
                : new WireError(ErrorCodes.Internal.Name, (int)ExitCode.Internal, exception.Message);
            return new RenderWorkerResponse
            {
                Id = request.Id,
                Ok = false,
                Error = error with
                {
                    Message = Shown(error.Message, request),
                    Hint = error.Hint is { } hint ? Shown(hint, request) : null,
                    Details = error.Details is { } details ? Shown(details, request) : null,
                },
            };
        }
    }

    /// <summary>
    /// The service renders its own copy of the document, so an error names the copy; the person
    /// previewed the original, which the error names instead, or its file name without one.
    /// </summary>
    private static string Shown(string text, RenderWorkerRequest request) =>
        text.Replace(request.Source, request.SourceOrigin ?? Path.GetFileName(request.Source), FilePaths.Comparison);

    private static JsonObject Shown(JsonObject details, RenderWorkerRequest request)
    {
        JsonObject shown = details.DeepClone().AsObject();
        Replace(shown);
        return shown;

        void Replace(JsonNode? node)
        {
            switch (node)
            {
                case JsonObject members:
                    foreach (string name in members.Select(static member => member.Key).ToArray())
                    {
                        if (members[name] is JsonValue value && value.TryGetValue(out string? text))
                        {
                            members[name] = Shown(text, request);
                        }
                        else
                        {
                            Replace(members[name]);
                        }
                    }
                    break;
                case JsonArray items:
                    for (int index = 0; index < items.Count; index++)
                    {
                        if (items[index] is JsonValue value && value.TryGetValue(out string? text))
                        {
                            items[index] = Shown(text, request);
                        }
                        else
                        {
                            Replace(items[index]);
                        }
                    }
                    break;
            }
        }
    }
}
