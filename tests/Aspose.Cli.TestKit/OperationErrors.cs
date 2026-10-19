using Aspose.Cli.Sdk.Errors;
using Xunit;

namespace Aspose.Cli.TestKit;

/// <summary>Assertions on the errors an edit batch reports for one of its operations.</summary>
public static class OperationErrors
{
    /// <summary>
    /// Asserts that <paramref name="error"/> is <c>OPS_INVALID</c> for the operation at
    /// <paramref name="index"/> of the batch, named <paramref name="op"/>, and that its message
    /// names <paramref name="mentioned"/>.
    /// </summary>
    public static void AssertInvalid(CliException error, int index, string op, string mentioned)
    {
        ArgumentNullException.ThrowIfNull(error);
        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Equal($"op-{index + 1:D4}", error.Details!["id"]!.GetValue<string>());
        Assert.Equal(op, error.Details["op"]!.GetValue<string>());
        Assert.Contains(mentioned, error.Message, StringComparison.Ordinal);
    }
}
