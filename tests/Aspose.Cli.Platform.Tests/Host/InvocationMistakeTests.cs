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
    public static TheoryData<string, string, string, string, string?> Mistakes => new()
    {
        // A group without a command.
        { "cells", "USAGE_ERROR", "'aspose-cli cells' needs a command.", "Run 'aspose-cli cells <command>'", null },
        { "--version --bogus", "USAGE_ERROR", "'--bogus' is not an option of --version", "--help", null },

        // An unknown command: the closest, a command declaring the name, a product command at the root.
        { "cells inpsect a.xlsx", "USAGE_ERROR", "'inpsect' is not a command of 'aspose-cli cells'.", "Did you mean 'cells inspect'?", "cells inspect" },
        { "words merge a.docx b.docx", "USAGE_ERROR", "'merge' is not a command of 'aspose-cli words'.", "import_sections operation", "words edit" },
        { "read a.docx", "USAGE_ERROR", "'read' is not a command of 'aspose-cli'.", "Run 'aspose-cli words read a.docx'.", "words read" },

        // A help or version word: as a command, and beside the command it belongs to.
        { "cells help", "USAGE_ERROR", "'help' is not a command of 'aspose-cli cells'.", "Did you mean 'cells --help'?", "cells --help" },
        { "version", "USAGE_ERROR", "'version' is not a command of 'aspose-cli'.", "Did you mean '--version'?", "--version" },
        { "cells inspect a.xlsx help", "USAGE_ERROR", "'help' is not a command of 'aspose-cli cells inspect'; help is the --help option.", "Did you mean 'cells inspect --help'?", "cells inspect --help" },

        // An argument's name written before its value.
        { "words read file a.docx", "USAGE_ERROR", "'file' stands for the <file> argument, whose value follows it.", "Run 'aspose-cli words read a.docx'.", null },

        // A refused value: outside the list, the value it stands for, a sibling's format, a number.
        { "words inspect a.docx --detail bogus", "OPTION_INVALID", "'bogus' is not a value of --detail.", "--help", null },
        { "pdf convert a.pdf --to word", "OPTION_INVALID", "'word' is not a value of --to.", "Did you mean 'docx'?", "docx" },
        { "cells convert a.xlsx --to png", "OPTION_INVALID", "'png' is not a value of --to.", "Run 'aspose-cli cells render a.xlsx --to png' for png.", null },
        { "cells convert a.xlsx --to xlsx --output-format pdf", "OPTION_INVALID", "'pdf' is not a value of --output-format.", "Did you mean '--to pdf'?", "--to pdf" },
        { "words render a.docx --dpi abc", "OPTION_INVALID", "'abc' is not a whole number, which --dpi takes.", "--help", null },

        // An unknown option: the closest, an option declaring the name, a letter, help, a refused option.
        { "cells read a.xlsx --rnage A1", "USAGE_ERROR", "--rnage", "Did you mean '--range'?", "--range" },
        { "slides search deck.pptx --text speaker", "USAGE_ERROR", "--text", "Did you mean '--pattern'?", "--pattern" },
        { "cells read a.xlsx -r A1", "USAGE_ERROR", "-r", "Did you mean '--range'", "--range" },
        { "cells read a.xlsx -?", "USAGE_ERROR", "-?", "Did you mean '--help'?", "--help" },
        { "pdf convert a.pdf --to docx --encrypt", "USAGE_ERROR", "--encrypt", "'pdf convert' does not encrypt its output.", null },

        // A stray argument beside a missing option it stands for.
        { "slides search deck.pptx speaker", "USAGE_ERROR", "speaker", "Did you mean '--pattern'?", "--pattern" },
    };

    [Theory]
    [MemberData(nameof(Mistakes))]
    public void EnsureValid_RefusesTheMistakeWithItsSuggestion(
        string commandLine, string code, string message, string hint, string? suggestion)
    {
        ParsedInvocation invocation = ActualCommandTree.Parser.Parse(commandLine.Split(' '));

        CliException error = Assert.Throws<CliException>(invocation.EnsureValid);

        Assert.Equal(code, error.Code.Name);
        Assert.Contains(message, error.Message, StringComparison.Ordinal);
        Assert.Contains(hint, error.Hint, StringComparison.Ordinal);
        Assert.Equal(suggestion, error.Details?["suggestions"]?.AsArray().FirstOrDefault()?.GetValue<string>());
    }
}
