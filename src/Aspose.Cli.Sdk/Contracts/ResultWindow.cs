namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// How much of a larger collection a bounded read or search returned, and the option values
/// that continue it. Every windowed result carries it as <c>window</c>, written after
/// the payload and before <c>license</c> and <c>warnings</c>, so an agent always finds it in
/// the same place.
/// </summary>
[SchemaId("result-window")]
public sealed record ResultWindow
{
    /// <summary>The unit <see cref="Returned"/> and <see cref="Total"/> count, e.g. <c>page</c>, <c>block</c> or <c>hit</c>.</summary>
    public required string Unit { get; init; }

    /// <summary>How many units this result returned.</summary>
    [Minimum(0)]
    public required int Returned { get; init; }

    /// <summary>How many units the selection holds; omitted when the read stopped before counting them all.</summary>
    [Minimum(0)]
    public long? Total { get; init; }

    /// <summary>Whether units remain after the returned ones, or the last one was cut short.</summary>
    public required bool Truncated { get; init; }

    /// <summary>
    /// The values of the command's options that return the next window, by option name without its
    /// leading dashes: the same command with these values returns it. Omitted when nothing remains.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Next { get; init; }

    /// <summary>The values of <see cref="Next"/>, each under its option's name without the leading dashes.</summary>
    /// <param name="options">Each option, as in <c>--skip</c>, and its value.</param>
    public static IReadOnlyDictionary<string, string> Options(params (string Option, string Value)[] options) =>
        options.ToDictionary(static option => option.Option.TrimStart('-'), static option => option.Value, StringComparer.Ordinal);
}
