using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Aspose.Cli.Sdk.Errors;

/// <summary>
/// Who raised an exception, read from its stack: from the frame that threw outward, past the
/// frames of .NET, the first frame decides. A frame of the CLI's own assemblies makes the failure
/// the CLI's; a frame of any other assembly, such as a document engine or its imaging stack, makes
/// it the engine's. This is the one rule every boundary that reports <c>ENGINE_ERROR</c> applies.
/// </summary>
public static class ExceptionOrigin
{
    // The launcher's assembly, named after the executable rather than Aspose.Cli.
    private static readonly Assembly? Launcher = Assembly.GetEntryAssembly();

    /// <summary>The assemblies of the frames <paramref name="exception"/> passed through, innermost first.</summary>
    public static Assembly[] Frames(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return [.. new StackTrace(exception, fNeedFileInfo: false).GetFrames()
            .Select(static frame => frame.GetMethod()?.DeclaringType?.Assembly)
            .OfType<Assembly>()];
    }

    /// <summary>
    /// Whether the engine threw <paramref name="exception"/>: its first frame outside .NET belongs
    /// to no assembly of the CLI (named <c>Aspose.Cli*</c>, or the launcher). A CLI error, a
    /// cancellation and a regular expression that ran out of its time are never the engine's.
    /// </summary>
    public static bool FromEngine(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (exception is CliException or OperationCanceledException or RegexMatchTimeoutException)
        {
            return false;
        }

        Assembly? owner = Frames(exception).FirstOrDefault(static assembly => !IsRuntime(assembly));
        return owner is not null && !IsOwn(owner);
    }

    private static bool IsOwn(Assembly assembly) =>
        assembly == Launcher || assembly.GetName().Name?.StartsWith("Aspose.Cli", StringComparison.Ordinal) == true;

    private static bool IsRuntime(Assembly assembly)
    {
        string name = assembly.GetName().Name ?? string.Empty;
        return name is "mscorlib" or "netstandard"
            || name.StartsWith("System.", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.", StringComparison.Ordinal);
    }
}
