using Aspose.Cli.Sdk.Preview;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>Product-neutral preview input.</summary>
public sealed record ProductPreviewRequest(
    string View,
    string? Password = null,
    ProductPreviewPayload? Selector = null,
    FontSearchProfile? FontProfile = null);
