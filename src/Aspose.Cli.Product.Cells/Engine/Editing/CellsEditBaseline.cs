using System.Diagnostics;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>Owns the bounded, private bytes captured before a workbook mutation.</summary>
internal sealed class CellsEditBaseline(string directory, OwnedTemporaryFile file) : IDisposable
{
    internal string Path => file.Path;

    internal static CellsEditBaseline Capture(string sourcePath, FileWritePrecondition precondition, ResourceBudgetLedger budgets)
    {
        string directory = UserStorage.CreateTemporaryDirectory("cells-edit-baseline");
        OwnedTemporaryFile? file = null;
        try
        {
            file = OwnedTemporaryFile.Create(System.IO.Path.Combine(directory, "baseline" + System.IO.Path.GetExtension(sourcePath)));
            using (Stream source = budgets.Inputs.OpenFile(sourcePath))
            using (var destination = new FileStream(file.Path, FileMode.Truncate, FileAccess.Write, FileShare.None))
            { source.CopyTo(destination); destination.Flush(flushToDisk: true); }
            file.BindProducedFile();
            FileFingerprints.EnsureUnchanged(sourcePath, precondition.Fingerprint, FileFingerprints.Capture(file.Path));
            return new CellsEditBaseline(directory, file);
        }
        catch
        {
            file?.Dispose();
            DeleteEmpty(directory);
            throw;
        }
    }

    public void Dispose()
    {
        file.Dispose();
        DeleteEmpty(directory);
    }

    private static void DeleteEmpty(string directory)
    {
        try { Directory.Delete(directory, recursive: false); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { Trace.TraceWarning("Verification baseline directory was preserved ({0}).", error.GetType().Name); }
    }
}
