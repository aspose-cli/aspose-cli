using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>Type-safe human-readable renderer contributed by one product.</summary>
public sealed class ProductOutputDefinition
{
    private readonly Action<ResultEnvelope, TableSurface> _render;

    private ProductOutputDefinition(
        Type resultType,
        Action<ResultEnvelope, TableSurface> render)
    {
        ResultType = resultType;
        _render = render;
    }

    /// <summary>Exact result-envelope type handled by this renderer.</summary>
    public Type ResultType { get; }

    /// <summary>Creates a renderer registration for one concrete result type.</summary>
    public static ProductOutputDefinition Create<TResult>(
        Action<TResult, TableSurface> renderer)
        where TResult : ResultEnvelope
    {
        ArgumentNullException.ThrowIfNull(renderer);
        return new ProductOutputDefinition(
            typeof(TResult),
            (result, surface) => renderer((TResult)result, surface));
    }

    internal void Render(ResultEnvelope result, TableSurface surface) =>
        _render(result, surface);
}
