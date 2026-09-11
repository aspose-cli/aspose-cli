using System.CommandLine;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.Output;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Host.Invocation;

/// <summary>
/// The options available on every command. Defined once and added to the
/// root as recursive options, so `--output json` works at any position.
/// </summary>
internal sealed class GlobalOptions
{
    public GlobalOptions(bool licensingApplicable)
    {
        License = null;
        Output = new Option<string?>("--output", "-f")
        {
            Description = "Output format: json (contract envelopes), table (human text) or markdown. " +
                          "Default: table on a terminal, json when redirected.",
            Recursive = true,
        }.WithInput(InputKind.None);
        Output.AcceptOnlyFromAmong("json", "table", "markdown");

        Quiet = new Option<bool>("--quiet", "-q")
        {
            Description = "Suppress warnings and notices on stderr (errors still print).",
            Recursive = true,
        };

        Verbose = new Option<bool>("--verbose", "-v")
        {
            Description = "Emit structured JSONL diagnostics (timing, error codes) to stderr; stdout is untouched.",
            Recursive = true,
        };

        if (licensingApplicable)
        {
            License = new Option<string?>("--license")
            {
                Description = "Path to an Aspose license file; overrides every other license source.",
                Recursive = true,
            }.WithInput(InputKind.None);
        }

        WorkDir = new Option<string?>("--workdir")
        {
            Description = "Base directory for relative paths. Default: the current directory.",
            Recursive = true,
        }.WithInput(InputKind.None);

        Timeout = new Option<int?>("--timeout")
        {
            Description = "Set one command deadline in seconds. Before exit 9, supervised work is stopped and staged outputs are recovered.",
            Recursive = true,
        };

        MaxInputBytes = new Option<long?>("--max-input-bytes")
        {
            Description =
                $"Maximum bytes admitted for one input before product runtime initialization (default {InputSizeGuard.DefaultMaxBytes}; hard maximum {InputSizeGuard.MaximumBytes}).",
            Recursive = true,
        };
    }

    public Option<string?> Output { get; }

    public Option<bool> Quiet { get; }

    public Option<bool> Verbose { get; }

    public Option<string?>? License { get; }

    public Option<string?> WorkDir { get; }

    public Option<int?> Timeout { get; }

    public Option<long?> MaxInputBytes { get; }

    public void AddTo(RootCommand root)
    {
        root.Options.Add(Output);
        root.Options.Add(Quiet);
        root.Options.Add(Verbose);
        if (License is not null)
        {
            root.Options.Add(License);
        }
        root.Options.Add(WorkDir);
        root.Options.Add(Timeout);
        root.Options.Add(MaxInputBytes);
    }

    /// <summary>Reads the global option values from a parse result.</summary>
    public GlobalValues Resolve(ParseResult parseResult)
    {
        ArgumentNullException.ThrowIfNull(parseResult);

        long maxInputBytes = ResolveMaxInputBytes(
            parseResult.GetValue(MaxInputBytes));
        var values = new GlobalValues(
            MapOutput(parseResult.GetValue(Output)),
            parseResult.GetValue(Quiet),
            parseResult.GetValue(Verbose),
            License is null ? null : parseResult.GetValue(License),
            parseResult.GetValue(WorkDir),
            parseResult.GetValue(Timeout),
            maxInputBytes);
        ServiceStartSecrets? service =
            ServiceStartSecretChannel.Current;
        return service is null
            ? values
            : values with
            {
                WorkDir = service.WorkDirectory,
                LicensePath = service.LicensePath,
            };
    }

    /// <summary>
    /// Best-effort read of the output mode and quiet flag for rendering a parse
    /// error. Parsing has already failed, so an option may be unreadable (e.g.
    /// <c>--output</c> itself carried the bad value); fall back to the redirect
    /// heuristic so a usage error still honors <c>--output json</c> when it can.
    /// </summary>
    public (OutputMode Output, bool Quiet) ResolveForErrorReporting(ParseResult parseResult)
    {
        ArgumentNullException.ThrowIfNull(parseResult);

        OutputMode mode;
        try
        {
            mode = MapOutput(parseResult.GetValue(Output));
        }
        catch (InvalidOperationException)
        {
            mode = MapOutput(null);
        }

        bool quiet;
        try
        {
            quiet = parseResult.GetValue(Quiet);
        }
        catch (InvalidOperationException)
        {
            quiet = false;
        }

        return (mode, quiet);
    }

    /// <summary>
    /// Resolves only non-secret output flags when composition failed before a
    /// <see cref="ParseResult"/> could reach the normal command boundary.
    /// </summary>
    internal static (OutputMode Output, bool Quiet) ResolveForProcessFailure(
        IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        string? output = null;
        bool quiet = false;
        for (int index = 0; index < args.Count; index++)
        {
            string argument = args[index];
            if (argument == "--")
            {
                break;
            }

            if (argument is "--quiet" or "-q")
            {
                quiet = true;
                continue;
            }

            string? candidate = null;
            if (argument is "--output" or "-f")
            {
                if (index + 1 < args.Count)
                {
                    candidate = args[++index];
                }
            }
            else if (argument.StartsWith("--output=", StringComparison.Ordinal))
            {
                candidate = argument["--output=".Length..];
            }
            else if (argument.StartsWith("-f=", StringComparison.Ordinal))
            {
                candidate = argument["-f=".Length..];
            }

            if (candidate is "json" or "table" or "markdown")
            {
                output = candidate;
            }
        }

        return (MapOutput(output), quiet);
    }

    private static OutputMode MapOutput(string? value) => value switch
    {
        "json" => OutputMode.Json,
        "table" => OutputMode.Table,
        "markdown" => OutputMode.Markdown,
        // Agents typically read the CLI through a pipe; humans get a table.
        _ => Console.IsOutputRedirected ? OutputMode.Json : OutputMode.Table,
    };

    private static long ResolveMaxInputBytes(long? requested)
    {
        if (requested is null)
        {
            return InputSizeGuard.ResolveMaxBytes(
                Environment.GetEnvironmentVariable);
        }
        if (requested is <= 0 or > InputSizeGuard.MaximumBytes)
        {
            throw CliErrors.OptionInvalid(
                "--max-input-bytes",
                $"value must be between 1 and {InputSizeGuard.MaximumBytes}",
                "Use the default for ordinary files, or choose a positive byte limit within the advertised hard maximum.");
        }
        return requested.Value;
    }
}

/// <summary>Resolved global option values for one invocation.</summary>
internal sealed record GlobalValues(
    OutputMode Output,
    bool Quiet,
    bool Verbose,
    string? LicensePath,
    string? WorkDir,
    int? TimeoutSeconds,
    long MaxInputBytes);
