namespace Aspose.Cli.Sdk.Errors;

// The errors of the CLI processes this CLI starts.
public static partial class CliErrors
{
    /// <summary>
    /// A process this CLI started, or the channel to it, failed without reporting an error of
    /// its own, so the failure is this CLI's: an internal error.
    /// </summary>
    /// <param name="message">What could not be done.</param>
    public static CliException Internal(string message) => CliException.Create(
        ErrorCodes.Internal,
        message);
}
