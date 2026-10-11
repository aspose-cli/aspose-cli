namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// The core every product's <c>edit --verify</c> reports: whether the staged output passed, the
/// issues found and the operations checked. A product derives one record that declares only its
/// own evidence, which its result schema then lists beside the core; the record may be inherited
/// across assemblies.
/// </summary>
public abstract record EditVerification
{
    /// <summary>Whether the verification found no issue.</summary>
    public required bool Ok { get; init; }

    /// <summary>The problems found, empty when <c>ok</c> is true.</summary>
    public required IReadOnlyList<VerificationIssue> Issues { get; init; }

    /// <summary>
    /// In batch order, the ids of the operations whose effect the verification read back from the
    /// staged output; an operation left out was not checked.
    /// </summary>
    public required IReadOnlyList<string> CheckedOps { get; init; }
}
