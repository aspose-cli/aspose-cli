using System.Text.Json.Nodes;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>
/// One operation that succeeds on the product's fixture document (<c>ScenarioFixtures</c>), for the
/// rules that run operations: C-R19 (a <c>set_</c> operation applied twice leaves what applying it
/// once leaves, an <c>add_</c> operation applied twice adds twice), C-R21 (the state fields
/// read paths the ops schemas declare read back) and C-R30 (its outcome tells the truth). The schemas say what an operation accepts, not
/// what the fixture holds, so the samples are written by hand against the fixtures: Sheet1 with
/// A1:B3 filled, one PDF page and five Word blocks. Operations that read a file name <c>other.&lt;format&gt;</c> (the fixture
/// again), <c>image.png</c> or <c>note.txt</c>; a secret field names <see cref="RuleWorkspace.SecretVariable"/>.
/// </summary>
/// <param name="Product">The product whose edit applies the operation.</param>
/// <param name="Json">The operation object.</param>
/// <param name="Setup">Operations that first build what the operation acts on, as a JSON array: an
/// edit applies them to the fixture, and the operation then edits that result.</param>
/// <param name="SetupArgs">Options of the setup edit, such as change tracking.</param>
/// <param name="Label">What sets this sample apart from another of the same operation, such as the
/// empty range on which it has nothing to do.</param>
/// <param name="Encrypted">Whether the setup or the operation encrypts the document, so every later
/// command opens it with the secret.</param>
/// <param name="Unseen">Why no comparison sees the operation's change, so C-R30 does not judge
/// whether its output differs from its input.</param>
/// <param name="Unrepeatable">Why the operation cannot be applied twice, so C-R19 does not judge it.</param>
/// <param name="FromHtml">The HTML the product's <c>create --from-html</c> turns into the document
/// the operation edits, instead of the fixture: a PDF with a form field.</param>
internal sealed record OperationSample(
    string Product,
    string Json,
    string? Setup = null,
    string[]? SetupArgs = null,
    string? Label = null,
    bool Encrypted = false,
    string? Unseen = null,
    string? Unrepeatable = null,
    string? FromHtml = null)
{
    /// <summary>The operation name.</summary>
    public string Op { get; } = JsonNode.Parse(Json)!["op"]!.GetValue<string>();

    /// <summary>Whether the operation edits the unedited fixture.</summary>
    public bool OnFixture => Setup is null && FromHtml is null && !Encrypted;

    /// <summary>The case subject: the product, the operation and its label.</summary>
    public string Subject => $"{Product} {Op}{(Label is null ? string.Empty : " on " + Label)}";
}

internal static class OperationSamples
{
    // rule-exemptions: begin
    /// <summary>
    /// Operations of the catalog that have no sample, each with the reason; an entry whose
    /// operation has a sample, or is gone, fails the coverage case.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Exempt { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
    // rule-exemptions: end

    /// <summary>One sample per operation of every product, and a labeled one for each case a rule names.</summary>
    public static IReadOnlyList<OperationSample> All { get; } =
    [
    ];
}
