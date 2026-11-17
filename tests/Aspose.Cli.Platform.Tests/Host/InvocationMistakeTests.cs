using System.Text.Json.Nodes;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Sdk.Errors;
using Xunit;

namespace Aspose.Cli.Host.Tests;

/// <summary>
/// Each way the parser recognises a mistaken command line, on the real command tree: a command
/// line, the error it gets, a fragment of its message and hint, and the first suggestion (null
/// when the error suggests nothing).
/// </summary>
public sealed class InvocationMistakeTests
{
    [Fact]
    public void EnsureValid_DoesNotRepeatAPasswordBesideAValueSpelledLikeACommand()
    {
        ParsedInvocation invocation = ActualCommandTree.Parser.Parse("convert a.docx --password hunter2 --to pdf".Split(' '));

        CliException error = Assert.Throws<CliException>(invocation.EnsureValid);

        Assert.Equal("words convert", error.Details?["suggestions"]?.AsArray().FirstOrDefault()?.GetValue<string>());
        Assert.DoesNotContain("hunter2", error.Message + error.Details?.ToJsonString(), StringComparison.Ordinal);
    }
}
