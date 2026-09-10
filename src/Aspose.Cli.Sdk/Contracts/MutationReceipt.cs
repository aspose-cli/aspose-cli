namespace Aspose.Cli.Sdk.Contracts;

/// <summary>Save/reopen and optional package-level mutation evidence.</summary>
public sealed record MutationReceipt
{
    /// <summary>Verification performed before the output was published.</summary>
    public required string Verification { get; init; }

    /// <summary>Decompressed Office package part changes; absent for non-ZIP formats.</summary>
    public PackageMutationReceipt? Package { get; init; }
}

/// <summary>Deterministic Office package part diff.</summary>
public sealed record PackageMutationReceipt(
    IReadOnlyList<PackagePartChange> ChangedParts,
    int PreservedParts);

/// <summary>One added, modified, or removed package part.</summary>
public sealed record PackagePartChange(string Path, string Change);
