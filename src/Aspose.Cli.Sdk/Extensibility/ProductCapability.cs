namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>
/// Shared, strongly typed cross-product capability slot. Slot identity is the
/// object instance declared by a neutral contract package, never a string,
/// product id, runtime type lookup, or service locator key.
/// </summary>
/// <typeparam name="TCapability">Narrow capability interface.</typeparam>
public sealed class ProductCapability<TCapability>
    where TCapability : class
{
    /// <summary>Creates a typed slot with a diagnostic-only display name.</summary>
    public ProductCapability(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        DisplayName = displayName;
    }

    /// <summary>Name used only in build-time validation messages.</summary>
    public string DisplayName { get; }
}

/// <summary>How one product relates to a predeclared typed capability slot.</summary>
public enum ProductCapabilityRelation
{
    /// <summary>The product provides the slot.</summary>
    Provides,

    /// <summary>The product can use the slot when a provider is compiled.</summary>
    Optional,
}

/// <summary>Type-erased declaration retained only inside the immutable catalog.</summary>
public sealed class ProductCapabilityDeclaration
{
    private ProductCapabilityDeclaration(
        object slot,
        Type capabilityType,
        string displayName,
        ProductCapabilityRelation relation)
    {
        Slot = slot;
        CapabilityType = capabilityType;
        DisplayName = displayName;
        Relation = relation;
    }

    /// <summary>Narrow interface carried by the typed slot.</summary>
    public Type CapabilityType { get; }

    /// <summary>Diagnostic-only slot name.</summary>
    public string DisplayName { get; }

    /// <summary>Provider or optional-consumer relation.</summary>
    public ProductCapabilityRelation Relation { get; }

    internal object Slot { get; }

    internal static ProductCapabilityDeclaration Create<TCapability>(
        ProductCapability<TCapability> slot,
        ProductCapabilityRelation relation)
        where TCapability : class
    {
        ArgumentNullException.ThrowIfNull(slot);
        return new ProductCapabilityDeclaration(
            slot,
            typeof(TCapability),
            slot.DisplayName,
            relation);
    }
}

/// <summary>
/// Type-erased provider factory retained inside one immutable product
/// definition. The factory receives only that provider's typed binding.
/// </summary>
internal sealed class ProductCapabilityProviderDefinition
{
    private ProductCapabilityProviderDefinition(
        object slot,
        Func<object, object> create)
    {
        Slot = slot;
        Create = create;
    }

    public object Slot { get; }

    public Func<object, object> Create { get; }

    public static ProductCapabilityProviderDefinition CreateTyped<TPort, TCapability>(
        ProductCapability<TCapability> slot,
        Func<ProductBinding<TPort>, TCapability> factory)
        where TPort : class
        where TCapability : class
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(factory);
        return new ProductCapabilityProviderDefinition(
            slot,
            binding => factory((ProductBinding<TPort>)binding)
                ?? throw new InvalidOperationException(
                    $"Capability provider '{slot.DisplayName}' returned null."));
    }
}
