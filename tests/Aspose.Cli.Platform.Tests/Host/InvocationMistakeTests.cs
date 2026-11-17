using System.Text.Json.Nodes;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Sdk.Errors;
using Xunit;

namespace Aspose.Cli.Host.Tests;

/// <summary>
/// A command line the parser refuses, on the real command tree, does not repeat a password
/// written on it.
/// </summary>
public sealed class InvocationMistakeTests
{
    [Fact]
    public void EnsureValid_DoesNotRepeatAPasswordBesideAValueSpelledLikeACommand()
    {
        ParsedInvocation invocation = ActualCommandTree.Parser.Parse("convert a.docx --password hunter2 --to pdf".Split(' '));

        CliException error = Assert.Throws<CliException>(invocation.EnsureValid);

        Assert.DoesNotContain("hunter2", error.Message + error.Details?.ToJsonString(), StringComparison.Ordinal);
    }
}
