using System.CommandLine;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.Output;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Host.Invocation;

/// <summary>
/// The options available on every command. Defined once and added to the
/// root as recursive options, so `--output json` works at any position.
/// </summary>
internal sealed class GlobalOptions
{
    /// <summary>The <c>--license-mode</c> value that resolves the license sources in order.</summary>
    internal const string AutoLicenseMode = "auto";

    /// <summary>The <c>--license-mode</c> value that reads no license source.</summary>
    internal const string EvaluationLicenseMode = "evaluation";

    /// <summary>The accepted <c>--output</c> values, in help order.</summary>
    private static readonly IReadOnlyDictionary<string, OutputMode> OutputModes =
        new Dictionary<string, OutputMode>(StringComparer.Ordinal)
        {
            ["json"] = OutputMode.Json,
            ["compact"] = OutputMode.Compact,
            ["table"] = OutputMode.Table,
            ["markdown"] = OutputMode.Markdown,
        };

    public GlobalOptions(bool licensingApplicable)
    {
        License = null;
        Output = new Option<string?>(GlobalOptionNames.Output, GlobalOptionNames.OutputAlias)
        {
            Description = "Output format: json (contract envelopes), compact (the same JSON on one line), " +
                          "table (human text) or markdown. Default: table on a terminal, json when redirected.",
            Recursive = true,
        }.WithInput(InputKind.None);
        Output.AcceptOnlyFromAmong([.. OutputModes.Keys]);

        Quiet = new Option<bool>(GlobalOptionNames.Quiet, GlobalOptionNames.QuietAlias)
        {
            Description = "Suppress warnings and notices on stderr (errors still print).",
            Recursive = true,
        };

        Verbose = new Option<bool>(GlobalOptionNames.Verbose, GlobalOptionNames.VerboseAlias)
        {
            Description = "Emit structured JSONL diagnostics (timing, error codes) to stderr; stdout is untouched.",
            Recursive = true,
        };

        if (licensingApplicable)
        {
            License = new Option<string?>(GlobalOptionNames.License)
            {
                Description = "Path to an Aspose license file; overrides every other license source.",
                Recursive = true,
            }.WithInput(InputKind.None);
            LicenseMode = new Option<string?>(GlobalOptionNames.LicenseMode)
            {
                Description = "auto (default) resolves the license sources in order; evaluation reads none of them "
                    + "and runs this command in evaluation mode. Cannot be combined with --license.",
                Recursive = true,
            }.WithInput(InputKind.None);
            LicenseMode.AcceptOnlyFromAmong(AutoLicenseMode, EvaluationLicenseMode);
        }

        WorkDir = new Option<string?>(GlobalOptionNames.WorkDir)
        {
            Description = "Base directory for relative paths. Default: the current directory.",
            Recursive = true,
        }.WithInput(InputKind.None);

        Timeout = new Option<int?>(GlobalOptionNames.Timeout)
        {
            Description = "Set one command deadline in seconds. Before exit 9, supervised work is stopped and staged outputs are recovered.",
            Recursive = true,
        };

        MaxInputBytes = new Option<long?>(GlobalOptionNames.MaxInputBytes)
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

    public Option<string?>? LicenseMode { get; }

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
        if (LicenseMode is not null)
        {
            root.Options.Add(LicenseMode);
        }
        root.Options.Add(WorkDir);
        root.Options.Add(Timeout);
        root.Options.Add(MaxInputBytes);
    }

    /// <summary>Reads the global option values from a parse result.</summary>
    public GlobalValues Resolve(ParseResult parseResult, GlobalValues? inherited = null)
    {
        ArgumentNullException.ThrowIfNull(parseResult);

        inherited ??= InvocationInputs.Current?.Inherited;
        string baseDirectory = inherited?.WorkDir ?? Directory.GetCurrentDirectory();
        string workDirectory = Path.GetFullPath(parseResult.GetValue(WorkDir) ?? baseDirectory, baseDirectory);
        string? explicitLicense = License is null ? null : parseResult.GetValue(License);
        if (explicitLicense is not null && string.IsNullOrWhiteSpace(explicitLicense))
        {
            // An empty path would otherwise resolve to the work directory.
            throw CliErrors.OptionInvalid(GlobalOptionNames.License,
                "the value is empty",
                "Pass the path of an Aspose license file, or omit --license to use the other license sources.");
        }
        string? requestedMode = LicenseMode is null ? null : parseResult.GetValue(LicenseMode);
        if (requestedMode == EvaluationLicenseMode && explicitLicense is not null)
        {
            throw CliErrors.OptionInvalid(GlobalOptionNames.LicenseMode,
                "evaluation mode reads no license, so it cannot be combined with --license",
                "Drop --license to run in evaluation mode, or drop --license-mode to apply the license.");
        }
        // A value given on this command replaces the inherited one, as --license does.
        bool evaluationRequested = requestedMode is null
            ? explicitLicense is null && inherited?.EvaluationRequested == true
            : requestedMode == EvaluationLicenseMode;
        string? licensePath = evaluationRequested ? null
            : explicitLicense is null ? inherited?.LicensePath
            : Path.GetFullPath(explicitLicense, workDirectory);
        long? requestedInputBytes = parseResult.GetValue(MaxInputBytes);
        if (inherited is not null && requestedInputBytes > inherited.MaxInputBytes)
        {
            throw CliErrors.OptionInvalid("--max-input-bytes",
                "the value exceeds the MCP host input budget",
                "Choose a limit within the budget selected when the MCP server was started.");
        }
        long maxInputBytes = ResolveMaxInputBytes(requestedInputBytes ?? inherited?.MaxInputBytes);
        var values = new GlobalValues(
            MapOutput(parseResult.GetValue(Output)),
            parseResult.GetValue(Quiet),
            parseResult.GetValue(Verbose),
            licensePath,
            workDirectory,
            parseResult.GetValue(Timeout),
            maxInputBytes)
        {
            EvaluationRequested = evaluationRequested,
        };
        ServiceStartSecrets? service =
            ServiceStartSecretChannel.Current;
        return service is null
            ? values
            : values with
            {
                WorkDir = service.WorkDirectory,
                LicensePath = service.LicensePath,
                // A service's own default is configured; each document carries its own request.
                EvaluationRequested = false,
            };
    }

    /// <summary>The refusal of <c>--license-mode evaluation</c> by a command whose effect outlives it.</summary>
    internal static CliException EvaluationRequestRefused(string command) =>
        CliErrors.OptionInvalid(GlobalOptionNames.LicenseMode,
            $"'{command}' does not run one command's documents, so evaluation mode cannot apply to it",
            $"Drop --license-mode from '{command}'; use it on the document commands whose output you check.");

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

            if (argument is GlobalOptionNames.Quiet or GlobalOptionNames.QuietAlias)
            {
                quiet = true;
                continue;
            }

            string? candidate = null;
            if (argument is GlobalOptionNames.Output or GlobalOptionNames.OutputAlias)
            {
                if (index + 1 < args.Count)
                {
                    candidate = args[++index];
                }
            }
            else if (argument.StartsWith(GlobalOptionNames.Output + "=", StringComparison.Ordinal))
            {
                candidate = argument[(GlobalOptionNames.Output.Length + 1)..];
            }
            else if (argument.StartsWith(GlobalOptionNames.OutputAlias + "=", StringComparison.Ordinal))
            {
                candidate = argument[(GlobalOptionNames.OutputAlias.Length + 1)..];
            }

            if (candidate is not null && OutputModes.ContainsKey(candidate))
            {
                output = candidate;
            }
        }

        return (MapOutput(output), quiet);
    }

    /// <summary>Whether a value is one of the accepted <c>--output</c> modes.</summary>
    internal static bool IsOutputMode(string value) => OutputModes.ContainsKey(value);

    // Agents typically read the CLI through a pipe; humans get a table.
    private static OutputMode MapOutput(string? value) =>
        value is not null && OutputModes.TryGetValue(value, out OutputMode mode) ? mode
        : Console.IsOutputRedirected ? OutputMode.Json : OutputMode.Table;

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
    long MaxInputBytes)
{
    /// <summary>
    /// <c>--license-mode evaluation</c>: no license source is read; <see cref="LicensePath"/> is
    /// then null.
    /// </summary>
    public bool EvaluationRequested { get; init; }
}
