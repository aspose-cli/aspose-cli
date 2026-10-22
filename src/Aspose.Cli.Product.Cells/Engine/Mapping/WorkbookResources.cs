using Aspose.Cells;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Input;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Supplies a loaded workbook's linked pictures from verified files beneath its folder and keeps
/// its SVG pictures to their own content, reporting what it left out.
/// </summary>
internal sealed class WorkbookResources(string path, ResourceBudgetLedger budgets) : IDisposable
{
    private readonly LocalDocumentResourceLoader _loader = new(path, budgets);
    private int _svgReferences;
    private int _svgPictures;

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
                byte[]? content = cached is { Length: > 0 } ? cached
                    : _loader.TryRead(reference, out byte[] data) ? data
                    : null;
                // The engine rasterizes an SVG it is given (CELLS-SVG-EGRESS): give it one that
                // names nothing outside itself.
                if (content is not null && SvgPictures.IsSvg(content))
                {
                    if (SvgReferences.KeepInternal(content) is { } cleaned)
                    {
                        content = cleaned.Content;
                        _svgReferences += cleaned.Removed.Count;
                    }
                    else
                    {
                        content = null;
                        _svgPictures++;
                    }
                }
                if (content is not null)
                {
                    picture.Data = content;
                }
                else
                {
                    sheet.Pictures.RemoveAt(index);
                }
            }
        }
    }

    /// <summary>
    /// Removes every reference outside an SVG picture from it, and every SVG picture that does
    /// not parse (CELLS-SVG-EGRESS), before anything renders or saves the workbook.
    /// </summary>
    internal void CleanSvgPictures(Workbook workbook)
    {
        (int references, int pictures) = SvgPictures.CleanLoaded(workbook, budgets.Deadline.ThrowIfExpired);
        _svgReferences += references;
        _svgPictures += pictures;
    }

    /// <summary>
    /// Counts what keeping an OpenDocument package's SVG pictures to themselves removed before
    /// the load (<see cref="OdsPackages"/>): references, and pictures left out.
    /// </summary>
    internal void NoteSvgPictures(int references, int pictures)
    {
        _svgReferences += references;
        _svgPictures += pictures;
    }

    internal void ThrowIfFailed() => _loader.ThrowIfFailed();

    /// <summary>
    /// What the load left out: resources the input names but the CLI did not load, and SVG
    /// pictures that do not parse.
    /// </summary>
    internal Warning[] Warnings => new[] { OmissionWarning, SvgPicturesWarning }.OfType<Warning>().ToArray();

    private Warning? OmissionWarning
    {
        get
        {
            if (_svgReferences == 0)
            {
                return _loader.Warning;
            }
            _loader.ThrowIfFailed();
            return SvgPictures.OmissionWarning(_loader.Omitted, _svgReferences);
        }
    }

    private Warning? SvgPicturesWarning => _svgPictures > 0
        ? new Warning(CellsDiagnostics.ImagesSkipped, $"{_svgPictures} SVG picture(s) were left out: their SVG is compressed or does not parse as XML, or they sit in a group.")
        {
            Hint = "Replace each such picture with a PNG, or with an SVG that parses and is not grouped, using insert_image.",
            AffectsCompleteness = true,
        }
        : null;

    public void Dispose() => _loader.Dispose();
}
