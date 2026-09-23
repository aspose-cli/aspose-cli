using System.CommandLine;
using Aspose.Cli.Host.Output;
using System.CommandLine.Parsing;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Host.Invocation;

/// <summary>The actual parsed command, resolved globals and declared execution ownership.</summary>
internal sealed class ParsedInvocation
{
    internal ParsedInvocation(ParseResult parse, GlobalOptions globals, GlobalValues? inherited = null)
    {
        ParseResult = parse;
        GlobalValues = parse.Errors.Count == 0 ? globals.Resolve(parse, inherited) : null;
        var path = new List<Command>();
        for (CommandResult? current = parse.CommandResult; current?.Parent is not null;
            current = current.Parent as CommandResult)
        {
            path.Add(current.Command);
        }
        path.Reverse();
        CommandPath = path.AsReadOnly();
        ProductId = path.Select(static command => command.Policy().ProductId).FirstOrDefault(static id => id is not null);
        Execution = path.Select(static command => command.Policy().Execution)
            .FirstOrDefault(static value => value != CommandExecutionOwnership.Worker);
        McpAllowed = ProductId is not null || parse.CommandResult.Command.Policy().McpReadOnly;
    }

    public ParseResult ParseResult { get; }
    public GlobalValues? GlobalValues { get; }
    public IReadOnlyList<Command> CommandPath { get; }
    public Command Command => ParseResult.CommandResult.Command;
    public string? ProductId { get; }
    public CommandExecutionOwnership Execution { get; }
    public bool McpAllowed { get; }

    internal void EnsureValid()
    {
        if (ParseResult.Errors.Count > 0)
        {
            throw CliErrors.Usage(ParseResult.Errors.Select(static error => error.Message).ToArray());
        }
    }
}

/// <summary>One command tree for CLI execution, supervision and MCP authorization.</summary>
internal sealed class InvocationParser(RootCommand root, GlobalOptions globals)
{
    internal ParsedInvocation Parse(string[] args, GlobalValues? inherited = null) =>
        new(root.Parse(args), globals, inherited);

    internal (OutputMode Output, bool Quiet) ResolveErrorOutput(IReadOnlyList<string> args) =>
        globals.ResolveForErrorReporting(root.Parse(args.ToArray()));
}
