using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Input;

namespace Aspose.Cli.Sdk.Results;

/// <summary>
/// The one way a product loader opens an input document: it re-verifies the input's admission
/// (<see cref="InputSizeGuard"/>), runs the product's engine call, and tells the write pipeline
/// how the opened document is protected (<see cref="InputProtection"/>), on which the
/// <c>ENCRYPTION_REMOVED</c> disclosure rests. A product supplies only the engine call.
/// </summary>
public static class InputOpening
{
    /// <summary>
    /// Opens the user input at <paramref name="path"/> through <paramref name="open"/> and
    /// records its protection with <paramref name="outputs"/>. When recording fails, the opened
    /// document is disposed before the failure propagates.
    /// </summary>
    /// <param name="outputs">The invocation's write pipeline; null only for a loader that inspects fonts and publishes nothing.</param>
    /// <param name="resourceBudgets">The invocation's resource budgets, which admitted the input.</param>
    /// <param name="path">The input file.</param>
    /// <param name="open">The product's engine call that opens the file.</param>
    /// <exception cref="Errors.CliException">
    /// <c>FILE_TOO_LARGE</c> above the input byte limit; <c>INPUT_CHANGED</c> after admission;
    /// any error of <paramref name="open"/>.
    /// </exception>
    public static T Open<T>(OutputPipeline? outputs, ResourceBudgetLedger resourceBudgets, string path, Func<T> open)
        where T : IOpenedInput, IDisposable
    {
        ArgumentNullException.ThrowIfNull(resourceBudgets);
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(open);
        InputSizeGuard.Ensure(resourceBudgets, path);
        T opened = open();
        if (outputs is null)
        {
            return opened;
        }

        try
        {
            outputs.Opened(opened.Protection);
        }
        catch
        {
            opened.Dispose();
            throw;
        }

        return opened;
    }

    /// <summary>
    /// Opens a candidate this invocation generated, such as a staged output it verifies. Its size
    /// is bounded by publication, not a second user-input admission, and it is no input whose
    /// protection the write pipeline compares outputs with, so nothing is recorded.
    /// </summary>
    /// <param name="open">The product's engine call that opens the file.</param>
    public static T OpenPublished<T>(Func<T> open)
        where T : IOpenedInput
    {
        ArgumentNullException.ThrowIfNull(open);
        return open();
    }
}
