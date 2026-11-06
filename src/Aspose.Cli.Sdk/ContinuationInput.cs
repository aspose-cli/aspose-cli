namespace Aspose.Cli.Sdk;

/// <summary>
/// An input document as a command that continues an invocation names it: the document and, when
/// the invocation read the document's password from an environment variable, that option and the
/// variable, whose name is not a secret. It never holds the password, so a command started from it
/// carries none; a password given literally or on stdin is not repeated, and the caller supplies it
/// again. A command starts from it only through <c>ContinuationAt</c>, as
/// <c>StandardInvocation.ContinuationAt</c> starts the commands on its own input.
/// </summary>
/// <param name="Document">The document, as the command names it.</param>
/// <param name="PasswordOption">The option that names the password's variable, such as <c>--password-env</c>; null without a variable.</param>
/// <param name="PasswordVariable">The environment variable that holds the password; null when the password came another way, or there is none.</param>
public sealed record ContinuationInput(string Document, string? PasswordOption = null, string? PasswordVariable = null);
