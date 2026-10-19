using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.IO.Input;

/// <summary>
/// The password an input is opened with and where it comes from, which the hint of a password
/// error names: a command option such as <c>--password-env</c> or
/// <c>--certificate-password-env</c>, the operation field of a file an operation reads, such as
/// <c>sourcePasswordEnv</c>, or nothing at all, for a file the command opens without a password,
/// such as a template, with what to do instead. The loader that opens the input passes it to
/// <see cref="InputLoading"/>, which builds the error with that hint, so no caller restates it.
/// The default is the command's <c>--password-env</c> without a password.
/// </summary>
public readonly struct PasswordSource
{
    private readonly string? _option;
    private readonly string? _field;
    private readonly string? _instead;

    private PasswordSource(Secret? password, string? option, string? field, string? instead)
    {
        Password = password;
        _option = option;
        _field = field;
        _instead = instead;
    }

    /// <summary>The password the input is opened with, if any.</summary>
    public Secret? Password { get; }

    /// <summary>A password a command option supplies, by default <c>--password-env</c>.</summary>
    /// <param name="password">The password, if the option was given.</param>
    /// <param name="environmentOption">The option that names the password's environment variable.</param>
    public static PasswordSource Option(Secret? password, string environmentOption = "--password-env")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentOption);
        return new(password, environmentOption, field: null, instead: null);
    }

    /// <summary>A password the operation that reads the input supplies through <paramref name="field"/>.</summary>
    /// <param name="password">The password, if the field was given.</param>
    /// <param name="field">The operation field that names the password's environment variable, such as <c>sourcePasswordEnv</c>.</param>
    public static PasswordSource OperationField(Secret? password, string field)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        return new(password, option: null, field, instead: null);
    }

    /// <summary>
    /// No password: the command opens the input without one, such as a template, so an encrypted
    /// one is refused with <paramref name="instead"/> in place of the advice to retry with a password.
    /// </summary>
    /// <param name="instead">What to do instead, such as using an unencrypted copy.</param>
    public static PasswordSource None(string instead)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instead);
        return new(password: null, option: null, field: null, instead);
    }

    /// <summary>
    /// The error for an encrypted <paramref name="path"/> this password does not open:
    /// <c>PASSWORD_REQUIRED</c> without a password, <c>PASSWORD_INVALID</c> with one, with the
    /// hint of where the password comes from.
    /// </summary>
    public CliException Refused(string path) =>
        _instead is not null ? CliErrors.PasswordUnavailable(path, _instead)
        : Password is null ? CliErrors.PasswordRequired(path, _field, _option)
        : CliErrors.PasswordInvalid(path, _field, _option);
}
