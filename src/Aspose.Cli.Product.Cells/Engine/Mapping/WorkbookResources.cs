using Aspose.Cells;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>Adapts verified resources to Cells and owns every stream supplied to the SDK.</summary>
internal sealed class WorkbookResources(string path, ResourceBudgetLedger budgets, bool embeddedContainer)
    : IStreamProvider, IDisposable
{
    private readonly LocalDocumentResourceLoader _loader = new(path, budgets);
    private readonly HashSet<Stream> _streams = [];
    private readonly object _gate = new();
    private bool _disposed;

    public void InitStream(StreamProviderOptions options)
    {
        // Default permits SDK fetching, so refusal must be explicit before any work.
        options.ResourceLoadingType = ResourceLoadingType.Skip;
        options.Stream = null;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_loader.TryRead(options.DefaultPath, out byte[] data))
            {
                var stream = new MemoryStream(data, writable: false);
                _streams.Add(stream);
                options.Stream = stream;
                options.ResourceLoadingType = ResourceLoadingType.UserProvided;
            }
        }
    }

    public void CloseStream(StreamProviderOptions options)
    {
        lock (_gate)
        {
            if (options.Stream is { } stream && _streams.Remove(stream))
            {
                stream.Dispose();
            }
            options.Stream = null;
        }
    }

    internal void MaterializeLinkedPictures(Workbook workbook)
    {
        foreach (Worksheet sheet in workbook.Worksheets)
        {
            for (int index = sheet.Pictures.Count - 1; index >= 0; index--)
            {
                budgets.Deadline.ThrowIfExpired("document-resource");
                Aspose.Cells.Drawing.Picture picture = sheet.Pictures[index];
                if (!picture.IsLink)
                {
                    continue;
                }
                string reference = picture.SourceFullName;
                // Changing the link clears Data. Retain stored document content before
                // preventing deferred external acquisition.
                byte[]? cached = picture.Data;
                picture.SourceFullName = string.Empty;
                picture.IsLink = false;
                if (cached is { Length: > 0 })
                {
                    picture.Data = cached;
                    continue;
                }
                if (_loader.TryRead(reference, out byte[] data))
                {
                    picture.Data = data;
                }
                else
                {
                    sheet.Pictures.RemoveAt(index);
                }
            }
        }
    }

    internal void ThrowIfFailed() => _loader.ThrowIfFailed();

    internal Warning? CoverageWarning =>
        _loader.Warning ?? (embeddedContainer ? new Warning
        {
            Code = CellsDiagnostics.MhtmlResourceCoverageUnverified,
            Message = "The SDK resolves embedded MHTML resources internally without reporting unresolved references.",
            Hint = "Inspect every required image and style; external resource completeness cannot be confirmed.",
            AffectsCompleteness = true,
        } : null);

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            foreach (Stream stream in _streams)
            {
                stream.Dispose();
            }
            _streams.Clear();
            _loader.Dispose();
        }
    }
}
