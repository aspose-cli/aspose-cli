using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Host.Preview;

internal sealed record PreviewStartState(
    PreviewSessionState Session,
    bool Reused,
    ResultEnvelopeMetadata Metadata);

internal sealed record PreviewStatusState(
    IReadOnlyList<PreviewSessionState> Sessions,
    IReadOnlyList<Warning> Warnings);

internal sealed record PreviewStopState(
    IReadOnlyList<string> Stopped,
    IReadOnlyList<PreviewSessionState> Sessions);

internal sealed record PreviewSessionState(
    string Id,
    string Product,
    string Url,
    int Pid,
    string File,
    string View,
    ProductPreviewPayload? Selector,
    int? Revision = null,
    ProductPreviewPayload? State = null);

internal sealed record PreviewInteractiveState(
    int Revision,
    ProductPreviewPayload? State);

internal sealed record PreviewPublishedState(
    PreviewSnapshot? Snapshot,
    int Revision,
    ProductPreviewPayload? State);
