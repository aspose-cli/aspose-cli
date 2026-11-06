using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// Starts the commands a hint or a <c>window.next</c> names on an input document: the one place
/// such a command names the input and its password's variable, so the command opens the input as
/// the invocation did.
/// </summary>
public static class ContinuationInputExtensions
{
    /// <summary>
    /// Starts the command at <paramref name="path"/>, such as <c>cells render</c>, on
    /// <paramref name="input"/>, with the option that names its password's variable when it has
    /// one. The caller appends its own options.
    /// </summary>
    /// <param name="input">The input as the command names it.</param>
    /// <param name="path">The command path below the root.</param>
    public static ContinuationCommand ContinuationAt(this ContinuationInput input, params string[] path)
    {
        ArgumentNullException.ThrowIfNull(input);
        ContinuationCommand next = new ContinuationCommand(path).Argument(input.Document);
        if (input is { PasswordOption: { } option, PasswordVariable: { } variable })
        {
            next.Option(option, variable);
        }

        return next;
    }

    /// <summary>
    /// Starts the command at <paramref name="path"/> on the source of a view: as the invocation
    /// that renders the view names it (<see cref="ViewRenderRequest.Continuation"/>), or as
    /// <paramref name="filePath"/>, the file rendered, when the request names none.
    /// </summary>
    /// <param name="request">The view's render request.</param>
    /// <param name="filePath">The file the view renders.</param>
    /// <param name="path">The command path below the root.</param>
    public static ContinuationCommand ContinuationAt(this ViewRenderRequest request, string filePath, params string[] path)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        return (request.Continuation ?? new ContinuationInput(filePath)).ContinuationAt(path);
    }
}
