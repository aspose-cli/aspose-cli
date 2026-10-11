using System.CommandLine;
using System.CommandLine.Help;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.Output;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Host.Invocation;

/// <summary>
/// The options available on every command. Defined once and added to the
/// root as recursive options, so `--output-format json` works at any position.
/// </summary>
internal sealed class GlobalOptions
{
    /// <summary>The <c>--license-mode</c> value that resolves the license sources in order.</summary>
    internal const string AutoLicenseMode = "auto";

    /// <summary>The <c>--license-mode</c> value that reads no license source.</summary>
    internal const string EvaluationLicenseMode = "evaluation";

    /// <summary>The accepted <c>--output-format</c> values, in help order.</summary>
    private static readonly IReadOnlyDictionary<string, OutputMode> OutputModes =
        new Dictionary<string, OutputMode>(StringComparer.Ordinal)
        {
            ["json"] = OutputMode.Json,
            ["compact"] = OutputMode.Compact,
            ["table"] = OutputMode.Table,
            ["markdown"] = OutputMode.Markdown,
        };

    public GlobalOptions()
    {
        OutputFormat = new Option<string?>(GlobalOptionNames.OutputFormat)
        {
            Description = "How results print: the contract envelope, indented or on one line, or human text. "
                + "Default: human text on a terminal, the indented envelope when redirected.",
            Recursive = true,
        }.WithInput(InputKind.None);
        OutputFormat.AcceptOnlyFromAmong([.. OutputModes.Keys]);

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

        License = new Option<string?>(GlobalOptionNames.License)
        {
            Description = "Path to an Aspose license file; overrides every other license source.",
            Recursive = true,
        }.WithInput(InputKind.None);
        LicenseMode = new Option<string?>(GlobalOptionNames.LicenseMode)
        {
            Description = "evaluation reads no license source and runs this command in evaluation mode; by default "
                + "the license sources are resolved in order. Cannot be combined with --license.",
            Recursive = true,
        }.WithInput(InputKind.None);
        LicenseMode.AcceptOnlyFromAmong(AutoLicenseMode, EvaluationLicenseMode);

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
                $"Maximum bytes admitted for one input before product runtime initialization. Default: {InputSizeGuard.DefaultMaxBytes}.",
            Recursive = true,
        }.InRange(1, InputSizeGuard.MaximumBytes);
    }

    public Option<string?> OutputFormat { get; }

    public Option<bool> Quiet { get; }

    public Option<bool> Verbose { get; }

    public Option<string?> License { get; }

    public Option<string?> LicenseMode { get; }

    public Option<string?> WorkDir { get; }

    public Option<int?> Timeout { get; }

    public Option<long?> MaxInputBytes { get; }

    /// <summary>
    /// Adds the global options to the root and keeps only <c>--help</c> and <c>-h</c> of the
    /// parser's help spellings.
    /// </summary>
    public void AddTo(RootCommand root)
    {
        ArgumentNullException.ThrowIfNull(root);
        HelpOption help = root.Options.OfType<HelpOption>().Single();
        foreach (string alias in help.Aliases.Where(static alias => alias != GlobalOptionNames.HelpAlias).ToArray())
        {
            help.Aliases.Remove(alias);
        }
        root.Options.Add(OutputFormat);
        root.Options.Add(Quiet);
        root.Options.Add(Verbose);
        root.Options.Add(License);
        root.Options.Add(LicenseMode);
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
        string? explicitLicense = parseResult.GetValue(License);
        if (explicitLicense is not null && string.IsNullOrWhiteSpace(explicitLicense))
        {
            // An empty path would otherwise resolve to the work directory.
            throw CliErrors.OptionInvalid(GlobalOptionNames.License,
                "the value is empty",
                "Pass the path of an Aspose license file, or omit --license to use the other license sources.");
        }
        string? requestedMode = parseResult.GetValue(LicenseMode);
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
        long? requestedInputBytes = parseResult.GetInRange(
            MaxInputBytes,
            "Use the default for ordinary files, or choose a positive byte limit within the advertised range.");
        if (inherited is not null && requestedInputBytes > inherited.MaxInputBytes)
        {
            throw CliErrors.OptionInvalid("--max-input-bytes",
                "the value exceeds the MCP host input budget",
                "Choose a limit within the budget selected when the MCP server was started.");
        }
        long maxInputBytes = ResolveMaxInputBytes(requestedInputBytes ?? inherited?.MaxInputBytes);
        var values = new GlobalValues(
            MapOutputFormat(parseResult.GetValue(OutputFormat)),
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
    /// <c>--output-format</c> itself carried the bad value); fall back to the redirect
    /// heuristic so a usage error still honors <c>--output-format json</c> when it can.
    /// </summary>
    public (OutputMode OutputFormat, bool Quiet) ResolveForErrorReporting(ParseResult parseResult)
    {
        ArgumentNullException.ThrowIfNull(parseResult);

        OutputMode mode;
        try
        {
            mode = MapOutputFormat(parseResult.GetValue(OutputFormat));
        }
        catch (InvalidOperationException)
        {
            mode = MapOutputFormat(null);
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
    internal static (OutputMode OutputFormat, bool Quiet) ResolveForProcessFailure(
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
            if (argument == GlobalOptionNames.OutputFormat)
            {
                if (index + 1 < args.Count)
                {
                    candidate = args[++index];
                }
            }
            else if (argument.StartsWith(GlobalOptionNames.OutputFormat + "=", StringComparison.Ordinal))
            {
                candidate = argument[(GlobalOptionNames.OutputFormat.Length + 1)..];
            }

            if (candidate is not null && OutputModes.ContainsKey(candidate))
            {
                output = candidate;
            }
        }

        return (MapOutputFormat(output), quiet);
    }

    /// <summary>Whether a value is one of the accepted <c>--output-format</c> modes.</summary>
    internal static bool IsOutputMode(string value) => OutputModes.ContainsKey(value);

    // Agents typically read the CLI through a pipe; humans get a table.
    private static OutputMode MapOutputFormat(string? value) =>
        value is not null && OutputModes.TryGetValue(value, out OutputMode mode) ? mode
        : Console.IsOutputRedirected ? OutputMode.Json : OutputMode.Table;

    private static long ResolveMaxInputBytes(long? requested) =>
        requested ?? InputSizeGuard.ResolveMaxBytes(Environment.GetEnvironmentVariable);
}

/// <summary>Resolved global option values for one invocation.</summary>
internal sealed record GlobalValues(
    OutputMode OutputFormat,
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
