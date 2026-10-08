using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.IO;

/// <summary>The errors of output publication, which report its snapshots and recovery.</summary>
internal static class PublicationErrors
{
    internal static CliException OutputConflict(
        string path,
        FilePublicationSnapshot expected,
        FilePublicationSnapshot? actual,
        Exception? inner = null) => CliException.Create(
        ErrorCodes.OutputConflict,
        $"Output changed while the operation was preparing to publish it: {path}",
        hint: "Inspect the newer file, then retry from that version or choose a different output path.",
        details: new JsonObject
        {
            ["path"] = path,
            ["expectedExists"] = expected.Exists,
            ["expectedSizeBytes"] = expected.Exists ? expected.Length : null,
            ["expectedSha256"] = expected.Sha256,
            ["actualExists"] = actual?.Exists,
            ["actualSizeBytes"] = actual is { Exists: true } ? actual.Length : null,
            ["actualSha256"] = actual?.Sha256,
        },
        innerException: inner);

    internal static CliException OutputPublicationFailure(
        Exception commitFailure,
        PublicationRecoveryReport recovery)
    {
        ArgumentNullException.ThrowIfNull(commitFailure);
        ArgumentNullException.ThrowIfNull(recovery);
        var items = new JsonArray();
        foreach (PublicationRecoveryItem item in recovery.Items)
        {
            items.Add(new JsonObject
            {
                ["target"] = item.Target,
                ["originalExisted"] = item.OriginalExisted,
                ["published"] = item.Published,
                ["status"] = item.Status,
                ["contentVerified"] = item.ContentVerified,
                ["metadataVerified"] = item.MetadataVerified,
                ["failure"] = item.Failure,
            });
        }

        string originalCode = commitFailure is CliException cli
            ? cli.Code.Name
            : ErrorCodes.OutputUnwritable.Name;
        var details = new JsonObject
        {
            ["originalError"] = new JsonObject
            {
                ["code"] = originalCode,
                ["details"] = commitFailure is CliException source && source.Details is not null
                    ? source.Details.DeepClone()
                    : null,
            },
            ["recoveryComplete"] = recovery.RecoveryComplete,
            ["targets"] = items,
        };
        if (recovery.RecoveryComplete
            && commitFailure is CliException original)
        {
            JsonObject originalDetails = original.Details?.DeepClone().AsObject() ?? [];
            originalDetails["recoveryComplete"] = true;
            originalDetails["targets"] = items.DeepClone();
            return CliException.Create(
                original.Code,
                original.Message,
                hint: original.Hint,
                details: originalDetails,
                docs: original.Docs,
                innerException: original);
        }

        return CliException.Create(
            recovery.RecoveryComplete
                ? ErrorCodes.OutputPublicationFailed
                : ErrorCodes.OutputPublicationPartial,
            recovery.RecoveryComplete
                ? "The output set could not be published; every target was restored and verified."
                : "The output set could not be published and recovery left one or more targets in an unknown or mixed state.",
            hint: recovery.RecoveryComplete
                ? "Correct the original output error and retry."
                : "Stop modifying the listed targets, inspect details.targets, and restore unknown targets from a trusted backup before retrying.",
            details: details,
            innerException: commitFailure);
    }
}
