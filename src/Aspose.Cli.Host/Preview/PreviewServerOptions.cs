using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Host.Preview;

/// <summary>
/// Listener-independent request behavior for one live-preview session. The
/// current snapshot is pulled through <see cref="CurrentSnapshot"/> on every
/// request, which makes atomic snapshot swaps visible immediately.
/// </summary>
/// <param name="View">
/// The view identifier reported to clients in the <c>hello</c> event and in
/// the page metadata (e.g. <c>html</c>). A plain token, embedded in JSON
/// without escaping.
/// </param>
/// <param name="ClientScript">The JavaScript served at <c>/live/client.js</c>.</param>
/// <param name="CurrentSnapshot">
/// Returns the snapshot to serve right now; null until the first render
/// completes (the server shows a waiting page in the meantime).
/// </param>
/// <param name="Hub">The SSE broadcaster that <c>/live/events</c> requests attach to.</param>
/// <param name="ScriptNonce">
/// Per-session CSP nonce applied only to the Host-owned inline metadata
/// bootstrap. Product-authored scripts remain blocked.
/// </param>
/// <param name="StateStorageKey">
/// Opaque per-document identifier used only to isolate browser-tab state.
/// </param>
/// <param name="RefreshRequested">
/// Invoked when a client requests a manual re-render via
/// <c>POST /live/refresh</c>; null leaves that endpoint a 404.
/// </param>
/// <param name="ShellStylesheet">
/// CSS served at <c>/live/shell.css</c> and linked from every composed page;
/// null serves no stylesheet (the route answers 404 and no link is injected).
/// </param>
/// <param name="DocumentName">
/// File name of the watched document, published in the page metadata and used
/// as the image view's page title; null publishes <c>"file":null</c> and
/// falls back to a generic title.
/// </param>
/// <param name="EvalMode">
/// Whether the engine runs under an evaluation license; published in the
/// page metadata so clients can surface the watermark state.
/// </param>
/// <param name="StateEndpoint">
/// Validates and synchronously publishes product-owned view state received
/// through <c>POST /live/state</c>; null leaves that endpoint unavailable.
/// </param>
/// <param name="DocumentPath">
/// Route that serves the composed document. Standalone previews use the
/// origin root; an embedding host can mount the document below its own shell.
/// </param>
/// <param name="SameOriginMount">
/// Whether the preview is mounted below the embedding host's own origin.
/// Mounted responses use same-origin frame and resource policy.
/// </param>
internal sealed record PreviewRequestOptions(
    string View,
    string ClientScript,
    Func<PreviewSnapshot?> CurrentSnapshot,
    LiveEventHub Hub,
    string CsrfToken,
    string ScriptNonce,
    string StateStorageKey,
    Action? RefreshRequested = null,
    string? ShellStylesheet = null,
    string? DocumentName = null,
    bool EvalMode = false,
    string DocumentPath = "/",
    bool SameOriginMount = false,
    PreviewViewStateEndpoint? StateEndpoint = null);

/// <summary>
/// Product-neutral wiring for state validation, synchronous immutable view
/// publication, and subsequent opaque-token routing.
/// </summary>
internal sealed record PreviewViewStateEndpoint(
    Action<ProductPreviewPayload> Validate,
    Func<ProductPreviewPayload, int, PreviewViewPublicationStore.PreviewViewLease> Publish,
    PreviewViewPublicationStore Publications);

/// <summary>
/// Standalone-listener configuration kept separate from the mountable preview
/// request pipeline.
/// </summary>
/// <param name="RequestedPort">The port to bind; 0 picks a free ephemeral port.</param>
/// <param name="Requests">The listener-independent preview request behavior.</param>
internal sealed record PreviewServerOptions(
    int RequestedPort,
    PreviewRequestOptions Requests);
