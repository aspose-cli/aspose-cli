namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>
/// Compile-time module registration with the product and SDK versions observed
/// by the host source generator.
/// </summary>
public sealed record ProductModuleRegistration(
    string ProductId,
    IProductModule Module,
    string ProductVersion,
    string SdkVersion)
{
    /// <summary>Assembly identity observed by compile-time discovery.</summary>
    public string? AssemblyName { get; init; }

    /// <summary>Fully-qualified module type observed by compile-time discovery.</summary>
    public string? ModuleTypeName { get; init; }
}
