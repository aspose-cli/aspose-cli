using System.CommandLine;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// Starts the commands a hint or a <c>window.next</c> names on an input document: the one place
/// such a command names the input and passes its password on, so the command opens the input as
/// the invocation did.
/// </summary>
public static class ContinuationInputExtensions
{
    /// <summary>
    /// Starts the command at <paramref name="path"/>, such as <c>cells render</c>, on
    /// <paramref name="input"/>, with the option that names its password's variable when it has a
    /// password: with the variable, or with the <c>&lt;password-env&gt;</c> placeholder for a
    /// password given another way. The caller appends its own options.
    /// </summary>
    /// <param name="input">The input as the command names it.</param>
    /// <param name="path">The command path below the root.</param>
    public static ContinuationCommand ContinuationAt(this ContinuationInput input, params string[] path)
    {
        ArgumentNullException.ThrowIfNull(input);
        ContinuationCommand next = new ContinuationCommand(path).Argument(input.Document);
        next = input.PasswordOption is { } option ? next.PasswordVariable(option, input.PasswordVariable) : next;
        return input.WorkDir is { } workDir ? next.Option(GlobalOptionNames.WorkDir, workDir) : next;
    }

    /// <summary>
    /// <paramref name="document"/>, the input of the command <paramref name="parse"/> holds, as a
    /// command that continues that command names it, with its password passed on as
    /// <see cref="StandardInvocation.ContinuationOf"/> passes it: for a command line refused before
    /// its command ran, such as one whose parser refuses a value.
    /// </summary>
    /// <param name="parse">The parsed command line.</param>
    /// <param name="document">The document, as the command names it.</param>
    public static ContinuationInput ContinuationOf(this ParseResult parse, string document)
    {
        ArgumentNullException.ThrowIfNull(parse);
        return StandardInvocation.InputOf(StandardOptions.Of(parse.CommandResult.Command), parse, document);
    }

    /// <summary>
    /// <paramref name="document"/>, a file that a command on <paramref name="input"/> writes and
    /// protects with the password the input opened with, such as a part a split writes: with the
    /// input's password passed on, so a command on that file opens it as one on the input does.
    /// </summary>
    /// <param name="input">The input the file is written from.</param>
    /// <param name="document">The file, as the command names it.</param>
    public static ContinuationInput WrittenAs(this ContinuationInput input, string document)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrEmpty(document);
        return input with { Document = document };
    }
}
