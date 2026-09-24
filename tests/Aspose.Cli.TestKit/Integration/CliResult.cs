using System.Text.Json.Nodes;
using Xunit;

namespace Aspose.Cli.TestKit;

/// <summary>Outcome of one CLI invocation.</summary>
public sealed record CliResult(int ExitCode, string StdOut, string StdErr)
{
    /// <summary>Asserts that the command completed (exit code 0) and returns this result.</summary>
    public CliResult Succeeded()
    {
        Assert.True(ExitCode == 0, $"exit {ExitCode}: {StdErr}{StdOut}");
        return this;
    }

    /// <summary>The JSON document a completed command wrote to stdout.</summary>
    public JsonNode Json() => JsonNode.Parse(Succeeded().StdOut)!;
}
