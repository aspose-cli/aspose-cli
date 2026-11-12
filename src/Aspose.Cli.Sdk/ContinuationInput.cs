namespace Aspose.Cli.Sdk;

/// <summary>
/// An input document as a command that continues an invocation names it: the document and, when
/// the invocation opened it with a password, the option that names the password's environment
/// variable, with the variable the invocation named, whose name is not a secret. It never holds the
/// password: a password the invocation read from stdin or got literally is passed on as a
/// <c>&lt;password-env&gt;</c> placeholder, which the caller replaces with a variable that holds it.
/// A command starts from it only through <c>ContinuationAt</c>, as
/// <c>StandardInvocation.ContinuationAt</c> starts the commands on its own input.
/// </summary>
/// <param name="Document">The document, as the command names it.</param>
/// <param name="PasswordOption">The option that names the password's variable, such as <c>--password-env</c>; null when the document opens without a password.</param>
/// <param name="PasswordVariable">The environment variable that holds the password; null when the password came another way, which the command names a placeholder for, or there is none.</param>
public sealed record ContinuationInput(string Document, string? PasswordOption = null, string? PasswordVariable = null);
