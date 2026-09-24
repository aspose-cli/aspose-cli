namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// The option names and aliases the command template owns: the common options that the
/// standard options and the bounded edit command declare for every product command. The
/// template declares its options from these names, and the product contract analyzer compiles
/// this file to reserve them from product commands, so a product cannot hand-declare an
/// option that the template gives it.
/// </summary>
internal static class StandardOptionNames
{
    public const string Out = "--out";
    public const string OutAlias = "-o";
    public const string OutDir = "--out-dir";
    public const string Overwrite = "--overwrite";
    public const string Password = "--password";
    public const string Encrypt = "--encrypt";
    public const string FontDir = "--font-dir";
    public const string Ops = "--ops";
    public const string Set = "--set";
    public const string InPlace = "--in-place";
    public const string Backup = "--backup";
    public const string IfMatch = "--if-match";
    public const string DryRun = "--dry-run";
    public const string BestEffort = "--best-effort";
    public const string Verify = "--verify";

    /// <summary>Appended to a password option to name the environment variable that holds it.</summary>
    public const string EnvironmentSuffix = "-env";

    /// <summary>Appended to a password option to read it from standard input.</summary>
    public const string StandardInputSuffix = "-stdin";

    /// <summary>
    /// Every name and alias a product option must not declare. The password options of a
    /// second input document are named after its argument (<c>--left-password</c>); the
    /// template rejects a product option that repeats one when it builds the command.
    /// </summary>
    public static readonly string[] Reserved =
    [
        Out, OutAlias, OutDir, Overwrite,
        Password, Password + EnvironmentSuffix, Password + StandardInputSuffix,
        Encrypt, Encrypt + EnvironmentSuffix,
        FontDir,
        Ops, Set, InPlace, Backup, IfMatch, DryRun, BestEffort, Verify,
    ];
}
