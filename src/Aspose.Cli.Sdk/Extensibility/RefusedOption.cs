namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>
/// Options a command does not have but a caller may reach for (<c>RefusedOptions</c> of a command definition).
/// </summary>
/// <param name="Names">The option names, such as <c>--encrypt</c> and <c>--encrypt-env</c>.</param>
/// <param name="Hint">What to do instead, the hint of the error that refuses them.</param>
public sealed record RefusedOption(IReadOnlyList<string> Names, string Hint);
