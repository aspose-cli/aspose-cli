using System.CommandLine;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>Owns the shared execution options for atomic bounded edits.</summary>
public sealed class BoundedEditOptions
{
    /// <summary>Initializes the stable bounded-edit option surface.</summary>
    public BoundedEditOptions()
    {
        IfMatch = new Option<string?>("--if-match")
        {
            Description = "Require the current input SHA-256 fingerprint before editing.",
        }.WithInput(InputKind.None);
        DryRun = new Option<bool>("--dry-run")
        {
            Description = "Resolve and apply operations in memory without writing.",
        };
        BestEffort = new Option<bool>("--best-effort")
        {
            Description = "Keep successful operations, report failures, and exit 8.",
        };
    }

    /// <summary>Gets the optimistic-concurrency option.</summary>
    public Option<string?> IfMatch { get; }

    /// <summary>Gets the no-publication execution option.</summary>
    public Option<bool> DryRun { get; }

    /// <summary>Gets the explicit partial-result execution option.</summary>
    public Option<bool> BestEffort { get; }

    /// <summary>Adds the shared options to one product edit command.</summary>
    public void AddTo(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Options.Add(IfMatch);
        command.Options.Add(DryRun);
        command.Options.Add(BestEffort);
    }

    /// <summary>Reads the command values and reconciles the document precondition.</summary>
    public EditCommandOptions Read(ParseResult parse, string? documentIfMatch)
    {
        ArgumentNullException.ThrowIfNull(parse);
        string? commandIfMatch = parse.GetValue(IfMatch);
        if (commandIfMatch is not null
            && documentIfMatch is not null
            && !string.Equals(
                commandIfMatch.Trim(),
                documentIfMatch.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            throw CliErrors.OptionInvalid(
                "--if-match",
                "the command value differs from the operations document ifMatch value",
                "Use one current source fingerprint in either location, or the same value in both.");
        }

        return new EditCommandOptions
        {
            IfMatch = commandIfMatch ?? documentIfMatch,
            DryRun = parse.GetValue(DryRun),
            BestEffort = parse.GetValue(BestEffort),
        };
    }
}
