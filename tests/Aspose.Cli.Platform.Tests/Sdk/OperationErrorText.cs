using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Platform.Tests.Sdk;

/// <summary>The words of an <c>OPS_INVALID</c> message that explain it, which <c>details.reason</c> names as a token.</summary>
internal static class OperationErrorText
{
    private const string Lead = "is invalid: ";
    private const string Listed = ". Must be one of:";

    /// <summary>The explanation after "is invalid: ", without the sentence that lists the choices.</summary>
    public static string Explanation(CliException error)
    {
        string message = error.Message;
        int start = message.IndexOf(Lead, StringComparison.Ordinal);
        string text = start < 0 ? message : message[(start + Lead.Length)..];
        int listed = text.IndexOf(Listed, StringComparison.Ordinal);
        return listed < 0 ? text : text[..listed];
    }
}
