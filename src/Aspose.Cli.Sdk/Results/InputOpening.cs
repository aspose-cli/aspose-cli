using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Formats;
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
    /// Opens the user input at <paramref name="path"/> for a command other than convert through
    /// <paramref name="open"/> and records its protection with <paramref name="outputs"/>. When
    /// recording fails, the opened document is disposed before the failure propagates.
    /// </summary>
    /// <param name="outputs">The invocation's write pipeline.</param>
    /// <param name="resourceBudgets">The invocation's resource budgets, which admitted the input.</param>
    /// <param name="formats">The product's format declarations; its <see cref="FormatUse.Input"/> formats are the ones the command reads.</param>
    /// <param name="path">The input file.</param>
    /// <param name="open">The product's engine call that opens the file.</param>
    /// <exception cref="CliException">
    /// <c>FORMAT_UNSUPPORTED</c> for an extension that names a format the command does not read,
    /// before the file is read (an extension no format declares, and none, are left to
    /// <paramref name="open"/>); <c>FILE_TOO_LARGE</c> above the input byte limit;
    /// <c>INPUT_CHANGED</c> after admission; any error of <paramref name="open"/>.
    /// </exception>
    public static T Open<T>(OutputPipeline outputs, ResourceBudgetLedger resourceBudgets, IReadOnlyList<FormatDescriptor> formats, string path, Func<T> open)
        where T : IOpenedInput, IDisposable =>
        OpenReading(outputs, resourceBudgets, formats, FormatUse.Input, path, open);

    /// <summary>
    /// Opens the user input of a convert command as <see cref="Open{T}"/> does, where the
    /// command reads the product's <see cref="FormatUse.ConvertInput"/> formats too.
    /// </summary>
    /// <inheritdoc cref="Open{T}" path="/param"/>
    /// <inheritdoc cref="Open{T}" path="/exception"/>
    public static T OpenForConvert<T>(OutputPipeline outputs, ResourceBudgetLedger resourceBudgets, IReadOnlyList<FormatDescriptor> formats, string path, Func<T> open)
        where T : IOpenedInput, IDisposable =>
        OpenReading(outputs, resourceBudgets, formats, FormatUse.Input | FormatUse.ConvertInput, path, open);

    private static T OpenReading<T>(OutputPipeline outputs, ResourceBudgetLedger resourceBudgets, IReadOnlyList<FormatDescriptor> formats, FormatUse reads, string path, Func<T> open)
        where T : IOpenedInput, IDisposable
    {
        ArgumentNullException.ThrowIfNull(outputs);
        ArgumentNullException.ThrowIfNull(resourceBudgets);
        ArgumentNullException.ThrowIfNull(formats);
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(open);
        FormatDescriptor[] read = [.. formats.Where(format => (format.Uses & reads) != 0)];
        if (!read.Any(format => format.Names(path)) && formats.Concat(outputs.DeclaredFormats).Any(format => format.Names(path)))
        {
            throw CliErrors.ExtensionNotRead(path, [.. read.SelectMany(static format => format.Extensions).Select(static extension => extension.ToLowerInvariant()).Distinct(StringComparer.Ordinal)]);
        }

        InputSizeGuard.Ensure(resourceBudgets, path);
        T opened = open();
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
