using System.Text.Json.Serialization;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// Result of <c>aspose-cli doctor</c>: environment self-diagnosis so an agent (or a
/// user) can tell whether the CLI is ready — licensed, on a supported runtime,
/// able to write output — before a task fails midway.
/// </summary>
public sealed record DoctorResult() : ResultEnvelope(CommonSchemaIds.Doctor, 2)
{
    /// <summary><c>true</c> when no check failed; warnings still allow readiness.</summary>
    [JsonPropertyOrder(-50)]
    public required bool Ok { get; init; }

    /// <summary>The diagnostic checks, in run order.</summary>
    public required IReadOnlyList<DoctorCheck> Checks { get; init; }

    /// <summary>Per-product readiness checks.</summary>
    public IReadOnlyList<DoctorProductStatus>? Products { get; init; }
}

/// <summary>Readiness of one compiled-in product.</summary>
public sealed record DoctorProductStatus
{
    public required string Product { get; init; }
    public required string LicenseMode { get; init; }
    public required string Engine { get; init; }
}

/// <summary>Outcome of one diagnostic check.</summary>
public sealed record DoctorCheck
{
    /// <summary>Short stable identifier, e.g. <c>license</c> or <c>runtime</c>.</summary>
    public required string Name { get; init; }

    /// <summary>One of the <see cref="DoctorStatuses"/> values.</summary>
    public required string Status { get; init; }

    /// <summary>What was found.</summary>
    public required string Detail { get; init; }

    /// <summary>How to improve a warn/fail result; omitted when ok.</summary>
    public string? Hint { get; init; }
}

/// <summary>Accepted values of <see cref="DoctorCheck.Status"/>.</summary>
public static class DoctorStatuses
{
    /// <summary>The check passed.</summary>
    public const string Ok = "ok";

    /// <summary>A non-fatal concern (e.g. evaluation mode).</summary>
    public const string Warn = "warn";

    /// <summary>A blocking problem.</summary>
    public const string Fail = "fail";
}
