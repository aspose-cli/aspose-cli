using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Result of <c>pdf validate</c>, a PDF/A conformance check.</summary>
public sealed record PdfValidateResult() : EngineResultEnvelope("validate-result"), IPartialOutcome
{
    /// <summary>The PDF checked.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>The PDF/A profile checked against.</summary>
    [AllowedValues("pdfa-1b", "pdfa-2b", "pdfa-3b")]
    public required string Profile { get; init; }

    /// <summary>Whether the file conforms to the profile.</summary>
    public required bool Valid { get; init; }

    /// <summary>The first compliance issues; a <c>LIST_TRUNCATED</c> warning names the total when there are more.</summary>
    [MaxItems(100)]
    public required IReadOnlyList<PdfComplianceIssue> Issues { get; init; }

    /// <summary>A file that does not conform makes the command exit 8.</summary>
    [JsonIgnore]
    public bool HasFailures => !Valid;
}

/// <summary>
/// One problem a PDF/A check found, with the clause of the standard it breaks; the
/// <c>details.problems</c> of a failed PDF/A conversion lists the same objects.
/// </summary>
public sealed record PdfComplianceIssue
{
    /// <summary>The code of every PDF/A compliance issue.</summary>
    public const string NonconformanceCode = "PDFA_NONCONFORMANCE";

    /// <summary>What kind of issue this is: always <c>PDFA_NONCONFORMANCE</c>.</summary>
    [AllowedValues(NonconformanceCode)]
    public required string Code { get; init; }

    /// <summary>The problem as the engine states it.</summary>
    public required string Message { get; init; }

    /// <summary>The clause of the PDF/A standard the problem breaks, such as <c>6.3.4</c>; absent when the engine names none.</summary>
    public string? Clause { get; init; }

    /// <summary>How serious the engine rates the problem, such as <c>error</c> or <c>warning</c>.</summary>
    public required string Severity { get; init; }

    /// <summary>The 1-based page the problem is on; absent when it concerns the whole document.</summary>
    [Minimum(1)]
    public int? Page { get; init; }

    /// <summary>Whether <c>pdf convert --to</c> the profile can fix the problem.</summary>
    public required bool Convertible { get; init; }
}
