using Aspose.Cli.Host.LocalServices;

namespace Aspose.Cli.Host.Viewer;

/// <summary>
/// The frame contract between the viewer service and its warm render worker.
/// Requests and responses travel as bounded JSON frames over the worker's
/// standard input and output; everything in them is product-neutral, so the
/// service never loads a product, an engine or a license.
/// </summary>
internal static class RenderWorkerProtocol
{
    /// <summary>Hidden command that serves render requests.</summary>
    public const string CommandName = "__render-worker";

    /// <summary>File the worker writes the rendered view manifest to.</summary>
    public const string ManifestFileName = "view.json";
}

/// <summary>One render the service asks the warm worker for.</summary>
internal sealed record RenderWorkerRequest(long MaxInputBytes = Aspose.Cli.Sdk.IO.ResourceBudgetDefaults.DefaultInputBytes)
{
    /// <summary>Correlates the response; unique for the life of the service.</summary>
    public required int Id { get; init; }

    /// <summary>Absolute path of the document copy to render.</summary>
    public required string Source { get; init; }

    /// <summary>Original document path defining the verified local resource boundary of a snapshot.</summary>
    public string? SourceOrigin { get; init; }

    /// <summary>Absolute path of the empty dedicated directory to render into.</summary>
    public required string Output { get; init; }

    /// <summary>Upper bound of rendered parts.</summary>
    public required int MaxPartCount { get; init; }

    /// <summary>Wall-clock budget for this render; the worker fails cleanly within it.</summary>
    public required int TimeoutMs { get; init; }

    /// <summary>Parent-created monotonic expiration shared with the worker.</summary>
    public long? ExpiresAtTick { get; init; }

    /// <summary>Product id, or null to select the product from the document.</summary>
    public string? Product { get; init; }

    /// <summary>View id, or null for the product's live view.</summary>
    public string? View { get; init; }

    /// <summary>Password of an encrypted document.</summary>
    public WireSecret? Password { get; init; }

    /// <summary>Explicit license file, or null for the configured sources.</summary>
    public string? License { get; init; }

    /// <summary>True for <c>--license-mode evaluation</c>: no license source is read.</summary>
    public bool EvaluationRequested { get; init; }

    /// <summary>The license context of the opening command, or null when it names its license.</summary>
    public RequesterLicense? RequesterLicense { get; init; }

    /// <summary>Explicit font directories, or null for the ambient environment.</summary>
    public IReadOnlyList<string>? FontDirectories { get; init; }

    /// <summary>Whether the response carries the product presenter assets.</summary>
    public bool Presentation { get; init; }
}

/// <summary>What one render produced, or why it did not.</summary>
internal sealed record RenderWorkerResponse
{
    /// <summary>Id of the request this answers.</summary>
    public required int Id { get; init; }

    /// <summary>Whether the view was rendered into the requested directory.</summary>
    public required bool Ok { get; init; }

    /// <summary>Product that rendered the document.</summary>
    public string? Product { get; init; }

    /// <summary>View the product rendered.</summary>
    public string? View { get; init; }

    /// <summary>License mode the engine rendered under.</summary>
    public string? License { get; init; }

    /// <summary>Parts the document contains, including those beyond the bound.</summary>
    public int TotalPartCount { get; init; }

    /// <summary>Product presenter script, when the request asked for it.</summary>
    public string? PresenterScript { get; init; }

    /// <summary>Product presenter stylesheet, when it ships one.</summary>
    public string? PresenterStylesheet { get; init; }

    /// <summary>Why the render failed, naming the previewed document rather than its copy.</summary>
    public WireError? Error { get; init; }

    /// <summary>
    /// Set when the worker is exiting because the license it applied no longer
    /// matches the configured one, or an evaluation engine refused more files
    /// after earlier requests; the supervisor restarts it and retries once.
    /// </summary>
    public bool Recycle { get; init; }
}

/// <summary>The frames of the render worker, in the Host's process frame format.</summary>
[System.Text.Json.Serialization.JsonSourceGenerationOptions(
    System.Text.Json.JsonSerializerDefaults.Web,
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
[System.Text.Json.Serialization.JsonSerializable(typeof(RenderWorkerRequest))]
[System.Text.Json.Serialization.JsonSerializable(typeof(RenderWorkerResponse))]
internal sealed partial class RenderWorkerJsonContext : System.Text.Json.Serialization.JsonSerializerContext;
