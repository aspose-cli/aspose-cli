namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>
/// The only product-package entry point understood by the host.
/// </summary>
/// <remarks>
/// Implementations must be pure definitions: <see cref="Define"/> must not
/// inspect user files, read process state, initialize an SDK, acquire a license,
/// start a thread, or perform any other observable work.
/// </remarks>
public interface IProductModule
{
    /// <summary>Returns the immutable definition of this product.</summary>
    ProductDefinition Define();
}
