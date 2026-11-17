using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.IO.Input;

/// <summary>
/// The password an input is opened with: one a command option or an operation field supplies, or
/// none at all, for a file the command opens without a password, such as a template. The loader
/// that opens the input passes it to <see cref="InputLoading"/>, which builds the password error,
/// so no caller restates it. The default is an option without a password.
/// </summary>
public readonly struct PasswordSource
{
    private readonly bool _none;

    private PasswordSource(Secret? password, bool none)
    {
        Password = password;
        _none = none;
    }

    /// <summary>The password the input is opened with, if any.</summary>
    public Secret? Password { get; }

    /// <summary>A password a command option or an operation field supplies.</summary>
    /// <param name="password">The password, if the option or field was given.</param>
    public static PasswordSource Option(Secret? password) => new(password, none: false);

    /// <summary>No password: the command opens the input without one, such as a template.</summary>
    public static PasswordSource None() => new(password: null, none: true);

    /// <summary>
    /// The error for an encrypted <paramref name="path"/> this password does not open:
    /// <c>PASSWORD_REQUIRED</c> without a password, <c>PASSWORD_INVALID</c> with one.
    /// </summary>
    public CliException Refused(string path) =>
        _none ? CliErrors.PasswordUnavailable(path)
        : Password is null ? CliErrors.PasswordRequired(path)
        : CliErrors.PasswordInvalid(path);
}
