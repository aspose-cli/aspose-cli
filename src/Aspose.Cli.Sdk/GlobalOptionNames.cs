namespace Aspose.Cli.Sdk;

/// <summary>
/// The option names and aliases that every command already accepts: the host's global options
/// and the parser's help and version options. The host declares its options from these names,
/// and the product contract analyzer compiles this file to reserve them from product commands.
/// </summary>
#if ASPOSE_CLI_ANALYZER
internal
#else
public
#endif
static class GlobalOptionNames
{
    public const string OutputFormat = "--output-format";
    public const string Quiet = "--quiet";
    public const string QuietAlias = "-q";
    public const string Verbose = "--verbose";
    public const string VerboseAlias = "-v";
    public const string License = "--license";
    public const string LicenseMode = "--license-mode";
    public const string WorkDir = "--workdir";
    public const string Timeout = "--timeout";
    public const string MaxInputBytes = "--max-input-bytes";
    public const string Help = "--help";
    public const string HelpAlias = "-h";
    public const string Version = "--version";

    /// <summary>Every name and alias a product option must not declare.</summary>
    public static readonly string[] Reserved =
    [
        OutputFormat, Quiet, QuietAlias, Verbose, VerboseAlias,
        License, LicenseMode, WorkDir, Timeout, MaxInputBytes,
        Help, HelpAlias, Version,
    ];
}
