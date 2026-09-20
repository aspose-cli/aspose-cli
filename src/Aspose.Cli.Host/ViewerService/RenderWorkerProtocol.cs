namespace Aspose.Cli.Host.ViewerService;

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
internal sealed record RenderWorkerRequest
{
    /// <summary>Correlates the response; unique for the life of the service.</summary>
    public required int Id { get; init; }

    /// <summary>Absolute path of the document copy to render.</summary>
    public required string Source { get; init; }

    /// <summary>Absolute path of the empty private directory to render into.</summary>
    public required string Output { get; init; }

    /// <summary>Upper bound of rendered parts.</summary>
    public required int MaxParts { get; init; }

    /// <summary>Wall-clock budget for this render; the worker fails cleanly within it.</summary>
    public required int TimeoutMs { get; init; }

    /// <summary>Product id, or null to select the product from the document.</summary>
    public string? Product { get; init; }

    /// <summary>View id, or null for the product's live view.</summary>
    public string? View { get; init; }

    /// <summary>Password of an encrypted document.</summary>
    public string? Password { get; init; }

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

    /// <summary>Parts the document contains, including those beyond the bound.</summary>
    public int TotalParts { get; init; }

    /// <summary>Product presenter script, when the request asked for it.</summary>
    public string? PresenterScript { get; init; }

    /// <summary>Product presenter stylesheet, when it ships one.</summary>
    public string? PresenterStylesheet { get; init; }

    /// <summary>Stable CLI error code when the render failed.</summary>
    public string? Code { get; init; }

    /// <summary>Message for the person, already free of private paths.</summary>
    public string? Message { get; init; }

    /// <summary>
    /// Set when the worker is exiting because the license it applied no longer
    /// matches the configured one; the supervisor restarts it and retries.
    /// </summary>
    public bool Recycle { get; init; }
}
