using System.Diagnostics;
using System.Reflection;

namespace Aspose.Cli.Sdk.Errors;

/// <summary>
/// Who raised an exception, read from its stack: the first frame owned by neither .NET nor the
/// CLI's own assemblies belongs to a third-party library, such as a document engine or its
/// imaging stack. A failure first raised by CLI code is the CLI's own.
/// </summary>
public static class ExceptionOrigin
{
    /// <summary>The assemblies of the frames <paramref name="exception"/> passed through, innermost first.</summary>
    public static Assembly[] Frames(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return [.. new StackTrace(exception, fNeedFileInfo: false).GetFrames()
            .Select(static frame => frame.GetMethod()?.DeclaringType?.Assembly)
            .OfType<Assembly>()];
    }

    /// <summary>
    /// Whether <paramref name="exception"/> was raised by a third-party library: its first frame
    /// outside .NET belongs to none of <paramref name="own"/>.
    /// </summary>
    public static bool IsThirdParty(Exception exception, IReadOnlySet<Assembly> own)
    {
        ArgumentNullException.ThrowIfNull(own);
        Assembly? owner = Frames(exception).FirstOrDefault(static assembly => !IsRuntime(assembly));
        return owner is not null && !own.Contains(owner);
    }

    private static bool IsRuntime(Assembly assembly)
    {
        string name = assembly.GetName().Name ?? string.Empty;
        return name is "mscorlib" or "netstandard"
            || name.StartsWith("System.", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.", StringComparison.Ordinal);
    }
}
