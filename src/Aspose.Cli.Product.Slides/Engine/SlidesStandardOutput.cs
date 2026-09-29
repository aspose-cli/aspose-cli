namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>
/// Keeps Aspose.Slides from writing to standard output while the engine works. The SDK writes
/// "Updating of Inner rules" there whenever it renders with font fallback rules (known issue
/// SLIDES-FALLBACK-STDOUT in KNOWN-ISSUES.md), and standard output carries the CLI's result and the
/// render worker's messages. Overlapping calls share one muted writer; the last one out restores
/// the writer the first one found.
/// </summary>
internal static class SlidesStandardOutput
{
    private static readonly Lock Gate = new();
    private static int _depth;
    private static TextWriter? _original;

    internal static T Muted<T>(Func<T> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        lock (Gate)
        {
            if (_depth++ == 0)
            {
                _original = Console.Out;
                Console.SetOut(TextWriter.Null);
            }
        }

        try
        {
            return work();
        }
        finally
        {
            lock (Gate)
            {
                if (--_depth == 0)
                {
                    Console.SetOut(_original!);
                    _original = null;
                }
            }
        }
    }
}
